import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {convertNode} from "../src/runtime/node-converter.ts";

const root="../UnityClient/Assets/Resources/Content", sources={};
function scan(dir){
 for(const item of readdirSync(join(root,dir),{withFileTypes:true})){
  const path=join(dir,item.name).replaceAll("\\","/");
  if(item.isDirectory())scan(path);
  else if(item.isFile()&&path.endsWith(".scm"))sources[path]=readFileSync(join(root,path),"utf8");
 }
}
scan("scripts");scan("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");
test("the new first-act tavern introduces a brief, optional trouble encounter",async()=>{
 const vm=await GameScriptSession.create("tavern-new",new GameRuntimeState(),sources,std);
 await vm.loadFile("scenes/world/world.scm");
 const script=sources["scenes/world/老街酒馆.scm"];
 assert.match(script,/第三次看场固定遇到两桌醉客/);
 assert.match(script,/start-encounter "醉客互相叫骂"/);
 assert.match(script,/第一幕 'on-tavern-work!/);
 const tavern=convertNode(await vm.evaluate("(car (old-street-tavern 'render-data))"));
 assert.ok(tavern.children.some(x=>x.name==="看场子"));
 assert.ok(tavern.children.every(x=>x.name!=="闹事：赖账的客人"),"retired trouble route must not leak");
});
test("rainy escort depends on fear, not capturing a blackmail suspect",()=>{
 const escort=sources["scenes/encounters/雨夜同行.scm"];
 const entry=sources["scenes/world/第一幕同行.scm"];
 assert.match(escort,/make-clock "回到住处" 4 'gauge/);
 assert.match(escort,/make-clock "夜莺的恐惧" 3 'pressure/);
 assert.match(escort,/define 人影上限 1/);
 assert.match(entry,/听夜莺说话/);
 assert.match(entry,/今晚走不开/);
 assert.doesNotMatch(entry,/required-before-rest/);
});
