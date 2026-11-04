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
let sequence = 0;

async function setup() {
  const state = new GameRuntimeState();
  const vm = await GameScriptSession.create("third-act-" + (++sequence), state, sources);
  await vm.loadFile("scenes/world/world.scm");
  await vm.evaluate("(第一幕 'load! (list (list \"opened\" #t) (list \"tavern\" #t) (list \"act\" 2) (list \"singer\" 2) (list \"rehearsal-seen\" #t)))");
  await vm.evaluate("(第二幕生活 'load! (list (list \"manager\" #t) (list \"manager-met-day\" 8)))");
  await vm.evaluate("(set! world-day 16)");
  state.setGlobal("世界日", 16);
  return { vm, state };
}
async function number(vm, expr) { return vm.evaluateNumber(expr); }
async function names(vm, loc) {
  const node = await vm.evaluate("(node \"测试地点\" :children (第三幕 'nodes-at \"" + loc + "\"))");
  return convertNode(node).children.map(child => child.name);
}


test("third act manager commission opens the real chase and prevents duplicate acceptance",async()=>{
  const {vm}=await setup();
  assert.equal(await number(vm,"(第一幕 'act)"),2);
  await vm.evaluate("(第三幕 'start!)");
  assert.equal(await number(vm,"(第一幕 'act)"),3);
  assert.equal(await number(vm,"(if (第三幕 'started?) 1 0)"),1);
  assert((await names(vm,"剧院")).includes("等待黑衣人"),
    "The first real case action is surveillance outside the theater");
  assert(!(await names(vm,"旧旅馆")).includes("问旅馆店员"),
    "The retired hotel branch must not be reintroduced as a dummy action");
  assert.equal(await number(vm,
    '(if (equal? (assoc-get (第三幕 \'save) "inquiry-stage" "") "守候") 1 0)'),1);
  await vm.evaluate("(get-render-data)");
  await assert.rejects(vm.evaluate("(第三幕 'start!)"),/重复|提前/);
});

test("third act rejects a retired scenario callback without fabricating Rocco settlement",async()=>{
  const {vm}=await setup();
  await vm.evaluate("(第三幕 'start!)");
  await assert.rejects(vm.evaluate("(第三幕 'scenario-result! '旅馆夜班 '见到联系人)"),
    /未知Scenario接口/,
    "Only the currently authored callback is permitted");
  assert.equal(await number(vm,
    '(if (equal? (assoc-get (第三幕 \'save) "case" "") "调查") 1 0)'),1,
    "An obsolete callback must not close a real investigation");
  assert((await names(vm,"剧院")).includes("等待黑衣人"));
  assert(!(await names(vm,"剧院")).includes("提交调查报告"));
  assert(!(await names(vm,"货运公司")).includes("潜入货运大楼"),
    "Players cannot infiltrate without the chase and the actual Rocco evidence");
});

test("third-act world calendar advances Lucy's independent writing without resurrecting hidden cast",async()=>{
  const {vm}=await setup();
  await vm.evaluate("(第三幕 'start!)");
  await vm.evaluate("(set! world-day 23)");
  await vm.evaluate("(第三幕 'on-day!)");
  assert.equal(await number(vm,'(if (assoc-get (第三幕 \'save) "lucy-mailed" #f) 1 0)'),1,
    "Lucy's own submission schedule continues when Neil is away");
  assert.equal(await number(vm,"(if (人物线开放? \"弗兰克\") 1 0)"),0);
  assert.equal(await number(vm,"(if (人物线开放? \"汤米\") 1 0)"),0);
  assert.equal(await number(vm,
    '(if (equal? (assoc-get (第三幕 \'save) "case" "") "调查") 1 0)'),1,
    "The calendar alone must not forge completion of the player's Rocco case");
  await vm.evaluate("(第三幕 'load! (第三幕 'save))");
  assert.equal(await number(vm,'(if (assoc-get (第三幕 \'save) "lucy-mailed" #f) 1 0)'),1,
    "Off-screen life changes remain part of the authored save");
});
