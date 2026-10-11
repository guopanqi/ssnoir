import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { GameScriptSession } from "../src/runtime/script-session.ts";
import { GameRuntimeState } from "../src/runtime/game-state.ts";
import { convertNode, convertNodes, schemeList } from "../src/runtime/node-converter.ts";

const root = "../UnityClient/Assets/Resources/Content/";
const source = path => readFileSync(root + path, "utf8");
const scripts = {
  "scripts/stdlib.scm": source("scripts/stdlib.scm"),
  "scripts/engine.scm": source("scripts/engine.scm"),
  "scripts/theatre.scm": source("scripts/theatre.scm")
};
const binary = readFileSync("node_modules/lips/dist/std.xcb");
const create = () => GameScriptSession.create("node-parse", new GameRuntimeState(), scripts, binary);

test("convert original node DSL with nested places, action costs and instant effect closure", async () => {
  const vm = await create();
  const expression = await vm.evaluate(`(node "码头" :place #t :title "旧码头" :children
    (list (node "工作" :subtitle "报酬" :requires (list (req-die) (req-item "金钱" 2))
      :resolve (instant (outcome (lambda () (add-item! "金钱" 4)))))
      (node "说明" :resolve (note "说明" "清晨还很安静"))))`);
  const node = convertNode(expression);
  assert.equal(node.title, "旧码头");
  assert.equal(node.isPlace, true);
  assert.equal(node.children.length, 2);
  assert.deepEqual(node.children[0].requires, [
    { type: "die" }, { type: "item", itemId: "金钱", qty: 2 }
  ]);
  assert.equal(node.children[0].resolve.kind, "instant");
  assert.ok(node.children[0].resolve.effect);
  assert.equal(node.children[1].resolve.noteTitle, "说明");
});

test("converter rejects invalid authoring instead of silently hiding children/actions", async () => {
  const vm = await create();
  const wrong = await vm.evaluate(`(node "错误" :children (list (node "子")) :resolve (note "标题" "内容"))`);
  assert.throws(() => convertNode(wrong), /cannot have both/);
  const dup = await vm.evaluate(`(list (node "重复") (node "重复"))`);
  assert.throws(() => convertNodes(dup), /Duplicate node/);
});

test("Scheme proper list conversion rejects improper tails without recursion", async () => {
  const vm = await create();
  const bad = await vm.evaluate("'(one . two)");
  assert.throws(() => schemeList(bad), /proper Scheme list/);
});
