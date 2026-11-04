import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";

const base="../UnityClient/Assets/Resources/Content";
const sources={};
function scan(directory){
  for(const entry of readdirSync(join(base,directory),{withFileTypes:true})){
    const key=join(directory,entry.name).replaceAll("\\","/");
    if(entry.isDirectory())scan(key);
    else if(entry.isFile()&&key.endsWith(".scm"))
      sources[key]=readFileSync(join(base,key),"utf8");
  }
}
scan("scripts");scan("scenes");
const standard=readFileSync("node_modules/lips/dist/std.xcb");

test("day-five world root arrival announces the farewell once without changing dice",async()=>{
  const scene=await GameSceneDirector.create(sources,standard,()=>0.5);
  const dice=scene.state.team.diceSnapshot();
  await scene.world.evaluate("(set! world-day 5) (set-global! '世界日 5)");
  const root=await scene.worldActions.enterPlace("世界");
  assert.equal(root.name,"世界");
  const events=scene.state.takeStoryEvents();
  assert.match(JSON.stringify(events),/明晚是夜莺在这里的最后一场演出/);
  assert.deepEqual(scene.state.team.diceSnapshot(),dice);
  await scene.worldActions.enterPlace("世界");
  assert.deepEqual(scene.state.takeStoryEvents(),[],"The one-shot announcement cannot repeat");
});
