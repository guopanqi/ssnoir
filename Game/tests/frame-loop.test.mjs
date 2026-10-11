import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";

const source = ts.transpileModule(readFileSync("src/foundation/frame-loop.ts", "utf8"), {
  compilerOptions: { target: ts.ScriptTarget.ES2020, module: ts.ModuleKind.ES2020 }
}).outputText;
const { createFrameLoop } = await import("data:text/javascript;base64," + Buffer.from(source).toString("base64"));

test("frame loop pauses, resumes once and ignores canceled callbacks after restart", () => {
  let frames = 0;
  let id = 0;
  const pending = new Map();
  const loop = createFrameLoop(() => frames++, callback => {
    pending.set(++id, callback); return id;
  }, handle => pending.delete(handle));
  loop.resume(); loop.resume();
  assert.equal(frames, 1);
  assert.equal(pending.size, 1);
  const staleCallback = pending.values().next().value;
  loop.pause(); loop.pause();
  assert.equal(pending.size, 0);
  loop.resume();
  staleCallback();
  assert.equal(frames, 2);
  assert.equal(pending.size, 1);
  loop.dispose(); loop.resume(); staleCallback();
  assert.equal(frames, 2);
  assert.equal(pending.size, 0);
});

test("render failure propagates and stops scheduling", () => {
  let requests = 0;
  const failure = new Error("shader failed");
  const loop = createFrameLoop(() => { throw failure; }, () => ++requests, () => {});
  assert.throws(() => loop.resume(), error => error === failure);
  assert.equal(requests, 0);
});
