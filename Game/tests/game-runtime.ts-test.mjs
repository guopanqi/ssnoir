import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { GameRuntimeState } from "../src/runtime/game-state.ts";
import { GameScriptSession } from "../src/runtime/script-session.ts";

const file = filename => readFileSync("../UnityClient/Assets/Resources/Content/" + filename, "utf8");
const sources = {
  "scripts/stdlib.scm": file("scripts/stdlib.scm"),
  "scripts/engine.scm": file("scripts/engine.scm"),
  "scripts/theatre.scm": file("scripts/theatre.scm")
};
const standardLibrary = readFileSync("node_modules/lips/dist/std.scm", "utf8");
const create = (name, state = new GameRuntimeState()) =>
  GameScriptSession.create(name, state, sources, standardLibrary);

test("stage 2 boots exact unedited engine.scm, its theatre library and stdlib.scm", async () => {
  const session = await create("phase2-boot");
  assert.equal(await session.evaluateNumber("(item-count \"金钱\")"), 15);
  assert.equal(await session.evaluateNumber("(item-count \"香烟\")"), 2);
  assert.equal(String(await session.evaluate("(get-global 'chapter)")), "0");
  let theatre;
  try { theatre = await session.evaluateNumber('(length (theatre-scene 1600 900 "black" (list)))'); }
  catch (e) { throw new Error("theatre-scene failed: " + e, { cause: e }); }
  assert.equal(theatre, 4);
  let node;
  try { node = await session.evaluate('(cadr (node "码头"))'); }
  catch (e) { throw new Error("node constructor failed: " + e, { cause: e }); }
  assert.equal(String(node), "码头");
});

test("stage 2 runs actual item helper contracts, including smoking cap, transaction rollback", async () => {
  const state = new GameRuntimeState();
  const session = await create("phase2-inventory", state);
  await session.evaluate('(add-item! "金钱" 12)');
  assert.equal(state.getItemCount("金钱"), 27);
  await session.evaluate('(add-item! "香烟" 8)');
  assert.equal(state.getItemCount("香烟"), 5);
  assert.equal(state.snapshot().notifications.length, 1);
  await session.evaluate('(remove-item! "金钱" 7)');
  assert.equal(state.getItemCount("金钱"), 20);
  await assert.rejects(session.evaluate('(begin (add-item! "金钱" 3) (remove-item! "香烟" 50))'));
  assert.equal(state.getItemCount("金钱"), 20);
  await assert.rejects(session.evaluate('(__set-item-count! "金钱" -5)'));
  assert.equal(state.getItemCount("金钱"), 20);
});

test("world/encounter VMs keep Scheme bindings isolated while sharing typed native state", async () => {
  const state = new GameRuntimeState();
  const world = await create("phase2-world", state);
  const encounter = await create("phase2-encounter", state);
  await world.evaluate('(define stage-local 17)');
  await encounter.evaluate('(define stage-local 2)');
  assert.equal(await world.evaluateNumber('stage-local'), 17);
  assert.equal(await encounter.evaluateNumber('stage-local'), 2);
  await world.evaluate('(begin (set-global! \'线索已看  #t) (add-item! "情报" 1))');
  assert.equal(await encounter.evaluate("(get-global '线索已看)"), true);
  assert.equal(await encounter.evaluateNumber('(item-count "情报")'), 1);
});

test("round/rest blocking helpers preserve ordering and save without Scheme closures", async () => {
  const state = new GameRuntimeState();
  const vm = await create("phase2-blockers", state);
  await vm.evaluate('(rest-block! "first" "先处理来信" "码头" "投信")');
  assert.equal(state.hasRestBlockers(), true);
  assert.equal(state.snapshot().restBlockers[0].location, "码头");
  await vm.evaluate('(rest-release! "first")');
  assert.equal(state.hasRestBlockers(), false);
  const snapshot = state.snapshot();
  const restored = new GameRuntimeState();
  restored.restore(JSON.parse(JSON.stringify(snapshot)));
  assert.deepEqual(restored.snapshot(), snapshot);
  assert.throws(() => restored.setItemCount("金钱", -1));
  assert.throws(() => restored.restore({ ...snapshot, inventory: { "金钱": -1 } }));
  assert.deepEqual(restored.snapshot(), snapshot);
});

test("script loader rejects path traversal and missing content instead of reading disk", async () => {
  const vm = await create("phase2-loader");
  await assert.rejects(vm.loadFile("../outside.scm"), /Invalid content script path/);
  await assert.rejects(vm.loadFile("scripts/unavailable.scm"), /not bundled/);
  await assert.rejects(vm.evaluate("(unimplemented-game-native 3)"));
});
