import path from 'node:path';
import { chromium } from 'playwright-core';
const ROOT = path.resolve(import.meta.dirname, '..');
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
await page.goto(`file://${path.join(ROOT, 'index.html')}`);
await page.waitForFunction(() => { const b = document.querySelector('#curtainCard button'); return b && !b.disabled; }, null, { timeout: 25000 });
await page.click('#curtainCard button');
await page.waitForFunction(() => document.querySelector('#curtain').classList.contains('open'));
await page.evaluate(() => ptTheatre.player.hold());
await page.evaluate(() => { ptTheatre.player.openScene(1, { autoplay: false }); ptTheatre.player.goto(3); });
await page.waitForTimeout(900);
const out = await page.evaluate(() => {
  const pack = document.querySelector('#stage .prop-pack');
  const pr = pack && pack.getBoundingClientRect();
  const a = ptTheatre.stage.state.actors['尼尔'];
  const el = ptTheatre.stage.actorEls['尼尔'].getBoundingClientRect();
  return { pack: pr && { x: Math.round(pr.x), y: Math.round(pr.y), w: Math.round(pr.width), h: Math.round(pr.height), visible: getComputedStyle(pack).display },
           neil: { x: Math.round(el.x), y: Math.round(el.y), w: Math.round(el.width), h: Math.round(el.height) }, pose: a.pose, ax: a.x };
});
console.log(JSON.stringify(out, null, 1));
await page.screenshot({ path: '/tmp/pt-s2-live.png', clip: { x: 420, y: 380, width: 400, height: 260 } });
await browser.close();
