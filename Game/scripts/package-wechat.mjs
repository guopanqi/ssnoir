import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { join } from "node:path";
const output = "dist/wechat";
mkdirSync(output, { recursive: true });
if (!existsSync(join(output, "game.js"))) throw new Error("Missing WeChat game.js");
for (const name of ["game.json", "project.config.json"]) {
  copyFileSync(join("platforms/wechat", name), join(output, name));
}
console.log("WeChat host PROBE written to dist/wechat; device validation still required.");
