import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {WorldTurnController} from "../src/runtime/world-turn.ts";
const base="../UnityClient/Assets/Resources/Content",src={};
function scan(p){
 for(const entry of readdirSync(join(base,p),{withFileTypes:true})){
  const path=join(p,entry.name).replaceAll("\\","/");
  if(entry.isDirectory())scan(path);
  else if(path.endsWith(".scm"))src[path]=readFileSync(join(base,path),"utf8");
 }
}
scan("scripts");scan("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");
test("real world calendar on-turn-end and on-turn-start advance day once",async()=>{
 const state=new GameRuntimeState();
 const world=await GameScriptSession.create("day-world",state,src,std);
 await world.loadFile("scenes/world/world.scm");
 const turn=new WorldTurnController(state,world);
 // Authored prologue now permits Neil to sleep before the visit. The
 // door becomes mandatory only AFTER the second evening (day 3).
 const first=await turn.endDay(()=>0.5);
 assert.equal(first.after,2);
 const second=await turn.endDay(()=>0.5);
 assert.equal(second.after,3);
 assert.equal(state.getGlobal("世界日"),3);
 assert.equal(await world.evaluateNumber("world-day"),3);
 await world.evaluate("(three-letters 'sync-blockers!)");
 assert.ok(state.hasRestBlockers(),"day-three door must block another night");
 await assert.rejects(turn.endDay(()=>0.5),/rest blockers/);
 assert.deepEqual(state.team.dice("player").map(x=>x.slotId),[0,1,2,3]);
});
