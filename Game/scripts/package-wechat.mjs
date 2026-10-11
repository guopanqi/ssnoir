import { copyFileSync, existsSync, mkdirSync, statSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const output = "dist/wechat";
mkdirSync(output, { recursive: true });
const entry = join(output, "game.js");
if (!existsSync(entry)) throw new Error("Missing WeChat game.js");
const bytes = statSync(entry).size;
if (bytes > 4 * 1024 * 1024) {
  throw new Error("WeChat main package exceeds 4 MiB: " + bytes);
}
copyFileSync("platforms/wechat/game.json", join(output, "game.json"));
const configPath = join(output, "project.config.json");
const config = JSON.parse(readFileSync("platforms/wechat/project.config.json", "utf8"));
if (existsSync(configPath)) {
  const local = JSON.parse(readFileSync(configPath, "utf8"));
  if (typeof local.appid !== "string" || !local.appid) throw new Error("Invalid local WeChat AppID");
  config.appid = local.appid;
}
writeFileSync(configPath, JSON.stringify(config, null, 2) + "\n");
console.log("WeChat shared Three/Pixi/Scheme bundle: " + bytes + " bytes. Device runtime test required.");
