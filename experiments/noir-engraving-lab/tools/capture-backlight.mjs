// 逆光 BACKLIGHT — by opencode · 本地视觉循环
// 用法（在 noir-engraving-lab 目录下）：
//   node tools/capture-backlight.mjs                          # 全部机位 × 体块/空气/灯/成片
//   node tools/capture-backlight.mjs --shot 02-hero           # 单机位
//   node tools/capture-backlight.mjs --mode final --out captures/backlight/trial-1
// 输出目录必须在 captures/ 内，运行时会被替换。

import { chromium } from 'playwright';
import { spawn, execFileSync } from 'node:child_process';
import { mkdir, rm, writeFile, readFile, readdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import sharp from 'sharp';

const ROOT = process.cwd();
const { values } = parseArgs({ options: {
  shot: { type: 'string' },
  mode: { type: 'string' },
  out: { type: 'string' },
  port: { type: 'string', default: '4322' },
  help: { type: 'boolean' },
} });
if (values.help) {
  console.log('capture-backlight [--shot 02-hero] [--mode final] [--out captures/...] [--port 4322]');
  process.exit(0);
}
const OUT = path.resolve(ROOT, values.out ?? 'captures/backlight/latest');
if (!OUT.startsWith(path.join(ROOT, 'captures') + path.sep)) {
  throw new Error('--out must be inside captures/');
}
const PORT = Number(values.port);
const URL = `http://127.0.0.1:${PORT}/backlight.html?capture=1`;

const allShots = ['01-across', '02-hero', '03-plan', '04-boulevard', '05-silhouette'];
const allModes = [
  { name: 'final', type: 'png', viewport: { width: 1600, height: 900 } },
  { name: 'light', type: 'jpeg', quality: 86, viewport: { width: 960, height: 540 } },
  { name: 'air', type: 'jpeg', quality: 86, viewport: { width: 960, height: 540 } },
  { name: 'shape', type: 'jpeg', quality: 86, viewport: { width: 960, height: 540 } },
];
const shots = values.shot ? allShots.filter((n) => n === values.shot) : allShots;
const modes = values.mode ? allModes.filter((m) => m.name === values.mode) : allModes;
if (!shots.length || !modes.length) throw new Error('Unknown --shot or --mode');

async function findChromium() {
  if (process.env.CHROME_PATH) return process.env.CHROME_PATH;
  const cacheDir = path.join(process.env.HOME ?? '', 'Library/Caches/ms-playwright');
  if (!existsSync(cacheDir)) return undefined;
  const pick = async (prefix, execRel) => {
    const dirs = (await readdir(cacheDir)).filter((d) => d.startsWith(prefix)).sort().reverse();
    for (const d of dirs) {
      const exec = path.join(cacheDir, d, execRel);
      if (existsSync(exec)) return exec;
    }
    return undefined;
  };
  return (
    (await pick('chromium_headless_shell-', 'chrome-mac/headless_shell')) ??
    (await pick('chromium-', 'chrome-mac/Chromium.app/Contents/MacOS/Chromium')) ??
    (await pick('chromium-', 'chrome-mac-arm64/Chromium.app/Contents/MacOS/Chromium')) ??
    (await pick('chromium_headless_shell-', 'chrome-mac-arm64/headless_shell'))
  );
}

async function sourceDigest(dir, hash = createHash('sha256')) {
  const entries = (await readdir(dir, { withFileTypes: true })).sort((a, b) => a.name.localeCompare(b.name));
  for (const e of entries) {
    const file = path.join(dir, e.name);
    if (e.isDirectory()) await sourceDigest(file, hash);
    else { hash.update(path.relative(ROOT, file)); hash.update(await readFile(file)); }
  }
  return hash;
}

let gitSha = null;
try { gitSha = execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim(); } catch {}

await rm(OUT, { recursive: true, force: true });
await mkdir(OUT, { recursive: true });

const server = spawn(process.execPath, [
  path.join(ROOT, 'node_modules/vite/bin/vite.js'),
  '--host', '127.0.0.1', '--port', String(PORT), '--strictPort',
], { cwd: ROOT, stdio: ['ignore', 'pipe', 'pipe'] });
let serverLog = '';
server.stdout.on('data', (d) => { serverLog += d; });
server.stderr.on('data', (d) => { serverLog += d; });

let browser;
try {
  const executablePath = await findChromium();
  browser = await chromium.launch({
    headless: true,
    executablePath,
    args: ['--no-sandbox', '--disable-dev-shm-usage'],
  });
  const page = await browser.newPage({ viewport: modes[0].viewport, deviceScaleFactor: 1 });
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.stack ?? String(e)));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(`${m.text()} (${m.location().url})`); });
  page.on('response', (r) => { if (r.status() >= 400) errors.push(`HTTP ${r.status()} ${r.url()}`); });

  let up = false;
  for (let i = 0; i < 60 && !up; i++) {
    try { up = (await fetch(URL, { signal: AbortSignal.timeout(1500) })).ok; } catch {}
    if (!up) await new Promise((r) => setTimeout(r, 500));
  }
  if (!up) throw new Error('dev server did not start');

  await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 60_000 });
  let ready = false;
  for (let i = 0; i < 240 && !ready; i++) {
    if (errors.length) throw new Error(errors.join('\n'));
    ready = await page.evaluate(() => window.__backlight?.ready === true).catch(() => false);
    if (!ready) await new Promise((r) => setTimeout(r, 500));
  }
  if (!ready) throw new Error('page never became ready');

  const metrics = {};
  for (const mode of modes) {
    metrics[mode.name] = {};
    await page.setViewportSize(mode.viewport);
    await page.evaluate((m) => { window.__backlight.setStage(m); window.__backlight.setTime(8.0); }, mode.name);
    await page.evaluate(() => new Promise((resolve) => {
      window.dispatchEvent(new Event('resize'));
      setTimeout(resolve, 300);
    }));
    for (const shot of shots) {
      const index = allShots.indexOf(shot);
      await page.evaluate((i) => { window.__backlight.setShot(i); window.__backlight.setTime(8.0); window.__backlight.render(); }, index);
      await new Promise((r) => setTimeout(r, 400));
      await page.evaluate(() => window.__backlight.render());
      const ext = mode.type === 'png' ? 'png' : 'jpg';
      const file = path.join(OUT, mode.name, `${shot}.${ext}`);
      await mkdir(path.dirname(file), { recursive: true });
      console.log(`capturing ${mode.name}/${shot}`);
      const buf = await page.screenshot({ path: file, type: mode.type, quality: mode.type === 'jpeg' ? mode.quality : undefined, timeout: 120_000 });
      if (errors.length) throw new Error(errors.join('\n'));
      metrics[mode.name][shot] = await imageMetrics(buf);
    }
  }

  const order = modes.map((m) => m.name);
  const thumbW = 640, thumbH = 360;
  const contact = sharp({ create: { width: thumbW * order.length, height: thumbH * shots.length, channels: 3, background: { r: 5, g: 7, b: 11 } } });
  const comps = [];
  for (let c = 0; c < order.length; c++) {
    for (let r = 0; r < shots.length; r++) {
      const ext = modes[c].type === 'png' ? 'png' : 'jpg';
      const label = `${order[c].toUpperCase()} · ${shots[r]}`;
      const svg = Buffer.from(`<svg width="${thumbW}" height="30" xmlns="http://www.w3.org/2000/svg"><rect width="${thumbW}" height="30" fill="rgba(0,0,0,.75)"/><text x="10" y="20" fill="#cdd8e0" font-size="14" font-family="Arial">${label}</text></svg>`);
      const input = await sharp(path.join(OUT, order[c], `${shots[r]}.${ext}`)).resize(thumbW, thumbH, { fit: 'cover' }).composite([{ input: svg, top: 0, left: 0 }]).jpeg({ quality: 88 }).toBuffer();
      comps.push({ input, left: c * thumbW, top: r * thumbH });
    }
  }
  await contact.composite(comps).jpeg({ quality: 90 }).toFile(path.join(OUT, 'contact-sheet.jpg'));

  const report = ['# BACKLIGHT capture report', '', '| mode | shot | mean | p50 | p90 | p99 | black<2% | bright>20% |', '|---|---|---:|---:|---:|---:|---:|---:|'];
  for (const m of modes) for (const s of shots) {
    const v = metrics[m.name][s];
    report.push(`| ${m.name} | ${s} | ${v.mean.toFixed(3)} | ${v.p50.toFixed(3)} | ${v.p90.toFixed(3)} | ${v.p99.toFixed(3)} | ${(v.black * 100).toFixed(1)}% | ${(v.bright * 100).toFixed(1)}% |`);
  }
  await writeFile(path.join(OUT, 'report.md'), report.join('\n') + '\n');
  await writeFile(path.join(OUT, 'manifest.json'), JSON.stringify({
    generatedAt: new Date().toISOString(),
    gitSha,
    sourceSha256: (await sourceDigest(path.join(ROOT, 'src/backlight'))).digest('hex'),
    browser: await browser.version(),
    shots, modes: modes.map((m) => m.name),
    metrics,
  }, null, 2) + '\n');
  console.log(`done -> ${OUT}`);
} catch (err) {
  process.stderr.write(serverLog);
  throw err;
} finally {
  await browser?.close();
  server.kill('SIGTERM');
}

async function imageMetrics(buffer) {
  const { data, info } = await sharp(buffer).resize({ width: 400, withoutEnlargement: true }).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const luma = [];
  let black = 0, bright = 0, sum = 0;
  for (let i = 0; i < data.length; i += info.channels) {
    const y = 0.2126 * data[i] / 255 + 0.7152 * data[i + 1] / 255 + 0.0722 * data[i + 2] / 255;
    luma.push(y); sum += y;
    if (y < 0.02) black++;
    if (y > 0.2) bright++;
  }
  luma.sort((a, b) => a - b);
  const pct = (p) => luma[Math.min(luma.length - 1, Math.floor((luma.length - 1) * p))];
  return { mean: sum / luma.length, p50: pct(0.5), p90: pct(0.9), p99: pct(0.99), black: black / luma.length, bright: bright / luma.length };
}
