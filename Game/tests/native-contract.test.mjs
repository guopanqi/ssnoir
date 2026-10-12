import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

/**
 * Prevent new Unity/Schemy native functions from silently breaking
 * the TypeScript/LIPS platform during content changes.
 * This checks *registered names*, not semantic correctness (covered by
 * runtime and authored story tests).
 */
test("every original C# game-native registration has a TS bridge implementation",()=>{
  const csharp=readFileSync("../Engine/Runtime/Scripting/NativeFunctions.cs","utf8");
  const typescript=readFileSync("src/runtime/native-bridge.ts","utf8");
  const fromCsharp=new Set([...csharp.matchAll(/Symbol\\.FromString\\("([^"]+)"\\)/g)]
    .map(match=>match[1]).filter(name=>name.startsWith("__")));
  const fromTypeScript=new Set([...typescript.matchAll(/^\\s*"([^"]+)":/gm)].map(match=>match[1]));
  const missing=[...fromCsharp].filter(name=>!fromTypeScript.has(name));
  assert.deepEqual(missing,[],"a legacy C# native function is absent from the browser/mini-game bridge");
  assert.ok(fromCsharp.has("__apply-drunk!")&&fromCsharp.has("__play-intertitle!")&&
    fromCsharp.has("__play-camera!"),"the authored story natives must be monitored");
});
