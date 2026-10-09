import { spawnSync } from "node:child_process";
const result = spawnSync(process.execPath, ["scripts/smoke-wechat-harness.mjs"], {
  env: { ...process.env, SSNOIR_MINIGAME_TARGET: "taptap" },
  stdio: "inherit"
});
if (result.error) throw result.error;
if (result.status !== 0) process.exit(result.status || 1);
