import { spawnSync } from "node:child_process";
import { copyFileSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { resolve, join } from "node:path";
import { createTapTapEntry } from "../adapters/taptap/startup.mjs";
import { convertTapTap } from "../adapters/taptap/convert.mjs";

const probe = process.argv.includes("--host-probe");
const startupProbe = process.argv.includes("--startup-probe");
if (probe && startupProbe) throw new Error("Choose one probe mode");
const root = resolve(probe ? "dist/taptap-host-probe" : startupProbe ? "dist/taptap-startup-probe" : "dist/taptap");
const wechat = resolve("dist/wechat");
const source = resolve(probe ? "dist/.taptap-probe-input" : startupProbe ? "dist/.taptap-startup-input" : "dist/.taptap-converter-input");
if (!probe && !readFileSync(join(wechat,"game.js"),"utf8").includes("__SSNOIR_WECHAT_FOUNDATION__"))
  throw new Error("Expected packaged SSNoir WeChat IIFE as converter input");
// The imported DevTools directory contains personal AppID/private settings.
// Build a clean converter input from the runtime and shared source manifests.
rmSync(source, { recursive: true, force: true });
mkdirSync(source, { recursive: true });
if (!probe) copyFileSync(join(wechat, "game.js"), join(source, "foundation.js"));
if (probe) copyFileSync(resolve("platforms/taptap/host-probe.js"), join(source, "game.js"));
else writeFileSync(join(source, "game.js"), createTapTapEntry("./foundation.js", {
  title: "SSNoir 启动失败", tag: "SSNoir TapTap startup"
}));
if (startupProbe) {
  const loader = readFileSync(join(source, "game.js"), "utf8");
  // Delay dependency loading until the device proves it executed this entry.
  writeFileSync(join(source, "game.js"),
    "wx.showModal({title:'SSNoir 启动探针 v2',content:'入口已执行。点确定后加载完整技术底座。',showCancel:false,success:function(){\n" +
    loader + "\n}});\n");
}
copyFileSync(resolve("platforms/wechat/game.json"), join(source, "game.json"));
// As in the working Laya release, supply the TapTap AppID before conversion.
// The vendor converter preserves project.config.json unchanged.
const tapConfig = JSON.parse(readFileSync(resolve("platforms/taptap/project.config.json"), "utf8"));
if (!/^tap[a-z0-9]+$/.test(tapConfig.appid ?? ""))
  throw new Error("TapTap packaging requires a valid TapTap AppID");
writeFileSync(join(source, "project.config.json"), JSON.stringify(tapConfig, null, 2) + "\n");
// The VS Code debugger reads game.json.appId, not project.config.json.appid.
const gameConfig = JSON.parse(readFileSync(join(source, "game.json"), "utf8"));
gameConfig.appId = tapConfig.appid;
writeFileSync(join(source, "game.json"), JSON.stringify(gameConfig, null, 2) + "\n");
convertTapTap({ source, target: root });

const validation = spawnSync("python3", ["scripts/validate-taptap.py", root, ...(probe ? ["--host-probe"] : [])],
  { cwd: resolve("."), stdio: "inherit" });
if (validation.error) throw validation.error;
if (validation.status !== 0) throw new Error("TapTap package validation failed");

const syntax = spawnSync(process.execPath, ["--check", join(root,"game","game.js")],
  { stdio: "inherit" });
if (syntax.error) throw syntax.error;
if (syntax.status !== 0) throw new Error("TapTap converted game.js is not parseable");
console.log("TapTap 2.0.5 converter integration passed. Runtime device acceptance still required.");
