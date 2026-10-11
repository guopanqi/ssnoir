import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { base64Bytes } from "../src/runtime/base64.ts";

test("LIPS compiled-stdlib binary survives Mini Game-compatible base64 decoding", () => {
  const binary = readFileSync("node_modules/lips/dist/std.xcb");
  assert.equal(binary.subarray(0, 4).toString(), "LIPS");
  assert.deepEqual(Buffer.from(base64Bytes(binary.toString("base64"))), binary);
});
test("base64 decoder rejects corrupt input", () => {
  assert.throws(() => base64Bytes("@==!"));
  assert.throws(() => base64Bytes("A"));
});
