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
