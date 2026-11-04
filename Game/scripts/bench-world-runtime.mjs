import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {performance} from "node:perf_hooks";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";
import {WorldSaveController} from "../src/runtime/world-save.ts";

const root="../UnityClient/Assets/Resources/Content",sources={};
function scan(dir){
  for(const entry of readdirSync(join(root,dir),{withFileTypes:true})){
    const path=join(dir,entry.name).replaceAll("\\","/");
    if(entry.isDirectory())scan(path);
    else if(entry.isFile()&&path.endsWith(".scm"))sources[path]=readFileSync(join(root,path),"utf8");
  }
}
scan("scripts");scan("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");
const t0=performance.now();
const director=await GameSceneDirector.create(sources,std,()=>0.4,false);
const bootMs=performance.now()-t0;
const runner=director.worldActions;

function stableTree(n){
  return {id:n.name,title:n.title,subtitle:n.subtitle,isPlace:n.isPlace,
    disabled:n.disabled,requiredBeforeRest:n.requiredBeforeRest??null,
    children:n.children.map(stableTree),
    clocks:n.clocks.map(c=>[c.label,c.current,c.max]),
    arrivals:n.arrivals.map(a=>a.id),
    requires:n.requires.map(r=>[r.type,r.itemId??"",r.qty??0])};
}
async function timed(label,action){
  const start=performance.now();
  const value=await action();
  const ms=performance.now()-start;
  console.log("SSNOIR_PERF "+JSON.stringify({label,ms:Math.round(ms)}));
  return {value,ms};
}
// Warm up LIPS and ensure world rest obligations have been projected before
// comparing paired materializations. No gameplay state is changed in the loop.
director.world.setFastKeywordLookup(false);
await runner.prepare();
const slow1=await timed("world-render-original-1",()=>runner.prepare());
director.world.setFastKeywordLookup(true);
const fast1=await timed("world-render-fast-1",()=>runner.prepare());
assert.deepEqual(stableTree(fast1.value),stableTree(slow1.value));
director.world.setFastKeywordLookup(false);
const slow2=await timed("world-render-original-2",()=>runner.prepare());
assert.deepEqual(stableTree(slow2.value),stableTree(slow1.value));
director.world.setFastKeywordLookup(true);
const fast2=await timed("world-render-fast-2",()=>runner.prepare());
assert.deepEqual(stableTree(fast2.value),stableTree(slow1.value));
const save=new WorldSaveController(director.state,director.world);
director.world.setFastKeywordLookup(false);
const slowSave=await timed("world-save-original",()=>save.capture());
director.world.setFastKeywordLookup(true);
const fastSave=await timed("world-save-fast",()=>save.capture());
assert.equal(fastSave.value.worldDatum,slowSave.value.worldDatum);
assert.deepEqual(fastSave.value.state,slowSave.value.state);
await timed("enter-place-fast",()=>runner.enterPlace("家"));
console.log("SSNOIR_PERF_SUMMARY "+JSON.stringify({
  bootstrapMs:Math.round(bootMs),
  worldRenderOriginalMs:Math.round((slow1.ms+slow2.ms)/2),
  worldRenderFastMs:Math.round((fast1.ms+fast2.ms)/2),
  saveOriginalMs:Math.round(slowSave.ms),
  saveFastMs:Math.round(fastSave.ms)
}));
