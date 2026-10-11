import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import { copyFileSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { resolve, join, delimiter } from "node:path";
import { gunzipSync } from "node:zlib";

/**
 * Runs exactly the submitted TapTap wx_converter.py v2.0.5.
 * The vendor's original Python source is stored losslessly gzip-compressed
 * (and hash-verified) to avoid committing the supplied node_modules.
 */
const vendor = resolve("vendor/taptap-converter");
const probe = process.argv.includes("--host-probe");
const startupProbe = process.argv.includes("--startup-probe");
if (probe && startupProbe) throw new Error("Choose one probe mode");
const root = resolve(probe ? "dist/taptap-host-probe" : startupProbe ? "dist/taptap-startup-probe" : "dist/taptap");
const tools = resolve("dist/.taptap-converter-tool");
const wechat = resolve("dist/wechat");
const source = resolve(probe ? "dist/.taptap-probe-input" : startupProbe ? "dist/.taptap-startup-input" : "dist/.taptap-converter-input");
const archive = join(vendor, "wx_converter.py.gz");
const expected = "19020e1b26ce360d156da07676326885957a4b98a4624457e328518b97974354";
const code = gunzipSync(readFileSync(archive));
const sha = createHash("sha256").update(code).digest("hex");
if (sha !== expected) throw new Error("TapTap converter source hash mismatch: " + sha);
mkdirSync(tools, { recursive: true });
mkdirSync(join(tools, "wx_unity_converter"), { recursive: true });
writeFileSync(join(tools, "wx_converter.py"), code);
for (const filename of [".babelrc","wx_unity_converter/wx_unity.js","wx_unity_converter/check-version.js"]) {
  copyFileSync(join(vendor, filename), join(tools, filename));
}
if (!probe && !readFileSync(join(wechat,"game.js"),"utf8").includes("__SSNOIR_WECHAT_FOUNDATION__"))
  throw new Error("Expected packaged SSNoir WeChat IIFE as converter input");
// The imported DevTools directory contains personal AppID/private settings.
// Build a clean converter input from the runtime and shared source manifests.
rmSync(source, { recursive: true, force: true });
mkdirSync(source, { recursive: true });
if (!probe) copyFileSync(join(wechat, "game.js"), join(source, "foundation.js"));
copyFileSync(resolve(probe ? "platforms/taptap/host-probe.js" : "platforms/taptap/game.js"), join(source, "game.js"));
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
rmSync(root, { recursive: true, force: true });
const bins = resolve("node_modules/.bin");
const env = { ...process.env, PATH: bins + delimiter + (process.env.PATH || "") };
const run = spawnSync("python3", [join(tools,"wx_converter.py"),
  "-s", source, "-t", root, "-y"], { cwd: tools, env, stdio: "inherit", timeout: 120_000 });
if (run.error) throw run.error;
if (run.status !== 0) throw new Error("TapTap vendor converter failed with exit " + run.status);

const validation = spawnSync("python3", ["scripts/validate-taptap.py", root, ...(probe ? ["--host-probe"] : [])],
  { cwd: resolve("."), stdio: "inherit" });
if (validation.error) throw validation.error;
if (validation.status !== 0) throw new Error("TapTap package validation failed");

const syntax = spawnSync(process.execPath, ["--check", join(root,"game","game.js")],
  { stdio: "inherit" });
if (syntax.error) throw syntax.error;
if (syntax.status !== 0) throw new Error("TapTap converted game.js is not parseable");
console.log("TapTap 2.0.5 converter integration passed. Runtime device acceptance still required.");
