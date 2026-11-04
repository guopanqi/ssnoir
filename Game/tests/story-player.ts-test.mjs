import test from "node:test";
import assert from "node:assert/strict";
import {StoryPlaybackQueue} from "../src/runtime/story-player.ts";

test("original Scheme dialogue reveals one line per acknowledgment and preserves order",()=>{
 const player=new StoryPlaybackQueue();
 player.push([{kind:"dialogue",args:[
  ["line","尼尔","你在吗？"],["line","夜莺","我在。"]
 ],immediate:false,blocking:true},
 {kind:"banter",args:[["line","摊主","报纸！"]],immediate:false,blocking:false},
 {kind:"spotlight",args:["第三封信","今晚她要死"],blocking:true,immediate:false}]);
 assert.equal(player.pendingCount(),3);
 assert.equal(player.current?.speaker,"尼尔");
 assert.equal(player.current?.text,"你在吗？");
 assert.deepEqual(player.takeBanter().map(x=>x.text),["报纸！"]);
 player.advance();assert.equal(player.current?.speaker,"夜莺");
 player.advance();assert.equal(player.current?.kind,"spotlight");
 player.advance();assert.equal(player.blocked,false);
});
test("stage events retain their raw structured commands without executing them",()=>{
 const player=new StoryPlaybackQueue();
 const commands=[["spawn","尼尔","neil",0,1],["say",["line","尼尔","夜莺。"]]];
 player.push([{kind:"stage",args:[commands],immediate:false,blocking:true}]);
 assert.equal(player.current?.kind,"stage");
 assert.deepEqual(player.current?.commands,[commands]);
});

test("authoritative intertitle splits its pages and preserves camera transitions",()=>{
 const q=new StoryPlaybackQueue();
 q.push([{kind:"intertitle",args:[true,[
  ["大雨下个不停。","序章/01"],["那天我还不知道会遇见她。","序章/02"]
 ]],immediate:false,blocking:true},
 {kind:"camera",args:["老街酒馆",0.8,"家",false],blocking:true,immediate:false}]);
 assert.equal(q.pendingCount(),3);
 assert.equal(q.current?.text,"大雨下个不停。");
 assert.deepEqual(q.current?.commands,[true,["大雨下个不停。","序章/01"]]);
 q.advance();assert.equal(q.current?.text,"那天我还不知道会遇见她。");
 q.advance();assert.equal(q.current?.kind,"camera");
 assert.deepEqual(q.current?.commands,["老街酒馆",0.8,"家",false]);
 q.advance();assert.equal(q.blocked,false);
});

test("four-argument camera cut flag remains intact; old three-argument payload is rejected",()=>{
 const q=new StoryPlaybackQueue();
 q.push([{kind:"camera",args:["老街酒馆-工作",0,"剧院",true],blocking:true,immediate:false}]);
 assert.deepEqual(q.current?.commands,["老街酒馆-工作",0,"剧院",true]);
 assert.match(q.current?.text??"",/切镜/);
 const bad=new StoryPlaybackQueue();
 assert.throws(()=>bad.push([{kind:"camera",args:["老街酒馆",0.8,"家"],blocking:true,immediate:false}]),/invalid camera/);
});


test("authored seven/eight-field Scheme line is presented as speaker and text", () => {
  const q = new StoryPlaybackQueue();
  q.push([{
    kind:"dialogue",blocking:true,immediate:false,
    args:[[
      ["neil","你好，夜莺。","",0,[],"尼尔",""],
      ["nightingale","今晚的彩排很重要。","",0,[],"夜莺","剧院舞台","nightingale-a"]
    ]]
  }]);
  assert.equal(q.current?.speaker,"尼尔");
  assert.equal(q.current?.text,"你好，夜莺。");
  q.advance();
  assert.equal(q.current?.speaker,"夜莺");
  assert.equal(q.current?.text,"今晚的彩排很重要。");
  q.advance();
  assert.equal(q.blocked,false);
});

test("authored banter does not show raw voice, stage and anchor metadata", () => {
  const q = new StoryPlaybackQueue();
  q.push([{
    kind:"banter",blocking:false,immediate:true,
    args:[[
      ["尼尔","今天看上去不太妙。","voice-1",2.5,[],"尼尔","酒馆"]
    ]]
  }]);
  const chatter=q.takeBanter();
  assert.equal(chatter.length,1);
  assert.equal(chatter[0].speaker,"尼尔");
  assert.equal(chatter[0].text,"今天看上去不太妙。");
  assert.equal(q.takeBanter().length,0);
});


test("recovery is metadata on its exact banter frame, never on blocking dialogue",()=>{
  const line=["player","说完才恢复。","",0,[],"尼尔","",
    ["ssnoir-bubble-recovery","player",2,"冷静+2",5,17]];
  const q=new StoryPlaybackQueue();
  q.push([{kind:"banter",args:[[line]],blocking:false,immediate:false}]);
  const frames=q.takeBanter();
  assert.equal(frames[0].text,"说完才恢复。");
  assert.deepEqual(frames[0].recovery,
    {actorId:"player",amount:2,notice:"冷静+2",worldDay:5,id:17});
  assert.throws(()=>new StoryPlaybackQueue().push([
    {kind:"dialogue",args:[[line]],blocking:true,immediate:false}
  ]),/only permitted in banter/);
});
