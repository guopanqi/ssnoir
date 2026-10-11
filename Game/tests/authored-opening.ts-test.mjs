import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";
import {WorldSaveController} from "../src/runtime/world-save.ts";
import {parseTheatre} from "../src/runtime/theatre-contract.ts";
const base="../UnityClient/Assets/Resources/Content",sources={};
function collect(dir){for(const item of readdirSync(join(base,dir),{withFileTypes:true})){
  const key=join(dir,item.name).replaceAll("\\","/");
  if(item.isDirectory())collect(key);
  else if(item.isFile()&&key.endsWith(".scm"))sources[key]=readFileSync(join(base,key),"utf8");
}}
collect("scripts");collect("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");
const build=()=>GameSceneDirector.create(sources,std,()=>0.45);
function flatten(node){return [node,...node.children.flatMap(flatten)];}

test("original chapter opening: door → real theatre/dialogue → 20 gold → authored save/load",async()=>{
  const d=await build();
  const world=await d.render();
  const opening=flatten(world).find(n=>n.name==="有人敲门");
  assert.ok(opening,"actual world must contain the original scripted door action");
  assert.equal(opening.resolve?.kind,"instant");
  assert.equal(opening.requires.length,0);
  assert.equal(d.state.getItemCount("金钱"),15);
  const place=world.children.find(x=>x.isPlace&&flatten(x).some(n=>n.name==="有人敲门"));
  assert.ok(place,"door must belong to an enterable original place");
  await d.worldActions.enterPlace(place.name);
  await d.flush();
  await d.worldActions.prepare();
  const result=await d.worldActions.performSlots("有人敲门",[]);
  await d.flush();
  assert.equal(result.name,"有人敲门");
  assert.equal(d.state.getItemCount("金钱"),35,
    "Nightingale's original C# content pays exactly 20 gold once");
  const changed=await d.render();
  assert.equal(flatten(changed).some(n=>n.name==="有人敲门"),false,
    "opening action disappears after the original Scheme story advances");
  const events=d.state.takeStoryEvents();
  const theatre=events.filter(x=>x.kind==="theatre");
  assert.ok(theatre.length>=2,"rainy arrival and the indoor commission should have scenes");
  for(const entry of theatre){
    const script=parseTheatre(entry.args);
    assert.ok(script.objects.length>0);
    assert.ok(script.cue.children.length>0);
    assert.equal(script.width,1600);
    assert.equal(script.height,900);
  }
  assert.ok(events.some(x=>x.kind==="remote-dialogue"),"original voiceover must reach narrative queue");
  assert.ok(events.some(x=>x.kind==="spotlight"),"original quest summary must reach narrative queue");
  const saved=await new WorldSaveController(d.state,d.world).capture();
  const data=WorldSaveController.parse(WorldSaveController.encode(saved));
  const fresh=await build();
  await WorldSaveController.restore(data,async()=>({state:fresh.state,session:fresh.world}));
  assert.equal(fresh.state.getItemCount("金钱"),35,"restore cannot replay advance payment");
  assert.equal(flatten(await fresh.render()).some(n=>n.name==="有人敲门"),false);
  assert.equal(fresh.state.snapshot().restBlockers.some(x=>x.id==="三封信/开场敲门"),false);
});
