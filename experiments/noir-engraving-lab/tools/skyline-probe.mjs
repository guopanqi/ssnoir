// 快速探针 3：读 rt1 像素 + 最小复现组合。node tools/skyline-probe.mjs
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import path from 'node:path';

const ROOT = process.cwd();
const PORT = 4321;
const URL = `http://127.0.0.1:${PORT}/skyline-noir-glm.html?capture=1`;

const server = spawn(process.execPath, [
  path.join(ROOT, 'node_modules/vite/bin/vite.js'),
  '--host', '127.0.0.1', '--port', String(PORT), '--strictPort',
], { cwd: ROOT, stdio: ['ignore', 'pipe', 'pipe'] });
server.stdout.on('data', () => {});
server.stderr.on('data', (d) => process.stderr.write(d));
await new Promise((r) => setTimeout(r, 2000));

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.on('pageerror', (e) => console.log('PAGEERROR:', e.message));
page.on('console', (m) => { if (m.type() === 'error') console.log('CONSOLE:', m.text()); });

await page.goto(URL, { waitUntil: 'domcontentloaded' });
for (let i = 0; i < 60; i++) {
  if (await page.evaluate(() => window.__skylineNoirGlm?.ready === true).catch(() => false)) break;
  await new Promise((r) => setTimeout(r, 500));
}
await page.evaluate(() => window.__skylineNoirGlm.setShot(0));
await new Promise((r) => setTimeout(r, 300));

const result = await page.evaluate(() => {
  const d = window.__skylineDebug;
  const renderer = d.renderer;
  const composer = d.composer;
  const rt = composer.renderTarget1;
  const read = (rti) => {
    const buf = new Float32Array(4 * 9);
    renderer.readRenderTargetPixels(rti, rti.width >> 1, rti.height >> 1, 3, 3, buf);
    let s = 0;
    for (let i = 0; i < 9; i++) s += buf[i * 4] + buf[i * 4 + 1] + buf[i * 4 + 2];
    return (s / 27).toFixed(4);
  };
  const out = {};

  // 只开 render
  for (const p of composer.passes) p.enabled = false;
  composer.passes[0].enabled = true;
  composer.render();
  out.afterRender_rt1 = read(composer.renderTarget1);
  out.afterRender_rt2 = read(composer.renderTarget2);

  // render + bloom
  composer.passes[1].enabled = true;
  composer.render();
  out.afterBloom_rt1 = read(composer.renderTarget1);
  out.afterBloom_rt2 = read(composer.renderTarget2);

  // 再加 output
  composer.passes[2].enabled = true;
  composer.render();
  out.afterOutput_rt1 = read(composer.renderTarget1);
  out.afterOutput_rt2 = read(composer.renderTarget2);

  out.readBufferName = composer.readBuffer.texture.name;
  out.writeBufferName = composer.writeBuffer.texture.name;
  out.bloomRenderTargetsOk = d.bloom.renderTargetsHorizontal.map((rti) => {
    const buf = new Float32Array(4);
    renderer.readRenderTargetPixels(rti, rti.width >> 1, rti.height >> 1, 1, 1, buf);
    return [buf[0].toFixed(3), buf[1].toFixed(3), buf[2].toFixed(3)].join(',');
  });
  return out;
});
console.log(JSON.stringify(result, null, 2));

await browser.close();
server.kill('SIGTERM');
