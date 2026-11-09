#!/usr/bin/env node

import { AsyncLocalStorage } from "node:async_hooks";
import { TaskPool } from "./task-pool.mjs";
import { createHash } from "node:crypto";
import { existsSync } from "node:fs";
import { copyFile, mkdir, open, readFile, realpath, rename, stat, unlink, writeFile } from "node:fs/promises";
import { basename, dirname, extname, resolve } from "node:path";
import process from "node:process";
import { spawn } from "node:child_process";
import { createConnection, createServer } from "node:net";
import { chromium } from "playwright";
import { downloadImage } from "./image-download.mjs";
import { classifyResponse, isQuotaNotice, quotaError, responseError } from "./response-state.mjs";

const REPO_ROOT = resolve(import.meta.dirname, "../..");
const DEFAULT_OUTPUT_ROOT = resolve(REPO_ROOT, "tmp/gemini-image-web");
const DEFAULT_PROFILE = resolve(process.env.CODEX_HOME || resolve(process.env.HOME || ".", ".codex"), "browser-profiles/gemini-manual-login");
const IMAGES_URL = "https://gemini.google.com/images";
const DEFAULT_IMAGE_MODEL = "Flash";
// 未获得会话 ID 的中断任务也按 manifest 定位，避免并行页共用 /app 地址。
const taskPages = new WeakMap();

function fail(message) {
  throw new Error(message);
}

function usage() {
  return `Gemini Images（gemini.google.com/images）网页自动化

用法：
  gemini-image-web.mjs login [--profile DIR]
  gemini-image-web.mjs doctor [--profile DIR] [--headed]
  gemini-image-web.mjs generate --prompt TEXT|--prompt-file FILE|- [--out PATH] [--session NAME] [--name NAME]
                                [--reference FILE ...]
  gemini-image-web.mjs resume --manifest FILE [--out PATH]
  gemini-image-web.mjs start [--concurrency N] [--profile DIR]
  gemini-image-web.mjs status [--profile DIR]
  gemini-image-web.mjs stop [--profile DIR]

generate：新开一个 Images 对话，默认选择 Flash 图片模型，提交提示词（可带参考图），等生成完成后保存图片。
  --out       把图另存到制作目录。扩展名由网页实际返回的格式决定（.png/.jpeg/.webp）：
              --out review/人形/03-faceted        → review/人形/03-faceted.png
              --out review/人形/03-faceted.png    → 同上；写错扩展名会被纠正，不报错
              --out review/人形/                  → review/人形/<name>.png
              给了 --out 就不用再给 --session/--name：session 取上一级目录名，name 取文件名。
  --session   生成图和 manifest 的目录：tmp/gemini-image-web/<session>/<name>/；没有 --out 时必填
  --name      本次候选名，默认按时间戳；同名会覆盖
  --prompt-file -  从 stdin 读提示词
  --reference 参考图，可重复；首次使用需先在网页上同意“Creating content from images and files”；上传约需数秒，CLI 会等进度消失后再提交
  --concurrency N 后台启动时设置容量（1–6，默认 6）；已有后台容量用 status 查看，修改需先 stop。
通用选项：--debug（保存脱敏网络诊断） --model Argon|Flash（默认 Flash） --output-root DIR --profile DIR --headed --timeout-ms N（默认且最大 90000）
成功时 stdout 前几行是摘要，最后一行是 manifest 路径；等待期间 stderr 每 30 秒打印一次心跳。
同一 --profile 后台默认最多同时运行六个 generate；超出容量或输出冲突立即报错。resume/doctor 独占。后台 Chrome 空闲 10 分钟后退出。`;
}

export function parseArgs(argv) {
  const [command, ...rest] = argv;
  if (!command || command === "--help" || command === "-h") return { command: "help" };
  const options = { command, reference: [] };
  for (let i = 0; i < rest.length; i += 1) {
    const token = rest[i];
    if (!token.startsWith("--")) fail(`无法识别的参数：${token}`);
    const key = token.slice(2).replaceAll("-", "_");
    if (!["headed", "debug", "profile", "output_root", "timeout_ms", "model", "mode", "out", "session", "name", "prompt", "prompt_file", "reference", "manifest", "concurrency"].includes(key)) fail(`未知参数：${token}`);
    if (key === "headed" || key === "debug") {
      options[key] = true;
      continue;
    }
    const value = rest[++i];
    if (value === undefined || (value.startsWith("--") && key !== "prompt")) fail(`${token} 缺少值。`);
    if (key === "reference") options.reference.push(value);
    else options[key] = value;
  }
  return options;
}

export async function validateOptions(options) {
  const common = {
    ...options,
    output_root: resolve(options.output_root || DEFAULT_OUTPUT_ROOT),
    profile: resolve(options.profile || DEFAULT_PROFILE),
    model: options.model || DEFAULT_IMAGE_MODEL,
    timeout_ms: Number(options.timeout_ms || DEFAULT_TIMEOUT_MS),
  };
  if (!Number.isFinite(common.timeout_ms) || common.timeout_ms < 1000 || common.timeout_ms > DEFAULT_TIMEOUT_MS) {
    fail("--timeout-ms 必须是 1000 到 90000 之间的数字；图片生成等待不允许超过 90 秒。");
  }
  if (!["Argon", "Flash"].includes(common.model)) fail("--model 仅支持 Argon 或 Flash。");
  if (options.concurrency !== undefined) {
    common.concurrency = Number(options.concurrency);
    if (!["serve", "start", "generate"].includes(options.command) || !Number.isInteger(common.concurrency) || common.concurrency < 1 || common.concurrency > 6) fail("--concurrency 仅用于 start/generate/serve，必须为 1 到 6 的整数；后台启动时生效。");
  }
  if (options.mode !== undefined) fail("--mode 已移除：请用 --model Argon|Flash，默认 Flash。");
  if (common.out) {
    common.out_is_dir = options.out.endsWith("/") || (existsSync(resolve(options.out)) && (await stat(resolve(options.out))).isDirectory());
    common.out = resolve(common.out);
  }
  if (common.command === "generate") {
    if (options.prompt !== undefined && options.prompt_file !== undefined) fail("--prompt 与 --prompt-file 只能指定一个。");
    // --out 已经说明了这张图是谁、放哪：session/name 从路径推出来，不用再报一遍。
    if (common.out) {
      const ext = extname(common.out).toLowerCase();
      const stem = common.out_is_dir ? null : basename(common.out, IMAGE_EXTENSIONS.has(ext) ? extname(common.out) : "");
      common.session = common.session || basename(common.out_is_dir ? common.out : dirname(common.out));
      common.name = common.name || stem || undefined;
    }
    if (!common.session) fail("generate 需要 --session（或用 --out 指定落点，session 从目录名推出）。");
    if (common.prompt_file === "-") common.prompt = await readStdin();
    else if (common.prompt_file) common.prompt = await readFile(resolve(common.prompt_file), "utf8");
    if (!common.prompt?.trim()) fail("generate 需要 --prompt 或 --prompt-file。");
    common.prompt = common.prompt.trim();
    common.reference = common.reference.map(path => resolve(path));
    for (const path of common.reference) if (!existsSync(path)) fail(`参考图不存在：${path}`);
    common.name = common.name || new Date().toISOString().replaceAll(/[-:]/g, "").replace(/\.\d+Z$/, "").replace("T", "-");
  }
  if (common.command === "resume") {
    if (!common.manifest) fail("resume 需要 --manifest。");
    common.manifest = resolve(common.manifest);
    if (!existsSync(common.manifest)) fail(`manifest 不存在：${common.manifest}`);
  }
  return common;
}

const IMAGE_EXTENSIONS = new Set([".png", ".jpg", ".jpeg", ".webp"]);

async function readStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) chunks.push(chunk);
  return Buffer.concat(chunks).toString("utf8");
}

// --out 的最终路径：目录 → <目录>/<name><实际扩展名>；文件 → 扩展名换成实际格式。
function resolveOut(options, resultPath) {
  const extension = extname(resultPath);
  if (options.out_is_dir) return resolve(options.out, `${options.name}${extension}`);
  const current = extname(options.out).toLowerCase();
  const stem = IMAGE_EXTENSIONS.has(current) ? options.out.slice(0, -current.length) : options.out;
  return `${stem}${extension}`;
}

async function saveOut(options, result, manifest) {
  if (!options.out) return;
  const target = resolveOut(options, result.path);
  await mkdir(dirname(target), { recursive: true });
  if (resolve(result.path) !== target) await copyFile(result.path, target);
  manifest.out = target;
}

async function sha256(path) {
  return createHash("sha256").update(await readFile(path)).digest("hex");
}

async function atomicJson(path, value) {
  await mkdir(dirname(path), { recursive: true });
  const temporary = `${path}.tmp`;
  await writeFile(temporary, `${JSON.stringify(value, null, 2)}\n`);
  await rename(temporary, path);
}

// 解析已存在的父目录，防止符号链接别名绕过并发输出保护。
async function canonicalPath(path) {
  try { return await realpath(path); }
  catch (error) {
    if (!["ENOENT", "ENOTDIR"].includes(error.code)) throw error;
    const parent = dirname(path);
    if (parent === path) throw error;
    return resolve(await canonicalPath(parent), basename(path));
  }
}

function safeName(value, flag) {
  const safe = value.replaceAll(/[^\p{L}\p{N}._-]+/gu, "-").replaceAll(/^-|-$/g, "");
  if (safe === "." || safe === "..") fail(`${flag} 不能是 . 或 ..。`);
  if (!safe) fail(`${flag} 必须至少包含一个字母或数字。`);
  return safe;
}

// Playwright 持久化 profile 不能被两个进程同时打开；用锁文件把并发变成明确的报错。
async function acquireProfileLock(profile) {
  await mkdir(profile, { recursive: true });
  const lockPath = resolve(profile, ".gemini-image-web.lock");
  for (let attempt = 0; attempt < 2; attempt += 1) {
    try {
      const file = await open(lockPath, "wx");
      try { await file.writeFile(`${process.pid}\n`); } finally { await file.close(); }
      return async () => { await unlink(lockPath).catch(() => {}); };
    } catch (error) {
      if (error.code !== "EEXIST") throw error;
      const previous = await readFile(lockPath, "utf8").catch(() => null);
      const pid = Number(previous?.trim());
      if (!Number.isInteger(pid) || pid <= 0) {
        if (attempt === 0) { await new Promise(resolvePromise => setTimeout(resolvePromise, 100)); continue; }
        fail(`浏览器 profile 锁文件无效：${lockPath}`);
      }
      try { process.kill(pid, 0); fail(`另一个 gemini-image-web 进程（pid ${pid}）正在使用同一浏览器 profile；请等它结束后再提交。`); }
      catch (processError) { if (processError.code !== "ESRCH") throw processError; }
      await unlink(lockPath).catch(() => {});
    }
  }
  fail(`无法取得浏览器 profile 锁：${lockPath}`);
}

function formatElapsed(ms) {
  const total = Math.round(ms / 1000);
  return `${Math.floor(total / 60)}m${String(total % 60).padStart(2, "0")}s`;
}

async function launch(options) {
  const release = await acquireProfileLock(options.profile);
  const context = await chromium.launchPersistentContext(options.profile, {
    channel: "chrome",
    timeout: 30_000,
    headless: !options.headed,
    // 与已成功登录的 Chrome 使用相同启动标记，登录仍由用户手动完成。
    ignoreDefaultArgs: ["--enable-automation"],
    args: ["--disable-blink-features=AutomationControlled"],
    acceptDownloads: true,
    viewport: { width: 1400, height: 950 },
  }).catch(async error => { await release(); throw error; });
  const close = context.close.bind(context);
  context.close = async () => {
    try {
      // gemini.google.com 偶尔会让 persistent Chrome 的关闭握手不返回。
      // CLI 入口随后会显式退出进程；这里不能因此永久占住 profile 锁。
      await Promise.race([
        close(),
        new Promise(resolvePromise => setTimeout(resolvePromise, 5_000)),
      ]);
    } finally {
      await release();
    }
  };
  return context;
}

// 正常一次生成十几秒；超过一分半基本是没提交上或网页卡住，再等也不会好。
const DEFAULT_TIMEOUT_MS = 90 * 1000;

function promptEditor(page) {
  return page.locator("div.ql-editor[aria-label]").first();
}

async function humanVerificationDetails(page) {
  const currentUrl = new URL(page.url());
  const isGoogleSorry = /(^|\.)google\.[a-z.]+$/i.test(currentUrl.hostname) && currentUrl.pathname.startsWith("/sorry/");
  const body = await page.locator("body").innerText().catch(() => "");
  const challengeText = /unusual traffic|automated quer(?:y|ies)|our systems have detected|not a robot|i['’]m not a robot|verify (?:that )?you are (?:a )?human|verify you['’]re human|complete (?:the )?captcha|prove (?:that )?you are not a robot|captcha/i.test(body);
  const captchaUi = await page.locator("#captcha-form, #recaptcha, iframe[src*='recaptcha'], iframe[title*='recaptcha' i], [data-sitekey]").first().isVisible().catch(() => false);
  if (!isGoogleSorry && !challengeText && !captchaUi) return null;

  // Do not persist Google's challenge query token in the manifest or logs.
  currentUrl.search = "";
  currentUrl.hash = "";
  return { url: currentUrl.toString(), text: body.trim().slice(0, 1200) };
}

async function assertNoHumanVerification(page) {
  const details = await humanVerificationDetails(page);
  if (!details) return;
  const error = responseError(
    "检测到 Google 人机验证。请在打开的 Chrome 窗口中手动完成；Gemini Image Web CLI 不会点击或解答验证。",
    details.text,
    "human-verification",
    details.url,
  );
  error.retryable = true;
  throw error;
}

async function findModelPicker(page) {
  for (const button of await page.locator("button").all()) {
    if (!(await button.isVisible().catch(() => false))) continue;
    const label = await button.getAttribute("aria-label").catch(() => "") || "";
    const text = (await button.innerText().catch(() => "")).trim();
    if (/mode picker|model picker/i.test(label) || /^(?:Gemini Auto|Argon)$/i.test(text) || /currently\s+(?:Gemini Auto|Argon)/i.test(label)) {
      return button;
    }
  }
  return null;
}

export async function ensureImageModel(page, model = DEFAULT_IMAGE_MODEL) {
  const picker = await findModelPicker(page);
  if (!picker) fail(`未找到 Gemini Images 模型选择器；无法确认默认图片模型 ${model}。请运行 doctor --headed 检查网页是否改版。`);

  const currentLabel = `${await picker.getAttribute("aria-label").catch(() => "") || ""} ${(await picker.innerText().catch(() => "")).trim()}`;
  if (!new RegExp(`\\b${model}\\b`, "i").test(currentLabel) || (model === "Flash" && /Flash[-\s]*Lite/i.test(currentLabel))) {
    await picker.click();
    const option = page.getByText(model === "Flash" ? /\bFlash\b(?![-\s]*Lite\b)/ : /\bArgon\b/, { exact: true }).last();
    await option.waitFor({ state: "visible", timeout: 10_000 }).catch(() => {
      fail(`Gemini Images 模型菜单中没有可选的 ${model}；当前选择器：${currentLabel.trim() || "未标注"}。`);
    });
    await option.click();
  }

  await page.waitForFunction(model => [...document.querySelectorAll("button")].some(button => {
    if (!button.getClientRects().length) return false;
    const label = button.getAttribute("aria-label") || "";
    const text = (button.innerText || "").trim();
    const picker = /mode picker|model picker/i.test(label) || /^(?:Gemini Auto|Argon)$/i.test(text) || /currently\s+(?:Gemini Auto|Argon)/i.test(label);
    return picker && new RegExp(`\\b${model}\\b`, "i").test(`${label} ${text}`) && !(model === "Flash" && /Flash[-\s]*Lite/i.test(`${label} ${text}`));
  }), model, { timeout: 10_000 }).catch(() => {
    fail(`已选择 ${model}，但页面没有确认该模型处于启用状态。`);
  });
}

async function ensureImagesTool(page) {
  const imageMode = page.locator("button[aria-label='Deselect Images']").first();
  if (!(await imageMode.isVisible().catch(() => false))) {
    await page.getByRole("button", { name: "Upload & tools", exact: true }).click();
    await page.getByText("Create image", { exact: true }).click();
    await imageMode.waitFor({ state: "visible", timeout: 15_000 });
  }
}

export async function assertImagesPage(page, model = DEFAULT_IMAGE_MODEL) {
  await page.waitForLoadState("domcontentloaded");
  await assertNoHumanVerification(page);
  await assertQuotaAvailable(page);
  if (/accounts\.google\.com/.test(page.url()) || await page.getByText(/^Sign in$|^登录$/).first().isVisible().catch(() => false)) {
    throw responseError("Gemini 尚未登录。先运行 login，并手动完成登录。", "", "auth-error", page.url());
  }
  await promptEditor(page).waitFor({ state: "visible", timeout: 30_000 }).catch(() => {});
  await assertQuotaAvailable(page);
  const body = await page.locator("body").innerText();
  if (/accounts\.google\.com/.test(page.url()) || (/Sign in|登录/.test(body) && !(await promptEditor(page).count()))) {
    throw responseError("Gemini 尚未登录。先运行 login，并手动完成登录。", "", "auth-error", page.url());
  }
  if (!(await promptEditor(page).isVisible().catch(() => false))) {
    fail(`Gemini Images 页面未显示提示词输入框（当前 URL：${page.url()}）。运行 doctor --headed 检查网页。`);
  }
  await ensureImagesTool(page);
  await ensureImageModel(page, model);
}

async function firstVisible(locator) {
  for (const candidate of await locator.all()) {
    if (await candidate.isVisible().catch(() => false)) return candidate;
  }
  return null;
}

async function readQuotaNotice(page) {
  const matches = page.getByText(/limit reached|you(?:'|’)ve reached (?:your\s+)?(?:daily\s+|image generation\s+)?limit|(?:额度|上限).{0,16}(?:已达到|已用尽|已超出)|(?:已达到|已用尽|已超出).{0,16}(?:额度|上限)/i);
  const notices = [];
  for (const candidate of await matches.all()) {
    if (!(await candidate.isVisible().catch(() => false))) continue;
    if (await candidate.evaluate(element => !!element.closest("user-query, model-response, .user-query, [data-test-id='user-query'], [contenteditable='true']"))) continue;
    const text = (await candidate.innerText({ timeout: 1_000 }).catch(() => "")).trim();
    // Avoid returning app-shell ancestors that contain the notice and the full page.
    if (text.length <= 240 && isQuotaNotice(text)) notices.push(text);
  }
  return notices.sort((a, b) => a.length - b.length)[0] || "";
}

async function assertQuotaAvailable(page) {
  const notice = await readQuotaNotice(page);
  if (notice) throw quotaError(notice, page.url());
}

export async function uploadReferences(page, references) {
  if (!references.length) return;

  const attachments = page.locator("img[alt='attachment'], img.gem-attachment-style-img");
  // setInputFiles 会立刻修改 DOM，因此新增附件的基线必须在触发上传前采集。
  const attachmentsBeforeUpload = await attachments.count();

  // 页面壳先显示 Add files，随后才挂载 Upload & tools 的事件和文件输入。
  // 点击成功不代表菜单已展开；只在输入框或上传菜单出现后继续。
  const imageInput = page.locator("input[type='file'][accept='image/*']").last();
  const menuItem = page.locator(
    "[data-test-id='local-images-files-uploader-button'], [role='menuitem'][aria-label^='Upload files']",
  ).last();
  let ready = false;
  for (let attempt = 0; attempt < 3; attempt++) {
    const add = await firstVisible(page.locator(
      "button[aria-label='Upload & tools'], button[aria-label*='Add files'], button[aria-label*='Upload']",
    ));
    if (!add) fail("未找到可见的图片上传入口；请运行 doctor --headed 检查。");
    await add.click();
    ready = await Promise.any([
      imageInput.waitFor({ state: "attached", timeout: 5_000 }),
      menuItem.waitFor({ state: "visible", timeout: 5_000 }),
    ]).then(() => true).catch(() => false);
    if (ready) break;
  }
  if (!ready) fail(`图片上传入口点击三次后仍未就绪（当前 URL：${page.url()}）。请运行 doctor --headed 检查。`);
  const directInputReady = await imageInput.count() > 0;
  if (directInputReady) {
    await imageInput.setInputFiles(references);
  } else {
    // 兼容仍使用菜单文件选择器的旧版页面。不要用 [role=menu].last()：新版同时
    // 渲染外层工具菜单和内层上传菜单，last() 会受动画/实验分支时序影响。
    await menuItem.waitFor({ state: "visible", timeout: 10_000 }).catch(async () => {
      const menus = await page.locator("[role='menu']:visible").count().catch(() => 0);
      fail(`点击图片上传入口后未找到 Upload files（当前可见菜单 ${menus} 个；网页可能已改版）。`);
    });
    const chooser = page.waitForEvent("filechooser", { timeout: 15_000 });
    await menuItem.click();
    await Promise.race([
      chooser.then(fc => fc.setFiles(references)),
      page.getByText("Creating content from images and files").waitFor({ timeout: 15_000 }).then(() => {
        fail("网页要求先同意“Creating content from images and files”。请用 --headed 运行一次并手动点击 Agree，之后才能自动上传参考图。");
      }),
    ]);
  }

  // 首页自身有一批 blob/data 的模板缩略图，不能用任意 img 出现作为上传完成
  // 条件。当前页面的真实附件是 img[alt="attachment"]，必须等它的数量增加到
  // 本次参考图数量后才能继续，否则 Gemini 会把提示词送入普通 /app 聊天。
  const expected = attachmentsBeforeUpload + references.length;
  const consentText = /Creating content from images and files|使用图片和文件创建内容/i;
  const uploadState = await Promise.race([
    page.waitForFunction(
      count => document.querySelectorAll("img[alt='attachment'], img.gem-attachment-style-img").length >= count,
      expected,
      { timeout: 60_000 },
    ).then(() => "attached"),
    page.getByText(consentText).last().waitFor({ state: "visible", timeout: 60_000 }).then(() => "consent"),
  ]).catch(() => "timeout");
  if (uploadState === "consent") {
    fail("网页要求先同意“Creating content from images and files”。请用 --headed 运行一次并手动点击 Agree，之后才能自动上传参考图。");
  }
  const actual = await attachments.count();
  if (uploadState !== "attached" || actual < expected) {
    fail(`参考图上传未完成：期望 ${references.length} 张新增附件，当前检测到 ${Math.max(0, actual - attachmentsBeforeUpload)} 张（文件：${references.join("、")}；当前 URL：${page.url()}）。`);
  }
  // 缩略图先于上传完成出现：附件 img 已在 DOM 里时，预览容器内还挂着上传进度
  // 条约 6 秒。此时点 Send，Gemini 会把整页重置回普通 /app 首页，附件与提示词
  // 一起丢失，且没有任何错误。必须等预览容器里的进度指示消失后再提交。
  const uploaded = await page.waitForFunction(() => {
    const container = document.querySelector("uploader-file-preview-container, .file-preview-container");
    if (!container) return false;
    if (container.querySelector("mat-progress-spinner, mat-spinner, [role='progressbar']")) return false;
    return !/uploading|loading/i.test(container.outerHTML);
  }, null, { timeout: 60_000 }).then(() => true).catch(() => false);
  if (!uploaded) fail(`参考图上传 60 秒内未完成（预览容器仍显示进度）；当前 URL：${page.url()}。`);
  await page.waitForTimeout(700);
}

// 第一次 Send 可能只把 /images 切到普通 /app，并清空编辑器但保留附件。
// 因此“编辑器变空”不是成功；只认带 ID 的会话 URL 或可见的匹配用户回合。
export async function submitPrompt(page, prompt, referenceCount, model = DEFAULT_IMAGE_MODEL) {
  for (let attempt = 1; attempt <= 3; attempt++) {
    await assertNoHumanVerification(page);
    await assertQuotaAvailable(page);
    const imageMode = page.locator("button[aria-label='Deselect Images']").first();
    if (!(await imageMode.isVisible().catch(() => false))) {
      await ensureImagesTool(page);
      await ensureImageModel(page, model);
      const remainingAttachments = await page.locator("img[alt='attachment'], img.gem-attachment-style-img").count();
      if (remainingAttachments < referenceCount) {
        fail(`恢复 Images 模式后参考图丢失：期望 ${referenceCount} 张，当前 ${remainingAttachments} 张；已停止提交，避免静默降级。`);
      }
    }
    const editor = promptEditor(page);
    await editor.waitFor({ state: "visible", timeout: 10_000 }).catch(() => {
      fail(`第 ${attempt} 次提交前未找到提示词输入框（当前 URL：${page.url()}）。`);
    });
    if ((await editor.innerText().catch(() => "")).trim() !== prompt) {
      await editor.fill(prompt);
    }
    const send = page.locator("button[aria-label='Send message']").first();
    await send.waitFor({ state: "visible", timeout: 10_000 }).catch(() => {
      fail(`第 ${attempt} 次提交前未找到 Send message 按钮（当前 URL：${page.url()}）。`);
    });
    if (await send.isDisabled()) fail(`第 ${attempt} 次提交时 Send message 按钮不可用，任务没有提交。`);
    await send.click();
    const submitted = await page.waitForFunction(
      expectedPrompt => {
        return [...document.querySelectorAll("user-query, .user-query, [data-test-id='user-query']")]
          .some(element => element.getClientRects().length > 0 && (element.textContent || "").replace(/\s+/g, " ").includes(expectedPrompt.replace(/\s+/g, " ").slice(0, 160)));
      },
      prompt,
      { timeout: 8_000 },
    ).then(() => true).catch(() => false);
    if (submitted) return;
    // Quota notices live outside model-response. Check before any automatic resend.
    await assertNoHumanVerification(page);
    await assertQuotaAvailable(page);
    const leftover = (await editor.innerText().catch(() => "")).trim();
    console.error(`[gemini-image-web] 第 ${attempt} 次点击 Send 后没有进入生成会话（当前 URL：${page.url()}；编辑器${leftover ? "仍有文字" : "已被清空"}），重试。`);
  }
  fail(`三次点击 Send message 后仍未出现用户回合或会话 ID，任务没有提交（当前 URL：${page.url()}）。`);
}

export async function waitForResult(page, timeoutMs, submittedAt) {
  let retried = false;
  const started = Date.now();
  let lastBeat = started;
  let lastResponseText = "";
  while (Date.now() - started < timeoutMs) {
    if (page.isClosed()) throw responseError("浏览器页面已关闭，请重新登录或恢复会话。", "", "browser-error", page.url());
    await assertNoHumanVerification(page);
    if (/accounts\.google\.com/.test(page.url())) throw responseError("生成期间登录失效，请先 login 再 resume。", "", "auth-error", page.url());
    await assertQuotaAvailable(page);
    // 用户提示词、侧栏和旧回复可能含错误词，只检查当前模型回复。
    const response = page.locator("model-response").last();
    const text = await response.innerText({ timeout: 1_000 }).catch(() => "");
    if (text.trim()) lastResponseText = text.trim();
    const state = classifyResponse(text);
    if (state.kind !== "pending") {
      const redo = response.getByRole("button", { name: "Redo", exact: true });
      if (state.kind === "transient-error" && !retried &&
          timeoutMs - (Date.now() - started) >= 15_000 && await redo.waitFor({ state: "visible", timeout: Math.min(3_000, Math.max(1, timeoutMs - (Date.now() - started))) }).then(() => true).catch(() => false)) {
        retried = true;
        lastResponseText = "";
        console.error("[gemini-image-web] 当前回复报告临时服务错误，点击 Redo 重试一次（仍使用原 90 秒总预算）。");
        await redo.click();
        await page.waitForTimeout(2_000);
        continue;
      }
      throw responseError(`Gemini ${state.kind === "transient-error" ? "服务临时失败" : "报告生成失败"}${retried ? "（Redo 重试后仍失败）" : ""}。`, state.message, state.kind, page.url());
    }
    const image = response.locator("img[alt*='AI generated']").last();
    if (await image.count()) {
      const ready = await image.evaluate(element => element.complete && element.naturalWidth > 200).catch(() => false);
      if (ready) return image;
    }
    const finished = await response.getByRole("button", { name: /^(?:Redo|Copy|Good response|Bad response)$/ }).first().isVisible().catch(() => false);
    const stopping = await page.getByRole("button", { name: /Stop response|停止回答/i }).isVisible().catch(() => false);
    if (text.trim() && finished && !stopping && !(await image.count())) {
      throw responseError("Gemini 已完成回复但没有生成图片；未自动改写提示词或重发请求。", text, "text-only", page.url());
    }
    if (Date.now() - lastBeat >= 30_000) {
      lastBeat = Date.now();
      console.error(`[gemini-image-web] 等待生成，已等待 ${formatElapsed(Date.now() - submittedAt)}`);
    }
    await page.waitForTimeout(Math.min(2_000, Math.max(0, timeoutMs - (Date.now() - started))));
  }
  throw responseError(`等待生成完成超时（${timeoutMs} ms）。`, lastResponseText, "timeout", page.url());
}


export async function generate(options, sharedContext = null) {
  const outputDir = resolve(options.output_root, safeName(options.session, "--session"), safeName(options.name, "--name"));
  await mkdir(outputDir, { recursive: true });
  const manifestPath = resolve(outputDir, "manifest.json");
  const manifest = {
    schema_version: 1,
    provider: "gemini-images-web",
    session: options.session,
    name: options.name,
    status: "prepared",
    prompt: options.prompt,
    model: options.model,
    requested_out: options.out,
    out_is_dir: options.out_is_dir,
    references: await Promise.all(options.reference.map(async path => ({ path, sha256: await sha256(path) }))),
    output_dir: outputDir,
    created_at: new Date().toISOString(),
  };
  await writeFile(resolve(outputDir, "prompt.txt"), `${options.prompt}\n`);
  await atomicJson(manifestPath, manifest);
  console.error(`[gemini-image-web] 准备生成，网页模型 ${options.model}；任务：${manifestPath}`);

  let context = sharedContext;
  let page;
  const diagnostics = [];
  const listeners = [];
  const record = event => {
    const item = { at: new Date().toISOString(), ...event };
    diagnostics.push(item);
    if (event.kind === "pageerror" || /processSession|StreamGenerate/.test(event.endpoint || "") || event.status >= 400) console.error(`[gemini-debug] ${JSON.stringify(item)}`);
  };
  const endpoint = url => { const u = new URL(url); return u.origin + u.pathname; };
  try {
    manifest.phase = "browser";
    context ||= await launch(options);
    page = sharedContext ? await context.newPage() : context.pages()[0] || await context.newPage();
    if (sharedContext) {
      if (!taskPages.has(context)) taskPages.set(context, new Map());
      taskPages.get(context).set(manifestPath, page);
    }
    if (options.debug) {
      const listen = (name, handler) => { page.on(name, handler); listeners.push([name, handler]); };
      listen("request", request => { if (request.resourceType() === "fetch" || request.resourceType() === "xhr") record({ kind: "request", method: request.method(), endpoint: endpoint(request.url()) }); });
      listen("requestfailed", request => record({ kind: "requestfailed", endpoint: endpoint(request.url()), error: request.failure()?.errorText }));
      listen("response", response => { if (response.status() >= 400 || /StreamGenerate|batchexecute|processSession/.test(response.url())) record({ kind: "response", endpoint: endpoint(response.url()), status: response.status() }); });
      listen("pageerror", error => record({ kind: "pageerror", message: error.message.slice(0, 300) }));
      listen("requestfinished", async request => {
        if (!/StreamGenerate|batchexecute|processSession/.test(request.url())) return;
        try {
          const response = await request.response();
          const body = await response.text();
          record({ kind: "rpc-finished", endpoint: endpoint(request.url()), bytes: Buffer.byteLength(body), errors: [...new Set(body.match(/RESOURCE_EXHAUSTED|PERMISSION_DENIED|INVALID_ARGUMENT|UNAUTHENTICATED|INTERNAL|UNAVAILABLE/g) || [])] });
        } catch {}
      });
    }
    await page.goto(IMAGES_URL, { waitUntil: "domcontentloaded" });
    manifest.phase = "preparing";
    await assertImagesPage(page, options.model);
    manifest.model_picker_label = await (await findModelPicker(page)).getAttribute("aria-label");
    await uploadReferences(page, options.reference);
    const editor = promptEditor(page);
    // fill 会直接聚焦可编辑文本框；Images 欢迎卡有时覆盖鼠标点击区域。
    await editor.fill(options.prompt);
    await page.waitForTimeout(300);
    const submittedAt = Date.now();
    const send = page.locator("button[aria-label='Send message']").first();
    await send.waitFor({ state: "visible", timeout: 10_000 }).catch(() => fail("填写提示词后未找到 Send message 按钮；网页可能已改版。"));
    if (await send.isDisabled()) fail("填写提示词后 Send message 按钮仍不可用，任务没有提交。");
    manifest.phase = "submitting";
    await submitPrompt(page, options.prompt, options.reference.length, options.model);
    manifest.status = "generating";
    manifest.phase = "generating";
    manifest.submitted_at = new Date(submittedAt).toISOString();
    console.error("[gemini-image-web] 请求已确认提交，等待生成结果。");
    await atomicJson(manifestPath, manifest);
    // 用户气泡已确认提交；会话 ID 可能直到生成完成才出现。
    manifest.conversation_url = page.url();
    const remaining = options.timeout_ms - (Date.now() - submittedAt);
    if (remaining <= 0) fail(`等待生成完成超时（${options.timeout_ms} ms）。对话仍保留在网页历史中：${page.url()}`);
    const image = await waitForResult(page, remaining, submittedAt);
    manifest.phase = "downloading";
    manifest.conversation_url = page.url();
    await atomicJson(manifestPath, manifest);
    const result = await downloadImage(page, image, outputDir);
    manifest.phase = "saving";
    manifest.conversation_url = page.url();
    manifest.download = result;
    await atomicJson(manifestPath, manifest);
    await saveOut(options, result, manifest);
    manifest.status = "completed";
    manifest.phase = "completed";
    manifest.completed_at = new Date().toISOString();
    delete manifest.error;
    delete manifest.retryable;
    delete manifest.response_text;
    delete manifest.response_kind;
    delete manifest.response_file;
    delete manifest.failure_screenshot;
    await atomicJson(manifestPath, manifest);
    console.log([
      `[gemini-image-web] 完成：${manifest.out || result.path}`,
      manifest.out ? `  生成图：${result.path}` : null,
      `  sha256=${result.sha256}`,
      `  对话：${manifest.conversation_url}`,
      `  耗时=${formatElapsed(Date.now() - submittedAt)}`,
    ].filter(Boolean).join("\n"));
    console.log(manifestPath);
  } catch (error) {
    manifest.status = "interrupted";
    manifest.error = error.message;
    manifest.response_kind = error.response_kind || (manifest.phase === "saving" ? "save-error" : manifest.phase === "browser" ? "browser-error" : "automation-error");
    manifest.retryable = error.retryable ?? ["timeout", "transient-error", "download-error", "save-error", "browser-error", "human-verification"].includes(manifest.response_kind);
    if (page && manifest.response_kind !== "human-verification") manifest.conversation_url = page.url();
    console.error(`[gemini-image-web] 失败记录：${manifestPath}`);
    if (!sharedContext && manifest.response_kind === "timeout") console.error("[gemini-image-web] 前台浏览器将关闭；没有会话 ID 的任务可能无法恢复。后台任务可优先 resume。");
    if (typeof error.response_text === "string") {
      manifest.response_text = error.response_text;
      manifest.response_kind = error.response_kind;
      if (error.response_kind === "human-verification") {
        manifest.human_verification_url = error.conversation_url;
        manifest.pre_submit = !manifest.submitted_at && manifest.phase === "preparing";
        if (!manifest.submitted_at) delete manifest.conversation_url;
      } else {
        manifest.conversation_url = error.conversation_url;
      }
      manifest.response_file = resolve(manifest.output_dir, "response.txt");
      await writeFile(manifest.response_file, error.response_text, "utf8");
    }
    manifest.updated_at = new Date().toISOString();
    if (page) {
      const screenshot = resolve(outputDir, "failure.png");
      if (await page.screenshot({ path: screenshot, fullPage: true }).then(() => true).catch(() => false)) manifest.failure_screenshot = screenshot;
    }
    if (manifest.response_kind === "human-verification") {
      console.error(`[gemini-image-web] 检测到人机验证。先运行 stop，再运行 login；请在打开的 Chrome 窗口中手动完成验证并关闭窗口，然后运行：\n  node tools/gemini-image-web/gemini-image-web.mjs resume --manifest '${manifestPath}'`);
    }
    await atomicJson(manifestPath, manifest);
    throw error;
  } finally {
    if (options.debug) {
      for (const [name, handler] of listeners) page?.off(name, handler);
      await writeFile(resolve(outputDir, "debug.json"), JSON.stringify(diagnostics, null, 2)).catch(error => console.error(`[gemini-debug] 诊断保存失败：${error.message}`));
    }
    if (!sharedContext) await context?.close().catch(error => console.error(`[gemini-image-web] 浏览器清理失败：${error.message}`));
    else if (manifest.status === "completed") {
      taskPages.get(context)?.delete(manifestPath);
      await page?.close().catch(error => console.error(`[gemini-image-web] 页面清理失败：${error.message}`));
    }
  }
}

export async function resume(options, sharedContext = null) {
  const manifest = JSON.parse(await readFile(options.manifest, "utf8"));
  if (!manifest.prompt?.trim()) fail("manifest 缺少原始提示词，不能确认会话身份。");
  options.out ||= manifest.out || manifest.requested_out;
  if (!options.out_is_dir && options.out === manifest.requested_out) options.out_is_dir = manifest.out_is_dir;
  options.name ||= manifest.name;
  const outputDir = manifest.output_dir || dirname(options.manifest);
  if ((manifest.response_kind === "human-verification" || manifest.human_verification_url) && manifest.pre_submit === true) {
    const generationOptions = {
      ...options,
      command: "generate",
      prompt: manifest.prompt,
      output_root: dirname(dirname(outputDir)),
      session: manifest.session || basename(dirname(outputDir)),
      name: manifest.name || options.name,
      model: manifest.model || options.model || DEFAULT_IMAGE_MODEL,
      out: options.out || manifest.requested_out,
      out_is_dir: options.out_is_dir ?? manifest.out_is_dir,
      reference: (manifest.references || []).map(reference => reference.path),
    };
    console.error("[gemini-image-web] 原提示词尚未提交；验证通过后将从新 Images 对话继续提交。");
    return generate(generationOptions, sharedContext);
  }
  let context = sharedContext, page;
  try {
    // 保存到制作目录失败时直接复用已校验的本地原图，不再次生成或下载。
    let result = manifest.download;
    const localValid = result?.path && existsSync(result.path) && await sha256(result.path) === result.sha256;
    if (!localValid) {
      if (!manifest.conversation_url) fail("manifest 没有会话地址，提示词可能尚未成功提交；不能 resume。");
      const conversation = new URL(manifest.conversation_url);
      if (conversation.origin !== "https://gemini.google.com" || !/^\/app(?:\/[^/?#]+)?$/.test(conversation.pathname)) fail("manifest 的 conversation_url 不是有效的 Gemini 会话地址。");
      const durable = /^\/app\/[^/?#]+$/.test(conversation.pathname);
      page = taskPages.get(sharedContext)?.get(options.manifest);
      if (page?.isClosed()) page = null;
      if (!page && durable) page = sharedContext?.pages().find(candidate => candidate.url() === manifest.conversation_url);
      if (!durable && !page) fail("会话尚未获得 ID，原浏览器页面已不存在；无法恢复，请重新生成。");
      context ||= await launch(options);
      if (!page) {
        page = await context.newPage();
        await page.goto(manifest.conversation_url, { waitUntil: "domcontentloaded" });
      }
      const prefix = manifest.prompt.replace(/\s+/g, " ").trim().slice(0, 80);
      const matched = await page.waitForFunction(({ path, prefix }) => {
        if (location.pathname !== path) return "redirected";
        return [...document.querySelectorAll("user-query, .user-query, [data-test-id='user-query']")]
          .some(element => (element.textContent || "").replace(/\s+/g, " ").includes(prefix)) ? "matched" : false;
      }, { path: conversation.pathname, prefix }, { timeout: 15_000 }).then(handle => handle.jsonValue()).catch(() => "timeout");
      if (matched !== "matched") fail(`无法确认原会话与提示词（${matched}，当前 URL：${page.url()}），已停止恢复。`);
      manifest.phase = "generating";
      const image = await waitForResult(page, options.timeout_ms, Date.now());
      manifest.phase = "downloading";
      manifest.conversation_url = page.url();
      await atomicJson(options.manifest, manifest);
      result = await downloadImage(page, image, outputDir);
    }
    manifest.phase = "saving";
    manifest.download = result;
    await atomicJson(options.manifest, manifest);
    await saveOut(options, result, manifest);
    manifest.status = "completed"; manifest.phase = "completed";
    manifest.completed_at = new Date().toISOString();
    for (const key of ["error", "retryable", "response_text", "response_kind", "response_file", "failure_screenshot"]) delete manifest[key];
    await atomicJson(options.manifest, manifest);
    console.log(`[gemini-image-web] 已恢复并保存：${manifest.out || result.path}\n  ${result.width}x${result.height}; ${result.mime_type}; ${result.extraction}\n${options.manifest}`);
  } catch (error) {
    manifest.status = "interrupted"; manifest.error = error.message;
    manifest.response_kind = error.response_kind || (manifest.phase === "saving" ? "save-error" : manifest.phase === "browser" ? "browser-error" : "automation-error");
    manifest.retryable = error.retryable ?? ["timeout", "transient-error", "download-error", "save-error", "browser-error", "human-verification"].includes(manifest.response_kind);
    if (page) {
      if (manifest.response_kind !== "human-verification") manifest.conversation_url = page.url();
      const screenshot = resolve(outputDir, "failure.png");
      if (await page.screenshot({ path: screenshot, fullPage: true }).then(() => true).catch(() => false)) manifest.failure_screenshot = screenshot;
    }
    if (typeof error.response_text === "string") {
      manifest.response_text = error.response_text;
      manifest.response_kind = error.response_kind;
      if (error.response_kind === "human-verification") {
        manifest.human_verification_url = error.conversation_url;
        manifest.pre_submit = !manifest.submitted_at && manifest.phase === "preparing";
        if (!manifest.submitted_at) delete manifest.conversation_url;
      } else {
        manifest.conversation_url = error.conversation_url;
      }
      manifest.response_file = resolve(outputDir, "response.txt");
      await writeFile(manifest.response_file, error.response_text);
    }
    manifest.updated_at = new Date().toISOString();
    await atomicJson(options.manifest, manifest);
    console.error(`[gemini-image-web] 失败记录：${options.manifest}`);
    if (manifest.response_kind === "human-verification") {
      console.error(`[gemini-image-web] 检测到人机验证。请在 login 打开的 Chrome 窗口中手动完成验证并关闭窗口，然后再次运行 resume：\n  node tools/gemini-image-web/gemini-image-web.mjs resume --manifest '${options.manifest}'`);
    }
    throw error;
  } finally {
    if (!sharedContext) await context?.close().catch(error => console.error(`[gemini-image-web] 浏览器清理失败：${error.message}`));
    else if (manifest.status === "completed") {
      taskPages.get(context)?.delete(options.manifest);
      await page?.close().catch(error => console.error(`[gemini-image-web] 页面清理失败：${error.message}`));
    }
  }
}

async function doctor(options, sharedContext = null) {
  const context = sharedContext || await launch(options);
  const page = sharedContext ? await context.newPage() : context.pages()[0] || await context.newPage();
  try {
    await page.goto(IMAGES_URL, { waitUntil: "domcontentloaded" });
    await assertImagesPage(page, options.model);
    const imageMode = await page.locator("button[aria-label='Deselect Images']").count();
    const add = await page.locator("button[aria-label*='Add files'], button[aria-label*='Upload']").count();
    console.log(`images: ok (Images 工具: ${imageMode ? "已选择" : "未找到"}; 图片模型: ${options.model}; 上传按钮: ${add ? "有" : "无"})`);
  } finally {
    if (sharedContext) await page.close();
    else await context.close();
  }
}

async function login(options) {
  const context = await launch({ ...options, headed: true });
  try {
    const page = context.pages()[0] || await context.newPage();
    await page.goto(IMAGES_URL, { waitUntil: "domcontentloaded" });
    if (await humanVerificationDetails(page)) {
      console.log("检测到 Google 人机验证。请在打开的 Chrome 窗口中手动完成验证；CLI 不会点击或解答验证。完成后关闭浏览器窗口（Cmd+Q），再使用 resume 继续未提交的任务。会话保存在：", options.profile);
    } else {
      console.log("浏览器已打开。完成登录或页面要求的人工验证后关闭浏览器窗口（Cmd+Q）；会话会保存在：", options.profile);
    }
    await new Promise(resolvePromise => context.on("close", resolvePromise));
  } finally {
    await context.close();
  }
}

const IDLE_MS = 10 * 60 * 1000;
const SOCKET_NAME = ".gemini-image-web.sock";

function socketPath(profile) {
  return resolve(profile, SOCKET_NAME);
}

export function connectDaemon(options, onProgress = event => {
  if (event.channel === "stderr") console.error(event.line);
  else console.log(event.line);
}) {
  return new Promise((resolvePromise, reject) => {
    const socket = createConnection(socketPath(options.profile));
    let buffer = "", settled = false;
    socket.setEncoding("utf8");
    const rejectOnce = error => { if (!settled) { settled = true; reject(error); } };
    socket.setTimeout(300_000, () => socket.destroy(new Error("后台任务 5 分钟没有结束。请检查 manifest，并运行 stop 或 doctor。")));
    socket.once("connect", () => socket.write(`${JSON.stringify(options)}\n`));
    socket.on("data", chunk => {
      buffer += chunk;
      let end;
      while ((end = buffer.indexOf("\n")) >= 0) {
        const line = buffer.slice(0, end); buffer = buffer.slice(end + 1);
        try {
          const event = JSON.parse(line);
          if (event.type === "progress") onProgress(event);
          else if (event.type === "result") { settled = true; resolvePromise(event.result); socket.end(); }
          else throw new Error("后台返回未知消息；请先 stop 再重试。");
        } catch (error) { rejectOnce(error); socket.destroy(); }
      }
    });
    socket.once("error", rejectOnce);
    socket.once("close", () => rejectOnce(new Error("后台浏览器连接提前断开；请查看任务 manifest，避免重复提交。")));
  });
}

async function acquireDaemonStartupLock(profile) {
  await mkdir(profile, { recursive: true });
  const lockPath = resolve(profile, ".gemini-image-web.start.lock");
  for (let attempt = 0; attempt < 3; attempt += 1) {
    try {
      const file = await open(lockPath, "wx");
      try { await file.writeFile(`${process.pid}\n`); } finally { await file.close(); }
      return async () => { await unlink(lockPath).catch(() => {}); };
    } catch (error) {
      if (error.code !== "EEXIST") throw error;
      const previous = await readFile(lockPath, "utf8").catch(() => null);
      const pid = Number(previous?.trim());
      if (!Number.isInteger(pid) || pid <= 0) {
        await new Promise(resolvePromise => setTimeout(resolvePromise, 100));
        continue;
      }
      try { process.kill(pid, 0); return null; }
      catch (processError) {
        if (processError.code !== "ESRCH") throw processError;
        await unlink(lockPath).catch(() => {});
      }
    }
  }
  return null;
}

async function connectIfReady(options) {
  try { return await connectDaemon(options); }
  catch (error) {
    if (error.code === "ENOENT" || error.code === "ECONNREFUSED") return null;
    throw error;
  }
}

async function generateWithDaemon(options) {
  let result = await connectIfReady(options);
  if (!result) {
    await mkdir(options.profile, { recursive: true });
    const logPath = resolve(options.profile, "gemini-daemon.log");
    const deadline = Date.now() + 45_000;
    while (!result && Date.now() < deadline) {
      const releaseStartupLock = await acquireDaemonStartupLock(options.profile);
      if (!releaseStartupLock) {
        await new Promise(resolvePromise => setTimeout(resolvePromise, 300));
        result = await connectIfReady(options);
        continue;
      }
      try {
        // Another caller may have started the daemon between our socket check and lock acquisition.
        result = await connectIfReady(options);
        if (!result) {
          const log = await open(logPath, "a");
          let child;
          try {
            child = spawn(process.execPath, [import.meta.filename, "serve", "--profile", options.profile, "--concurrency", String(options.concurrency || 6)], {
              detached: true, stdio: ["ignore", log.fd, log.fd], cwd: REPO_ROOT,
            });
          } finally { await log.close(); }
          child.unref();
          while (!result && Date.now() < deadline) {
            await new Promise(resolvePromise => setTimeout(resolvePromise, 300));
            result = await connectIfReady(options);
            if (child.exitCode !== null && !result) break;
          }
        }
      } finally { await releaseStartupLock(); }
      break;
    }
    if (!result) {
      const detail = (await readFile(logPath, "utf8").catch(() => "")).slice(-1500).trim();
      fail(`后台 Chrome 启动失败。日志：${logPath}\n${detail || "45 秒内未就绪；运行 doctor --headed 检查。"}`);
    }
  }
  for (const line of result.stdout || []) console.log(line);
  for (const line of result.stderr || []) console.error(line);
  if (!result.ok) throw Object.assign(new Error(result.error || "后台生成失败。"), { response_kind: result.response_kind });
}

export async function serve(options, sharedContext = null, handlers = { generate, resume, doctor }) {
  const context = sharedContext || await launch(options);
  const path = socketPath(options.profile);
  await unlink(path).catch(error => { if (error.code !== "ENOENT") throw error; });
  const pool = new TaskPool(options.concurrency || 6);
  const onBrowserClose = () => { pool.blocked = "browser-error"; };
  context.on?.("close", onBrowserClose);
  const logs = new AsyncLocalStorage();
  const oldLog = console.log;
  const oldError = console.error;
  console.log = (...parts) => logs.getStore()?.("stdout", parts) ?? oldLog(...parts);
  console.error = (...parts) => logs.getStore()?.("stderr", parts) ?? oldError(...parts);
  let idleTimer;
  const armIdle = () => {
    clearTimeout(idleTimer);
    if (server.listening && !pool.active.size && !pool.stopping) idleTimer = setTimeout(() => server.close(), IDLE_MS);
  };
  const server = createServer(socket => {
    let request = "";
    socket.setEncoding("utf8");
    socket.on("error", () => {});
    socket.on("data", chunk => {
      request += chunk;
      if (!request.includes("\n")) return;
      const message = request.slice(0, request.indexOf("\n"));
      socket.removeAllListeners("data");
      clearTimeout(idleTimer);
      const reply = result => { if (!socket.destroyed) socket.end(JSON.stringify({ type: "result", result }) + "\n"); };
      const progress = (channel, parts) => {
        if (!socket.destroyed) socket.write(JSON.stringify({ type: "progress", channel, line: parts.join(" ") }) + "\n");
        return true;
      };
      void logs.run(progress, async () => {
        try {
          const job = JSON.parse(message);
          if (job.profile !== options.profile) fail("后台请求无效。");
          if (job.command === "status") {
            reply({ ok: true, stdout: [JSON.stringify({ running: true, ...pool.status() })] });
            return;
          }
          if (job.concurrency !== undefined && job.concurrency !== pool.capacity) fail(`后台容量为 ${pool.capacity}；请先 stop，再用 --concurrency ${job.concurrency} 启动。`);
          if (job.command === "start") {
            reply({ ok: true, stdout: [JSON.stringify({ running: true, ...pool.status() })] });
            return;
          }
          if (job.command === "stop") {
            await pool.stop();
            reply({ ok: true, stdout: ["[gemini-image-web] 后台浏览器已停止。"] });
            server.close();
            return;
          }
          if (!["generate", "resume", "doctor"].includes(job.command)) fail("后台请求无效。");
          const keys = [];
          if (job.command === "generate") {
            keys.push(resolve(job.output_root, safeName(job.session, "--session"), safeName(job.name, "--name")));
            if (job.out) {
              // 输出扩展名取决于下载内容，按无扩展名目标保护。
              const target = job.out_is_dir ? resolve(job.out, job.name) : job.out;
              const extension = extname(target);
              keys.push(IMAGE_EXTENSIONS.has(extension.toLowerCase()) ? target.slice(0, -extension.length) : target);
            }
          }
          await pool.run(await Promise.all(keys.map(canonicalPath)), job.command !== "generate", async () => {
            if (job.command === "doctor") await handlers.doctor(job, context);
            else if (job.command === "resume") await handlers.resume(job, context);
            else await handlers.generate(job, context);
          });
          reply({ ok: true });
        } catch (error) {
          reply({ ok: false, error: error.message, response_kind: error.response_kind });
        } finally { armIdle(); }
      });
    });
  });
  try {
    await new Promise((resolvePromise, reject) => {
      server.once("error", reject);
      server.listen(path, () => {
        server.removeListener("error", reject);
        resolvePromise();
      });
    });
    idleTimer = setTimeout(() => server.close(), IDLE_MS);
    await new Promise(resolvePromise => server.on("close", resolvePromise));
  } finally {
    clearTimeout(idleTimer);
    context.off?.("close", onBrowserClose);
    console.log = oldLog;
    console.error = oldError;
    await unlink(path).catch(() => {});
    await context.close();
  }
}

export async function main(argv = process.argv.slice(2)) {
  const parsed = parseArgs(argv);
  if (parsed.command === "help") {
    console.log(usage());
    return;
  }
  const options = await validateOptions(parsed);
  if (options.command === "serve") return serve(options);
  if (options.command === "start") return generateWithDaemon(options);
  if (options.command === "status" || options.command === "stop") {
    const result = await connectDaemon(options).catch(error => {
      if (error.code === "ENOENT" || error.code === "ECONNREFUSED") return { ok: true, stdout: [options.command === "status" ? JSON.stringify({ running: false }) : "[gemini-image-web] 后台浏览器未运行。"] };
      throw error;
    });
    for (const line of result.stdout || []) console.log(line);
    if (!result.ok) fail(result.error);
    return;
  }
  if (options.command === "login") return login(options);
  if (options.command === "doctor") {
    const result = await connectDaemon(options).catch(error => {
      if (error.code === "ENOENT" || error.code === "ECONNREFUSED") return null;
      throw error;
    });
    if (!result) return doctor(options);
    for (const line of result.stdout || []) console.log(line);
    if (!result.ok) fail(result.error);
    return;
  }
  if (options.command === "generate") return options.headed ? generate(options) : generateWithDaemon(options);
  if (options.command === "resume") {
    if (options.headed) return resume(options);
    const result = await connectDaemon(options).catch(error => {
      if (error.code === "ENOENT" || error.code === "ECONNREFUSED") return null;
      throw error;
    });
    if (!result) return resume(options);
    for (const line of result.stdout || []) console.log(line);
    for (const line of result.stderr || []) console.error(line);
    if (!result.ok) fail(result.error || "后台恢复失败。");
    return;
  }
  fail(`未知命令：${options.command}\n\n${usage()}`);
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) {
  main().then(
    () => process.exit(0),
    error => {
      console.error(`[gemini-image-web] ${error.message}`);
      process.exit(1);
    },
  );
}
