import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {createNativeBridge} from "../src/runtime/native-bridge.ts";
const root="../UnityClient/Assets/Resources/Content/scripts/";
const sources=Object.fromEntries(["stdlib.scm","engine.scm","theatre.scm"].map(x=>["scripts/"+x,readFileSync(root+x,"utf8")]));
test("Scheme story actions enqueue ordered, typed blocking and banter beats for platform UI",async()=>{
 const state=new GameRuntimeState();
 const vm=await GameScriptSession.create("story-queue",state,sources);
 state.beginActionReport();
 await vm.evaluate('(begin (play-dialogue! (line "尼尔" "有人吗？")) (__play-banter! (list (line "夜莺" "我在。"))) (play-video! "chandelier") (play-motion! "剧院/吊灯" "落下" "cam"))');
 state.takeActionReport();
 const queue=state.takeStoryEvents();
 assert.deepEqual(queue.map(x=>x.kind),["dialogue","banter","video","motion"]);
 assert.deepEqual(queue.map(x=>x.blocking),[true,false,true,true]);
 assert.equal(queue[0].immediate,false);
 assert.equal(queue[2].args[0],"chandelier");
});
test("transaction canceled after Scheme error does not leak ghost dialogue",async()=>{
 const state=new GameRuntimeState();
 const vm=await GameScriptSession.create("story-failed",state,sources);
 state.beginActionReport();
 await vm.evaluate('(play-dialogue! (line "尼尔" "永远不应该播放"))');
 state.cancelActionReport();
 assert.deepEqual(state.takeStoryEvents(),[]);
 assert.throws(()=>createNativeBridge(state)["__result-supplement!"]("extra"),/requires executing action/);
});
test("video outside action is intentionally ignored, but dialogue goes to immediate queue",async()=>{
 const state=new GameRuntimeState();
 const vm=await GameScriptSession.create("story-immediate",state,sources);
 await vm.evaluate('(play-video! "ignored")');
 await vm.evaluate('(play-dialogue! (line "尼尔" "你好"))');
 const queue=state.takeStoryEvents();
 assert.equal(queue.length,1);
 assert.equal(queue[0].immediate,true);
 assert.equal(queue[0].kind,"dialogue");
});

test("current four-field C# camera native validates and queues cut/glide without dropping flag",()=>{
 const state=new GameRuntimeState(),bridge=createNativeBridge(state);
 state.beginActionReport();
 assert.throws(()=>bridge["__play-camera!"]("老街",0.5,"家"),/expects 4/);
 assert.throws(()=>bridge["__play-camera!"]("老街",0.5,"家","yes"),/invalid camera/);
 bridge["__play-camera!"]("老街",0.5,"家",false);
 bridge["__play-camera!"]("剧院",0,"后台",true);
 state.takeActionReport();
 const events=state.takeStoryEvents();
 assert.deepEqual(events.map(x=>x.args),[["老街",0.5,"家",false],["剧院",0,"后台",true]]);
 assert.ok(events.every(x=>x.kind==="camera"&&x.blocking));
});


test("bubble recovery wraps an authored line but never heals before display",async()=>{
  const state=new GameRuntimeState();
  state.setGlobal("世界日",4);
  state.team.spend("player",3);
  const vm=await GameScriptSession.create("bubble-recovery",state,sources);
  await vm.evaluate('(play-bubble! (bubble-restore-composure (line "尼尔" "酒总得有人买。") "player" 2 "冷静恢复"))');
  assert.equal(state.team.composure("player"),2,"building the bubble does not heal");
  const events=state.takeStoryEvents();
  assert.equal(events.length,1);
  assert.equal(events[0].kind,"banter");
  const line=events[0].args[0][0];
  assert.equal(line[1],"酒总得有人买。");
  assert.deepEqual(line.at(-1).slice(0,5),["ssnoir-bubble-recovery","player",2,"冷静恢复",4]);
  assert(Number.isSafeInteger(line.at(-1)[5]));
});
