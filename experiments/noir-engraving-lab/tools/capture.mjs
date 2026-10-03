import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile, mkdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';

const ROOT = process.cwd();
const DIST = path.join(ROOT, 'dist');
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

const mime = new Map([
  ['.html', 'text/html; charset=utf-8'],
  ['.js', 'text/javascript; charset=utf-8'],
  ['.css', 'text/css; charset=utf-8'],
  ['.json', 'application/json; charset=utf-8'],
  ['.png', 'image/png'],
  ['.svg', 'image/svg+xml'],
  ['.woff2', 'font/woff2'],
]);

function createStaticServer() {
  return createServer(async (req, res) => {
    try {
      const url = new URL(req.url, 'http://127.0.0.1');
      let relative = decodeURIComponent(url.pathname);
      while (relative.startsWith('/')) relative = relative.slice(1);
      if (!relative) relative = 'index.html';

      if (relative.split('/').includes('..')) {
        res.writeHead(403).end('Forbidden');
        return;
      }

      const resolved = path.join(DIST, relative);
      const body = await readFile(resolved);

      res.writeHead(200, {
        'Content-Type': mime.get(path.extname(resolved)) ?? 'application/octet-stream',
        'Cache-Control': 'no-store',
      });
      res.end(body);
    } catch (error) {
      console.error('[static-server]', req.url, error);
      res.writeHead(404).end('Not found');
    }
  });
}

async function listen(server) {
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(PORT, '127.0.0.1', resolve);
  });
}

async function closeServer(server) {
  await new Promise((resolve) => server.close(resolve));
}

await rm(OUT, { recursive: true, force: true });
await mkdir(OUT, { recursive: true });

const server = createStaticServer();
await listen(server);

const browser = await chromium.launch({
  headless: true,
  args: [
    '--enable-webgl',
    '--ignore-gpu-blocklist',
    '--enable-unsafe-swiftshader',
    '--use-angle=swiftshader',
  ],
});

const page = await browser.newPage({
  viewport: VIEWPORT,
  deviceScaleFactor: 1,
});

let browserLog = '';
page.on('console', (message) => {
  const line = `[browser:${message.type()}] ${message.text()}\n`;
  browserLog += line;
  process.stdout.write(line);
});
page.on('pageerror', (error) => {
  const line = `[pageerror] ${error.stack ?? error.message}\n`;
  browserLog += line;
  process.stderr.write(line);
});
page.on('requestfailed', (request) => {
  const line = `[requestfailed] ${request.url()} :: ${request.failure()?.errorText ?? 'unknown'}\n`;
  browserLog += line;
  process.stderr.write(line);
});
page.on('response', (response) => {
  if (response.status() >= 400) {
    const line = `[response:${response.status()}] ${response.url()}\n`;
    browserLog += line;
    process.stderr.write(line);
  }
});

try {
  await page.goto(URL, {
    waitUntil: 'domcontentloaded',
    timeout: 10000,
  });

  await page.waitForFunction(
    () => window.__noirLab?.ready === true,
    null,
    { timeout: 10000 },
  );

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
  if (browserLog) process.stderr.write(browserLog);
  throw error;
} finally {
  await browser.close();
  await closeServer(server);
}
