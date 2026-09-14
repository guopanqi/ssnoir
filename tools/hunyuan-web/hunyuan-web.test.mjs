import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { chromium } from "playwright";
import { assertHasQuota, assetTimestamp, normalizeBatchJobs, parseArgs, remainingQuota, saveViewerFbx, singaporeTimestamp, stageDirectoryName, timestampClose, trackModelRequests, validateOptions } from "./hunyuan-web.mjs";

test("parses asset preview timestamps from Studio URLs", () => {
  assert.equal(
    assetTimestamp("https://example.com/3DGameStudio/x/20260912190735_567cd4df.png"),
    "20260912190735",
  );
  assert.equal(assetTimestamp("https://example.com/no-stamp.png"), null);
});

test("singapore wall clock is used for submit-time gates", () => {
  assert.equal(singaporeTimestamp(new Date("2026-09-12T11:02:19.324Z")), "20260912190219");
});

test("accepts the one-second timestamp skew between preview and FBX", () => {
  assert.equal(timestampClose("20260914222809", "20260914222810"), true);
  assert.equal(timestampClose("20260914222700", "20260914222810"), false);
});

test("downloads the FBX resource loaded by the selected preview without a browser download event", async () => {
  const fbx = Buffer.concat([Buffer.from("Kaydara FBX Binary  \0\x1a\0", "binary"), Buffer.alloc(64)]);
  const server = createServer((request, response) => {
    if (request.url?.endsWith(".fbx")) {
      response.writeHead(200, { "content-type": "application/octet-stream" });
      response.end(fbx);
      return;
    }
    if (request.url?.endsWith(".png")) {
      response.writeHead(200, { "content-type": "image/svg+xml" });
      response.end(`<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10" fill="red"/></svg>`);
      return;
    }
    const origin = `http://127.0.0.1:${server.address().port}`;
    response.writeHead(200, { "content-type": "text/html" });
    response.end(`<img src="${origin}/3DGameStudio/x/20260914222810_preview.png" onclick="fetch('${origin}/3DGameStudio/x/20260914222809_model.fbx')">`);
  });
  await new Promise(resolvePromise => server.listen(0, "127.0.0.1", resolvePromise));
  const directory = await mkdtemp(join(tmpdir(), "hunyuan-viewer-download-test-"));
  let browser;
  try {
    browser = await chromium.launch({ channel: "chrome", headless: true });
    const page = await browser.newPage();
    const origin = `http://127.0.0.1:${server.address().port}`;
    const preview = `${origin}/3DGameStudio/x/20260914222810_preview.png`;
    await page.goto(origin);
    const started = Date.now();
    const result = await saveViewerFbx(page, preview, directory, 5_000);
    assert.ok(Date.now() - started < 2_000, "URL 一出现就应立即下载，不能等固定超时");
    assert.equal(result.filename, "20260914222809_model.fbx");
    assert.deepEqual(await readFile(result.path), fbx);
  } finally {
    await browser?.close();
    server.close();
    await rm(directory, { recursive: true, force: true });
  }
});

test("uses an FBX request observed before asset selection without waiting", async () => {
  const fbx = Buffer.concat([Buffer.from("Kaydara FBX Binary  \0\x1a\0", "binary"), Buffer.alloc(32)]);
  const server = createServer((request, response) => {
    if (request.url?.endsWith(".fbx")) { response.writeHead(200); response.end(fbx); return; }
    const origin = `http://127.0.0.1:${server.address().port}`;
    response.writeHead(200, { "content-type": "text/html" });
    response.end(`<img src="${origin}/3DGameStudio/x/20260914230357_preview.png"><script>fetch('${origin}/3DGameStudio/x/20260914230357_model.fbx')</script>`);
  });
  await new Promise(resolvePromise => server.listen(0, "127.0.0.1", resolvePromise));
  const directory = await mkdtemp(join(tmpdir(), "hunyuan-early-request-test-"));
  let browser;
  try {
    browser = await chromium.launch({ channel: "chrome", headless: true });
    const page = await browser.newPage();
    const observed = trackModelRequests(page);
    const origin = `http://127.0.0.1:${server.address().port}`;
    await page.goto(origin);
    const started = Date.now();
    const result = await saveViewerFbx(page, `${origin}/3DGameStudio/x/20260914230357_preview.png`, directory, 5_000, observed);
    assert.ok(Date.now() - started < 2_000);
    assert.deepEqual(await readFile(result.path), fbx);
  } finally {
    await browser?.close(); server.close(); await rm(directory, { recursive: true, force: true });
  }
});

test("download timeout fails promptly without submitting a task", async () => {
  let postCount = 0;
  const server = createServer((request, response) => {
    if (request.method === "POST") postCount += 1;
    if (request.url?.endsWith(".png")) {
      response.writeHead(200, { "content-type": "image/svg+xml" });
      response.end(`<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10" fill="red"/></svg>`);
      return;
    }
    response.writeHead(200, { "content-type": "text/html" });
    response.end(`<img src="/3DGameStudio/x/20260914230357_preview.png">`);
  });
  await new Promise(resolvePromise => server.listen(0, "127.0.0.1", resolvePromise));
  const directory = await mkdtemp(join(tmpdir(), "hunyuan-download-timeout-test-"));
  let browser;
  try {
    browser = await chromium.launch({ channel: "chrome", headless: true });
    const page = await browser.newPage();
    const origin = `http://127.0.0.1:${server.address().port}`;
    await page.goto(origin);
    const started = Date.now();
    await assert.rejects(saveViewerFbx(page, `${origin}/3DGameStudio/x/20260914230357_preview.png`, directory, 300), /下载阶段超时/);
    assert.ok(Date.now() - started < 2_000);
    assert.equal(postCount, 0, "恢复下载不得重新提交生成任务");
  } finally {
    await browser?.close(); server.close(); await rm(directory, { recursive: true, force: true });
  }
});

test("reads remaining daily quota and blocks when exhausted", () => {
  assert.equal(remainingQuota("今日剩余生成次数：19"), 19);
  assert.equal(remainingQuota("今日剩余生成次数: 0"), 0);
  assert.equal(remainingQuota("未登录"), null);
  assert.equal(assertHasQuota("今日剩余生成次数：3", "geo"), 3);
  assert.throws(() => assertHasQuota("今日剩余生成次数：0", "poly"), /今日剩余生成次数为 0/);
  assert.throws(() => assertHasQuota("无额度字段", "geo"), /无法读取/);
});

test("parses Poly retry settings", () => {
  const parsed = validateOptions(parseArgs([
    "poly", "--source", "model.glb", "--session", "电影院", "--level", "medium", "--topology", "quad",
  ]));
  assert.equal(parsed.command, "poly");
  assert.equal(parsed.level, "medium");
  assert.equal(parsed.topology, "quad");
  assert.match(parsed.source, /model\.glb$/);
});

test("uses conservative defaults", () => {
  const geo = validateOptions(parseArgs(["geo", "--image", "ref.png", "--session", "x"]));
  const poly = validateOptions(parseArgs(["poly", "--source", "model.glb", "--session", "x"]));
  assert.equal(geo.faces, "50k");
  assert.equal(poly.level, "low");
  assert.equal(poly.topology, "quad");
});

test("rejects unsupported topology budget", () => {
  assert.throws(
    () => validateOptions(parseArgs(["poly", "--source", "model.glb", "--session", "x", "--level", "extreme"])),
    /--level/,
  );
});

test("retrying poly at another level lands in its own directory", () => {
  const low = validateOptions(parseArgs(["poly", "--source", "m.glb", "--session", "x"]));
  const medium = validateOptions(parseArgs(["poly", "--source", "m.glb", "--session", "x", "--level", "medium"]));
  assert.equal(stageDirectoryName(low, "poly"), "poly-low-quad");
  assert.equal(stageDirectoryName(medium, "poly"), "poly-medium-quad");
  assert.equal(stageDirectoryName(validateOptions(parseArgs(["geo", "--image", "r.png", "--session", "x"])), "geo"), "geo-50k");
});

test("--out must match --format", () => {
  const ok = validateOptions(parseArgs(["poly", "--source", "m.glb", "--session", "x", "--out", "out/m.fbx"]));
  assert.match(ok.out, /out\/m\.fbx$/);
  assert.throws(
    () => validateOptions(parseArgs(["poly", "--source", "m.glb", "--session", "x", "--out", "m.glb"])),
    /--out/,
  );
});

test("normalizes Geo batch jobs without routing them through Poly", async () => {
  const directory = await mkdtemp(join(tmpdir(), "hunyuan-batch-test-"));
  const first = join(directory, "a.jpg");
  const second = join(directory, "b.jpg");
  await writeFile(first, "a");
  await writeFile(second, "b");
  try {
    const jobs = normalizeBatchJobs([
      { stage: "geo", image: first, session: "a", faces: "50k" },
      { stage: "geo", image: second, session: "b", faces: "500k" },
    ], { output_root: directory, profile: join(directory, "profile") });
    assert.deepEqual(jobs.map(job => job.command), ["geo", "geo"]);
    assert.deepEqual(jobs.map(job => job.image), [first, second]);
    assert.equal(jobs[0].source, undefined);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});

test("batch preflight rejects mixed stages, duplicate destinations, and missing inputs", async () => {
  const directory = await mkdtemp(join(tmpdir(), "hunyuan-batch-test-"));
  const image = join(directory, "a.jpg");
  const model = join(directory, "a.fbx");
  await writeFile(image, "a");
  await writeFile(model, "fbx");
  const common = { output_root: directory, profile: join(directory, "profile") };
  try {
    assert.throws(() => normalizeBatchJobs([
      { stage: "geo", image, session: "a" },
      { stage: "poly", source: model, session: "b" },
    ], common), /同一 stage/);
    assert.throws(() => normalizeBatchJobs([
      { stage: "geo", image, session: "same", out: join(directory, "a.fbx") },
      { stage: "geo", image, session: "same", out: join(directory, "b.fbx") },
    ], common), /同一 manifest/);
    assert.throws(() => normalizeBatchJobs([
      { stage: "geo", image: join(directory, "missing.jpg"), session: "missing" },
    ], common), /上传文件不存在/);
    const occupied = join(directory, "occupied", "geo-50k");
    await mkdir(occupied, { recursive: true });
    await writeFile(join(occupied, "manifest.json"), "{}");
    assert.throws(() => normalizeBatchJobs([
      { stage: "geo", image, session: "occupied" },
    ], common), /不会覆盖已有 manifest/);
    const occupiedOutput = join(directory, "already.fbx");
    await writeFile(occupiedOutput, "existing");
    assert.throws(() => normalizeBatchJobs([
      { stage: "geo", image, session: "new", out: occupiedOutput },
    ], common), /不会覆盖已有 out/);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});

test("doctor launches Chrome and recognizes both workflow pages", { timeout: 30_000 }, async () => {
  const server = createServer((request, response) => {
    const stage = request.url?.endsWith("/poly") ? "低模生成" : "几何生成";
    response.writeHead(200, { "content-type": "text/html; charset=utf-8" });
    response.end(`<main><h1>${stage}</h1><p>今日剩余生成次数：29</p><input type="file"><button>立即生成</button></main>`);
  });
  await new Promise(resolvePromise => server.listen(0, "127.0.0.1", resolvePromise));
  const address = server.address();
  const profile = await mkdtemp(join(tmpdir(), "hunyuan-web-test-"));
  try {
    const result = await new Promise((resolvePromise, rejectPromise) => {
      const child = spawn(process.execPath, [
        new URL("./hunyuan-web.mjs", import.meta.url).pathname,
        "doctor", "--base-url", `http://127.0.0.1:${address.port}`, "--profile", profile,
      ]);
      let stdout = "";
      let stderr = "";
      child.stdout.on("data", chunk => { stdout += chunk; });
      child.stderr.on("data", chunk => { stderr += chunk; });
      child.on("error", rejectPromise);
      child.on("close", code => resolvePromise({ code, stdout, stderr }));
    });
    assert.equal(result.code, 0, result.stderr);
    assert.match(result.stdout, /geo: ok/);
    assert.match(result.stdout, /poly: ok/);
  } finally {
    server.close();
    await rm(profile, { recursive: true, force: true });
  }
});
