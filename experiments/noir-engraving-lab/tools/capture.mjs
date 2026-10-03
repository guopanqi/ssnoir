import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';

const ROOT = process.cwd();
const OUT = path.join(ROOT, 'captures', 'latest');
const PORT = 4173;
const URL = `http://127.0.0.1:${PORT}/?capture=1`;
const VIEWPORT = { width: 1600, height: 900 };

const shots = [
  ['01-theater-street', 0],
  ['02-warehouse-fog', 1],
  ['03-alley-mouth', 2],
  ['04-high-city', 3],
];

const modes = ['shape', 'line', 'final'];

function percentile(sorted, p) {
  if (!sorted.length) return 0;
  return sorted[Math.min(sorted.length - 1, Math.floor((sorted.length - 1) * p))];
}

async function imageMetrics(buffer) {
  const { data, info } = await sharp(buffer)
    .resize({ width: 400, withoutEnlargement: true })
    .removeAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });

  const luma = new Array(info.width * info.height);
  let black = 0;
  let bright = 0;
  let sum = 0;

  for (let i = 0, px = 0; i < data.length; i += info.channels, px++) {
    const r = data[i] / 255;
    const g = data[i + 1] / 255;
    const b = data[i + 2] / 255;
    const y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
    luma[px] = y;
    sum += y;
    if (y < 0.02) black++;
    if (y > 0.20) bright++;
  }

  luma.sort((a, b) => a - b);
  return {
    mean: sum / luma.length,
    p50: percentile(luma, 0.50),
    p90: percentile(luma, 0.90),
    p95: percentile(luma, 0.95),
    p99: percentile(luma, 0.99),
    blackUnder02: black / luma.length,
    brightOver20: bright / luma.length,
  };
}

function startServer() {
  return spawn(
    process.execPath,
    [path.join(ROOT, 'node_modules/vite/bin/vite.js'), '--host', '127.0.0.1', '--port', String(PORT), '--strictPort'],
    { cwd: ROOT, stdio: ['ignore', 'pipe', 'pipe'] },
  );
}

async function waitForServer(page, attempts = 60) {
  let available = false;
  for (let i = 0; i < attempts; i++) {
    try {
      const response = await fetch(URL, { signal: AbortSignal.timeout(1500) });
      if (response.ok) {
        available = true;
        break;
      }
    } catch {
      // Vite may still be starting; do not reload a rendering browser page.
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  if (!available) throw new Error('Noir Engraving Lab dev server did not start.');
  await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 120_000 });
  await page.waitForFunction(() => window.__noirLab?.ready === true, null, { timeout: 120_000 });
}

await rm(OUT, { recursive: true, force: true });
await mkdir(OUT, { recursive: true });

const server = startServer();
let serverLog = '';
server.stdout.on('data', (d) => { serverLog += d.toString(); });
server.stderr.on('data', (d) => { serverLog += d.toString(); });

let browser;

try {
  browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  page.on('pageerror', (error) => process.stderr.write(`[browser:error] ${error.stack ?? error}\n`));
  await waitForServer(page);

  // UI is useful interactively but not part of the rendered benchmark.
  await page.evaluate(() => {
    document.querySelector('.hud-top')?.remove();
    document.querySelector('.hud-bottom')?.remove();
    document.querySelector('#panel')?.remove();
  });

  const metrics = {};

  for (const mode of modes) {
    metrics[mode] = {};
    const dir = path.join(OUT, mode);
    await mkdir(dir, { recursive: true });

    await page.evaluate((name) => window.__noirLab.setMode(name), mode);

    for (const [name, index] of shots) {
      await page.evaluate((i) => window.__noirLab.setShot(i), index);
      console.log(`Capturing ${mode}/${name}`);
      const screenshot = await page.screenshot({
        path: path.join(dir, `${name}.png`),
        type: 'png',
        timeout: 120_000,
      });
      metrics[mode][name] = await imageMetrics(screenshot);
    }
  }

  const info = await page.evaluate(() => window.__noirLab.info());
  await writeFile(
    path.join(OUT, 'manifest.json'),
    JSON.stringify({
      generatedAt: new Date().toISOString(),
      gitSha: process.env.GITHUB_SHA ?? null,
      viewport: VIEWPORT,
      modes,
      shots: shots.map(([name, index]) => ({ name, index })),
      info,
      metrics,
    }, null, 2) + '\n',
  );
} catch (error) {
  process.stderr.write(serverLog);
  throw error;
} finally {
  await browser?.close();
  server.kill('SIGTERM');
}
