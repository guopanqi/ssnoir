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
scan("scripts");scan("scenes");test("real blackmail scenario taxes one composure and executes ordered opponent rules before reroll",async()=>{
 const director=await GameSceneDirector.create(sources,()=>0.5);
 await director.start("勒索信");
 const initial=director.state.team.composure("player");
 const report=await director.endRound();
 assert.equal(report[0].phase,"time-tax");
 assert.equal(director.state.team.composure("player"),initial-1);
 assert.equal(director.state.team.dice("player").length,4);
});

test("patient paper archive does not impose an unrelated round injury",async()=>{
  const director=await GameSceneDirector.create(sources,()=>0.999);
  const tree=await director.start("上午十一点零七分");
  assert.equal(director.mode,"scenario");
  assert(tree.children.length>0);
  const initial=director.state.team.composure("player");
  const injury=director.state.injury.severity;
  for(let turn=0;turn<12;turn++){
    const frames=await director.endRound();
    assert.equal(frames[0].phase,"time-tax");
    assert.equal(director.mode,"scenario");
    assert.equal(director.state.team.composure("player"),initial,
      "Paper sorting waits do not burn composure");
    assert.equal(director.state.injury.severity,injury,
      "The authored no-failure investigation cannot hospitalize a patient investigator");
  }
});

test("authored banquet changes from group mingling into four real guests after three dances",async()=>{
 const director=await GameSceneDirector.create(sources,()=>0.999);
 let room=await director.start("晚宴");
 assert.equal(director.mode,"scenario");
 assert(room.children.some(x=>x.name==="加入赞助人"),"First half starts with the social groups");
 assert(director.state.team.diceSnapshot()["夜莺"],"Nightingale is recruited by authored entrance");
 for(let turn=1;turn<=3;turn++){
   const frames=await director.endRound();
   assert(frames.some(x=>x.phase==="opponent-rule"),
     "Each dance progresses by the real opponent rule");
 }
 room=await director.render();
 assert.deepEqual(
   ["和薇拉谈","陪他喝一杯","和贝恩斯谈","和记者谈"]
     .filter(id=>room.children.some(x=>x.name===id)),
   ["和薇拉谈","陪他喝一杯","和贝恩斯谈","和记者谈"],
   "The complete second-half character choices must be constructible");
 assert(!room.children.some(x=>x.name==="加入赞助人"),
   "The player must not see first-half group actions after changing acts");
 assert.equal(director.mode,"scenario","Entering act two is not an exit");
 for(let turn=4;turn<=6;turn++)await director.endRound();
 assert.equal(director.mode,"world","The sixth dance ends through the original authored countdown");
});

test("original dock trial stability countdown crashes without inventing a hospital day",async()=>{
  const director=await GameSceneDirector.create(sources,()=>0.999);
  director.state.setGlobal("世界日",25);
  let tree=await director.start("码头试运行");
  assert.equal(director.mode,"scenario");
  assert(tree.children.some(x=>x.name==="处理偏载货箱"));
  // All three unresolved alarms drain three stability per real opponent turn.
  // With 8 total stability, the third transition must end the experiment.
  for(let round=1;round<=3;round++){
    const frames=await director.endRound();
    assert(frames.some(x=>x.phase==="opponent-rule"),
      "Original concurrent hazards must resolve every round");
    if(round<3)assert.equal(director.mode,"scenario");
  }
  assert.equal(director.mode,"world",
    "An empty system-stability clock ends the original Scenario normally");
  assert.equal(director.state.getGlobal("世界日"),25,
    "System failure is not a hospitalization or a world-day transition");
  assert.equal(director.state.hasPendingHospitalization(),false,
    "No injury collapse is pending when the controller crashes");
});
