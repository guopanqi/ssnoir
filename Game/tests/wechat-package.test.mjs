import test from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, copyFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { execFileSync } from "node:child_process";

test("WeChat packaging preserves local AppID and private DevTools settings", () => {
  const root = mkdtempSync(join(tmpdir(), "ssnoir-wx-package-"));
  try {
    mkdirSync(join(root, "platforms/wechat"), { recursive: true });
    mkdirSync(join(root, "dist/wechat"), { recursive: true });
    for (const name of ["game.json", "project.config.json"]) {
      copyFileSync("platforms/wechat/" + name, join(root, "platforms/wechat", name));
    }
    writeFileSync(join(root, "dist/wechat/game.js"), "void 0;");
    writeFileSync(join(root, "dist/wechat/project.config.json"), JSON.stringify({ appid: "wx-local-test", compileType: "miniprogram" }));
    const privateConfig = '{"libVersion":"3.17.3"}\n';
    writeFileSync(join(root, "dist/wechat/project.private.config.json"), privateConfig);
    execFileSync(process.execPath, [resolve("scripts/package-wechat.mjs")], { cwd: root });
    const config = JSON.parse(readFileSync(join(root, "dist/wechat/project.config.json"), "utf8"));
    assert.equal(config.appid, "wx-local-test");
    assert.equal(config.compileType, "game");
    assert.equal(readFileSync(join(root, "dist/wechat/project.private.config.json"), "utf8"), privateConfig);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
