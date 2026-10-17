import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {ActionTurnState} from "../src/runtime/turn-state.ts";
import {GameActionRunner} from "../src/runtime/action-runner.ts";
import {fateStrip,resolveFate} from "../src/runtime/fate-strip.ts";

const src=path=>readFileSync("../UnityClient/Assets/Resources/Content/"+path,"utf8");
const sources={"scripts/stdlib.scm":src("scripts/stdlib.scm"),
 "scripts/engine.scm":src("scripts/engine.scm"),"scripts/theatre.scm":src("scripts/theatre.scm")};
const binary=readFileSync("node_modules/lips/dist/std.xcb");

test("A+ percentage rows match C# and every percentile resolves consistently",()=>{
 const rows=[
  [0,[80,20,0]],[1,[80,20,0]],[2,[60,40,0]],[3,[40,50,10]],
  [4,[30,50,20]],[5,[0,70,30]],[6,[0,50,50]],
  [7,[0,10,90]],[8,[0,0,100]],[10,[0,0,100]]
 ];
 for(const [prepared,[fail,neutral,success]] of rows){
  const strip=fateStrip(prepared);
  assert.equal(strip.length,100);
  assert.equal(strip.filter(x=>x==="fail").length,fail);
  assert.equal(strip.filter(x=>x==="neutral").length,neutral);
  assert.equal(strip.filter(x=>x==="success").length,success);
  for(let roll=1;roll<=100;roll++)
    assert.equal(resolveFate(4,0,prepared-4,roll).outcome,strip[roll-1]);
 }
 assert.equal(resolveFate(3,1,0,1).outcome,"fail");
 assert.equal(resolveFate(3,1,0,31).outcome,"neutral");
 assert.equal(resolveFate(3,1,0,81).outcome,"success");
 assert.throws(()=>resolveFate(4,0,0,0),/percentage roll/);
 assert.throws(()=>resolveFate(4,0,0,101),/percentage roll/);
});
test("fixed dice slots survive spending without index confusion",()=>{
 const rolls=[0,0.2,0.4,0.8];
 const state=new ActionTurnState(()=>rolls.shift()??0);
 assert.deepEqual(state.available().map(x=>x.slotId),[0,1,2,3]);
 assert.equal(state.spend(1).value,2);
 assert.deepEqual(state.available().map(x=>x.slotId),[0,2,3]);
 assert.equal(state.get(2).value,3);
 assert.throws(()=>state.spend(1),/unavailable/);
});
test("authored instant and roll actions execute live Scheme effects on shared state",async()=>{
 const state=new GameRuntimeState();
 const vm=await GameScriptSession.create("action-smoke",state,sources,binary);
 const turn=new ActionTurnState(()=>0.99);
 const runner=await GameActionRunner.create(vm,state,turn);
 const root=await runner.prepare(`(node "世界" :children (list
   (node "搬货" :requires (list (req-die) (req-item "金钱" 2))
     :resolve (instant (outcome (lambda () (add-item! "金钱" 6)))))
   (node "访谈" :requires (list (req-die))
     :resolve (roll 'knowledge
       (outcome (lambda () (add-item! "情报" 1)))
       (outcome (lambda () (add-item! "情报" 2)))
       (outcome (lambda () (add-item! "情报" 3)))))))`);
 assert.equal(root.children.length,2);
 const result=await runner.perform("搬货",1);
 assert.equal(result.mode,"instant");
 assert.equal(state.getItemCount("金钱"),19); // +6 reward -2 cost
 assert.deepEqual(turn.available().map(x=>x.slotId),[0,2,3]);
 const roll=await runner.perform("访谈",2);
 assert.equal(roll.mode,"roll");
 assert.equal(roll.outcome,"success");
 assert.equal(state.getItemCount("情报"),3);
 assert.deepEqual(turn.available().map(x=>x.slotId),[0,3]);
});
test("validation before effects preserves inventory/dice on failure",async()=>{
 const state=new GameRuntimeState();
 const vm=await GameScriptSession.create("action-negative",state,sources,binary);
 const turn=new ActionTurnState(()=>0.5);
 const runner=await GameActionRunner.create(vm,state,turn);
 await runner.prepare(`(node "世界" :children (list
  (node "买票" :requires (list (req-die) (req-item "金钱" 99))
   :resolve (instant (outcome (lambda () (add-item! "情报" 1)))))))`);
 await assert.rejects(runner.perform("买票",0),/insufficient item/);
 assert.equal(state.getItemCount("金钱"),15);
 assert.equal(state.getItemCount("情报"),0);
 assert.equal(turn.available().length,4);
});
