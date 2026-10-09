import { copyFileSync, existsSync, mkdirSync, statSync } from "node:fs";
import { join } from "node:path";

const output = "dist/wechat";
mkdirSync(output, { recursive: true });
const entry = join(output, "game.js");
if (!existsSync(entry)) throw new Error("Missing WeChat game.js");
const bytes = statSync(entry).size;
if (bytes > 4 * 1024 * 1024) {
  throw new Error("WeChat main package exceeds 4 MiB: " + bytes);
}
for (const name of ["game.json", "project.config.json"]) {
  copyFileSync(join("platforms/wechat", name), join(output, name));
}
console.log("WeChat shared Three/Pixi/Scheme bundle: " + bytes + " bytes. Device runtime test required.");
