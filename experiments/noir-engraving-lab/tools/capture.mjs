import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';

const ROOT = process.cwd();
const OUT = path.join(ROOT, 'captures', 'latest');
const PORT = 4173;
const URL = `http://127.0.0.1:${PORT}/?capture=1`;

const shots = [
  ['01-theater-street', 0],
  ['02-warehouse-fog', 1],
  ['03-alley-mouth', 2],
  ['04-high-city', 3],
];

const modes = [
  { name: 'final', type: 'png', viewport: { width: 1600, height: 900 } },
  { name: 'shape', type: 'jpeg', quality: 84, viewport: { width: 1200, height: 675 } },
  { name: 'line', type: 'jpeg', quality: 84, viewport: { width: 1200, height: 675 } },
];

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
      // Vite may still be starting.
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
  browser = await chromium.launch({
    headless: true,
    executablePath: process.env.CHROME_PATH || undefined,
    args: process.env.CI ? ['--no-sandbox', '--disable-dev-shm-usage'] : [],
  });

  const page = await browser.newPage({ viewport: modes[0].viewport, deviceScaleFactor: 1 });
  page.on('pageerror', (error) => process.stderr.write(`[browser:error] ${error.stack ?? error}\n`));
  await waitForServer(page);

  await page.evaluate(() => {
    document.querySelector('.hud-top')?.remove();
    document.querySelector('.hud-bottom')?.remove();
    document.querySelector('#panel')?.remove();
  });

  const metrics = {};
  const outputs = {};

  for (const mode of modes) {
    metrics[mode.name] = {};
    outputs[mode.name] = {};

    await page.setViewportSize(mode.viewport);
    await page.evaluate((name) => window.__noirLab.setMode(name), mode.name);

    const dir = path.join(OUT, mode.name);
    await mkdir(dir, { recursive: true });

    for (const [name, index] of shots) {
      await page.evaluate((i) => window.__noirLab.setShot(i), index);
      console.log(`Capturing ${mode.name}/${name}`);

      const extension = mode.type === 'jpeg' ? 'jpg' : 'png';
      const file = path.join(dir, `${name}.${extension}`);
      const screenshot = await page.screenshot({
        path: file,
        type: mode.type,
        quality: mode.type === 'jpeg' ? mode.quality : undefined,
        timeout: 120_000,
      });

      outputs[mode.name][name] = file;
      metrics[mode.name][name] = await imageMetrics(screenshot);
    }
  }

  const info = await page.evaluate(() => window.__noirLab.info());

  const contactOrder = ['shape', 'line', 'final'];
  const thumbW = 520;
  const thumbH = 292;
  const contact = sharp({
    create: {
      width: thumbW * contactOrder.length,
      height: thumbH * shots.length,
      channels: 3,
      background: { r: 6, g: 8, b: 12 },
    },
  });

  const composites = [];
  for (let col = 0; col < contactOrder.length; col++) {
    for (let row = 0; row < shots.length; row++) {
      const modeName = contactOrder[col];
      const [shotName] = shots[row];
      const label = `${modeName.toUpperCase()} · ${shotName}`;
      const svg = Buffer.from(
        `<svg width="${thumbW}" height="${thumbH}" xmlns="http://www.w3.org/2000/svg">
          <rect x="0" y="0" width="310" height="28" fill="rgba(0,0,0,.78)"/>
          <text x="9" y="19" fill="white" font-size="13" font-family="Arial, sans-serif">${label}</text>
        </svg>`
      );

      const input = await sharp(outputs[modeName][shotName])
        .resize(thumbW, thumbH, { fit: 'cover' })
        .composite([{ input: svg, top: 0, left: 0 }])
        .jpeg({ quality: 88 })
        .toBuffer();

      composites.push({
        input,
        left: col * thumbW,
        top: row * thumbH,
      });
    }
  }

  await contact
    .composite(composites)
    .jpeg({ quality: 90 })
    .toFile(path.join(OUT, 'contact-sheet.jpg'));

  const report = [
    '# Noir Engraving capture report',
    '',
    '| mode | shot | mean | p50 | p90 | p95 | p99 | black<2% | bright>20% |',
    '|---|---|---:|---:|---:|---:|---:|---:|---:|',
  ];

  for (const modeName of contactOrder) {
    for (const [shotName] of shots) {
      const m = metrics[modeName][shotName];
      report.push(
        `| ${modeName} | ${shotName} | ${m.mean.toFixed(3)} | ${m.p50.toFixed(3)} | ${m.p90.toFixed(3)} | ${m.p95.toFixed(3)} | ${m.p99.toFixed(3)} | ${(m.blackUnder02 * 100).toFixed(1)}% | ${(m.brightOver20 * 100).toFixed(1)}% |`
      );
    }
  }

  await writeFile(path.join(OUT, 'report.md'), report.join('\n') + '\n');

  await writeFile(
    path.join(OUT, 'manifest.json'),
    JSON.stringify({
      generatedAt: new Date().toISOString(),
      gitSha: process.env.GITHUB_SHA ?? null,
      modes: Object.fromEntries(modes.map((m) => [m.name, {
        type: m.type,
        viewport: m.viewport,
      }])),
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
