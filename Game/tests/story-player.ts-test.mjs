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
