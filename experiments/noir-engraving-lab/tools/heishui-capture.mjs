/**
 * 黑水 · 本地视觉循环
 *
 *   node tools/heishui-capture.mjs                        # 全部机位 × 四个阶段
 *   node tools/heishui-capture.mjs --shot 05-rain         # 单机位
 *   node tools/heishui-capture.mjs --mode final --out captures/heishui-trial-x
 *
 * 输出：captures/<out>/{shape,haze,line,final}/*.png|jpg + contact-sheet.jpg
 *       + report.md（亮度统计与逐层增量）+ manifest.json
 *
 * 输出目录必须在 captures/ 内——运行时会被整个替换。
 */
import { chromium } from 'playwright';
import { execFileSync } from 'node:child_process';
import { mkdir, rm, writeFile, readFile, readdir } from 'node:fs/promises';
import { findChromium, startVite, waitForServer, waitReady } from './heishui-harness.mjs';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { parseArgs } from 'node:util';
import sharp from 'sharp';

const ROOT = process.cwd();
const { values } = parseArgs({ options: {
  shot: { type: 'string' }, mode: { type: 'string' }, out: { type: 'string' },
  port: { type: 'string', default: '4183' }, time: { type: 'string', default: '7.5' },
  hero: { type: 'boolean' },
} });
const OUT = path.resolve(ROOT, values.out ?? 'captures/heishui-latest');
if (!OUT.startsWith(path.join(ROOT, 'captures') + path.sep)) throw new Error('--out 必须在 captures/ 内');
const PORT = Number(values.port);
const TIME = Number(values.time);
const URL = `http://127.0.0.1:${PORT}/heishui.html?capture=1`;

const SHOT_IDS = ['01-world', '02-across', '03-river', '04-port', '05-rain', '06-silhouette', '07-plan'];
const MODES = [
  { name: 'final', type: 'png', viewport: { width: 1600, height: 900 } },
  { name: 'hero', type: 'png', viewport: { width: 2560, height: 1440 } },
  { name: 'shape', type: 'jpeg', quality: 86, viewport: { width: 960, height: 540 } },
  { name: 'haze', type: 'jpeg', quality: 86, viewport: { width: 960, height: 540 } },
  { name: 'line', type: 'jpeg', quality: 86, viewport: { width: 960, height: 540 } },
];

const HERO_SHOTS = ['02-across', '04-port', '05-rain', '07-plan'];
const shots = values.shot ? SHOT_IDS.filter((n) => n === values.shot) : SHOT_IDS;
const modes = (values.mode ? MODES.filter((m) => m.name === values.mode) : MODES.filter((m) => m.name !== 'hero'));
if (values.hero) { modes.length = 0; modes.push(MODES.find((m) => m.name === 'hero')); }
if (!shots.length || !modes.length) throw new Error('未知 --shot 或 --mode');

async function sourceDigest(dir = path.join(ROOT, 'src'), hash = createHash('sha256')) {
  for (const e of (await readdir(dir, { withFileTypes: true })).sort((a, b) => a.name.localeCompare(b.name))) {
    const f = path.join(dir, e.name);
    if (e.isDirectory()) await sourceDigest(f, hash);
    else { hash.update(path.relative(ROOT, f)); hash.update(await readFile(f)); }
  }
  return hash;
}
const sourceSha256 = (await sourceDigest()).digest('hex');
let gitSha = process.env.GITHUB_SHA ?? null;
try { gitSha ??= execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim(); } catch {}

function pct(sorted, p) { return sorted.length ? sorted[Math.min(sorted.length - 1, Math.floor((sorted.length - 1) * p))] : 0; }

async function metrics(buf) {
  const { data, info } = await sharp(buf).resize({ width: 400, withoutEnlargement: true }).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const luma = [];
  let black = 0, mid = 0, bright = 0, sum = 0;
  for (let i = 0; i < data.length; i += info.channels) {
    const y = (0.2126 * data[i] + 0.7152 * data[i + 1] + 0.0722 * data[i + 2]) / 255;
    luma.push(y); sum += y;
    if (y < 0.02) black++; else if (y > 0.22) bright++; else mid++;
  }
  luma.sort((a, b) => a - b);
  const n = luma.length;
  return {
    mean: +(sum / n).toFixed(4), p50: +pct(luma, .5).toFixed(4), p90: +pct(luma, .9).toFixed(4),
    p99: +pct(luma, .99).toFixed(4), black: +(black / n).toFixed(4), mid: +(mid / n).toFixed(4), bright: +(bright / n).toFixed(4),
  };
}


await rm(OUT, { recursive: true, force: true });
await mkdir(OUT, { recursive: true });

const server = startVite(ROOT, PORT);
let serverLog = '';
server.stdout.on('data', (d) => { serverLog += d; });
server.stderr.on('data', (d) => { serverLog += d; });

let browser;
try {
  browser = await chromium.launch({
    headless: true,
    executablePath: await findChromium(),
    args: ['--use-angle=metal', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', ...(process.env.CI ? ['--no-sandbox'] : [])],
  });
  const page = await browser.newPage({ viewport: modes[0].viewport, deviceScaleFactor: 1 });
  const errors = [];
  const ignore = (t) => /favicon/.test(t);
  page.on('pageerror', (e) => { if (!ignore(String(e))) errors.push(e.stack ?? String(e)); });
  page.on('console', (m) => {
    const u = m.location()?.url ?? '';
    if (m.type() === 'error' && !ignore(m.text()) && !ignore(u)) errors.push(m.text() + ' @ ' + u);
  });
  page.on('response', (r) => { if (r.status() >= 400 && !ignore(r.url())) errors.push(`HTTP ${r.status()} ${r.url()}`); });

  await waitForServer(URL);
  await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await waitReady(page, errors);
  await page.evaluate(() => { document.querySelectorAll('.hud,.bar,#boot').forEach((e) => e.remove()); });

  const results = {};
  const info = await page.evaluate(() => window.__heishui.info());

  for (const mode of modes) {
    results[mode.name] = {};
    await page.setViewportSize(mode.viewport);
    await page.evaluate(() => window.__heishui.setPaused(true));
    await page.evaluate((t) => { window.__heishui.setTime(t); }, TIME);
    await page.evaluate((m) => window.__heishui.setMode(m), mode.name);
    const dir = path.join(OUT, mode.name);
    await mkdir(dir, { recursive: true });

    for (const id of (mode.name === 'hero' ? HERO_SHOTS : shots)) {
      const idx = SHOT_IDS.indexOf(id);
      // 冻结时钟 -> 摆到机位 -> 设时间 -> 等纹理/着色器稳定 -> 再渲一帧。
      // 不冻结的话，等待期间主循环会继续推进 uTime，同一格每次截图都不同。
      await page.evaluate(() => window.__heishui.setPaused(true));
      await page.evaluate((i) => window.__heishui.setShot(i), idx);
      await page.evaluate((t) => { window.__heishui.setTime(t); }, TIME);
      await new Promise((r) => setTimeout(r, 300));
      await page.evaluate(() => window.__heishui.render());
      await new Promise((r) => setTimeout(r, 120));
      const ext = mode.type === 'jpeg' ? 'jpg' : 'png';
      const file = path.join(dir, `${id}.${ext}`);
      const buf = await page.screenshot({ path: file, type: mode.type, quality: mode.type === 'jpeg' ? mode.quality : undefined, timeout: 180000 });
      results[mode.name][id] = await metrics(buf);
      console.log(`captured ${mode.name}/${id}`);
      if (errors.length) throw new Error(errors.join('\n'));
    }
  }

  // 联络表：行=机位，列=阶段。单阶段（如 hero）时不拼表。
  const order = ['shape', 'haze', 'line', 'final'].filter((m) => results[m]);
  const tw = 460, th = 259;
  if (order.length) {
  const composites = [];
  for (let col = 0; col < order.length; col++) {
    for (let row = 0; row < shots.length; row++) {
      const mode = order[col], id = shots[row];
      const ext = MODES.find((m) => m.name === mode).type === 'jpeg' ? 'jpg' : 'png';
      const label = `${mode.toUpperCase()} · ${id}`;
      const svg = Buffer.from(`<svg width="${tw}" height="${th}" xmlns="http://www.w3.org/2000/svg"><rect width="300" height="26" fill="rgba(0,0,0,.8)"/><text x="8" y="18" fill="#dfe9f7" font-size="13" font-family="Helvetica,Arial">${label}</text></svg>`);
      composites.push({
        input: await sharp(path.join(OUT, mode, `${id}.${ext}`)).resize(tw, th, { fit: 'cover' }).composite([{ input: svg, top: 0, left: 0 }]).jpeg({ quality: 90 }).toBuffer(),
        left: col * tw, top: row * th,
      });
    }
  }
  await sharp({ create: { width: tw * order.length, height: th * shots.length, channels: 3, background: { r: 4, g: 6, b: 10 } } })
    .composite(composites).jpeg({ quality: 92 }).toFile(path.join(OUT, 'contact-sheet.jpg'));
  }

  const lines = ['# 黑水 · 捕获报告', '', `时间 ${new Date().toISOString()}`, '', '| 阶段 | 机位 | mean | p50 | p90 | p99 | 黑<2% | 中调 | 亮>22% |', '|---|---|---:|---:|---:|---:|---:|---:|---:|'];
  for (const mode of order) for (const id of shots) {
    const m = results[mode]?.[id];
    if (!m) continue;
    lines.push(`| ${mode} | ${id} | ${m.mean} | ${m.p50} | ${m.p90} | ${m.p99} | ${(m.black * 100).toFixed(1)}% | ${(m.mid * 100).toFixed(1)}% | ${(m.bright * 100).toFixed(1)}% |`);
  }
  if (order.length > 1) {
    lines.push('', '## 逐层增量（mean）', '', '| 机位 | 体块→空气 | 空气→描线 | 描线→成片 |', '|---|---:|---:|---:|');
    for (const id of shots) {
      const g = (m) => results[m]?.[id]?.mean ?? 0;
      lines.push(`| ${id} | ${(g('haze') - g('shape')).toFixed(4)} | ${(g('line') - g('haze')).toFixed(4)} | ${(g('final') - g('line')).toFixed(4)} |`);
    }
  }
  await writeFile(path.join(OUT, 'report.md'), lines.join('\n') + '\n');
  await writeFile(path.join(OUT, 'manifest.json'), JSON.stringify({
    generatedAt: new Date().toISOString(), gitSha, sourceSha256,
    browser: await browser.version(), time: TIME, info, shots, modes: order, metrics: results,
  }, null, 2) + '\n');

  console.log(`\n联络表 → ${path.relative(ROOT, path.join(OUT, 'contact-sheet.jpg'))}`);
  console.log(`报告   → ${path.relative(ROOT, path.join(OUT, 'report.md'))}`);
} catch (e) {
  process.stderr.write(serverLog.slice(-4000));
  throw e;
} finally {
  await browser?.close();
  server.kill('SIGTERM');
}
