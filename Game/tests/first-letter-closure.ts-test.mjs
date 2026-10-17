import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {GameScriptSession} from "../src/runtime/script-session.ts";

const root="../UnityClient/Assets/Resources/Content";
const sources={};
function walk(path){
 for(const entry of readdirSync(join(root,path),{withFileTypes:true})){
  const key=join(path,entry.name).replaceAll("\\","/");
  if(entry.isDirectory())walk(key);
  else if(entry.isFile()&&key.endsWith(".scm"))
   sources[key]=readFileSync(join(root,key),"utf8");
 }
}
walk("scripts");walk("scenes");
const standard=readFileSync("node_modules/lips/dist/std.xcb");
let index=0;
async function makeWorld(){
 const state=new GameRuntimeState();
 const vm=await GameScriptSession.create("first-letter-closure-"+(++index),state,sources,standard);
 await vm.loadFile("scenes/world/world.scm");
 return vm;
}
async function setStages(vm,chapter,photo,source){
 await vm.evaluate(`(three-letters 'load! (list (list "story-stage" ${chapter})))`);
 await vm.evaluate(`(photo-case 'load! (list (list "stage" ${photo}) (list "source-stage" ${source})))`);
}
async function truth(vm,expr){
 return await vm.evaluateNumber(`(if ${expr} 1 0)`);
}
test("late marriage job never reopens first-letter source hunt after interception",async()=>{
 const vm=await makeWorld();
 await setStages(vm,1,3,1); // developed while ransom remains live
 assert.equal(await truth(vm,"(photo-case 'shop-open?)"),1);
 assert.equal(await vm.evaluateNumber('(length (photo-case \'nodes-at "照相馆"))'),1);
 await setStages(vm,2,3,1); // rainy interception just finished, no city-day advanced
 assert.equal(await truth(vm,"(photo-case 'shop-open?)"),0);
 assert.equal(await vm.evaluateNumber('(length (photo-case \'nodes-at "照相馆"))'),0);
 assert.equal(await vm.evaluateNumber('(length (photo-case \'nodes-at "街角咖啡馆"))'),2); // wife + marital handoff; no source talk
 assert.equal(await vm.evaluateNumber('(length (photo-case \'nodes-at "老街酒馆"))'),0);
});
test("unfinished marriage film processing remains legal, stale source talk closes",async()=>{
 const vm=await makeWorld();
 await setStages(vm,2,2,0);
 assert.equal(await truth(vm,"(photo-case 'shop-open?)"),1);
 assert.equal(await vm.evaluateNumber('(length (photo-case \'nodes-at "照相馆"))'),1);
 await setStages(vm,1,4,3);
 assert.equal(await vm.evaluateNumber('(length (photo-case \'nodes-at "老街酒馆"))'),1);
 await setStages(vm,2,4,3);
 assert.equal(await vm.evaluateNumber('(length (photo-case \'nodes-at "老街酒馆"))'),0);
});
