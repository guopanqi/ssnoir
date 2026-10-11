import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { gunzipSync } from "node:zlib";
import { createHash } from "node:crypto";
import { runInNewContext } from "node:vm";

test("vendored user-supplied TapTap converter v2.0.5 unchanged", () => {
  const source = gunzipSync(readFileSync("vendor/taptap-converter/wx_converter.py.gz"));
  assert.equal(createHash("sha256").update(source).digest("hex"),
    "19020e1b26ce360d156da07676326885957a4b98a4624457e328518b97974354");
  assert.match(source.toString(), /CONVERTER_VERSION = "2\.0\.5"/);
});

test("TapTap startup reports synchronous foundation load failures", () => {
  const messages = [];
  const modals = [];
  runInNewContext(readFileSync("platforms/taptap/game.js", "utf8"), {
    console: { log() {}, error: (...args) => messages.push(args.join(" ")) },
    wx: { onError() {}, showModal: detail => modals.push(detail) },
    require: () => {
      const error = new SyntaxError("device parser rejected foundation");
      error.stack = "@tjapp://game-runtime/tjfs/index.js:29:639463";
      throw error;
    }
  });
  assert.equal(modals.length, 1);
  assert.match(modals[0].content, /device parser rejected foundation/);
  assert.match(modals[0].content, /^SyntaxError: device parser rejected foundation/);
  assert.match(messages[0], /SSNoir TapTap startup/);
});
