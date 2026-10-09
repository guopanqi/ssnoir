import { mkdirSync } from "node:fs";
import { chromium } from "playwright";
import { createServer } from "vite";

const server = await createServer({
  configFile: "vite.config.ts",
  server: { host: "127.0.0.1", port: 4173, strictPort: true }
});
let browser;
let page;
try {
  await server.listen();
  browser = await chromium.launch({
    headless: true,
    args: [
      "--no-sandbox",
      "--enable-webgl",
      "--enable-unsafe-swiftshader",
      "--use-gl=angle",
      "--use-angle=swiftshader"
    ]
  });
  page = await browser.newPage({ viewport: { width: 1024, height: 576 }, deviceScaleFactor: 1 });
  const errors = [];
  page.on("pageerror", error => errors.push(String(error)));
  await page.goto("http://127.0.0.1:4173/", { waitUntil: "networkidle" });
  await page.waitForFunction(
    () => window.__SSNOIR_FOUNDATION__?.getSchemeValue() === 1,
    null, { timeout: 20000 }
  );
  await page.mouse.click(160, 483);
  await page.waitForFunction(
    () => window.__SSNOIR_FOUNDATION__?.getSchemeValue() === 2,
    null, { timeout: 10000 }
  );
  if (errors.length) throw new Error(errors.join("\n"));
  mkdirSync("artifacts", { recursive: true });
  await page.screenshot({ path: "artifacts/foundation-web.png", fullPage: true });
  console.log("PASS: WebGL, Pixi pointertap and Scheme evaluation; screenshot saved.");
} catch (error) {
  if (page) {
    mkdirSync("artifacts", { recursive: true });
    await page.screenshot({ path: "artifacts/foundation-web-failed.png", fullPage: true }).catch(() => {});
  }
  throw error;
} finally {
  if (browser) await browser.close();
  await server.close();
}
