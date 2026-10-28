import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {convertNode} from "../src/runtime/node-converter.ts";

const root="../UnityClient/Assets/Resources/Content",sources={};
function scan(dir){
  for(const item of readdirSync(join(root,dir),{withFileTypes:true})){
    const key=join(dir,item.name).replaceAll("\\","/");
    if(item.isDirectory())scan(key);
    else if(item.isFile()&&key.endsWith(".scm"))
      sources[key]=readFileSync(join(root,key),"utf8");
  }
}
scan("scripts");scan("scenes");
const standard=readFileSync("node_modules/lips/dist/std.xcb");
async function setup(){
  const state=new GameRuntimeState();
  const vm=await GameScriptSession.create("act-two-refactor",state,sources,standard);
  await vm.loadFile("scenes/world/world.scm");
  return {vm,state};
}
async function act(vm,day){
  await vm.evaluate(`(第一幕 'load! (list
    (list "opened" #t) (list "tavern" #t) (list "act" 2)
    (list "singer" 2) (list "rehearsal-seen" #t)))`);
  await vm.evaluate(`(set! world-day ${day})`);
}
async function count(vm,expr){return vm.evaluateNumber(`(length ${expr})`);}
async function scenario(vm,name){
  await vm.loadFile(`scenes/scenarios/${name}.scm`);
  await vm.evaluate("(on-scenario-enter)");
  return convertNode(await vm.evaluate("(get-render-data)"));
}
const labels=node=>node.children.map(x=>x.name);

test("public daily rhythm runs independently of whether Neil takes the shift",async()=>{
  const {vm}=await setup();
  await act(vm,7);
  assert.equal(await count(vm,`(第二幕节奏 'arrivals-at "老街酒馆")`),1);
  assert.equal(await count(vm,`(第二幕节奏 'nodes-at "码头")`),0);
  await act(vm,9);
  assert.equal(await count(vm,`(第二幕节奏 'arrivals-at "码头")`),1);
  assert.equal(await count(vm,`(第二幕节奏 'nodes-at "码头")`),1);
  await vm.evaluate(`(第二幕节奏 'load! (list
    (list "ship" #t) (list "ship-work" "已结算")))`);
  assert.equal(await count(vm,`(第二幕节奏 'nodes-at "码头")`),0);
  await act(vm,13);
  assert.equal(await count(vm,`(第二幕节奏 'arrivals-at "码头")`),1);
  assert.equal(await count(vm,`(第二幕节奏 'arrivals-at "剧院")`),1);
  await vm.evaluate(`(第二幕节奏 'load! (list (list "dock-mark" #t) (list "poster" #t)))`);
  assert.equal(await count(vm,`(第二幕节奏 'arrivals-at "码头")`),0);
});

test("cargo ship is a finite multi-round risk scenario, not a renamed wage job",async()=>{
  const {vm}=await setup();
  const n=await scenario(vm,"大船靠岸");
  assert.equal(n.name,"大船靠岸");
  assert.ok(labels(n).includes("齐心卸货"));
  assert.ok(labels(n).includes("抢卸高架货"));
  assert.ok(labels(n).includes("收紧吊索"));
  await vm.evaluate("(搬运! 2 0)");
  assert.equal(await vm.evaluateNumber("(if 意外? 1 0)"),1);
  assert.ok((await vm.evaluateNumber("(危险 'current)"))>=1);
});

test("Eddie risks his job when the witness backs away; only two permanent outcomes",async()=>{
  const {vm}=await setup();
  const n=await scenario(vm,"少了十块钱");
  assert.equal(n.name,"少了十块钱");
  assert.ok(labels(n).includes("核对工时簿"));
  assert.ok(!labels(n).includes("请弗兰克帮忙"));
  await vm.evaluate("(查证! 2 0)");
  assert.equal(await vm.evaluateNumber("(if 退缩? 1 0)"),1);
  const advanced=convertNode(await vm.evaluate("(get-render-data)"));
  assert.ok(labels(advanced).includes("查吊车班次"));
  assert.ok(labels(advanced).includes("请弗兰克帮忙"));
  assert.ok(!labels(advanced).includes("请工友对证"));
});

test("sponsor stage only contests the performance window, not who Nightingale becomes",async()=>{
  const {vm}=await setup();
  const n=await scenario(vm,"赞助人试演");
  assert.equal(n.name,"赞助人试演");
  assert.ok(labels(n).includes("协调演出时间"));
  assert.ok(labels(n).includes("抢留舞台空档"));
  assert.ok(!labels(n).includes("找旧乐手救场"));
  await vm.evaluate("(投入! 2 0)");
  assert.equal(await vm.evaluateNumber("(if 改期? 1 0)"),1);
});

test("costume contract mixes investigation dice with a decreasing-cost cash shortcut",async()=>{
  const {vm}=await setup();
  await act(vm,9);
  await vm.evaluate(`(第二幕生活 'load! (list
    (list "manager" #t) (list "contract" "已接下") (list "progress" 0)))`);
  let n=convertNode(await vm.evaluate(`(node "剧院生活" :children (第二幕生活 'nodes-at "剧院"))`));
  assert.ok(labels(n).includes("翻找旧库房"));
  assert.ok(labels(n).includes("请人代找"));

  assert.equal(await count(vm,`(第二幕生活 'nodes-at "剧院")`),4);
  await vm.evaluate(`(第二幕生活 'load! (list (list "manager" #t)
    (list "contract" "已接下") (list "progress" 2)))`);


});
 
test("wage is after the big ship and the late-night relationship is optional",async()=>{
  const {vm}=await setup();
  await act(vm,9);
  assert.equal(await count(vm,`(第二幕 'arrivals-at "码头")`),0);
  await act(vm,10);
  assert.equal(await count(vm,`(第二幕 'arrivals-at "码头")`),1);
  assert.equal(await count(vm,`(第二幕剧院 'nodes-at "剧院")`),1);
});
test("city still has activities without relationship gates",async()=>{
  const {vm}=await setup();
  await act(vm,7);
  assert.equal(await count(vm,`(第二幕生活 'nodes-at "码头居民区")`),2);
});
