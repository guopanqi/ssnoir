import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";
const base="../UnityClient/Assets/Resources/Content",sources={};
function scan(p){for(const entry of readdirSync(join(base,p),{withFileTypes:true})){
 const key=join(p,entry.name).replaceAll("\\","/");
 if(entry.isDirectory())scan(key);
 else if(key.endsWith(".scm"))sources[key]=readFileSync(join(base,key),"utf8");
}}
scan("scripts");scan("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");
test("original chapter-one blackmail scenario opens in isolated LIPS VM and returns world dice",async()=>{
 const scene=await GameSceneDirector.create(sources,std,()=>0.5);
 assert.equal(scene.mode,"world");
 const stash=scene.state.team.diceSnapshot();
 const render=await scene.start("勒索信");
 assert.ok(render.children.length>0,"original scenario should expose real cards");
 assert.equal(scene.mode,"scenario");
 assert.equal(scene.state.getGlobal("location"),"scenario");
 assert.ok(scene.scenario);
 await scene.scenario.evaluate("(define only-scenario 42)");
 await assert.rejects(scene.world.evaluate("only-scenario"),/Unbound/);
 scene.requestEnd("returned");
 await scene.flush();
 assert.equal(scene.mode,"world");
 assert.equal(scene.state.getGlobal("location"),"world");
 assert.deepEqual(scene.state.team.diceSnapshot(),stash);
});
test("scenario native callback receives real Scheme result without serializing closures",async()=>{
 const scene=await GameSceneDirector.create(sources,std,()=>0.5);
 await scene.world.evaluate('(start-scenario "勒索信" (lambda (result) (set-global! \'scenario-returned result)))');
 await scene.flush();
 assert.equal(scene.mode,"scenario");
 scene.requestEnd("线索");
 await scene.flush();
 assert.equal(scene.state.getGlobal("scenario-returned"),"线索");
});
test("no saving during scenario and no path escapes in start-scenario",async()=>{
 const scene=await GameSceneDirector.create(sources,std,()=>0.5);
 await assert.rejects(scene.start("../evil"),/invalid/);
 assert.equal(scene.mode,"world");
 await scene.start("勒索信");
 assert.throws(()=>scene.requestStart("巷子"),/already in scenario/);
});
