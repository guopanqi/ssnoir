import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import ts from "typescript";
const exports = {};
runInNewContext(ts.transpileModule(readFileSync("adapters/wechat/src/canvas-type.ts", "utf8"), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 }
}).outputText, { exports, WeakSet, Object });
test("registered host handles are Canvas; ordinary texture resources are not", () => {
  const registry = exports.createCanvasTypeRegistry();
  const global = { HTMLCanvasElement: Object };
  const screen = registry.register({});
  const offscreen = registry.register({});
  registry.install(global);
  assert.equal(screen instanceof global.HTMLCanvasElement, true);
  assert.equal(offscreen instanceof global.HTMLCanvasElement, true);
  assert.equal({} instanceof global.HTMLCanvasElement, false);
  assert.equal(new Uint8Array(4) instanceof global.HTMLCanvasElement, false);
  assert.equal(null instanceof global.HTMLCanvasElement, false);
});
test("native Canvas constructors are preserved", () => {
  class NativeCanvas {}
  const global = { HTMLCanvasElement: NativeCanvas };
  exports.createCanvasTypeRegistry().install(global);
  assert.equal(global.HTMLCanvasElement, NativeCanvas);
});
