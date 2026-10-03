import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';

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

const modes = ['final', 'shape'];

function startServer() {
  return spawn(
    process.platform === 'win32' ? 'npm.cmd' : 'npm',
    ['run', 'preview', '--', '--host', '127.0.0.1', '--port', String(PORT), '--strictPort'],
    { cwd: ROOT, stdio: ['ignore', 'pipe', 'pipe'] },
  );
}

async function waitForServer(page, attempts = 60) {
  for (let i = 0; i < attempts; i++) {
    try {
      await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 3000 });
      await page.waitForFunction(() => window.__noirLab?.ready === true, null, { timeout: 3000 });
      return;
    } catch {
      await new Promise((resolve) => setTimeout(resolve, 500));
    }
  }
  throw new Error('Noir Engraving Lab dev server did not become ready.');
}

await rm(OUT, { recursive: true, force: true });
await mkdir(OUT, { recursive: true });

const server = startServer();
let serverLog = '';
server.stdout.on('data', (d) => { serverLog += d.toString(); });
server.stderr.on('data', (d) => { serverLog += d.toString(); });

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
page.on('console', (message) => {
  const line = `[browser:${message.type()}] ${message.text()}\n`;
  serverLog += line;
  process.stdout.write(line);
});
page.on('pageerror', (error) => {
  const line = `[pageerror] ${error.stack ?? error.message}\n`;
  serverLog += line;
  process.stderr.write(line);
});

try {
  await waitForServer(page);

  // UI is useful interactively but not part of the rendered benchmark.
  await page.evaluate(() => {
    document.querySelector('.hud-top')?.remove();
    document.querySelector('.hud-bottom')?.remove();
    document.querySelector('#panel')?.remove();
  });

  for (const mode of modes) {
    const dir = path.join(OUT, mode);
    await mkdir(dir, { recursive: true });

    await page.evaluate((name) => window.__noirLab.setMode(name), mode);

    for (const [name, index] of shots) {
      await page.evaluate((i) => window.__noirLab.setShot(i), index);
      await page.evaluate(() => window.__noirLab.render());
      await page.screenshot({
        path: path.join(dir, `${name}.png`),
        type: 'png',
      });
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
    }, null, 2) + '\n',
  );
} catch (error) {
  process.stderr.write(serverLog);
  throw error;
} finally {
  await browser.close();
  server.kill('SIGTERM');
}
