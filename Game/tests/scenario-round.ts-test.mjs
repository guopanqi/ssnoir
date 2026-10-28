import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";
const root="../UnityClient/Assets/Resources/Content",sources={};
function scan(p){for(const x of readdirSync(join(root,p),{withFileTypes:true})){
 const path=join(p,x.name).replaceAll("\\","/");
 if(x.isDirectory())scan(path);
 else if(path.endsWith(".scm"))sources[path]=readFileSync(join(root,path),"utf8");
}}
scan("scripts");scan("scenes");const std=readFileSync("node_modules/lips/dist/std.xcb");
test("real blackmail scenario taxes one composure and executes ordered opponent rules before reroll",async()=>{
 const director=await GameSceneDirector.create(sources,std,()=>0.5);
 await director.start("勒索信");
 const initial=director.state.team.composure("player");
 const report=await director.endRound();
 assert.equal(report[0].phase,"time-tax");
 assert.equal(director.state.team.composure("player"),initial-1);
 assert.equal(director.state.team.dice("player").length,4);
});
