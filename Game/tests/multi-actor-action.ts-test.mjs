import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameActionRunner} from "../src/runtime/action-runner.ts";
import {ActionTurnState} from "../src/runtime/turn-state.ts";
const root="../UnityClient/Assets/Resources/Content/scripts/";
const src=Object.fromEntries(["stdlib.scm","engine.scm","theatre.scm"].map(x=>["scripts/"+x,readFileSync(root+x,"utf8")]));
const std=readFileSync("node_modules/lips/dist/std.xcb");
test("multiple slotted dice and a companion resolve authentic outcome closure",async()=>{
 const state=new GameRuntimeState();
 state.team.recruit("lin","林",{violence:0,knowledge:2,sharpness:1,social:1});
 state.team.rollDice(()=>0.5);
 const vm=await GameScriptSession.create("multidie",state,src,std);
 const runner=await GameActionRunner.create(vm,state,new ActionTurnState(()=>0.99));
 await runner.prepare(`(node "世界" :children (list
  (node "并肩推车" :requires (list (req-die) (req-die))
   :resolve (roll 'knowledge (lambda () (list (modifier 1 "同行")))
    (outcome (lambda () (add-item! "情报" 1)))
    (outcome (lambda () (add-item! "情报" 2)))
    (outcome (lambda () (add-item! "情报" 3)))))))`);
 const before=state.team.dice("player");
 const result=await runner.performSlots("并肩推车",[
  {type:"die",actorId:"lin",slotId:0},{type:"die",actorId:"player",slotId:2}]);
 assert.equal(result.outcome,"success");
 assert.equal(state.getItemCount("情报"),3);
 assert.equal(state.team.dice("lin").length,0);
 assert.equal(state.team.dice("player").length,before.length-1);
 await assert.rejects(runner.performSlots("并肩推车",[
  {type:"die",actorId:"lin",slotId:0},{type:"die",actorId:"player",slotId:0}]),/unavailable/);
});
