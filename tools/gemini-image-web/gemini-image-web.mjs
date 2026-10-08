#!/usr/bin/env node

import { createHash } from "node:crypto";
import { existsSync } from "node:fs";
import { copyFile, mkdir, open, readFile, rename, stat, unlink, writeFile } from "node:fs/promises";
import { basename, dirname, extname, resolve } from "node:path";
import process from "node:process";
import { spawn } from "node:child_process";
import { createConnection, createServer } from "node:net";
import { chromium } from "playwright";
import { classifyResponse, responseError } from "./response-state.mjs";

const REPO_ROOT = resolve(import.meta.dirname, "../..");
const DEFAULT_OUTPUT_ROOT = resolve(REPO_ROOT, "tmp/gemini-image-web");
const DEFAULT_PROFILE = resolve(process.env.CODEX_HOME || resolve(process.env.HOME || ".", ".codex"), "browser-profiles/gemini-aistudio");
const IMAGES_URL = "https://gemini.google.com/images";

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
  gemini-image-web.mjs stop [--profile DIR]

generate：新开一个 Images 对话，提交提示词（可带参考图），等生成完成后保存图片。
  --out       把图另存到制作目录。扩展名由网页实际返回的格式决定（.png/.jpeg/.webp）：
              --out review/人形/03-faceted        → review/人形/03-faceted.png
              --out review/人形/03-faceted.png    → 同上；写错扩展名会被纠正，不报错
              --out review/人形/                  → review/人形/<name>.png
              给了 --out 就不用再给 --session/--name：session 取上一级目录名，name 取文件名。
  --session   生成图和 manifest 的目录：tmp/gemini-image-web/<session>/<name>/；没有 --out 时必填
  --name      本次候选名，默认按时间戳；同名会覆盖
  --prompt-file -  从 stdin 读提示词
  --reference 参考图，可重复；首次使用需先在网页上同意“Creating content from images and files”；上传约需数秒，CLI 会等进度消失后再提交
通用选项：--output-root DIR --profile DIR --headed --timeout-ms N（默认且最大 90000）
成功时 stdout 前几行是摘要，最后一行是 manifest 路径；等待期间 stderr 每 30 秒打印一次心跳。
同一 --profile 同时只能跑一个任务；连续 generate 自动复用后台 Chrome（空闲 10 分钟后退出）。`;
}

export function parseArgs(argv) {
  const [command, ...rest] = argv;
  if (!command || command === "--help" || command === "-h") return { command: "help" };
  const options = { command, reference: [] };
  for (let i = 0; i < rest.length; i += 1) {
    const token = rest[i];
    if (!token.startsWith("--")) fail(`无法识别的参数：${token}`);
    const key = token.slice(2).replaceAll("-", "_");
    if (key === "headed") {
      options.headed = true;
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
    timeout_ms: Number(options.timeout_ms || DEFAULT_TIMEOUT_MS),
  };
  if (!Number.isFinite(common.timeout_ms) || common.timeout_ms < 1000 || common.timeout_ms > DEFAULT_TIMEOUT_MS) {
    fail("--timeout-ms 必须是 1000 到 90000 之间的数字；图片生成等待不允许超过 90 秒。");
  }
  if (options.mode !== undefined) fail("--mode 已移除：Images 固定使用页面提供的 Nano Banana 2，不应选择 Gemini 对话模型。");
  if (common.out) {
    common.out_is_dir = options.out.endsWith("/") || (existsSync(resolve(options.out)) && (await stat(resolve(options.out))).isDirectory());
    common.out = resolve(common.out);
  }
  if (common.command === "generate") {
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
  await copyFile(result.path, target);
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

function safeName(value, flag) {
  const safe = value.replaceAll(/[^\p{L}\p{N}._-]+/gu, "-").replaceAll(/^-|-$/g, "");
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
    headless: !options.headed,
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

async function assertImagesPage(page) {
  await page.waitForLoadState("domcontentloaded");
  await promptEditor(page).waitFor({ state: "visible", timeout: 30_000 }).catch(() => {});
  // “Create images”标题会因账号实验而变化；Images 工具 chip 是功能就绪信号。
  await page.locator("button[aria-label='Deselect Images']").first().waitFor({ state: "visible", timeout: 30_000 }).catch(() => {});
  const body = await page.locator("body").innerText();
  if (/Sign in|登录/.test(body) && !(await promptEditor(page).count())) {
    fail("Gemini 尚未登录。先运行 login，并在打开的浏览器中完成登录。");
  }
  if (!(await promptEditor(page).count())) {
    fail(`Gemini Images 页面未显示提示词输入框（当前 URL：${page.url()}）。可能尚未登录或网页已改版；运行 doctor --headed 检查。`);
  }
}

async function firstVisible(locator) {
  for (const candidate of await locator.all()) {
    if (await candidate.isVisible().catch(() => false)) return candidate;
  }
  return null;
}

async function uploadReferences(page, references) {
  if (!references.length) return;

  const attachments = page.locator("img[alt='attachment'], img.gem-attachment-style-img");
  // setInputFiles 会立刻修改 DOM，因此新增附件的基线必须在触发上传前采集。
  const attachmentsBeforeUpload = await attachments.count();

  // 当前 Gemini Images 把入口改成了“Upload & tools”。点击后会动态挂载
  // 一个 accept=image/* 的 input；直接设置它比依赖 Material menu 的 DOM 时序稳定，
  // 也能一次性保留多张参考图。
  const add = await firstVisible(page.locator(
    "button[aria-label='Upload & tools'], button[aria-label*='Add files'], button[aria-label*='Upload']",
  ));
  if (!add) fail("未找到可见的图片上传入口；当前页面可能已改版。请运行 doctor --headed 检查。");
  await add.click();

  const imageInput = page.locator("input[type='file'][accept='image/*']").last();
  const directInputReady = await imageInput.waitFor({ state: "attached", timeout: 10_000 })
    .then(() => true).catch(() => false);
  if (directInputReady) {
    await imageInput.setInputFiles(references);
  } else {
    // 兼容仍使用菜单文件选择器的旧版页面。不要用 [role=menu].last()：新版同时
    // 渲染外层工具菜单和内层上传菜单，last() 会受动画/实验分支时序影响。
    const menuItem = page.locator(
      "[data-test-id='local-images-files-uploader-button'], [role='menuitem'][aria-label^='Upload files']",
    ).last();
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
async function submitPrompt(page, prompt, referenceCount) {
  for (let attempt = 1; attempt <= 3; attempt++) {
    const imageMode = page.locator("button[aria-label='Deselect Images']").first();
    if (!(await imageMode.isVisible().catch(() => false))) {
      const imagesEntry = page.getByText("Images", { exact: true }).first();
      await imagesEntry.click().catch(() => {
        fail(`第 ${attempt} 次提交前 Images 模式已丢失，且无法重新选择（当前 URL：${page.url()}）。`);
      });
      await imageMode.waitFor({ state: "visible", timeout: 15_000 }).catch(() => {
        fail(`第 ${attempt} 次提交前无法恢复 Images 模式（当前 URL：${page.url()}）。`);
      });
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
        if (/\/app\/[^/?#]+/.test(location.pathname)) return true;
        return [...document.querySelectorAll("user-query, .user-query, [data-test-id='user-query']")]
          .some(element => element.getClientRects().length > 0 && (element.textContent || "").includes(expectedPrompt));
      },
      prompt,
      { timeout: 8_000 },
    ).then(() => true).catch(() => false);
    if (submitted) return;
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
    // 用户提示词、侧栏和旧回复可能含错误词，只检查当前模型回复。
    const response = page.locator("model-response").last();
    const text = await response.innerText({ timeout: 1_000 }).catch(() => "");
    if (text.trim()) lastResponseText = text.trim();
    const state = classifyResponse(text);
    if (state.kind !== "pending") {
      const redo = response.getByRole("button", { name: "Redo", exact: true });
      if (state.kind === "transient-error" && !retried &&
          timeoutMs - (Date.now() - started) >= 15_000 && await redo.isVisible().catch(() => false)) {
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
    if (Date.now() - lastBeat >= 30_000) {
      lastBeat = Date.now();
      console.error(`[gemini-image-web] 等待生成，已等待 ${formatElapsed(Date.now() - submittedAt)}`);
    }
    await page.waitForTimeout(2_000);
  }
  throw responseError(`等待生成完成超时（${timeoutMs} ms）。`, lastResponseText, "timeout", page.url());
}

async function downloadImage(page, image, outputDir) {
  // 优先保存图片源字节：先尝试页面 fetch，再由 Playwright 请求绕过页面 CORS。
  // 只有源字节不可读时才尝试 canvas 重编码或网页下载按钮。
  let result = await image.evaluate(async element => {
    const width = element.naturalWidth;
    const height = element.naturalHeight;
    try {
      const response = await fetch(element.currentSrc || element.src);
      if (!response.ok) throw new Error(`image fetch returned ${response.status}`);
      const blob = await response.blob();
      const data_url = await new Promise((resolvePromise, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolvePromise(reader.result);
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(blob);
      });
      return { data_url, mime_type: blob.type || "application/octet-stream", width, height, extraction: "source-blob" };
    } catch { return null; }
  }).catch(() => null);
  if (!result) {
    const source = await image.evaluate(element => ({ src: element.currentSrc || element.src, width: element.naturalWidth, height: element.naturalHeight }));
    if (/^https?:\/\//.test(source.src)) {
      const response = await page.context().request.get(source.src).catch(() => null);
      if (response?.ok()) {
        const mime_type = response.headers()["content-type"]?.split(";")[0];
        if (["image/png", "image/jpeg", "image/webp"].includes(mime_type)) {
          result = {
            data_url: `data:${mime_type};base64,${(await response.body()).toString("base64")}`,
            mime_type, width: source.width, height: source.height, extraction: "browser-request",
          };
        }
      }
    }
    if (!result) {
      // 只在取不到源文件时才重编码；跨域图片会使 canvas 受污染，此时继续尝试下载按钮。
      result = await image.evaluate(element => {
        const canvas = document.createElement("canvas");
        canvas.width = element.naturalWidth;
        canvas.height = element.naturalHeight;
        canvas.getContext("2d").drawImage(element, 0, 0);
        return {
          data_url: canvas.toDataURL("image/png"), mime_type: "image/png",
          width: canvas.width, height: canvas.height, extraction: "canvas-png",
        };
      }).catch(() => null);
    }
    if (!result) {
      const downloadButton = page.locator("button[aria-label*='Download'], button[aria-label*='download']").last();
      if (await downloadButton.count()) {
        const downloadPromise = page.waitForEvent("download", { timeout: 15_000 });
        await downloadButton.click();
        const download = await downloadPromise;
        const name = download.suggestedFilename().toLowerCase();
        const mime_type = name.endsWith(".png") ? "image/png" : name.endsWith(".webp") ? "image/webp" : /\.jpe?g$/.test(name) ? "image/jpeg" : null;
        if (!mime_type) fail(`Gemini 下载了不支持的图片格式：${name}`);
        const temporary = resolve(outputDir, `download${extname(name)}`);
        await download.saveAs(temporary);
        result = {
          data_url: `data:${mime_type};base64,${(await readFile(temporary)).toString("base64")}`,
          mime_type, width: source.width, height: source.height, extraction: "download-button",
        };
        await unlink(temporary);
      }
    }
    if (!result) fail(`无法读取生成图（图片 URL：${source.src.slice(0, 160)}）。`);
  }
  const extensions = { "image/jpeg": ".jpeg", "image/png": ".png", "image/webp": ".webp" };
  const extension = extensions[result.mime_type];
  if (!extension) fail(`Gemini 返回了不支持的图片格式：${result.mime_type}`);
  const destination = resolve(outputDir, `image${extension}`);
  const separator = result.data_url.indexOf(",");
  if (separator < 0) fail("页面返回了无效的图片数据。");
  await writeFile(destination, Buffer.from(result.data_url.slice(separator + 1), "base64"));
  return {
    path: destination,
    mime_type: result.mime_type,
    width: result.width,
    height: result.height,
    extraction: result.extraction,
    sha256: await sha256(destination),
  };
}

async function generate(options, sharedContext = null) {
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
    references: await Promise.all(options.reference.map(async path => ({ path, sha256: await sha256(path) }))),
    output_dir: outputDir,
    created_at: new Date().toISOString(),
  };
  await writeFile(resolve(outputDir, "prompt.txt"), `${options.prompt}\n`);
  await atomicJson(manifestPath, manifest);

  const context = sharedContext || await launch(options);
  let page;
  try {
    page = context.pages()[0] || await context.newPage();
    await page.goto(IMAGES_URL, { waitUntil: "domcontentloaded" });
    await assertImagesPage(page);
    await uploadReferences(page, options.reference);
    const editor = promptEditor(page);
    // fill 会直接聚焦可编辑文本框；Images 欢迎卡有时覆盖鼠标点击区域。
    await editor.fill(options.prompt);
    await page.waitForTimeout(300);
    const submittedAt = Date.now();
    const send = page.locator("button[aria-label='Send message']").first();
    await send.waitFor({ state: "visible", timeout: 10_000 }).catch(() => fail("填写提示词后未找到 Send message 按钮；网页可能已改版。"));
    if (await send.isDisabled()) fail("填写提示词后 Send message 按钮仍不可用，任务没有提交。");
    await submitPrompt(page, options.prompt, options.reference.length);
    manifest.status = "generating";
    manifest.submitted_at = new Date(submittedAt).toISOString();
    await atomicJson(manifestPath, manifest);
    // /app 是普通新聊天的中间页；真正的生成会进入 /app/<conversation-id>。
    // 不能只等 /app，否则附件尚未同步时的误提交会被当成已进入生成会话，
    // 随后 waitForResult 只能无意义地超时。
    const elapsedAfterSubmit = Date.now() - submittedAt;
    const conversationWait = Math.min(10_000, Math.max(1_000, options.timeout_ms - elapsedAfterSubmit));
    await page.waitForURL(/gemini\.google\.com\/app\/[^/?#]+/, { timeout: conversationWait }).catch(() => {
      fail(`提交后未进入带会话 ID 的 Gemini Images 对话（当前 URL：${page.url()}）。提示词可能在附件同步完成前提交，或网页已改版。`);
    });
    manifest.conversation_url = page.url();
    const remaining = options.timeout_ms - (Date.now() - submittedAt);
    if (remaining <= 0) fail(`等待生成完成超时（${options.timeout_ms} ms）。对话仍保留在网页历史中：${page.url()}`);
    const image = await waitForResult(page, remaining, submittedAt);
    const result = await downloadImage(page, image, outputDir);
    manifest.status = "completed";
    manifest.completed_at = new Date().toISOString();
    manifest.conversation_url = page.url();
    manifest.download = result;
    await saveOut(options, result, manifest);
    delete manifest.error;
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
    if (typeof error.response_text === "string") {
      manifest.response_text = error.response_text;
      manifest.response_kind = error.response_kind;
      manifest.conversation_url = error.conversation_url;
      manifest.response_file = resolve(manifest.output_dir, "response.txt");
      await writeFile(manifest.response_file, error.response_text, "utf8");
    }
    manifest.updated_at = new Date().toISOString();
    if (page) {
      const screenshot = resolve(outputDir, "failure.png");
      await page.screenshot({ path: screenshot, fullPage: true }).catch(() => {});
      manifest.failure_screenshot = screenshot;
    }
    await atomicJson(manifestPath, manifest);
    throw error;
  } finally {
    if (!sharedContext) await context.close();
  }
}

async function resume(options, sharedContext = null) {
  const manifest = JSON.parse(await readFile(options.manifest, "utf8"));
  if (manifest.status === "completed") fail(`任务已经完成：${manifest.download?.path || options.manifest}`);
  if (!manifest.conversation_url) fail("manifest 没有 conversation_url，任务可能尚未成功提交，不能 resume。");
  const conversation = new URL(manifest.conversation_url);
  if (conversation.origin !== "https://gemini.google.com" || !/^\/app\/[^/?#]+$/.test(conversation.pathname)) {
    fail(`manifest 的 conversation_url 不是有效的 Gemini 会话地址：${manifest.conversation_url}`);
  }
  const outputDir = manifest.output_dir || dirname(options.manifest);
  const context = sharedContext || await launch(options);
  let page;
  try {
    page = sharedContext && context.pages().find(candidate => candidate.url() === manifest.conversation_url);
    if (!page) {
      page = context.pages()[0] || await context.newPage();
      await page.goto(manifest.conversation_url, { waitUntil: "domcontentloaded" });
    }
    // Gemini 会先对不存在/未持久化的会话返回页面壳，数秒后才重定向到 /app。
    // 同时观察会话路径和用户回合，不能用固定 sleep 猜 SPA 的重定向时刻。
    const promptPrefix = manifest.prompt?.replaceAll(/\s+/g, " ").trim().slice(0, 80);
    if (promptPrefix) {
      const stateHandle = await page.waitForFunction(
        ({ expectedPath, prefix }) => {
          if (location.pathname !== expectedPath) return "redirected";
          const matches = [...document.querySelectorAll("user-query, .user-query, [data-test-id='user-query']")]
            .some(element => (element.textContent || "").replaceAll(/\s+/g, " ").includes(prefix));
          return matches ? "matched" : false;
        },
        { expectedPath: conversation.pathname, prefix: promptPrefix },
        { timeout: 15_000 },
      ).catch(() => null);
      const state = stateHandle ? await stateHandle.jsonValue() : "timeout";
      if (state === "redirected") {
        fail(`Gemini 无法重新打开该会话：${manifest.conversation_url}（已重定向到 ${page.url()}）。生成中的页面可能在浏览器关闭时被取消，不能 resume；请重新生成。`);
      }
      if (state !== "matched") {
        fail(`已打开会话，但找不到与 manifest 匹配的用户提示词；拒绝从可能错误的会话下载：${page.url()}`);
      }
    }
    const image = await waitForResult(page, options.timeout_ms, Date.now());
    const result = await downloadImage(page, image, outputDir);
    manifest.status = "completed";
    manifest.completed_at = new Date().toISOString();
    manifest.download = result;
    options.name = options.name || manifest.name;    // --out 是目录时文件名取自 manifest
    await saveOut(options, result, manifest);
    delete manifest.error;
    delete manifest.response_text;
    delete manifest.response_kind;
    delete manifest.response_file;
    delete manifest.failure_screenshot;
    await atomicJson(options.manifest, manifest);
    console.log(`[gemini-image-web] 已恢复并保存：${result.path}\n  ${result.width}x${result.height}; ${result.mime_type}; ${result.extraction}\n${options.manifest}`);
  } catch (error) {
    manifest.status = "interrupted";
    manifest.error = error.message;
    if (typeof error.response_text === "string") {
      manifest.response_text = error.response_text;
      manifest.response_kind = error.response_kind;
      manifest.conversation_url = error.conversation_url;
      manifest.response_file = resolve(manifest.output_dir, "response.txt");
      await writeFile(manifest.response_file, error.response_text, "utf8");
    }
    manifest.updated_at = new Date().toISOString();
    if (page) {
      const screenshot = resolve(outputDir, "failure.png");
      await page.screenshot({ path: screenshot, fullPage: true }).catch(() => {});
      manifest.failure_screenshot = screenshot;
    }
    await atomicJson(options.manifest, manifest);
    throw error;
  } finally {
    if (!sharedContext) await context.close();
  }
}

async function doctor(options) {
  const context = await launch(options);
  try {
    const page = context.pages()[0] || await context.newPage();
    await page.goto(IMAGES_URL, { waitUntil: "domcontentloaded" });
    await assertImagesPage(page);
    const imageMode = await page.locator("button[aria-label='Deselect Images']").count();
    const add = await page.locator("button[aria-label*='Add files'], button[aria-label*='Upload']").count();
    console.log(`images: ok (Images 工具: ${imageMode ? "已选择" : "未找到"}; 上传按钮: ${add ? "有" : "无"})`);
  } finally {
    await context.close();
  }
}

async function login(options) {
  const context = await launch({ ...options, headed: true });
  try {
    const page = context.pages()[0] || await context.newPage();
    await page.goto(IMAGES_URL, { waitUntil: "domcontentloaded" });
    console.log("浏览器已打开。完成登录后关闭浏览器窗口（Cmd+Q）；会话会保存在：", options.profile);
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

function connectDaemon(options) {
  return new Promise((resolvePromise, reject) => {
    const socket = createConnection(socketPath(options.profile));
    let response = "";
    socket.once("connect", () => socket.write(`${JSON.stringify(options)}\n`));
    socket.on("data", chunk => { response += chunk; });
    socket.once("error", reject);
    socket.once("end", () => {
      try { resolvePromise(JSON.parse(response)); }
      catch { reject(new Error("后台浏览器返回了无效响应。")); }
    });
  });
}

async function generateWithDaemon(options) {
  let result;
  try {
    result = await connectDaemon(options);
  } catch (error) {
    if (error.code !== "ENOENT" && error.code !== "ECONNREFUSED") throw error;
    const child = spawn(process.execPath, [import.meta.filename, "serve", "--profile", options.profile], {
      detached: true, stdio: "ignore", cwd: REPO_ROOT,
    });
    child.unref();
    const deadline = Date.now() + 45_000;
    while (Date.now() < deadline) {
      await new Promise(resolvePromise => setTimeout(resolvePromise, 300));
      try { result = await connectDaemon(options); break; }
      catch (retryError) {
        if (retryError.code !== "ENOENT" && retryError.code !== "ECONNREFUSED") throw retryError;
      }
    }
    if (!result) fail("后台 Chrome 未能在 45 秒内启动；运行 doctor --headed 检查登录和浏览器环境。");
  }
  for (const line of result.stdout || []) console.log(line);
  for (const line of result.stderr || []) console.error(line);
  if (!result.ok) fail(result.error || "后台生成失败。");
}

async function serve(options) {
  const context = await launch(options);
  const path = socketPath(options.profile);
  await unlink(path).catch(error => { if (error.code !== "ENOENT") throw error; });
  let queue = Promise.resolve();
  let idleTimer;
  const server = createServer(socket => {
    let request = "";
    socket.on("data", chunk => {
      request += chunk;
      if (!request.includes("\n")) return;
      const message = request.slice(0, request.indexOf("\n"));
      socket.removeAllListeners("data");
      clearTimeout(idleTimer);
      queue = queue.then(async () => {
        const stdout = [];
        const stderr = [];
        const oldLog = console.log;
        const oldError = console.error;
        try {
          const job = JSON.parse(message);
          if (job.profile !== options.profile) fail("后台请求无效。");
          if (job.command === "stop") {
            socket.end(JSON.stringify({ ok: true, stdout: ["[gemini-image-web] 后台浏览器已停止。"] }));
            server.close();
            return;
          }
          if (job.command !== "generate" && job.command !== "resume") fail("后台请求无效。");
          console.log = (...parts) => stdout.push(parts.join(" "));
          console.error = (...parts) => stderr.push(parts.join(" "));
          if (job.command === "resume") await resume(job, context);
          else await generate(job, context);
          socket.end(JSON.stringify({ ok: true, stdout, stderr }));
        } catch (error) {
          socket.end(JSON.stringify({ ok: false, error: error.message, stdout, stderr }));
        } finally {
          console.log = oldLog;
          console.error = oldError;
          if (server.listening) idleTimer = setTimeout(() => server.close(), IDLE_MS);
        }
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
  if (options.command === "stop") {
    const result = await connectDaemon(options).catch(error => {
      if (error.code === "ENOENT" || error.code === "ECONNREFUSED") return { ok: true, stdout: ["[gemini-image-web] 后台浏览器未运行。"] };
      throw error;
    });
    for (const line of result.stdout || []) console.log(line);
    if (!result.ok) fail(result.error);
    return;
  }
  if (options.command === "login") return login(options);
  if (options.command === "doctor") return doctor(options);
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
