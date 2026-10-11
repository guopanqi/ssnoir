import { mkdirSync } from "node:fs";
import { chromium } from "playwright";
import { createServer } from "vite";

const server = await createServer({
  configFile: "vite.config.ts",
  server: { host: "127.0.0.1", port: 4173, strictPort: true }
});
let browser;
try {
  await server.listen();
  browser = await chromium.launch({
    headless: true,
    args: ["--no-sandbox", "--enable-webgl", "--enable-unsafe-swiftshader", "--use-gl=angle", "--use-angle=swiftshader"]
  });

  for (const scenario of [
    { name: "desktop", width: 1024, height: 576, touch: false },
    { name: "mobile-landscape", width: 812, height: 375, touch: true },
    { name: "compact-landscape", width: 640, height: 480, touch: true }
  ]) {
    const context = await browser.newContext({
      viewport: { width: scenario.width, height: scenario.height },
      deviceScaleFactor: 1,
      hasTouch: scenario.touch,
      isMobile: scenario.touch
    });
    const page = await context.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push("pageerror: " + String(error)));
    page.on("console", msg => {
      if (msg.type() === "error") errors.push("console: " + msg.text());
    });
    try {
      await page.goto("http://127.0.0.1:4173/", { waitUntil: "networkidle" });
      await page.waitForFunction(() => window.__SSNOIR_FOUNDATION__?.getSchemeValue() === 1, null, { timeout: 20000 });

      const scale = scenario.height / 576;
      const x = 160 * scale;
      const y = (scenario.height - 576 * scale) / 2 + 483 * scale;
      if (scenario.touch) await page.touchscreen.tap(x, y);
      else await page.mouse.click(x, y);

      await page.waitForFunction(() => window.__SSNOIR_FOUNDATION__?.getSchemeValue() === 2, null, { timeout: 10000 });
      // Exercise the existing session after a host resize and remap input.
      await page.setViewportSize({ width: 960, height: 540 });
      await page.waitForFunction(() => document.querySelector("canvas")?.height === 540);
      if (scenario.touch) await page.touchscreen.tap(150, 452.8125);
      else await page.mouse.click(150, 452.8125);
      await page.waitForFunction(() => window.__SSNOIR_FOUNDATION__?.getSchemeValue() === 3);
      await page.setViewportSize({ width: scenario.width, height: scenario.height });
      await page.waitForFunction(height => document.querySelector("canvas")?.height === height, scenario.height);
      if (errors.length) throw new Error(errors.join("\n"));
      mkdirSync("artifacts", { recursive: true });
      await page.screenshot({ path: "artifacts/foundation-" + scenario.name + ".png", fullPage: true });
      console.log("PASS: " + scenario.name + " shared Three/Pixi context + Scheme + user input");
    } catch (error) {
      mkdirSync("artifacts", { recursive: true });
      await page.screenshot({ path: "artifacts/foundation-" + scenario.name + "-failed.png", fullPage: true }).catch(() => {});
      const details = await page.evaluate(() => ({
        diagnostics: document.querySelector("#error")?.textContent || "",
        scheme: window.__SSNOIR_FOUNDATION__?.getSchemeValue(),
        canvasSize: [document.querySelector("canvas")?.width, document.querySelector("canvas")?.height]
      })).catch(e => ({ diagnostics: String(e) }));
      console.error("Capture failed for", scenario.name, JSON.stringify(details), "console/page errors", errors);
      throw error;
    } finally {
      await context.close();
    }
  }
} finally {
  if (browser) await browser.close();
  await server.close();
}
