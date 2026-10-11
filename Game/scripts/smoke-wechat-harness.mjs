import { readFileSync, mkdirSync } from "node:fs";
import { chromium } from "playwright";

const target = process.env.SSNOIR_MINIGAME_TARGET === "taptap" ? "taptap" : "wechat";
const sourceFile = target === "taptap" ? "dist/taptap/game/game.js" : "dist/wechat/game.js";
const source = readFileSync(sourceFile, "utf8");
const canvasMode = process.env.SSNOIR_WX_CANVAS_MODE || "offscreen";
if (!["offscreen", "missing", "throws"].includes(canvasMode)) throw new Error("Unknown canvas mode: " + canvasMode);
const browser = await chromium.launch({
  headless: true,
  args: ["--no-sandbox", "--enable-webgl", "--enable-unsafe-swiftshader", "--use-gl=angle", "--use-angle=swiftshader"]
});
const page = await browser.newPage({ viewport: { width: 1024, height: 576 } });
const errors = [];
page.on("pageerror", error => errors.push("pageerror: " + (error.stack || String(error))));
page.on("console", msg => {
  if (msg.type() === "error") errors.push("console: " + msg.text());
  if (msg.type() === "log" && msg.text().includes("[SSNoir]")) console.log("browser:", msg.text());
});
try {
  await page.setContent('<canvas id="mini" width="1024" height="576"></canvas>');
  await page.evaluate((canvasMode) => {
    const canvas = document.getElementById("mini");
    canvas.style.width = "1024px";
    canvas.style.height = "576px";
    window.__wxFrames = new Set();
    canvas.requestAnimationFrame = callback => {
      const id = requestAnimationFrame(time => {
        window.__wxFrames.delete(id);
        callback(time);
      });
      window.__wxFrames.add(id);
      return id;
    };
    canvas.cancelAnimationFrame = id => {
      window.__wxFrames.delete(id);
      cancelAnimationFrame(id);
    };
    // Official converter prepends GameGlobal.fetch = undefined; a TapTap
    // compatibility host exposes GameGlobal and can route wx APIs.
    window.GameGlobal = window;
    let canvasCount = 0;
    window.__wxCanvasUsage = { screen: 0, secondary: 0, offscreen: 0 };
    const wxMock = {
      createCanvas: () => {
        canvasCount++;
        if (canvasCount === 1) {
          window.__wxCanvasUsage.screen++;
          return canvas;
        }
        window.__wxCanvasUsage.secondary++;
        return document.createElement("canvas");
      },
      createImage: () => new Image(),
      getSystemInfoSync: () => ({ windowWidth: 1024, windowHeight: 576, screenWidth: 1024, screenHeight: 576, platform: "chromium-wx-mock", pixelRatio: 1, devicePixelRatio: 1 }),
      onTouchEnd: callback => { window.__wxTouchEnd = callback; },
      onHide: callback => { window.__wxHide = callback; },
      onShow: callback => { window.__wxShow = callback; },
      showModal: detail => { throw new Error("wx.showModal: " + detail.content); }
    };
    if (canvasMode === "offscreen") {
      wxMock.createOffscreenCanvas = ({ width, height }) => {
        window.__wxCanvasUsage.offscreen++;
        const c = document.createElement("canvas");
        c.width = width;
        c.height = height;
        return c;
      };
    } else if (canvasMode === "throws") {
      wxMock.createOffscreenCanvas = () => {
        window.__wxCanvasUsage.offscreen++;
        throw new Error("Simulated createOffscreenCanvas failure");
      };
    }
    window.wx = wxMock;
  }, canvasMode);
  await page.addScriptTag({ content: source }).catch(error => { errors.push("addScriptTag: " + (error.stack || String(error))); });
  await page.waitForFunction(
    () => window.__SSNOIR_WECHAT_FOUNDATION__?.getSchemeValue() === 1, null, { timeout: 20000 }
  );
  await page.evaluate(() => window.__wxTouchEnd({ changedTouches: [{ clientX: 160, clientY: 483 }] }));
  await page.waitForFunction(
    () => window.__SSNOIR_WECHAT_FOUNDATION__?.getSchemeValue() === 2, null, { timeout: 10000 }
  );
  await page.evaluate(() => { window.__wxHide(); window.__wxHide(); });
  if (await page.evaluate(() => window.__wxFrames.size) !== 0) {
    throw new Error("Hidden WeChat runtime still has a pending render frame");
  }
  if (await page.evaluate(() => window.__SSNOIR_WECHAT_FOUNDATION__.getDiagnostics().visible)) {
    throw new Error("WeChat hide state was not applied");
  }
  await page.evaluate(() => { window.__wxShow(); window.__wxShow(); });
  if (await page.evaluate(() => window.__wxFrames.size) !== 1) {
    throw new Error("WeChat resume must schedule exactly one render frame");
  }
  await page.evaluate(() => window.__wxTouchEnd({ changedTouches: [{ clientX: 160, clientY: 483 }] }));
  await page.waitForFunction(() => window.__SSNOIR_WECHAT_FOUNDATION__.getSchemeValue() === 3);
  if (!await page.evaluate(() => window.__SSNOIR_WECHAT_FOUNDATION__.getDiagnostics().visible)) {
    throw new Error("WeChat show state was not applied");
  }
  if (errors.length) throw new Error(errors.join("\n"));
  const canvasUsage = await page.evaluate(() => window.__wxCanvasUsage);
  if (canvasUsage.screen !== 1) throw new Error("Expected exactly one on-screen canvas: " + JSON.stringify(canvasUsage));
  if (canvasMode === "offscreen" && !(canvasUsage.offscreen > 0) ||
      canvasMode !== "offscreen" && !(canvasUsage.secondary > 0)) {
    throw new Error("Wrong Pixi 2D canvas backend: " + JSON.stringify(canvasUsage));
  }
  mkdirSync("artifacts", { recursive: true });
  const suffix = canvasMode === "offscreen" ? "" : "-" + canvasMode;
  await page.screenshot({ path: "artifacts/" + target + "-harness" + suffix + ".png" });
  console.log("PASS:", target, canvasMode, "canvas backend", JSON.stringify(canvasUsage), "shared WebGL2/Pixi + Scheme touch");
} catch (error) {
  mkdirSync("artifacts", { recursive: true });
  await page.screenshot({ path: "artifacts/" + target + "-harness-" + canvasMode + "-failed.png" }).catch(() => {});
  const diagnosis = await page.evaluate(() => ({
    wxPresent: typeof wx !== "undefined",
    ready: !!window.__SSNOIR_WECHAT_FOUNDATION__,
    counter: window.__SSNOIR_WECHAT_FOUNDATION__?.getSchemeValue(),
    canvas: [document.getElementById("mini")?.width, document.getElementById("mini")?.height],
    usage: window.__wxCanvasUsage
  })).catch(e => ({ evaluateError: String(e) }));
  console.error(target + " harness error", JSON.stringify(diagnosis), "browser errors:", JSON.stringify(errors));
  throw error;
} finally {
  await browser.close();
}
