import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";

const folder="../UnityClient/Assets/Resources/Content",sources={};
function scan(dir){
  for(const entry of readdirSync(join(folder,dir),{withFileTypes:true})){
    const name=join(dir,entry.name).replaceAll("\\","/");
    if(entry.isDirectory())scan(name);
    else if(name.endsWith(".scm"))sources[name]=readFileSync(join(folder,name),"utf8");
  }
}
scan("scripts");scan("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");
const build=()=>GameSceneDirector.create(sources,std,()=>0.45);

test("real blackmail scenario exposes original carry item, not inside the authored scene tree",async()=>{
  const director=await build();
  const root=await director.start("勒索信");
  assert.ok(root.children.length>0);
  const carry=director.actions.getCarryNodes();
  assert.ok(carry.some(n=>n.name==="抽烟"&&n.carryItemId==="香烟"));
  assert.ok(!root.children.some(n=>n.name==="抽烟"),"carry card must be outside scenario tree");
  const before=director.state.getItemCount("香烟");
  assert.deepEqual(carry.find(n=>n.name==="抽烟")?.requires.map(x=>x.type),["item","die"],
    "original carry action spends one consumable and one die");
  const action=await director.actions.performSlots("抽烟",[
    {type:"item",itemId:"香烟",qty:1},{type:"die",actorId:"player",slotId:0}
  ]);
  assert.equal(action.mode,"instant");
  assert.equal(director.state.getItemCount("香烟"),before-1);
  assert.equal(director.state.team.dice("player").length,3,"original carry action spends a die");
  await director.actions.prepare();
  assert.ok(director.actions.getCarryNodes().some(n=>n.name==="抽烟"));
  await director.actions.performSlots("抽烟",[
    {type:"item",itemId:"香烟",qty:1},{type:"die",actorId:"player",slotId:1}
  ]);
  assert.equal(director.state.getItemCount("香烟"),0);
  await director.actions.prepare();
  assert.ok(!director.actions.getCarryNodes().some(n=>n.name==="抽烟"),
    "cigarette card disappears after the last one is consumed");
});

test("real Frank support summons helper exactly once per scenario and resets in a new one",async()=>{
  const director=await build();
  director.state.team.grantSupport("弗兰克");
  await director.start("勒索信");
  const card=director.actions.getCarryNodes().find(n=>n.name==="叫个人来");
  assert.ok(card,"authored carried Frank support card is shown");
  assert.equal(card.supportId,"弗兰克");
  assert.equal(card.disabled,false);
  assert.equal(director.supportUsed,false);
  const result=await director.actions.performSlots("叫个人来",[]);
  assert.equal(result.mode,"instant");
  assert.equal(director.supportUsed,true,"native engine owns once-per-scenario cost");
  assert.equal(director.state.team.require("老街帮手").temporary,true);
  assert.equal(director.state.team.dice("老街帮手").length,1);
  await director.actions.prepare();
  assert.equal(director.actions.getCarryNodes().find(n=>n.name==="叫个人来")?.disabled,true);
  await assert.rejects(director.actions.performSlots("叫个人来",[]),/unavailable|support already used/);
  director.requestEnd(false);
  await director.flush();
  assert.equal(director.supportUsed,false);
  assert.equal(director.state.team.find("老街帮手"),undefined);
  await director.start("勒索信");
  assert.equal(director.actions.getCarryNodes().find(n=>n.name==="叫个人来")?.disabled,false);
});

test("support for a different carried relationship cannot be executed through a stale card",async()=>{
  const director=await build();
  director.state.team.grantSupport("弗兰克");
  await director.start("勒索信");
  director.state.team.setCarriedSupport("");
  await assert.rejects(director.actions.performSlots("叫个人来",[]),/does not match/);
  assert.equal(director.supportUsed,false);
  assert.equal(director.state.team.find("老街帮手"),undefined);
});

test("failed support effect restores once-per-scenario flag",async()=>{
 const director=await build();
 director.state.team.grantSupport("弗兰克");
 await director.start("勒索信");
 // Disposable VM-only failure injection; original story scripts are unmodified.
 await director.scenario.evaluate('(define (support-frank) (node "叫个人来" :support "弗兰克" :resolve (instant (outcome (lambda () (error "support failed"))))))');
 await director.actions.prepare();
 await assert.rejects(director.actions.performSlots("叫个人来",[]),/support failed/);
 assert.equal(director.supportUsed,false);
 assert.equal(director.state.team.find("老街帮手"),undefined);
});
