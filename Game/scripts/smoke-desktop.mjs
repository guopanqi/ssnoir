import { spawn } from "node:child_process";
import { resolve } from "node:path";

const exe = resolve("node_modules/.bin/electron");
const child = spawn(exe, ["."], {
  env: { ...process.env, SSNOIR_DESKTOP_SMOKE: "1" },
  stdio: "inherit"
});
const deadline = setTimeout(() => {
  child.kill("SIGTERM");
  console.error("Electron smoke timed out after 40 seconds.");
  process.exitCode = 1;
}, 40000);
child.once("error", error => {
  clearTimeout(deadline);
  console.error(error);
  process.exitCode = 1;
});
child.once("exit", (code, signal) => {
  clearTimeout(deadline);
  process.exitCode = code === 0 && !signal ? 0 : 1;
});
