import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import { copyFileSync, mkdirSync, readFileSync, rmSync, writeFileSync, existsSync, realpathSync, symlinkSync } from "node:fs";
import { resolve, join, dirname, delimiter } from "node:path";
import { fileURLToPath } from "node:url";
import { gunzipSync } from "node:zlib";
import { createRequire } from "node:module";

/** Convert any prepared WeChat game directory; no SSNoir content or IDs involved. */
export function convertTapTap({ source, target, binDir = resolve("node_modules/.bin") }) {
  source = resolve(source);
  target = resolve(target);
  if (source === target || source.startsWith(target + "/") || target.startsWith(source + "/"))
    throw new Error("Converter source and target must be separate directories");
  for (const name of ["game.js", "game.json", "project.config.json"])
    readFileSync(join(source, name));
  const config = JSON.parse(readFileSync(join(source, "project.config.json"), "utf8"));
  if (!/^tap[a-z0-9]+$/.test(config.appid ?? ""))
    throw new Error("Prepared converter input must have a TapTap AppID");
  const vendor = join(dirname(fileURLToPath(import.meta.url)), "vendor/converter");
  const code = gunzipSync(readFileSync(join(vendor, "wx_converter.py.gz")));
  const sha = createHash("sha256").update(code).digest("hex");
  if (sha !== "19020e1b26ce360d156da07676326885957a4b98a4624457e328518b97974354")
    throw new Error("TapTap converter source hash mismatch: " + sha);
  const tools = join(dirname(target), "." + target.split(/[\\/]/).pop() + "-converter-tool");
  mkdirSync(join(tools, "wx_unity_converter"), { recursive: true });
  // Upstream calls npx babel. Make npx resolve the pinned local toolchain,
  // including when the conversion output sits outside the caller's project.
  const modules = resolve(binDir, "..");
  const toolModules = join(tools, "node_modules");
  if (!existsSync(toolModules)) symlinkSync(modules, toolModules, process.platform === "win32" ? "junction" : "dir");
  if (realpathSync(toolModules) !== realpathSync(modules)) throw new Error("Converter toolchain directory mismatch");
  writeFileSync(join(tools, "wx_converter.py"), code);
  for (const name of [".babelrc", "wx_unity_converter/wx_unity.js", "wx_unity_converter/check-version.js"])
    copyFileSync(join(vendor, name), join(tools, name));
  // Resolve the same preset from this tool's installation, even for /tmp outputs.
  const babelConfig = JSON.parse(readFileSync(join(vendor, ".babelrc"), "utf8"));
  const require = createRequire(import.meta.url);
  babelConfig.presets = babelConfig.presets.map(name => require.resolve(name));
  writeFileSync(join(tools, ".babelrc"), JSON.stringify(babelConfig, null, 2));
  rmSync(target, { recursive: true, force: true });
  const run = spawnSync("python3", [join(tools, "wx_converter.py"), "-s", source, "-t", target, "-y"], {
    cwd: tools,
    env: { ...process.env, npm_config_offline: "true", PATH: resolve(binDir) + delimiter + (process.env.PATH || "") },
    stdio: "inherit", timeout: 120_000
  });
  if (run.error) throw run.error;
  if (run.status !== 0) throw new Error("TapTap converter failed: " + run.status);
  return join(target, "game.zip");
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const value = flag => process.argv[process.argv.indexOf(flag) + 1];
  if (!process.argv.includes("--source") || !process.argv.includes("--target"))
    throw new Error("Usage: node convert.mjs --source <prepared-wechat-dir> --target <output-dir>");
  console.log(convertTapTap({ source: value("--source"), target: value("--target") }));
}
