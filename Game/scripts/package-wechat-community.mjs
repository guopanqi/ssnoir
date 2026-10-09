import { copyFileSync, mkdirSync, statSync } from "node:fs";
import { join } from "node:path";
const output = "dist/wechat-community";
mkdirSync(output, { recursive: true });
if (statSync(join(output, "game.js")).size > 4 * 1024 * 1024) throw Error("Candidate WeChat main package exceeds 4MiB");
for (const name of ["game.json","project.config.json"])
  copyFileSync(join("platforms/wechat", name), join(output, name));
console.log("Candidate community-adapter bundle packaged (test-only)");
