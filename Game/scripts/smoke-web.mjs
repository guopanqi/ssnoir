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
    args: ["--no-sandbox", "--enable-webgl", "--enable-unsafe-swiftshader",
      "--use-gl=angle", "--use-angle=swiftshader"]
  });
  const page = await browser.newPage({ viewport: { width: 1440, height: 850 } });
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  page.on("console", msg => { if (msg.type() === "error") errors.push(msg.text()); });
  await page.goto("http://127.0.0.1:4173/?seed=42", { waitUntil: "domcontentloaded" });
  await page.getByTestId("game-ready").waitFor({ timeout: 60000 });
  async function drainStory() {
    for (let i = 0; i < 100; i++) {
      const current = await page.evaluate(() => window.__SSNOIR_WEB__.snapshot().story);
      if (!current) return;
      await page.getByTestId("story-continue").click();
    }
    throw new Error("Story exceeded 100 blocking frames");
  }
  await drainStory();
  const before = await page.evaluate(() => window.__SSNOIR_WEB__.snapshot());
  if (before.mode !== "world" || !before.entries.length || before.day !== 1)
    throw new Error("Unplayable original Scheme world: " + JSON.stringify(before));
  mkdirSync("artifacts", { recursive: true });
  await page.screenshot({ path: "artifacts/web-game-city.png" });
  const place = before.entries.find(x => x.isPlace && !x.disabled);
  if (!place) throw new Error("World does not expose a visitable place");
  await page.evaluate(id => window.__SSNOIR_WEB__.open(id), place.id);
  await drainStory();
  const after = await page.evaluate(() => window.__SSNOIR_WEB__.snapshot());
  if (after.mode === "world" && !after.path.length) throw new Error("Place navigation did not arrive");
  if (!after.entries.length) throw new Error("Entered destination has no visible content");
  await page.screenshot({ path: "artifacts/web-game-place.png" });
  if (errors.length) throw new Error(errors.join("\n"));
  console.log("PASS: browser started original world.scm, visited " + place.title +
    ", rendered " + after.entries.length + " nodes");
  await page.close();
} finally {
  await browser?.close();
  await server.close();
}
