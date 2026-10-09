import { readFileSync, mkdirSync } from "node:fs";
import { chromium } from "playwright";

const source = readFileSync("dist/wechat/game.js", "utf8");
const browser = await chromium.launch({
  headless: true,
  args: ["--no-sandbox", "--enable-webgl", "--enable-unsafe-swiftshader", "--use-gl=angle", "--use-angle=swiftshader"]
});
const page = await browser.newPage({ viewport: { width: 1024, height: 576 } });
const errors = [];
page.on("pageerror", error => errors.push(String(error)));
page.on("console", msg => {
  if (msg.type() === "error") errors.push(msg.text());
});
try {
  await page.setContent('<canvas id="mini" width="1024" height="576"></canvas>');
  await page.evaluate(() => {
    const canvas = document.getElementById("mini");
    canvas.style.width = "1024px";
    canvas.style.height = "576px";
    canvas.requestAnimationFrame = callback => requestAnimationFrame(callback);
    canvas.cancelAnimationFrame = id => cancelAnimationFrame(id);
    window.wx = {
      createCanvas: () => canvas,
      createOffscreenCanvas: ({ width, height }) => {
        const c = document.createElement("canvas");
        c.width = width;
        c.height = height;
        return c;
      },
      createImage: () => new Image(),
      getSystemInfoSync: () => ({ windowWidth: 1024, windowHeight: 576, platform: "chromium-wx-mock", pixelRatio: 1 }),
      onTouchEnd: callback => { window.__wxTouchEnd = callback; },
      showModal: detail => { throw new Error("wx.showModal: " + detail.content); }
    };
  });
  await page.addScriptTag({ content: source });
  await page.waitForFunction(
    () => window.__SSNOIR_WECHAT_FOUNDATION__?.getSchemeValue() === 1, null, { timeout: 20000 }
  );
  await page.evaluate(() => window.__wxTouchEnd({ changedTouches: [{ clientX: 160, clientY: 483 }] }));
  await page.waitForFunction(
    () => window.__SSNOIR_WECHAT_FOUNDATION__?.getSchemeValue() === 2, null, { timeout: 10000 }
  );
  if (errors.length) throw new Error(errors.join("\n"));
  mkdirSync("artifacts", { recursive: true });
  await page.screenshot({ path: "artifacts/wechat-harness.png" });
  console.log("PASS: bundled WeChat IIFE with mocked wx host, shared WebGL2/Pixi and touch Scheme action");
} catch (error) {
  mkdirSync("artifacts", { recursive: true });
  await page.screenshot({ path: "artifacts/wechat-harness-failed.png" }).catch(() => {});
  throw error;
} finally {
  await browser.close();
}
