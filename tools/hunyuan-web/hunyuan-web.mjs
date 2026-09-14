#!/usr/bin/env node

import { createHash } from "node:crypto";
import { existsSync } from "node:fs";
import { copyFile, mkdir, readFile, readdir, rename, stat, unlink, writeFile } from "node:fs/promises";
import { basename, dirname, extname, resolve } from "node:path";
import process from "node:process";
import { chromium } from "playwright";

const REPO_ROOT = resolve(import.meta.dirname, "../..");
const DEFAULT_OUTPUT_ROOT = resolve(REPO_ROOT, "tmp/hunyuan-web");
const DEFAULT_PROFILE = resolve(process.env.CODEX_HOME || resolve(process.env.HOME || ".", ".codex"), "browser-profiles/hunyuan-studio");
const GEO_URL = "https://3d.hunyuan.tencent.com/studio/creation/geo";
const POLY_URL = "https://3d.hunyuan.tencent.com/studio/creation/poly";

export const LEVELS = new Set(["low", "medium", "high"]);
export const TOPOLOGIES = new Set(["triangle", "quad"]);
export const GEO_FACES = new Set(["50k", "500k", "1m", "1.5m"]);
export const DOWNLOAD_FORMATS = new Set(["fbx", "stl", "usdz"]);

function fail(message) {
  throw new Error(message);
}

function usage() {
  return `Hunyuan Studio 网页自动化

两件以上一律用 batch（一次提交、一起等待、逐个下载）；geo/poly 单件命令只适合零散的一件。

用法：
  hunyuan-web.mjs login [--profile DIR]
  hunyuan-web.mjs doctor [--profile DIR] [--headed]
  hunyuan-web.mjs geo --image FILE --session NAME [--faces 50k|500k|1m|1.5m] [--out FILE]
  hunyuan-web.mjs poly --source FILE --session NAME [--level low|medium|high] [--topology triangle|quad] [--out FILE]
  hunyuan-web.mjs resume --manifest FILE [--out FILE]
  hunyuan-web.mjs batch --jobs FILE
  hunyuan-web.mjs assets --stage geo|poly            列出该页资产卡（序号、状态、预览时间戳）
  hunyuan-web.mjs fetch --stage geo|poly --preview URL --out FILE   下载指定预览 URL 的那张卡

batch：一个浏览器里连续提交多个同阶段任务，再一起等待；全部完成后用独立浏览器逐个下载。--jobs 是 JSON 数组，
Geo 每项 {stage:"geo", image, session, faces?, out?}，Poly 每项 {stage:"poly", source, session, level?, topology?, out?}；
一批只能是同一 stage（Geo 与 Poly 是两个页面）。每个任务仍各自写 manifest。

通用选项：--output-root DIR --profile DIR --headed --base-url URL --timeout-ms N --format fbx|stl|usdz
--out FILE：完成后把结果另存一份到该路径（扩展名须与 --format 一致），省去从 manifest 里找随机文件名。
成功时 stdout 最后一行是 manifest 路径，前面一行是结果摘要；等待期间 stderr 每 30 秒打印一次心跳。
下载失败时 manifest 状态是 ready_to_download（任务已完成、额度已扣），直接 resume --manifest 即可重新下载，不会重新提交。
同一 --profile 同时只能跑一个任务（浏览器 profile 不能共享），第二个进程会立即报错。`;
}

export function parseArgs(argv) {
  const [command, ...rest] = argv;
  if (!command || command === "--help" || command === "-h") return { command: "help" };
  const options = { command };
  for (let i = 0; i < rest.length; i += 1) {
    const token = rest[i];
    if (!token.startsWith("--")) fail(`无法识别的参数：${token}`);
    const key = token.slice(2).replaceAll("-", "_");
    if (key === "headed") {
      options.headed = true;
      continue;
    }
    const value = rest[++i];
    if (!value || value.startsWith("--")) fail(`${token} 缺少值。`);
    options[key] = value;
  }
  return options;
}

export function validateOptions(options) {
  const common = {
    ...options,
    output_root: resolve(options.output_root || DEFAULT_OUTPUT_ROOT),
    profile: resolve(options.profile || DEFAULT_PROFILE),
    timeout_ms: Number(options.timeout_ms || 45 * 60 * 1000),
    format: (options.format || "fbx").toLowerCase(),
  };
  if (!Number.isFinite(common.timeout_ms) || common.timeout_ms < 1000) fail("--timeout-ms 必须是不小于 1000 的数字。");
  if (!DOWNLOAD_FORMATS.has(common.format)) fail("--format 只能是 fbx、stl 或 usdz。");
  if (common.out) {
    common.out = resolve(common.out);
    if (extname(common.out).toLowerCase() !== `.${common.format}`) fail(`--out 的扩展名必须是 .${common.format}（与 --format 一致）。`);
  }
  if (["geo", "poly"].includes(common.command) && !common.session) fail(`${common.command} 需要 --session。`);
  if (common.command === "batch" && !common.jobs) fail("batch 需要 --jobs。");
  if (["assets", "fetch"].includes(common.command) && !["geo", "poly"].includes(common.stage)) fail(`${common.command} 需要 --stage geo|poly。`);
  if (common.command === "fetch" && !(common.preview && common.out)) fail("fetch 需要 --preview 与 --out。");
  if (common.command === "geo") {
    if (!common.image) fail("geo 需要 --image。");
    common.image = resolve(common.image);
    common.faces = (common.faces || "50k").toLowerCase();
    if (!GEO_FACES.has(common.faces)) fail("--faces 只能是 50k、500k、1m 或 1.5m。");
  }
  if (common.command === "poly") {
    if (!common.source) fail("poly 需要 --source。");
    common.source = resolve(common.source);
    common.level = (common.level || "low").toLowerCase();
    common.topology = (common.topology || "quad").toLowerCase();
    if (!LEVELS.has(common.level)) fail("--level 只能是 low、medium 或 high。");
    if (!TOPOLOGIES.has(common.topology)) fail("--topology 只能是 triangle 或 quad。");
  }
  return common;
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

// 同一 session 下不同档位各占一个目录，改 --level 重试不会覆盖上一档的 manifest。
export function stageDirectoryName(options, stage) {
  return stage === "geo" ? `geo-${options.faces}` : `poly-${options.level}-${options.topology}`;
}

function sessionDirectory(options, stage) {
  const safe = options.session.replaceAll(/[^\p{L}\p{N}._-]+/gu, "-").replaceAll(/^-|-$/g, "");
  if (!safe) fail("--session 必须至少包含一个字母或数字。");
  return resolve(options.output_root, safe, stageDirectoryName(options, stage));
}

// Playwright 持久化 profile 不能被两个进程同时打开；用锁文件把并发变成明确的报错。
async function acquireProfileLock(profile) {
  await mkdir(profile, { recursive: true });
  const lockPath = resolve(profile, ".hunyuan-web.lock");
  const previous = await readFile(lockPath, "utf8").catch(() => null);
  if (previous) {
    const pid = Number(previous.trim());
    let alive = false;
    try { process.kill(pid, 0); alive = true; } catch {}
    if (alive) fail(`另一个 hunyuan-web 进程（pid ${pid}）正在使用同一浏览器 profile；请等它结束后再提交，或用 --profile 指定另一份已登录的 profile。`);
  }
  await writeFile(lockPath, `${process.pid}\n`);
  return async () => { await unlink(lockPath).catch(() => {}); };
}

function heartbeat(label, intervalMs = 30_000) {
  const started = Date.now();
  let last = started;
  return status => {
    if (Date.now() - last < intervalMs) return;
    last = Date.now();
    console.error(`[hunyuan-web] ${label} ${status}，已等待 ${formatElapsed(Date.now() - started)}`);
  };
}

function formatElapsed(ms) {
  const total = Math.round(ms / 1000);
  return `${Math.floor(total / 60)}m${String(total % 60).padStart(2, "0")}s`;
}

async function finishResult(options, manifest, result) {
  const lines = [`[hunyuan-web] ${manifest.stage} 完成：${result.path}`, `  sha256=${result.sha256}`];
  if (options.out) {
    await mkdir(dirname(options.out), { recursive: true });
    await copyFile(result.path, options.out);
    manifest.out = options.out;
    lines.push(`  另存到：${options.out}`);
  }
  if (manifest.remaining_quota_before_submit !== undefined) lines.push(`  提交前今日剩余次数=${manifest.remaining_quota_before_submit}`);
  lines.push(`  耗时=${formatElapsed(Date.now() - new Date(manifest.created_at).getTime())}`);
  console.log(lines.join("\n"));
}

async function launch(options) {
  const release = await acquireProfileLock(options.profile);
  const context = await chromium.launchPersistentContext(options.profile, {
    channel: "chrome",
    headless: !options.headed,
    acceptDownloads: true,
    downloadsPath: options.download_dir,
    viewport: { width: 1512, height: 982 },
  }).catch(async error => { await release(); throw error; });
  const close = context.close.bind(context);
  context.close = async () => { try { await close(); } finally { await release(); } };
  if (process.env.HUNYUAN_DEBUG) {
    context.on("close", () => console.error("[debug] context closed"));
    context.on("page", p => { console.error("[debug] new page", p.url()); p.on("close", () => console.error("[debug] page closed", p.url())); });
    for (const p of context.pages()) p.on("close", () => console.error("[debug] page closed", p.url()));
  }
  return context;
}

async function assertStudio(page, stage) {
  await page.waitForLoadState("domcontentloaded");
  const expected = stage === "geo" ? "几何生成" : "低模生成";
  await page.getByText(expected, { exact: true }).first().waitFor({ state: "visible", timeout: 30_000 }).catch(() => {});
  const body = await page.locator("body").innerText();
  if (/登录|扫码登录|验证码/.test(body) && !/今日剩余生成次数/.test(body)) {
    fail(`Hunyuan Studio 尚未登录。先运行 login，并在打开的浏览器中完成登录。`);
  }
  if (!body.includes(expected)) {
    fail(`Hunyuan Studio 未显示“${expected}”工作区（当前 URL：${page.url()}）。可能尚未登录、无头浏览器被拦截或网页已改版；运行 login 或 doctor --headed 检查。`);
  }
}

async function studioImageSources(page, cardsOnly = false) {
  return page.locator('img[src*="/3DGameStudio/"]').evaluateAll((images, onlyCards) => images
    .filter(image => !onlyCards || Array.from(document.querySelectorAll("div")).some(div =>
      div.className.includes("aspect-square") && div.contains(image)))
    .map(image => image.src), cardsOnly);
}

async function setUpload(page, file, waitForImage = false) {
  if (!existsSync(file)) fail(`上传文件不存在：${file}`);
  const before = waitForImage ? new Set(await studioImageSources(page)) : null;
  const input = page.locator('input[type="file"]').first();
  await input.waitFor({ state: "attached", timeout: 30_000 });
  await input.setInputFiles(file);
  const started = Date.now();
  while (Date.now() - started < 30_000) {
    const body = await page.locator("body").innerText();
    let imagesReady = !before;
    if (before) {
      const freshSources = (await studioImageSources(page)).filter(source => !before.has(source));
      for (const source of freshSources) {
        const image = page.locator(`img[src="${source}"]`).first();
        const loaded = await image.evaluate(element => element.complete && element.naturalWidth > 0).catch(() => false);
        if (loaded && await image.isVisible().catch(() => false)) {
          imagesReady = true;
          break;
        }
      }
    }
    //  站点会在页面里直接写拒绝原因（如“不支持上传灰度图”：R=G=B 的图会被拒，哪怕是 RGB 编码；
    //  给图加一点点色偏再传）。抓到就立刻报，不要等到“立即生成”超时。
    const rejected = body.match(/不支持上传[^\s]*|图片(?:格式|大小|分辨率)[^\s]*不(?:支持|符合)[^\s]*|上传失败[^\s]*/);
    if (rejected) fail(`站点拒绝了上传：${rejected[0]}（${file}）`);
    if (!/上传中/.test(body) && imagesReady) return;
    await page.waitForTimeout(250);
  }
  fail(`上传未在 30 秒内完成：${file}`);
}

async function chooseText(page, text) {
  const target = page.getByText(text, { exact: true }).first();
  await target.waitFor({ state: "visible", timeout: 30_000 });
  await target.click();
}

async function chooseRadio(page, name) {
  const radio = page.getByRole("radio", { name, exact: true }).first();
  await radio.waitFor({ state: "visible", timeout: 30_000 });
  await radio.locator("xpath=ancestor::label[1]").click();
  if (!await radio.isChecked()) fail(`无法选择“${name}”。`);
  await page.locator('input[type="file"]').first().waitFor({ state: "attached", timeout: 30_000 });
}

function remainingQuota(body) {
  const match = body.match(/今日剩余生成次数[：:]\s*(\d+)/);
  return match ? Number(match[1]) : null;
}

export { remainingQuota };

export function assertHasQuota(body, stage) {
  const quota = remainingQuota(body);
  if (quota === null) {
    fail("无法读取“今日剩余生成次数”；网页可能已改版，或尚未登录。");
  }
  if (quota <= 0) {
    const label = stage === "geo" ? "几何生成（Geo）" : "低模生成（Poly）";
    fail(`Hunyuan Studio 今日剩余生成次数为 0，${label}暂不可用。请改日再试，或改用其他网格服务。`);
  }
  return quota;
}

function resultSignature(body) {
  return body
    .replace(/今日剩余生成次数[：:]\s*\d+/, "")
    .replace(/上传中\.\.\.|生成中|排队中|处理中|正在生成/g, "")
    .trim();
}

export function singaporeTimestamp(date) {
  return new Date(date.getTime() + 8 * 60 * 60 * 1000)
    .toISOString().replaceAll(/[-:T]/g, "").slice(0, 14);
}

export function assetTimestamp(source) {
  return source.match(/\/(\d{14})_[^/]+\.(?:png|jpg|jpeg)(?:\?|$)/i)?.[1] || null;
}

async function assetCardState(card) {
  return card.evaluate(element => {
    const text = (element.innerText || "").trim();
    if (/生成失败|任务失败/.test(text)) return { status: "failed", preview: null };
    const image = element.querySelector('img[src*="/3DGameStudio/"]');
    const html = element.innerHTML.slice(0, 4_000);
    const hasProgress = Boolean(element.querySelector('[class*="progress"], [role="progressbar"], .t-progress'))
      || /progress|t-loading|loading-spin/i.test(html);
    if (/生成中|排队中|处理中|正在生成/.test(text) || hasProgress || !image) {
      return { status: "pending", preview: null };
    }
    if (!(image.complete && image.naturalWidth > 0)) return { status: "pending", preview: null };
    return { status: "ready", preview: image.src };
  }).catch(() => ({ status: "pending", preview: null }));
}

async function clickAssetCardByPreview(page, preview, timeoutMs = 30_000) {
  //  用真实鼠标事件点缩略图：DOM 上合成的 click() 不会让查看器切换到这张卡，
  //  之后“下载”下载的还是上一张。
  const image = page.locator(`img[src="${preview}"]`).first();
  await image.waitFor({ state: "visible", timeout: timeoutMs }).catch(() => {});
  if (!await image.count() || !await image.isVisible().catch(() => false)) {
    fail(`未能在资产列表中点选预览：${preview}`);
  }
  await image.scrollIntoViewIfNeeded();
  await image.click();
}

async function lockSubmittedTaskCard(page, timeoutMs, jobTag = null, baselineTag = null) {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    const selector = baselineTag
      ? `div[class*="aspect-square"]:not([data-codex-baseline="${baselineTag}"])`
      : 'div[class*="aspect-square"]:not([data-codex-job])';
    const first = page.locator(selector).first();
    if (await first.count() && (await assetCardState(first)).status === "pending") {
      const tag = jobTag || "true";
      await first.evaluate((element, value) => {
        element.setAttribute("data-codex-submitted-task", value);
        if (value !== "true") element.setAttribute("data-codex-job", value);
      }, tag);
      return page.locator(`[data-codex-submitted-task="${tag}"]`);
    }
    await page.waitForTimeout(250);
  }
  fail("提交后未找到处于生成中/排队中的任务卡片；Poly 进度条卡与 Geo 文案卡都会被识别。");
}

async function waitForGeneration(page, timeoutMs, baseline = null, requireStart = true, onAssetSelected = null) {
  const started = Date.now();
  if (requireStart) {
    let observedStart = false;
    while (Date.now() - started < Math.min(timeoutMs, 30_000)) {
      const body = await page.locator("body").innerText();
      const button = page.locator("button").filter({ hasText: "立即生成" }).first();
      if (/生成中|排队中|处理中|正在生成/.test(body)
        || !await button.isEnabled().catch(() => true)
        || (baseline?.quota && remainingQuota(body) !== baseline.quota)) {
        observedStart = true;
        break;
      }
      await page.waitForTimeout(250);
    }
    if (!observedStart) fail("提交后未观察到生成任务启动；为避免误下载输入模型，已停止。请检查页面或 manifest。");
  }
  let selectedAsset = baseline?.selected_asset || null;
  let submittedCard = requireStart ? await lockSubmittedTaskCard(page, 30_000) : null;
  const beat = heartbeat("等待生成");
  while (Date.now() - started < timeoutMs) {
    const body = await page.locator("body").innerText();
    const stillRunning = /生成中|排队中|处理中|正在生成/.test(body);
    beat(selectedAsset ? "结果已就绪，等待下载按钮" : stillRunning ? "任务生成中" : "等待任务卡完成");
    const download = page.locator("button").filter({ hasText: "下载" }).last();
    if (!selectedAsset && submittedCard) {
      const state = await assetCardState(submittedCard);
      if (state.status === "failed") fail("Hunyuan Studio 报告本次生成失败。");
      if (state.status === "ready") {
        const timestamp = assetTimestamp(state.preview);
        if (baseline?.asset_not_before && timestamp && timestamp < baseline.asset_not_before) {
          fail(`锁定任务卡完成后的预览早于本次提交（${timestamp} < ${baseline.asset_not_before}），拒绝下载旧资产。`);
        }
        selectedAsset = state.preview;
        await submittedCard.click();
        if (onAssetSelected) await onAssetSelected(selectedAsset);
        submittedCard = null;
        await page.waitForTimeout(500);
      }
    } else if (!selectedAsset && baseline?.asset_sources) {
      const current = await studioImageSources(page, true);
      const fresh = current.find(source => {
        const timestamp = assetTimestamp(source);
        return !baseline.asset_sources.includes(source)
          && (!baseline.asset_not_before || (timestamp && timestamp >= baseline.asset_not_before));
      });
      if (fresh) {
        await clickAssetCardByPreview(page, fresh);
        selectedAsset = fresh;
        if (onAssetSelected) await onAssetSelected(fresh);
        await page.waitForTimeout(500);
      }
    }
    const changedResult = !baseline || resultSignature(body) !== baseline.signature;
    const correctAsset = !baseline?.asset_sources || Boolean(selectedAsset);
    const canDownload = correctAsset && await download.isVisible().catch(() => false);
    // 已点选本次完成卡后，以任务卡为准；中心遮罩偶发残留“生成中”时不要卡住。
    if (canDownload && selectedAsset && (!stillRunning || !submittedCard)) return download;
    if (canDownload && !stillRunning && changedResult) return download;
    await page.waitForTimeout(5_000);
  }
  fail(`等待生成完成超时（${timeoutMs} ms）。manifest 已保留，可用 resume 继续。`);
}

// 单件 geo/poly 与 resume 的下载走这里：等待阶段用的那个浏览器只负责监控；下载在一个新开的独立
// context 里按锁定的预览 URL 点卡下载（与 batch 相同）。blob 下载偶发令 Chrome 自行退出
// （download.saveAs: Target page ... closed），那时监控页早已关掉也无所谓，重开一个即可。
async function downloadByPreview(options, manifest, outputDir) {
  let context;
  try {
    context = await launch({ ...options, download_dir: outputDir });
    const page = context.pages()[0] || await context.newPage();
    // Poly 会在页面导航时立即加载当前模型；监听必须早于 goto，否则只能等后续某次重载。
    const observedModelUrls = trackModelRequests(page);
    await page.goto(manifest.page_url);
    await assertStudio(page, manifest.stage);
    return await downloadCard(page, manifest.remote_asset_preview, outputDir, options.format, observedModelUrls);
  } finally {
    await context?.close().catch(() => {});
  }
}

async function downloadResult(page, outputDir, timeoutMs, format, baseline = null, requireStart = true, onAssetSelected = null) {
  const trigger = await waitForGeneration(page, timeoutMs, baseline, requireStart, onAssetSelected);
  await trigger.click();
  return saveDownload(page, outputDir, format);
}

async function waitForNativeDownload(startedAt, format, timeoutMs = 10_000) {
  const directory = resolve(process.env.HOME || ".", "Downloads");
  const previousSizes = new Map();
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    const candidates = [];
    for (const name of await readdir(directory).catch(() => [])) {
      if (!name.startsWith(".com.google.Chrome.")) continue;
      const path = resolve(directory, name);
      const info = await stat(path).catch(() => null);
      if (!info?.isFile() || info.size === 0 || info.mtimeMs < startedAt - 2_000) continue;
      if (format === "fbx") {
        const header = (await readFile(path)).subarray(0, 24).toString("ascii");
        if (!header.startsWith("Kaydara FBX")) continue;
      }
      candidates.push({ path, size: info.size, mtime: info.mtimeMs });
    }
    candidates.sort((a, b) => b.mtime - a.mtime);
    if (candidates.length === 1 && previousSizes.get(candidates[0].path) === candidates[0].size) return candidates[0].path;
    for (const candidate of candidates) previousSizes.set(candidate.path, candidate.size);
    await new Promise(resolvePromise => setTimeout(resolvePromise, 500));
  }
  return null;
}

// 已点开“下载”菜单后：选格式、接住下载、落盘并算哈希。Chrome 偶发不走 Playwright 的下载
// 事件，所以还有两级兜底：扫输出目录里的新文件，再扫 ~/Downloads 里的 Chrome 临时文件。
// 两个 YYYYMMDDHHmmss 时间戳是否在 tolerance 秒之内。
export function timestampClose(a, b, tolerance = 2) {
  const parse = t => {
    const m = /^(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})$/.exec(t || "");
    return m ? Date.UTC(+m[1], +m[2] - 1, +m[3], +m[4], +m[5], +m[6]) : NaN;
  };
  const x = parse(a), y = parse(b);
  return Number.isFinite(x) && Number.isFinite(y) && Math.abs(x - y) <= tolerance * 1000;
}

export function trackModelRequests(page) {
  const urls = [];
  page.on("request", request => {
    const url = request.url();
    if (/\/3DGameStudio\/.*\.fbx(?:\?|$)/i.test(url)) urls.push(url);
  });
  return urls;
}

export async function saveViewerFbx(page, preview, outputDir, timeoutMs = 120_000, observedModelUrls = []) {
  const expectedTimestamp = assetTimestamp(preview);
  const started = Date.now();
  const deadline = started + timeoutMs;
  const beat = heartbeat("下载阶段", 15_000);
  console.error(`[hunyuan-web] 下载阶段开始：点选目标资产，超时 ${Math.round(timeoutMs / 1000)}s`);
  try {
    await clickAssetCardByPreview(page, preview, Math.max(1, deadline - Date.now()));
  } catch (error) {
    if (Date.now() >= deadline) fail(`下载阶段超时（${timeoutMs} ms）：等待目标资产卡可点击。`);
    throw error;
  }
  console.error(`[hunyuan-web] 已点选目标资产，等待查看器请求对应 FBX（${formatElapsed(Date.now() - started)}）`);
  let modelUrl = null;
  while (Date.now() < deadline) {
    const resources = await page.evaluate(() => performance.getEntriesByType("resource").map(entry => entry.name));
    modelUrl = [...observedModelUrls, ...resources].find(url => {
      if (!/\/3DGameStudio\/.*\.fbx(?:\?|$)/i.test(url)) return false;
      const timestamp = url.match(/\/(\d{14})_[^/]+\.fbx(?:\?|$)/i)?.[1] || null;
      return expectedTimestamp ? timestampClose(timestamp, expectedTimestamp) : true;
    }) || null;
    if (modelUrl) break;
    beat(`等待目标 FBX 请求（预览时间戳 ${expectedTimestamp || "未知"}）`);
    await page.waitForTimeout(250);
  }
  if (!modelUrl) fail(`下载阶段超时（${timeoutMs} ms）：未观察到对应 FBX 请求（预览时间戳 ${expectedTimestamp || "未知"}）。`);
  console.error(`[hunyuan-web] 已捕获模型 URL，开始下载（${formatElapsed(Date.now() - started)}）`);
  const remainingMs = Math.max(1, deadline - Date.now());
  const downloadHeartbeat = setInterval(() => beat("正在通过登录 context 下载 FBX"), 1_000);
  let response;
  try {
    response = await page.context().request.get(modelUrl, { timeout: remainingMs });
  } catch (error) {
    if (Date.now() >= deadline || /Timeout/i.test(error.message)) {
      fail(`下载阶段超时（${timeoutMs} ms）：已捕获 FBX URL，但文件传输未完成。`);
    }
    throw error;
  } finally {
    clearInterval(downloadHeartbeat);
  }
  if (!response.ok()) fail(`下载 FBX 失败：HTTP ${response.status()} ${modelUrl}`);
  const body = await response.body();
  if (!body.subarray(0, 24).toString("ascii").startsWith("Kaydara FBX")) fail(`下载结果不是有效 FBX：${modelUrl}`);
  const filename = basename(new URL(modelUrl).pathname);
  const destination = resolve(outputDir, filename);
  await writeFile(destination, body);
  console.error(`[hunyuan-web] 下载阶段完成：${filename}（${formatElapsed(Date.now() - started)}）`);
  return { path: destination, filename, sha256: await sha256(destination), source_url: modelUrl };
}

async function saveDownload(page, outputDir, format, expectTimestamp = null) {
  const downloadStartedAt = Date.now();
  const downloadPromise = page.waitForEvent("download", { timeout: 30_000 });
  await page.getByText(format, { exact: true }).last().click();
  const download = await downloadPromise;
  const suggested = download.suggestedFilename();
  if (process.env.HUNYUAN_DEBUG) console.error(`[debug] download event ${suggested} url=${download.url()}`);
  //  “下载”下载的是查看器里当前加载的模型，不一定是刚点的那张卡（模型异步加载）。
  //  结果文件名与卡片预览共用同一个时间戳前缀，用它核对，对不上就不落盘。
  //  预览图与结果文件的时间戳偶尔差 1 秒（各自生成时刻），按 ±2 秒容差核对，不按精确前缀。
  if (expectTimestamp && !timestampClose(suggested.slice(0, expectTimestamp.length), expectTimestamp)) {
    //  不能 cancel()：对 blob 下载 cancel 会把整个页面连同浏览器上下文一起关掉。让它下完再丢。
    const scratch = resolve(outputDir, `.mismatch-${Date.now()}`);
    await download.saveAs(scratch).catch(() => {});
    await unlink(scratch).catch(() => {});
    throw new Error(`下载到的不是目标资产：${suggested}（期望时间戳 ${expectTimestamp}）`);
  }
  const destination = resolve(outputDir, suggested);
  try {
    await download.saveAs(destination);
  } catch (error) {
    const candidates = [];
    for (const name of await readdir(outputDir)) {
      if (["manifest.json", "failure.png"].includes(name) || name === suggested) continue;
      if (name.endsWith(".crdownload") || name.startsWith(".")) continue;
      if (extname(name).toLowerCase() !== `.${format}`) continue;
      const path = resolve(outputDir, name);
      const info = await stat(path).catch(() => null);
      if (!info?.isFile() || info.size === 0 || info.mtimeMs < downloadStartedAt - 2_000) continue;
      if (format === "fbx") {
        const header = (await readFile(path)).subarray(0, 24).toString("ascii");
        if (!header.startsWith("Kaydara FBX")) continue;
      }
      candidates.push({ path, mtime: info.mtimeMs });
    }
    candidates.sort((a, b) => b.mtime - a.mtime);
    if (candidates.length) {
      await rename(candidates[0].path, destination);
    } else {
      const nativeDownload = await waitForNativeDownload(downloadStartedAt, format);
      if (!nativeDownload) throw error;
      await rename(nativeDownload, destination);
    }
  }
  for (const name of await readdir(outputDir).catch(() => [])) {
    if (name.endsWith(".crdownload")) await unlink(resolve(outputDir, name)).catch(() => {});
  }
  return { path: destination, filename: suggested, sha256: await sha256(destination) };
}

async function writeInitialManifest(options, stage, source) {
  const outputDir = sessionDirectory(options, stage);
  await mkdir(outputDir, { recursive: true });
  const manifestPath = resolve(outputDir, "manifest.json");
  const manifest = {
    schema_version: 1,
    provider: "hunyuan-studio-web",
    stage,
    session: options.session,
    status: "prepared",
    source: { path: source, sha256: await sha256(source) },
    settings: stage === "geo"
      ? { faces: options.faces }
      : { level: options.level, topology: options.topology },
    page_url: stage === "geo" ? GEO_URL : POLY_URL,
    output_dir: outputDir,
    created_at: new Date().toISOString(),
  };
  await atomicJson(manifestPath, manifest);
  return { manifest, manifestPath, outputDir };
}

async function runStage(options, stage) {
  const source = stage === "geo" ? options.image : options.source;
  const { manifest, manifestPath, outputDir } = await writeInitialManifest(options, stage, source);
  const context = await launch({ ...options, download_dir: outputDir });
  let page;
  try {
    page = context.pages()[0] || await context.newPage();
    await page.goto(options.base_url || manifest.page_url);
    await assertStudio(page, stage);
    if (stage === "geo") {
      await chooseRadio(page, "上传单图");
      await setUpload(page, source, true);
      await chooseText(page, { "50k": "50k", "500k": "500k", "1m": "1M", "1.5m": "1.5M" }[options.faces]);
    } else {
      await setUpload(page, source);
      await chooseText(page, { low: "低", medium: "中", high: "高" }[options.level]);
      await chooseText(page, options.topology === "triangle" ? "三角面" : "四边面");
    }
    const beforeSubmit = await page.locator("body").innerText();
    if (process.env.HUNYUAN_DEBUG) {
      await page.screenshot({ path: resolve(outputDir, "debug-after-upload.png"), fullPage: true }).catch(() => {});
      console.error("[debug] body after upload:", beforeSubmit.replace(/\s+/g, " ").slice(0, 600));
    }
    const quota = assertHasQuota(beforeSubmit, stage);
    const baseline = {
      quota,
      signature: resultSignature(beforeSubmit),
      asset_sources: await studioImageSources(page, true),
      asset_not_before: singaporeTimestamp(new Date(Date.now() - 5_000)),
    };
    manifest.remaining_quota_before_submit = quota;
    const generate = page.locator("button").filter({ hasText: "立即生成" }).first();
    await generate.waitFor({ state: "visible", timeout: 30_000 });
    if (await generate.isDisabled().catch(() => false)) {
      fail(`“立即生成”按钮不可用（今日剩余生成次数：${quota}）。可能额度已耗尽或上传/参数未就绪。`);
    }
    await generate.click();
    manifest.status = "generating";
    manifest.updated_at = new Date().toISOString();
    await atomicJson(manifestPath, manifest);
    const onSelected = async sourceUrl => {
      manifest.remote_asset_preview = sourceUrl;
      manifest.updated_at = new Date().toISOString();
      await atomicJson(manifestPath, manifest);
    };
    let result;
    try {
      await waitForGeneration(page, options.timeout_ms, baseline, true, onSelected);
    } catch (error) {
      if (!manifest.remote_asset_preview) throw error;
    }
    if (manifest.remote_asset_preview) {
      manifest.status = "ready_to_download";
      await atomicJson(manifestPath, manifest);
      await context.close().catch(() => {});
      result = await downloadByPreview(options, manifest, outputDir);
    } else {
      result = await downloadResult(page, outputDir, options.timeout_ms, options.format, baseline, true, onSelected);
    }
    manifest.status = "completed";
    manifest.completed_at = new Date().toISOString();
    manifest.downloads = [result];
    delete manifest.error;
    delete manifest.failure_screenshot;
    await unlink(resolve(outputDir, "failure.png")).catch(() => {});
    await finishResult(options, manifest, result);
    await atomicJson(manifestPath, manifest);
    console.log(manifestPath);
  } catch (error) {
    manifest.status = manifest.remote_asset_preview ? "ready_to_download" : "interrupted";
    manifest.error = manifest.remote_asset_preview ? `生成已完成但下载失败，可用 resume 继续：${error.message}` : error.message;
    manifest.updated_at = new Date().toISOString();
    if (page && !page.isClosed()) {
      const screenshot = resolve(outputDir, "failure.png");
      await page.screenshot({ path: screenshot, fullPage: true }).catch(() => {});
      manifest.failure_screenshot = screenshot;
    }
    await atomicJson(manifestPath, manifest);
    throw error;
  } finally {
    await context.close().catch(() => {});
  }
}

async function doctor(options) {
  const context = await launch(options);
  try {
    for (const [stage, url] of [["geo", GEO_URL], ["poly", POLY_URL]]) {
      const page = await context.newPage();
      await page.goto(options.base_url ? `${options.base_url}/${stage}` : url);
      await assertStudio(page, stage);
      const body = await page.locator("body").innerText();
      const quota = remainingQuota(body);
      const fileInputs = await page.locator('input[type="file"]').count();
      const generateButtons = await page.getByText("立即生成", { exact: true }).count();
      if (fileInputs < 1 || generateButtons < 1) fail(`${stage} 页面缺少上传框或生成按钮。`);
      if (quota === null) fail(`${stage} 页面无法读取“今日剩余生成次数”。`);
      console.log(`${stage}: ok (今日剩余生成次数: ${quota})`);
      if (quota <= 0) console.log(`${stage}: unavailable today (quota exhausted)`);
      await page.close();
    }
  } finally {
    await context.close();
  }
}

async function login(options) {
  const context = await chromium.launchPersistentContext(options.profile, { channel: "chrome", headless: false });
  const page = context.pages()[0] || await context.newPage();
  await page.goto(GEO_URL);
  console.log("浏览器已打开。完成登录后关闭浏览器窗口；会话会保存在：", options.profile);
  await new Promise(resolvePromise => context.on("close", resolvePromise));
}

async function resume(options) {
  if (!options.manifest) fail("resume 需要 --manifest。");
  const path = resolve(options.manifest);
  const manifest = JSON.parse(await readFile(path, "utf8"));
  if (manifest.status === "completed") {
    if (options.out && manifest.downloads?.[0]) await finishResult(options, manifest, manifest.downloads[0]);
    console.log(path);
    return;
  }
  if (manifest.remote_asset_preview) {
    try {
      const result = await downloadByPreview(options, manifest, manifest.output_dir);
      manifest.status = "completed";
      manifest.completed_at = new Date().toISOString();
      manifest.downloads = [result];
      delete manifest.error;
      delete manifest.failure_screenshot;
      await finishResult(options, manifest, result);
      await atomicJson(path, manifest);
      console.log(path);
      return;
    } catch (error) {
      manifest.status = "ready_to_download";
      manifest.error = `生成已完成但下载失败，可再次 resume：${error.message}`;
      manifest.updated_at = new Date().toISOString();
      await atomicJson(path, manifest);
      throw new Error(manifest.error);
    }
  }
  const context = await launch({ ...options, download_dir: manifest.output_dir });
  try {
    const page = context.pages()[0] || await context.newPage();
    await page.goto(manifest.page_url);
    await assertStudio(page, manifest.stage);
    const baseline = {
      signature: "",
      asset_sources: [],
      asset_not_before: singaporeTimestamp(new Date(new Date(manifest.created_at).getTime() - 5_000)),
      selected_asset: manifest.remote_asset_preview || null,
    };
    if (baseline.selected_asset) {
      await clickAssetCardByPreview(page, baseline.selected_asset);
      await page.waitForTimeout(500);
    } else {
      console.error("[hunyuan-web] 提醒：manifest 里没有锁定的预览，resume 只能取提交之后最新完成的一张卡；同批提交过多个任务时可能拿错，请先用 assets 核对。");
    }
    const result = await downloadResult(
      page, manifest.output_dir, options.timeout_ms, options.format, baseline, false,
      async sourceUrl => {
        manifest.remote_asset_preview = sourceUrl;
        manifest.updated_at = new Date().toISOString();
        await atomicJson(path, manifest);
      },
    );
    manifest.status = "completed";
    manifest.completed_at = new Date().toISOString();
    manifest.downloads = [result];
    delete manifest.error;
    delete manifest.failure_screenshot;
    await finishResult(options, manifest, result);
    await atomicJson(path, manifest);
    console.log(path);
  } catch (error) {
    manifest.status = "interrupted";
    manifest.error = `恢复未找到可下载的原任务；没有重新提交。${error.message}`;
    manifest.updated_at = new Date().toISOString();
    await atomicJson(path, manifest);
    throw new Error(manifest.error);
  } finally {
    await context.close();
  }
}

// 提交一个任务：上传、选参数、读额度、点生成、确认任务启动、锁定那张新卡。不等待完成。
async function submitJob(page, job, manifest, manifestPath, jobTag) {
  const stage = job.command;
  if (stage === "geo") {
    await chooseRadio(page, "上传单图");
    await setUpload(page, job.image, true);
    await chooseText(page, { "50k": "50k", "500k": "500k", "1m": "1M", "1.5m": "1.5M" }[job.faces]);
  } else {
    await setUpload(page, job.source);
    await chooseText(page, { low: "低", medium: "中", high: "高" }[job.level]);
    await chooseText(page, job.topology === "triangle" ? "三角面" : "四边面");
  }
  const beforeSubmit = await page.locator("body").innerText();
  const quota = assertHasQuota(beforeSubmit, stage);
  manifest.remaining_quota_before_submit = quota;
  const generate = page.locator("button").filter({ hasText: "立即生成" }).first();
  await generate.waitFor({ state: "visible", timeout: 30_000 });
  if (await generate.isDisabled().catch(() => false)) {
    fail(`“立即生成”按钮不可用（今日剩余生成次数：${quota}）。可能额度已耗尽或上传/参数未就绪。`);
  }
  await page.locator('div[class*="aspect-square"]').evaluateAll((cards, tag) => {
    for (const card of cards) card.setAttribute("data-codex-baseline", tag);
  }, jobTag);
  await generate.click();
  manifest.status = "generating";
  manifest.updated_at = new Date().toISOString();
  await atomicJson(manifestPath, manifest);
  const started = Date.now();
  let observedStart = false;
  while (Date.now() - started < 30_000) {
    const body = await page.locator("body").innerText();
    if (/生成中|排队中|处理中|正在生成/.test(body) || !await generate.isEnabled().catch(() => true)
      || remainingQuota(body) !== quota) { observedStart = true; break; }
    await page.waitForTimeout(250);
  }
  if (!observedStart) fail("提交后未观察到生成任务启动；为避免误下载输入模型，已停止。");
  return lockSubmittedTaskCard(page, 30_000, jobTag, jobTag);
}

export function normalizeBatchJobs(raw, options) {
  if (!Array.isArray(raw) || !raw.length) fail("--jobs 必须是非空 JSON 数组。");
  const jobs = raw.map((job, index) => {
    const argv = [job.stage, "--session", job.session || ""];
    for (const key of ["image", "source", "faces", "level", "topology", "out", "format"]) {
      if (job[key] !== undefined) argv.push(`--${key}`, String(job[key]));
    }
    const parsed = validateOptions(parseArgs(argv));
    return { ...parsed, index, output_root: options.output_root, profile: options.profile };
  });
  const stage = jobs[0].command;
  if (!["geo", "poly"].includes(stage)) fail("batch 里每项的 stage 只能是 geo 或 poly。");
  if (jobs.some(job => job.command !== stage)) fail("batch 一批只能是同一 stage：Geo 与 Poly 是两个页面。");
  const directories = jobs.map(job => sessionDirectory(job, stage));
  if (new Set(directories).size !== directories.length) fail("batch 中存在会写入同一 manifest 目录的重复 session/设置。");
  const existing = directories.map(directory => resolve(directory, "manifest.json")).filter(existsSync);
  if (existing.length) fail(`batch 不会覆盖已有 manifest；请 resume 或使用新 session：${existing.join(", ")}`);
  const outputs = jobs.filter(job => job.out).map(job => job.out);
  if (new Set(outputs).size !== outputs.length) fail("batch 中存在重复 out 路径。");
  const occupiedOutputs = outputs.filter(existsSync);
  if (occupiedOutputs.length) fail(`batch 不会覆盖已有 out 文件：${occupiedOutputs.join(", ")}`);
  for (const job of jobs) {
    const source = stage === "geo" ? job.image : job.source;
    if (!existsSync(source)) fail(`上传文件不存在：${source}`);
  }
  return jobs;
}

async function batch(options) {
  const jobsPath = resolve(options.jobs);
  const raw = JSON.parse(await readFile(jobsPath, "utf8"));
  const jobs = normalizeBatchJobs(raw, options);
  const stage = jobs[0].command;

  const prepared = [];
  const context = await launch({ ...options, download_dir: options.output_root });
  const summary = { completed: [], failed: [] };
  try {
    const page = context.pages()[0] || await context.newPage();
    await page.goto(options.base_url || (stage === "geo" ? GEO_URL : POLY_URL));
    await assertStudio(page, stage);
    const quota = assertHasQuota(await page.locator("body").innerText(), stage);
    if (quota < jobs.length) {
      fail(`今日剩余生成次数 ${quota}，少于 batch 的 ${jobs.length} 个任务；为避免只提交一部分，已在提交前中止。`);
    }
    for (const job of jobs) {
      const source = stage === "geo" ? job.image : job.source;
      const { manifest, manifestPath, outputDir } = await writeInitialManifest(job, stage, source);
      prepared.push({ job, manifest, manifestPath, outputDir, card: null, done: false });
    }
    // 逐个提交：每次提交只花十几秒，排队与生成都在服务端并行
    for (const item of prepared) {
      const tag = `job${item.job.index}`;
      try {
        item.card = await submitJob(page, item.job, item.manifest, item.manifestPath, tag);
        item.submittedAt = singaporeTimestamp(new Date(Date.now() - 5_000));
        console.error(`[hunyuan-web] 已提交 ${item.job.session}（${tag}），剩余次数 ${item.manifest.remaining_quota_before_submit}`);
      } catch (error) {
        item.done = true;
        item.manifest.status = "interrupted"; item.manifest.error = error.message;
        await atomicJson(item.manifestPath, item.manifest);
        summary.failed.push({ session: item.job.session, error: error.message });
        console.error(`[hunyuan-web] 提交失败 ${item.job.session}：${error.message}`);
      }
    }
    // 一起等：这里只记录完成卡。大 FBX 下载偶发关闭 Chrome context，不能在监控 context 里下载。
    const started = Date.now();
    const beat = heartbeat("批量等待");
    while (prepared.some(item => !item.done) && Date.now() - started < options.timeout_ms) {
      for (const item of prepared) {
        if (item.done) continue;
        const state = await assetCardState(item.card);
        if (state.status === "pending") continue;
        item.done = true;
        try {
          if (state.status === "failed") fail("Hunyuan Studio 报告本次生成失败。");
          const timestamp = assetTimestamp(state.preview);
          if (timestamp && timestamp < item.submittedAt) {
            fail(`锁定任务卡完成后的预览早于本次提交（${timestamp} < ${item.submittedAt}），拒绝下载旧资产。`);
          }
          item.manifest.remote_asset_preview = state.preview;
          item.manifest.status = "ready_to_download";
          item.manifest.updated_at = new Date().toISOString();
          await atomicJson(item.manifestPath, item.manifest);
          item.preview = state.preview;
          console.error(`[hunyuan-web] 生成完成 ${item.job.session}，等待独立下载`);
        } catch (error) {
          item.manifest.status = "interrupted"; item.manifest.error = error.message;
          await atomicJson(item.manifestPath, item.manifest);
          summary.failed.push({ session: item.job.session, error: error.message });
          console.error(`[hunyuan-web] 失败 ${item.job.session}：${error.message}`);
        }
      }
      const pending = prepared.filter(item => !item.done).length;
      beat(`还有 ${pending} 个任务未完成`);
      if (pending) await page.waitForTimeout(5_000);
    }
    for (const item of prepared.filter(item => !item.done)) {
      item.manifest.status = "interrupted"; item.manifest.error = `批量等待超时（${options.timeout_ms} ms），可用 resume 继续。`;
      await atomicJson(item.manifestPath, item.manifest);
      summary.failed.push({ session: item.job.session, error: item.manifest.error });
    }
  } finally {
    await context.close();
  }
  // 每个结果使用独立 context。即使某次 blob 下载令 Chrome 自行退出，也不会中断其他结果。
  for (const item of prepared.filter(candidate => candidate.preview)) {
    try {
      item.manifest.remote_asset_preview = item.preview;
      const result = await downloadByPreview({ ...options, format: item.job.format }, item.manifest, item.outputDir);
      item.manifest.status = "completed";
      item.manifest.completed_at = new Date().toISOString();
      item.manifest.downloads = [result];
      delete item.manifest.error;
      await finishResult(item.job, item.manifest, result);
      await atomicJson(item.manifestPath, item.manifest);
      summary.completed.push({ session: item.job.session, manifest: item.manifestPath, path: item.job.out || result.path });
    } catch (error) {
      item.manifest.status = "ready_to_download";
      item.manifest.error = `生成已完成但下载失败，可用 resume 继续：${error.message}`;
      await atomicJson(item.manifestPath, item.manifest);
      summary.failed.push({ session: item.job.session, error: item.manifest.error });
    }
  }
  console.log(JSON.stringify(summary, null, 2));
  if (summary.failed.length) process.exitCode = 1;
}

// 点选一张卡再下载，并核对下载文件属于这张卡；查看器加载慢时等一等重试。
async function downloadCard(page, preview, outputDir, format, observedModelUrls = []) {
  if (format === "fbx") return saveViewerFbx(page, preview, outputDir, 120_000, observedModelUrls);
  const expect = assetTimestamp(preview);
  let lastError = null;
  for (let attempt = 0; attempt < 4; attempt += 1) {
    if (process.env.HUNYUAN_DEBUG) console.error(`[debug] downloadCard attempt ${attempt}`);
    await clickAssetCardByPreview(page, preview);
    await page.waitForTimeout(1_500 * (attempt + 1));
    const download = page.locator("button").filter({ hasText: "下载" }).last();
    await download.waitFor({ state: "visible", timeout: 30_000 });
    await download.click();
    try {
      return await saveDownload(page, outputDir, format, expect);
    } catch (error) {
      lastError = error;
      if (process.env.HUNYUAN_DEBUG) console.error(`[debug] attempt failed: ${error.message}`);
      if (page.isClosed()) throw lastError;
      await page.keyboard.press("Escape").catch(() => {});
      await page.waitForTimeout(1_000);
    }
  }
  throw lastError;
}

async function listCards(page) {
  const images = page.locator('img[src*="/3DGameStudio/"]');
  const n = await images.count();
  const out = [];
  for (let i = 0; i < n; i += 1) {
    const preview = await images.nth(i).getAttribute("src");
    if (!preview || out.some(item => item.preview === preview)) continue;
    out.push({ index: out.length, status: "ready", preview, timestamp: assetTimestamp(preview) });
  }
  return out;
}

// 资产卡列表与按预览 URL 下载：批量提交后某个任务的进程侧记录丢了（浏览器崩溃、DOM 标记失效）
// 时的人工恢复路径。resume 在没有锁定预览时会退回“最新的一张卡”，同批多任务下会拿错，
// 所以恢复要先 assets 看清楚，再 fetch 指定那一张。
async function assets(options) {
  const context = await launch({ ...options, download_dir: options.output_root });
  try {
    const page = context.pages()[0] || await context.newPage();
    await page.goto(options.stage === "geo" ? GEO_URL : POLY_URL);
    await assertStudio(page, options.stage);
    await page.locator('img[src*="/3DGameStudio/"]').first().waitFor({ state: "attached", timeout: 15_000 }).catch(() => {});
    console.log(JSON.stringify(await listCards(page), null, 2));
  } finally {
    await context.close();
  }
}

async function fetchCard(options) {
  //  下载先落在工具自己的目录：saveDownload 的兜底会扫输出目录里的新文件，不能指向用户目录
  const outDir = resolve(options.output_root, "_fetch");
  await mkdir(outDir, { recursive: true });
  const context = await launch({ ...options, download_dir: outDir });
  try {
    const page = context.pages()[0] || await context.newPage();
    const observedModelUrls = trackModelRequests(page);
    await page.goto(options.stage === "geo" ? GEO_URL : POLY_URL);
    await assertStudio(page, options.stage);
    await page.waitForTimeout(1_500);
    const result = await downloadCard(page, options.preview, outDir, options.format, observedModelUrls);
    await mkdir(dirname(options.out), { recursive: true });
    await copyFile(result.path, options.out);
    await unlink(result.path).catch(() => {});
    console.log(`[hunyuan-web] 已下载：${options.out}\n  sha256=${result.sha256}\n  preview=${options.preview}`);
  } finally {
    await context.close();
  }
}

export async function main(argv = process.argv.slice(2)) {
  const parsed = parseArgs(argv);
  if (parsed.command === "help") {
    console.log(usage());
    return;
  }
  const options = validateOptions(parsed);
  if (options.command === "login") return login(options);
  if (options.command === "doctor") return doctor(options);
  if (options.command === "geo") return runStage(options, "geo");
  if (options.command === "poly") return runStage(options, "poly");
  if (options.command === "resume") return resume(options);
  if (options.command === "batch") return batch(options);
  if (options.command === "assets") return assets(options);
  if (options.command === "fetch") return fetchCard(options);
  fail(`未知命令：${options.command}\n\n${usage()}`);
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) {
  main().catch(error => {
    console.error(`[hunyuan-web] ${error.message}`);
    process.exitCode = 1;
  });
}
