import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";

const root="../UnityClient/Assets/Resources/Content";
const sources={};
function scan(dir){
  for(const entry of readdirSync(join(root,dir),{withFileTypes:true})){
    const path=join(dir,entry.name).replaceAll("\\","/");
    if(entry.isDirectory())scan(path);
    else if(entry.isFile()&&path.endsWith(".scm"))sources[path]=readFileSync(join(root,path),"utf8");
  }
}
scan("scripts");scan("scenes");

test("rehearsal time tax can hospitalize a player without leaving a stuck scenario",async()=>{
  const director=await GameSceneDirector.create(sources,()=>0.5);
  const day=director.state.getGlobal("世界日");
  director.state.team.require("player").composure=0;
  director.state.injury.restore({severity:6,part:"手",scars:[]});
  await director.start("第一次彩排");
  const frames=await director.endRound();
  assert(frames.length>0);
  assert(director.state.hasPendingHospitalization());
  const recovered=await director.resolveHospitalization();
  assert(recovered);
  assert.equal(recovered.after,day+1,"Forced hospital night is the next real Scheme calendar day");
  assert.equal(director.mode,"world");
  assert.equal(director.state.hasPendingHospitalization(),false);
  assert.equal(director.state.injury.severity,0);
  assert(director.state.injury.scarCount("手")>=1);
  assert(director.state.team.dice("player").length>0,"New day rolls city dice, not stale scenario dice");
  const spotlights=recovered.events.filter(x=>x.kind==="spotlight").map(x=>x.args[0]);
  assert.deepEqual(spotlights.slice(0,2),["你倒下了","在诊所醒来"]);
  await director.flush();
  assert.equal(director.mode,"world","No stale end-turn or scene-end request can replay");
  await director.render();
});

test("hospitalization can advance an authored world day despite a mandatory rest blocker",async()=>{
  const director=await GameSceneDirector.create(sources,()=>0.5);
  assert(director.state.hasRestBlockers(),"The first morning has a mandatory authored action");
  const original=director.state.getGlobal("世界日");
  director.state.applyInjury(7,()=>0.5);
  assert(director.state.hasPendingHospitalization());
  const recovered=await director.resolveHospitalization();
  assert.equal(recovered.after,original+1);
  assert.equal(director.state.hasPendingHospitalization(),false);
  assert.equal(director.mode,"world");
  assert(director.state.team.dice("player").length>0);
});

test("invalid authored collapse policy is rejected without consuming the hospitalization",async()=>{
  const badSource=[
    '(define (on-scenario-enter) #t)',
    "(define (on-scenario-collapse) (list 'unexpected))",
    '(define (get-render-data) (node "坏的倒下策略"))'
  ].join("\n");
  const director=await GameSceneDirector.create({
    ...sources,"scenes/scenarios/倒下策略非法.scm":badSource
  },()=>0.5);
  await director.start("倒下策略非法");
  director.state.applyInjury(7,()=>0.5);
  await assert.rejects(director.resolveHospitalization(),/invalid authored scenario collapse policy/);
  assert.equal(director.mode,"scenario");
  assert.equal(director.state.hasPendingHospitalization(),true);
  assert.equal(director.state.getGlobal("世界日"),1);
});
