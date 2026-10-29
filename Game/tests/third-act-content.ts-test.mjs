import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { GameScriptSession } from "../src/runtime/script-session.ts";
import { GameRuntimeState } from "../src/runtime/game-state.ts";
import { convertNode } from "../src/runtime/node-converter.ts";

const root = "../UnityClient/Assets/Resources/Content";
const sources = {};
function scan(dir) {
  for (const item of readdirSync(join(root, dir), { withFileTypes: true })) {
    const key = join(dir, item.name).replaceAll("\\", "/");
    if (item.isDirectory()) scan(key);
    else if (item.isFile() && key.endsWith(".scm"))
      sources[key] = readFileSync(join(root, key), "utf8");
  }
}
scan("scripts"); scan("scenes");
const std = readFileSync("node_modules/lips/dist/std.xcb");
let sequence = 0;

async function setup() {
  const state = new GameRuntimeState();
  const vm = await GameScriptSession.create("third-act-" + (++sequence), state, sources, std);
  await vm.loadFile("scenes/world/world.scm");
  await vm.evaluate("(第一幕 'load! (list (list \"opened\" #t) (list \"tavern\" #t) (list \"act\" 2) (list \"singer\" 2) (list \"rehearsal-seen\" #t)))");
  await vm.evaluate("(第二幕生活 'load! (list (list \"manager\" #t)))");
  await vm.evaluate("(set! world-day 16)");
  state.setGlobal("世界日", 16);
  return { vm, state };
}
async function number(vm, expr) { return vm.evaluateNumber(expr); }
async function names(vm, loc) {
  const node = await vm.evaluate("(node \"测试地点\" :children (第三幕 'nodes-at \"" + loc + "\"))");
  return convertNode(node).children.map(child => child.name);
}

test("third act starts from manager commission and exposes city investigation", async () => {
  const { vm } = await setup();
  assert.equal(await number(vm, "(第一幕 'act)"), 2);
  await vm.evaluate("(第三幕 'start!)");
  assert.equal(await number(vm, "(第一幕 'act)"), 3);
  assert.equal(await number(vm, "(if (第三幕 'started?) 1 0)"), 1);
  assert.ok((await names(vm, "旧旅馆")).includes("问旅馆店员"));
  assert.ok((await names(vm, "剧院")).includes("翻看旧合同"));
  assert.ok((await names(vm, "老街酒馆")).includes("核对旧演出账"));
  await vm.evaluate("(get-render-data)");
  await assert.rejects(vm.evaluate("(第三幕 'start!)"));
});

test("third act scenario results settle Rocco case and retain evidence", async () => {
  const { vm } = await setup();
  await vm.evaluate("(第三幕 'start!)");
  await vm.evaluate("(第三幕 'scenario-result! '旅馆夜班 '见到联系人)");
  assert.equal(await number(vm, "(if (assoc-get (第三幕 'save) \"watch\" #f) 1 0)"), 1);
  await vm.evaluate("(第三幕 'scenario-result! '货栈之夜 '完成结案)");
  assert.ok((await names(vm, "剧院")).includes("提交调查报告"));
  assert.ok(!(await names(vm, "码头")).includes("货栈交锋"));
  assert.equal(await number(vm, "(if (equal? (assoc-get (第三幕 'save) \"case\" \"\") \"结案\") 1 0)"), 1);
});

test("third act supporting cast progresses even without Neil participating", async () => {
  const { vm } = await setup();
  await vm.evaluate("(第三幕 'start!)");
  await vm.evaluate("(set! world-day 23)");
  await vm.evaluate("(第三幕 'on-day!)");
  assert.ok((await names(vm, "码头")).includes("艾迪的新工作靴"));
  assert.ok((await names(vm, "街角咖啡馆")).includes("露西寄出的信"));
  assert.ok((await names(vm, "码头居民区")).includes("黑鸥号没有签"));
  assert.ok((await names(vm, "夜总会")).includes("汤米坐进包厢"));
  await vm.evaluate("(第三幕 'load! (第三幕 'save))");
  assert.ok((await names(vm, "街角咖啡馆")).includes("露西寄出的信"));
});
