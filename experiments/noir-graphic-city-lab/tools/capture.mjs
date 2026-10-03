import { chromium } from 'playwright';
import { spawn, execFileSync } from 'node:child_process';
import { mkdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { parseArgs } from 'node:util';
import sharp from 'sharp';
import { sourceFingerprint } from './source-fingerprint.mjs';

const ROOT = process.cwd();
const { values } = parseArgs({ options: {
  shot: { type: 'string' },
  mode: { type: 'string' },
  out: { type: 'string' },
  port: { type: 'string', default: '4174' },
} });

const OUT = path.resolve(ROOT, values.out ?? 'captures/latest');
if (!OUT.startsWith(path.join(ROOT, 'captures') + path.sep)) {
  throw new Error('--out must be inside captures/.');
}
const PORT = Number(values.port);
if (!Number.isInteger(PORT) || PORT < 1024 || PORT > 65535) throw new Error('Invalid --port');
const URL = `http://127.0.0.1:${PORT}/?capture=1`;

const allShots = [
  ['01-theater-tableau', 0],
  ['02-crowd-crossing', 1],
  ['03-alley-confrontation', 2],
  ['04-city-canyon', 3],
];
const allModes = [
  { name: 'shape', type: 'jpeg', quality: 86, viewport: { width: 960, height: 540 } },
  { name: 'line', type: 'jpeg', quality: 86, viewport: { width: 960, height: 540 } },
  { name: 'accent', type: 'jpeg', quality: 86, viewport: { width: 960, height: 540 } },
  { name: 'final', type: 'png', viewport: { width: 1600, height: 900 } },
];
const shots = values.shot ? allShots.filter(([name]) => name === values.shot) : allShots;
const modes = values.mode ? allModes.filter((m) => m.name === values.mode) : allModes;
if (!shots.length || !modes.length) throw new Error('Unknown --shot or --mode');

const sourceSha256 = await sourceFingerprint(ROOT);
let gitSha = process.env.GITHUB_SHA ?? null;
try { gitSha ??= execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8', stdio: ['ignore','pipe','ignore'] }).trim(); } catch {}

async function imageMetrics(buffer) {
  const { data, info } = await sharp(buffer)
    .resize({ width: 400, withoutEnlargement: true })
    .removeAlpha().raw().toBuffer({ resolveWithObject: true });
  let sum = 0, black = 0, cream = 0, gold = 0;
  const bins = [0,0,0,0,0];
  const count = info.width * info.height;
  for (let i = 0; i < data.length; i += info.channels) {
    const r = data[i] / 255, g = data[i+1] / 255, b = data[i+2] / 255;
    const l = 0.2126*r + 0.7152*g + 0.0722*b;
    sum += l;
    if (l < 0.08) black++;
    if (l > 0.70) cream++;
    if (r > 0.45 && (r - b) > 0.30 && (r - g) > 0.10 && (g - b) > 0.08) gold++;
    bins[Math.min(4, Math.floor(l * 5))]++;
  }
  return {
    mean: sum / count,
    blackUnder08: black / count,
    creamOver70: cream / count,
    goldShare: gold / count,
    lumaBins: bins.map((n) => n / count),
  };
}

function startServer() {
  return spawn(process.execPath, [
    path.join(ROOT, 'node_modules/vite/bin/vite.js'),
    '--host','127.0.0.1','--port',String(PORT),'--strictPort',
  ], { cwd: ROOT, stdio: ['ignore','pipe','pipe'] });
}

async function waitReady(page) {
  for (let i = 0; i < 80; i++) {
    try {
      const response = await fetch(URL, { signal: AbortSignal.timeout(1200) });
      if (response.ok) break;
    } catch {}
    await new Promise((r) => setTimeout(r, 350));
  }
  await page.goto(URL, { waitUntil:'domcontentloaded', timeout:120000 });
  for (let i = 0; i < 160; i++) {
    if (await page.evaluate(() => window.__graphicCityLab?.ready === true).catch(() => false)) return;
    await new Promise((r) => setTimeout(r, 250));
  }
  throw new Error('Graphic City Lab page did not become ready.');
}

await rm(OUT, { recursive:true, force:true });
await mkdir(OUT, { recursive:true });
const server = startServer();
let serverLog = '';
server.stdout.on('data', d => serverLog += d.toString());
server.stderr.on('data', d => serverLog += d.toString());
let browser;

try {
  browser = await chromium.launch({
    headless: true,
    executablePath: process.env.CHROME_PATH || undefined,
    args: process.env.CI ? ['--no-sandbox','--disable-dev-shm-usage'] : [],
  });
  const page = await browser.newPage({ viewport:modes[0].viewport, deviceScaleFactor:1 });
  const browserErrors = [];
  page.on('pageerror', e => browserErrors.push(e.stack ?? String(e)));
  page.on('console', m => { if (m.type() === 'error') browserErrors.push(m.text()); });
  page.on('response', r => { if (r.status() >= 400) browserErrors.push(`HTTP ${r.status()} ${r.url()}`); });
  const assertHealthy = () => { if (browserErrors.length) throw new Error(browserErrors.join('\n')); };

  await waitReady(page);
  await page.evaluate(() => document.querySelector('.hud')?.remove());

  const outputs = {}, metrics = {}, timings = {};
  const started = performance.now();

  for (const mode of modes) {
    outputs[mode.name] = {};
    metrics[mode.name] = {};
    timings[mode.name] = {};
    await page.setViewportSize(mode.viewport);
    await page.evaluate(() => new Promise(resolve => {
      dispatchEvent(new Event('resize'));
      requestAnimationFrame(() => { window.__graphicCityLab.render(); requestAnimationFrame(resolve); });
    }));
    await page.evaluate(name => window.__graphicCityLab.setMode(name), mode.name);
    const dir = path.join(OUT, mode.name);
    await mkdir(dir, { recursive:true });

    for (const [shotName, index] of shots) {
      await page.evaluate(i => { window.__graphicCityLab.setShot(i); window.__graphicCityLab.render(); }, index);
      const t0 = performance.now();
      const ext = mode.type === 'jpeg' ? 'jpg' : 'png';
      const file = path.join(dir, `${shotName}.${ext}`);
      const image = await page.screenshot({
        path:file, type:mode.type,
        quality:mode.type === 'jpeg' ? mode.quality : undefined,
        timeout:120000,
      });
      assertHealthy();
      outputs[mode.name][shotName] = file;
      metrics[mode.name][shotName] = await imageMetrics(image);
      timings[mode.name][shotName] = Math.round(performance.now() - t0);
    }
  }

  const order = ['shape','line','accent','final'].filter(x => outputs[x]);
  const thumbW = 480, thumbH = 270;
  const sheet = sharp({ create:{
    width:thumbW * order.length,
    height:thumbH * shots.length,
    channels:3,
    background:{r:5,g:5,b:5},
  }});
  const comps = [];
  for (let col=0; col<order.length; col++) {
    for (let row=0; row<shots.length; row++) {
      const mode = order[col], shot = shots[row][0];
      const label = Buffer.from(`<svg width="${thumbW}" height="${thumbH}" xmlns="http://www.w3.org/2000/svg">
        <rect width="300" height="27" fill="rgba(0,0,0,.82)"/>
        <text x="8" y="18" fill="#eee5d0" font-size="12" font-family="Arial">${mode.toUpperCase()} · ${shot}</text>
      </svg>`);
      const input = await sharp(outputs[mode][shot]).resize(thumbW,thumbH,{fit:'cover'})
        .composite([{input:label,top:0,left:0}]).jpeg({quality:89}).toBuffer();
      comps.push({input,left:col*thumbW,top:row*thumbH});
    }
  }
  await sheet.composite(comps).jpeg({quality:91}).toFile(path.join(OUT,'contact-sheet.jpg'));

  const report = [
    '# Graphic City capture report','',
    '| mode | shot | mean | black<8% | cream>70% | gold share |',
    '|---|---|---:|---:|---:|---:|',
  ];
  for (const mode of order) for (const [shot] of shots) {
    const m = metrics[mode][shot];
    report.push(`| ${mode} | ${shot} | ${m.mean.toFixed(3)} | ${(m.blackUnder08*100).toFixed(1)}% | ${(m.creamOver70*100).toFixed(1)}% | ${(m.goldShare*100).toFixed(2)}% |`);
  }
  report.push('', '## Accent budget', '');
  if (metrics.final) for (const [shot] of shots) {
    const gold = metrics.final[shot].goldShare;
    report.push(`- ${shot}: ${(gold*100).toFixed(2)}% ${gold > 0.028 ? '⚠ above provisional 2.8% budget' : ''}`);
  }
  report.push('', `Total capture: ${((performance.now()-started)/1000).toFixed(1)}s`);
  await writeFile(path.join(OUT,'report.md'), report.join('\n') + '\n');

  const info = await page.evaluate(() => window.__graphicCityLab.info());
  await writeFile(path.join(OUT,'manifest.json'), JSON.stringify({
    generatedAt:new Date().toISOString(),
    gitSha, sourceSha256, browser:await browser.version(),
    shots:shots.map(([name,index])=>({name,index})),
    modes:Object.fromEntries(modes.map(m=>[m.name,{type:m.type,viewport:m.viewport}])),
    info, metrics, timings,
  }, null, 2) + '\n');
} catch (error) {
  process.stderr.write(serverLog);
  throw error;
} finally {
  await browser?.close();
  server.kill('SIGTERM');
}
