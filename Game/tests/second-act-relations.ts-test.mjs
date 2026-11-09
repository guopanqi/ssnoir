import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {convertNode} from "../src/runtime/node-converter.ts";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";

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
async function setup(){
  const state=new GameRuntimeState();
  const vm=await GameScriptSession.create("act-two-refactor",state,sources);
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


test("the public day-nine ship announces itself independently of whether Neil takes the night shift",async()=>{
  const {vm}=await setup();
  await act(vm,7);
  assert.equal(await count(vm,`(第二幕节奏 'arrivals-at "老街酒馆")`),1);
  await act(vm,9);
  // The city first announces recruitment and then actually docks the ship.
  assert.equal(await count(vm,`(第二幕节奏 'arrivals-at "码头")`),2);
  assert.equal(await count(vm,`(第二幕节奏 'nodes-at "码头")`),1);
  const entry=convertNode(await vm.evaluate(`(node "码头" :children (第二幕节奏 'nodes-at "码头"))`));
  assert(entry.children.some(x=>x.name==="参加大船夜班"),
    "The night shift must be a genuine optional city action");
  await vm.evaluate(`(第二幕节奏 'load! (list
    (list "ship" #t) (list "ship-news" #t) (list "ship-work" "已结算")))`);
  assert.equal(await count(vm,`(第二幕节奏 'nodes-at "码头")`),0,
    "Settled night work cannot be paid twice");
  await act(vm,15);
  assert.equal(await count(vm,`(第二幕节奏 'arrivals-at "码头")`),1);
  assert.equal(await count(vm,`(第二幕节奏 'arrivals-at "剧院")`),1);
  await vm.evaluate(`(第二幕节奏 'load! (list
    (list "dock-mark" #t) (list "poster" #t) (list "ship-work" "已结算")))`);
  assert.equal(await count(vm,`(第二幕节奏 'arrivals-at "码头")`),0,
    "The harbor survey announcement is one-shot");
});


test("the actually linked cargo night is a finite multi-stage joint-lift scenario",async()=>{
  const director=await GameSceneDirector.create(sources,()=>0.999);
  let room=await director.start("大船靠岸（联合起吊）");
  assert.equal(room.name,"联合起吊");
  assert.ok(labels(room).includes("搬运货箱"));
  assert.ok(labels(room).includes("协助挂索"));
  assert.ok(!labels(room).includes("前端加快推进"),
    "Crane controls must stay locked until normal cargo is unloaded");
  // Resolve two genuine 2-point die actions; phase changes through the real
  // ActionRunner, which owns focus-node! and the action's committed Clock.
  for(const slotId of [0,1]){
    const effect=await director.actions.performSlots("搬运货箱",[
      {type:"die",actorId:"player",slotId}
    ]);
    assert.equal(effect.mode,"roll");
    assert.equal(effect.outcome,"success");
  }
  room=await director.render();
  assert.ok(labels(room).includes("协助检查吊具"));
  assert.ok(labels(room).includes("整理落货区域"));
  assert.ok(labels(room).includes("布置辅助防摆"));
  assert.equal(await director.scenario.evaluateNumber("(时间 'current)"),6,
    "Ordinary cargo work must not consume the six-round heavy lift budget");
  assert.equal(await director.scenario.evaluateNumber("(if (eq? 阶段 '准备) 1 0)"),1);
  assert.equal(director.state.team.dice("player").length,2,
    "The scene must consume only the two genuinely used dice");
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



test("the authored sponsor performance leads to the two-stage black-coat investigation, not a retired stage scenario",async()=>{
  const {vm}=await setup();
  await act(vm,13);
  const invitation=convertNode(await vm.evaluate(`(node "剧院" :children (第二幕剧院 'nodes-at "剧院"))`));
  assert(labels(invitation).includes("赴试唱之约"),
    "The required day-thirteen performance must remain playable");

  const director=await GameSceneDirector.create(sources,()=>0.999);
  let room=await director.start("最后一排的观众");
  assert.equal(room.name,"球形剧场");
  assert.ok(labels(room).includes("公共走廊"));
  assert.ok(labels(room).includes("工作通道"));
  assert.equal(await director.scenario.evaluateNumber("(if (= 阶段 1) 1 0)"),1);
  const route=String(await director.scenario.evaluate("真路线"));
  const actionId=route==="公共"?"公共走廊搜寻":"工作通道搜寻";
  assert(["公共","后台"].includes(route),"Only the two original routes are legitimate");
  for(const slotId of [0,1]){
    const effect=await director.actions.performSlots(actionId,[
      {type:"die",actorId:"player",slotId}
    ]);
    assert.equal(effect.mode,"roll");
    assert.equal(effect.outcome,"success");
  }
  room=await director.render();
  assert.ok(labels(room).includes("打听离场去向"));
  assert.ok(labels(room).includes("打听后台消息"));
  assert.equal(await director.scenario.evaluateNumber("(收工 'current)"),3,
    "The three-round deadline begins only after choosing a real route");
  assert.equal(director.mode,"scenario");
});

test("the authored costume job combines nested dice investigation with decreasing item-slot cash cost",async()=>{
  const {vm}=await setup();
  await act(vm,9);
  await vm.evaluate(`(经理的演出服 'load! (list
    (list "stage" "已接下") (list "appeared" 9) (list "progress" 0)))`);
  const cards=async()=>{
    const root=convertNode(await vm.evaluate(
      `(node "剧院" :children (经理的演出服 'nodes-at "剧院"))`));
    const group=root.children.find(x=>x.name==="演出服-后台搜寻组");
    assert(group,"Costume actions must be provided by the current 城市事件 owner");
    const search=group.children.find(x=>x.name==="旧库房找衣箱");
    const hire=group.children.find(x=>x.name==="雇人代找衣箱");
    assert(search&&hire,"Both original investigation and paid shortcut must remain visible");
    assert.equal(search.requires[0]?.type,"die");
    assert.equal(hire.requires[0]?.type,"item");
    assert.equal(hire.requires[0]?.itemId,"金钱");
    return {group,hire};
  };
  let current=await cards();
  assert.equal(current.hire.requires[0].qty,25);
  await vm.evaluate(`(经理的演出服 'load! (list
    (list "stage" "已接下") (list "appeared" 9) (list "progress" 2)))`);
  current=await cards();
  assert.equal(current.hire.requires[0].qty,15,
    "Progress must reduce the actual cost of the item-slot shortcut");
  assert.equal(await count(vm,`(经理的演出服 'nodes-at "码头")`),1,
    "The freight clerk must remain an independent alternative clue source");
});

test("the day-nine wage shift is optional and Nightingale's later dates are separate player opportunities",async()=>{
  const {vm}=await setup();
  await act(vm,9);
  assert.equal(await count(vm,`(第二幕节奏 'nodes-at "码头")`),1);
  await act(vm,10);
  assert.equal(await count(vm,`(第二幕节奏 'nodes-at "码头")`),0,
    "An unworked night shift cannot be collected on the following day");
  let theater=convertNode(await vm.evaluate(`(node "剧院" :children (第二幕剧院 'nodes-at "剧院"))`));
  assert(labels(theater).includes("和夜莺吃夜宵"),
    "The relationship meeting belongs to its own day-ten to twelve window");
  await vm.evaluate(`(第二幕剧院 'load! (list (list "private" #t)))`);
  theater=convertNode(await vm.evaluate(`(node "剧院" :children (第二幕剧院 'nodes-at "剧院"))`));
  assert(!labels(theater).includes("和夜莺吃夜宵"),"The private meeting is once only");
  await act(vm,13);
  theater=convertNode(await vm.evaluate(`(node "剧院" :children (第二幕剧院 'nodes-at "剧院"))`));
  assert(labels(theater).includes("赴试唱之约"),
    "The day-thirteen required performance must remain independently available");
});
test("city still has activities without relationship gates",async()=>{
  const {vm}=await setup();
  await act(vm,7);
  assert.equal(await count(vm,`(第二幕生活 'nodes-at "码头居民区")`),2);
});
