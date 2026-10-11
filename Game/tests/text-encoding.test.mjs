import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import ts from "typescript";

const source = readFileSync("adapters/wechat/vendor/fast-text-encoding/text.min.js", "utf8");
const boundary = ts.transpileModule(readFileSync("adapters/wechat/src/text-encoding.ts", "utf8"), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 }
}).outputText;
function install(host) {
  const hadNativeTextEncoder = typeof host.TextEncoder === "function";
  runInNewContext(source, { window: host, Uint8Array, ArrayBuffer });
  runInNewContext(boundary, {
    globalThis: host, exports: {}, require: name => name === "./global-scope" ? { hadNativeTextEncoder } : {}
  });
}
test("Mini Game UTF-8 polyfill matches native encoding and decoding", () => {
  const host = {};
  install(host);
  for (const text of ["", "LIPS  1", "黑色电影", "A𠮷B", "\ud800X\udc00"]) {
    const expected = new TextEncoder().encode(text);
    const actual = new host.TextEncoder().encode(text);
    assert.deepEqual(Array.from(actual), Array.from(expected));
    assert.equal(new host.TextDecoder().decode(actual), new TextDecoder().decode(expected));
  }
});
test("UTF-8 polyfill preserves native host codecs", () => {
  const host = { TextEncoder, TextDecoder };
  install(host);
  assert.equal(host.TextEncoder, TextEncoder);
  assert.equal(host.TextDecoder, TextDecoder);
});
