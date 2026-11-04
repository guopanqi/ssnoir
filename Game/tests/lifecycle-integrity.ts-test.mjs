import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { GameRuntimeState } from "../src/runtime/game-state.ts";
import { GameSceneDirector } from "../src/runtime/scene-director.ts";

const root="../UnityClient/Assets/Resources/Content";
const sources={};
function scan(relative){
  for(const entry of readdirSync(join(root,relative),{withFileTypes:true})){
    const file=join(relative,entry.name).replaceAll("\\","/");
    if(entry.isDirectory())scan(file);
    else if(entry.isFile()&&file.endsWith(".scm"))
      sources[file]=readFileSync(join(root,file),"utf8");
  }
}
scan("scripts");scan("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");

test("restoring durable state discards abandoned action contexts, reports and story events",()=>{
  const state=new GameRuntimeState();
  const original=state.snapshot();
  state.queueStory({kind:"dialogue",blocking:true,args:[["old","should not display"]]});
  state.beginActionReport();
  state.recordClockEffect("work","progress",1);
  state.beginActionContext("player","world");
  state.queueStory({kind:"banter",blocking:false,args:[["old","also abandoned"]]});
  state.setItemCount("金钱",100);
  state.restore(original);
  assert.equal(state.getItemCount("金钱"),original.inventory["金钱"]);
  assert.equal(state.isActionReporting(),false);
  assert.throws(()=>state.currentActor(),/inside action/);
  assert.deepEqual(state.takeStoryEvents(),[]);
  state.beginActionReport();
  assert.deepEqual(state.takeActionReport(),[]);
});

test("a failed scenario entrance rolls back native state AND pending lifecycle signals",async()=>{
  const failureFixture=[
    "(define (on-scenario-enter)",
    "  (add-item! \"金钱\" 8)",
    "  (play-dialogue! (line \"世界\" \"这条失败的台词不能出现。\"))",
    "  (end-turn!)",
    "  (end-scenario '无交付)",
    "  (error \"进场故意失败\"))",
    "(define (get-render-data)",
    "  (node \"失败进场\" :children (list (note-node \"回声\" \"错误\" \"不应出现\"))))"
  ].join("\n");
  const contents={...sources,"scenes/scenarios/失败进场.scm":failureFixture};
  const director=await GameSceneDirector.create(contents,std,()=>0.5);
  const initial=director.state.snapshot();
  const dice=director.state.team.diceSnapshot();
  await assert.rejects(director.start("失败进场"),/进场故意失败/);
  assert.equal(director.mode,"world");
  assert.equal(director.scenario,null);
  assert.equal(director.scenarioActions,null);
  assert.deepEqual(director.state.snapshot(),initial);
  assert.deepEqual(director.state.team.diceSnapshot(),dice);
  assert.deepEqual(director.state.takeStoryEvents(),[]);
  // Previously the next flush still consumed the aborted end-scenario or
  // end-turn signal, leaving the city unplayable.
  await director.flush();
  assert.equal(director.mode,"world");
  const scene=await director.start("第一次彩排");
  assert.equal(director.mode,"scenario");
  assert(scene.children.some(node=>node.name==="搬运布景"));
  director.requestEnd("无交付");
  await director.flush();
  assert.equal(director.mode,"world");
  assert.deepEqual(director.state.team.diceSnapshot(),dice);
});


test("scenario result callback can enqueue a second scenario without losing the world transition",async()=>{
  const director=await GameSceneDirector.create(sources,std,()=>0.5);
  await director.world.evaluate(
    '(start-scenario "第一次彩排" (lambda (result) (start-scenario "最后一排的观众")))'
  );
  await director.flush();
  assert.equal(director.mode,"scenario");
  director.requestEnd("无交付");
  await director.flush();
  assert.equal(director.mode,"world",
    "First scenario has completed before the queued new scenario starts");
  // The world callback scheduled the next transition. It must survive cleanup
  // of the finished scenario's dice, active interpreter and result closure.
  await director.flush();
  assert.equal(director.mode,"scenario");
  assert.equal(director.getPreparedRoot()?.name,"最后一排的观众");
  director.requestEnd("错失车辆");
  await director.flush();
  assert.equal(director.mode,"world");
});
