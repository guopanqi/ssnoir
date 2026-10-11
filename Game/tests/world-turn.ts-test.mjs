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
 await assert.rejects(turn.endDay(()=>0.5),/rest blockers/);
 state.clearRestBlockers();
 const before=await world.evaluateNumber("world-day");
 const after=await turn.endDay(()=>0.5);
 assert.equal(after.after,before+1);
 assert.equal(state.getGlobal("世界日"),before+1);
 assert.equal(await world.evaluateNumber("world-day"),before+1);
 assert.deepEqual(state.team.dice("player").map(x=>x.slotId),[0,1,2,3]);
});
