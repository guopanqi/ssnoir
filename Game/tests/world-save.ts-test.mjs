import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {WorldSaveController} from "../src/runtime/world-save.ts";
const root="../UnityClient/Assets/Resources/Content";
const sources={};
function walk(path) {
 for(const entry of readdirSync(join(root,path),{withFileTypes:true})){
  const key=join(path,entry.name).replaceAll("\\","/");
  if(entry.isDirectory())walk(key);
  else if(entry.isFile()&&key.endsWith(".scm"))sources[key]=readFileSync(join(root,key),"utf8");
 }
}
walk("scripts");walk("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");
let index=0;
async function makeWorld(){
 const state=new GameRuntimeState();
 const session=await GameScriptSession.create("world-save-"+(++index),state,sources,std);
 await session.loadFile("scenes/world/world.scm");
 return {state,session};
}
test("new chapter world-save/world-load! roundtrip retains day and native resources",async()=>{
 const original=await makeWorld();
 await original.session.evaluate("(set! world-day 6)");
 original.state.setGlobal("世界日",6);
 // No retired case rest blocker remains; the date and resources must still round-trip.
 // Test exact inventory delta independently of the temporary 2× debug income setting.
 await original.session.evaluate("(set! debug-money-income-multiplier 1)");
 await original.session.evaluate('(add-item! "金钱" 22)');
 assert.equal(original.state.getItemCount("金钱"),37);
 original.state.team.grantSupport("林");
 const protagonist=original.state.team.require("player");
 protagonist.actionDice=[6,2];
 protagonist.actionDiceSlotIds=[0,3];
 const saved=await new WorldSaveController(original.state,original.session).capture({lang:"zh",sound:true});
 assert.match(saved.worldDatum,/day/);
 const wire=WorldSaveController.encode(saved);
 const parsed=WorldSaveController.parse(wire);
 const loaded=await WorldSaveController.restore(parsed,makeWorld);
 assert.equal(loaded.settings.lang,"zh");
 assert.equal(await loaded.session.evaluateNumber("world-day"),6);
 assert.equal(loaded.state.getGlobal("世界日"),6);
 assert.equal(loaded.state.getItemCount("金钱"),37);
 assert.equal(loaded.state.team.getCarriedSupport(),"林");
 assert.equal(loaded.state.getGlobal("location"),"world");
 assert.deepEqual(loaded.state.team.require("player").actionDice,[6,2]);
 assert.deepEqual(loaded.state.team.require("player").actionDiceSlotIds,[0,3]);
 assert.deepEqual(loaded.state.snapshot().restBlockers,original.state.snapshot().restBlockers);
 // A second authored world-save after load must preserve every module datum,
 // not merely the visible world-day global.
 const replay=await new WorldSaveController(loaded.state,loaded.session).capture({lang:"zh",sound:true});
 assert.equal(replay.worldDatum,saved.worldDatum,"all authored module state must round-trip");
 // Reloading must not award money or replay advancement.
 const again=await WorldSaveController.restore(parsed,makeWorld);
 assert.equal(again.state.getItemCount("金钱"),37);
 assert.equal(await again.session.evaluateNumber("world-day"),6);
 // Tampering the native day while preserving all authored module data must
 // never return a seemingly valid save with contradictory story deadlines.
 const mismatched=structuredClone(parsed);
 mismatched.state.globals["世界日"]=9;
 await assert.rejects(WorldSaveController.restore(mismatched,makeWorld),/calendar mismatch/);
});
test("bad save inputs rejected before altering an existing game",async()=>{
 const value=await makeWorld();
 assert.throws(()=>WorldSaveController.parse('{"format":"ssnoir-world","version":2}'),/unsupported/);
 assert.throws(()=>WorldSaveController.parse("{}"),/unsupported/);
 value.state.setGlobal("location","scenario");
 await assert.rejects(new WorldSaveController(value.state,value.session).capture(),/scenario/);
 value.state.setGlobal("location","world");
 value.state.team.recruit("temp","帮手",{violence:0,knowledge:0,sharpness:0,social:0},true);
 await assert.rejects(new WorldSaveController(value.state,value.session).capture(),/temporary/);
});
