import test from "node:test";
import assert from "node:assert/strict";
import {parseStage} from "../src/runtime/stage-contract.ts";
import {StagePlayback} from "../src/runtime/stage-playback.ts";
const make=raw=>new StagePlayback(parseStage([raw]));
const line=(speaker,text)=>["say",[speaker,text,"",0,null,speaker]];

test("real Unity story-stage beats execute concurrently; clicking cannot skip movement",()=>{
  const player=make([
    [["spawn","尼尔","Portraits/Neil",-4,"front"]],
    [["move","尼尔",4,4],["pause",2]],
    [line("尼尔","你听见了吗？")],
    [["remove","尼尔"]]
  ]);
  player.start();
  assert.equal(player.entities.get("尼尔").x,-4);
  player.tick(1);
  assert.equal(player.entities.get("尼尔").x,-2);
  player.advance();
  assert.equal(player.beatIndex,1,"click cannot fast-forward timed animation");
  player.tick(3);
  assert.equal(player.entities.get("尼尔").x,4);
  assert.equal(player.requiresInput,true);
  player.advance();
  assert.equal(player.requiresInput,true,"first click reveals long dialogue");
  player.advance();
  assert.equal(player.complete,true);
  assert.equal(player.entities.size,0);
});
test("original C# de Casteljau paths, relative movement and anchored props",()=>{
  const p=make([
    [["spawn","尼尔","Neil",1,"middle"]],
    [["path-relative","尼尔",2,[[0,0],[2,4],[4,0]]]],
    [["prop-at","信封","Mail","尼尔",1,-2,"front"]]
  ]);
  p.start();p.tick(1);
  assert.deepEqual([p.entities.get("尼尔").x,p.entities.get("尼尔").y],[3,2]);
  p.tick(1);
  assert.deepEqual([p.entities.get("尼尔").x,p.entities.get("尼尔").y],[5,0]);
  assert.deepEqual([p.entities.get("信封").x,p.entities.get("信封").y],[6,-2]);
  assert.equal(p.complete,true);
});
test("stage say is input-blocking, pose/light/effects keep author intent",()=>{
  const p=make([
    [["spawn","夜莺","Nightingale",3,"front"]],
    [["pose","夜莺","看向尼尔"],["light","夜莺","faint"],["effect","夜莺","线条迸射",0,1]],
    [line("夜莺","今晚别走。")],
    [["pause",1]]
  ]);
  p.start();p.tick(0);
  assert.equal(p.entities.get("夜莺").pose,"看向尼尔");
  assert.equal(p.entities.get("夜莺").light,"faint");
  assert.equal(p.effects[0].asset,"线条迸射");
  assert.equal(p.requiresInput,true);
  p.tick(5);assert.equal(p.beatIndex,2);
  p.advance();p.tick(1);
  assert.equal(p.complete,true);
});
