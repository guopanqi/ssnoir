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
      await page.goto("http://127.0.0.1:4173/?foundation-debug=1", { waitUntil: "networkidle" });
      await page.waitForFunction(() => window.__SSNOIR_FOUNDATION__?.getSchemeValue() === 1 &&
        window.__SSNOIR_FOUNDATION__?.getMoney() === 15, null, { timeout: 20000 });

      // In addition to the old platform probe, assert the actual unmodified
      // world.scm becomes navigable in each Web viewport.
      await page.waitForFunction(() => window.__SSNOIR_PREVIEW__?.getStatus().loaded === true &&
        window.__SSNOIR_PREVIEW__?.getStatus().places > 0, null, { timeout: 45000 });
      // Real authored portrait bytes are bundled, decoded through the actual
      // host Image contract, then uploaded into the same Pixi runtime.
      await page.waitForFunction(()=>
        window.__SSNOIR_PREVIEW__?.getStageAssets?.().loaded===4,null,{timeout:20000});
      const stageAssets=await page.evaluate(()=>window.__SSNOIR_PREVIEW__.getStageAssets());
      if(stageAssets.pending||stageAssets.errors.length)
        throw new Error("Original stage portrait decoding failed: "+JSON.stringify(stageAssets));
      if(scenario.name==="desktop"){
        // Save is an authored-world transaction, not the independent counter.
        await page.mouse.click(620,479);
        await page.waitForFunction(() =>
          Boolean(window.localStorage.getItem("ssnoir-phase2-preview-v1")),null,{timeout:20000});
        const saved=await page.evaluate(()=>JSON.parse(
          window.localStorage.getItem("ssnoir-phase2-preview-v1")
        ));
        if(saved.format!=="ssnoir-world"||saved.version!==1||!saved.worldDatum)
          throw new Error("real-world preview did not persist a versioned Scheme save");
        await page.mouse.click(745,479);
        await page.waitForFunction(()=>
          window.__SSNOIR_PREVIEW__?.getStatus().restores===1,null,{timeout:60000});
        const restored=await page.evaluate(()=>window.__SSNOIR_PREVIEW__.getStatus());
        if(restored.mode!=="world"||String(saved.state.globals["世界日"])!==restored.day)
          throw new Error("browser did not restore original authored world day");
      }
      const scale = scenario.height / 576;
      const x = 160 * scale;
      const y = (scenario.height - 576 * scale) / 2 + 483 * scale;
      if (scenario.touch) await page.touchscreen.tap(x, y);
      else await page.mouse.click(x, y);

      await page.waitForFunction(() => window.__SSNOIR_FOUNDATION__?.getSchemeValue() === 2 &&
        window.__SSNOIR_FOUNDATION__?.getMoney() === 16, null, { timeout: 10000 });
      // Exercise the existing session after a host resize and remap input.
      await page.setViewportSize({ width: 960, height: 540 });
      await page.waitForFunction(() => document.querySelector("canvas")?.height === 540);
      if (scenario.touch) await page.touchscreen.tap(150, 452.8125);
      else await page.mouse.click(150, 452.8125);
      await page.waitForFunction(() => window.__SSNOIR_FOUNDATION__?.getSchemeValue() === 3 &&
        window.__SSNOIR_FOUNDATION__?.getMoney() === 17);
      await page.setViewportSize({ width: scenario.width, height: scenario.height });
      await page.waitForFunction(height => document.querySelector("canvas")?.height === height, scenario.height);
      if (errors.length) throw new Error(errors.join("\n"));
      mkdirSync("artifacts", { recursive: true });
      await page.screenshot({ path: "artifacts/foundation-" + scenario.name + ".png", fullPage: true });
      console.log("PASS: " + scenario.name + " shared Three/Pixi + original engine.scm cash transaction + input");
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
  // Player-facing game screenshot and actual pointer-to-world-place latency
  // regression. The platform QA counter is intentionally absent here.
  const gameContext=await browser.newContext({viewport:{width:1024,height:576}});
  try{
    const page=await gameContext.newPage();
    const browserErrors=[];
    page.on("pageerror",error=>browserErrors.push(String(error)));
    page.on("console",msg=>{if(msg.type()==="error")browserErrors.push(msg.text());});
    await page.goto("http://127.0.0.1:4173/",{waitUntil:"networkidle"});
    await page.waitForFunction(()=>window.__SSNOIR_PREVIEW__?.getStatus().loaded===true,
      null,{timeout:60000});
    mkdirSync("artifacts",{recursive:true});
    await page.screenshot({path:"artifacts/ssnoir-game-city.png"});
    // In the original script this is Neil's home, first authored map row.
    await page.mouse.click(491,137);
    await page.waitForFunction(()=>{
      const status=window.__SSNOIR_PREVIEW__?.getStatus();
      return status&&status.navigationMs!==null&&!status.busy;
    },null,{timeout:60000});
    const nav=await page.evaluate(()=>window.__SSNOIR_PREVIEW__.getStatus());
    if(nav.materializations>2)
      throw new Error("World place entry rematerialized too often: "+JSON.stringify(nav));
    if(!nav.view)
      throw new Error("Entered city place but no scene is displayed: "+JSON.stringify(nav));
    await page.screenshot({path:"artifacts/ssnoir-game-entered-place.png"});
    if(browserErrors.length)throw new Error(browserErrors.join("\\n"));
    console.log("PASS: player world place navigation, "+nav.navigationMs+
      " ms; "+nav.materializations+" authored tree materializations");
  } finally {
    await gameContext.close();
  }
  // Golden browser capture through the SAME original Frank Scheme content,
  // LIPS native bridge, StagePlayback and shared Pixi renderer as Mini Games.
  const context=await browser.newContext({viewport:{width:1024,height:576}});
  try{
    const page=await context.newPage();
    const errors=[];
    page.on("pageerror",error=>errors.push(String(error)));
    page.on("console",msg=>{if(msg.type()==="error")errors.push(msg.text());});
    await page.goto("http://127.0.0.1:4173/?stage-preview=frank",{waitUntil:"networkidle"});
    await page.waitForFunction(()=>window.__SSNOIR_PREVIEW__?.getStatus()?.storyKind==="stage",null,
      {timeout:60000});
    await page.waitForFunction(()=>window.__SSNOIR_PREVIEW__?.getStageAssets()?.loaded===4,null,
      {timeout:20000});
    await page.waitForTimeout(250);
    if(errors.length)throw new Error(errors.join("\n"));
    await page.screenshot({path:"artifacts/original-frank-stage.png"});
    const state=await page.evaluate(()=>window.__SSNOIR_PREVIEW__.getStatus());
    if(state.stageBeat<1)throw new Error("original stage failed to enter its first spoken beat");
    console.log("PASS: actual authored Frank stage rendered with original portrait assets",state.stageBeat);
  } finally {
    await context.close();
  }
} finally {
  if (browser) await browser.close();
  await server.close();
}
