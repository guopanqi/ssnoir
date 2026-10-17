import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameRuntimeState} from "../src/runtime/game-state.ts";

const root="../UnityClient/Assets/Resources/Content",sources={};
function scan(dir){
 for(const entry of readdirSync(join(root,dir),{withFileTypes:true})){
  const key=join(dir,entry.name).replaceAll("\\","/");
  if(entry.isDirectory())scan(key);
  else if(entry.isFile()&&key.endsWith(".scm"))sources[key]=readFileSync(join(root,key),"utf8");
 }
}
scan("scripts");scan("scenes");
const standard=readFileSync("node_modules/lips/dist/std.xcb");

test("bartender's second visit reveals both livelihood locations and cafe offers photo work",async()=>{
 const state=new GameRuntimeState();
 const vm=await GameScriptSession.create("cafe-introduction",state,sources,standard);
 await vm.loadFile("scenes/world/world.scm");
 await vm.evaluate(`(three-letters 'load! (list (list "story-stage" 1)))`);
 const truth=async(expr)=>await vm.evaluateNumber(`(if ${expr} 1 0)`);
 const visible=(name)=>truth(`(member? "${name}" (map (lambda (loc) (car (loc 'render-data))) '()))`);
 assert.equal(await truth("(opening-life 'routes-open?)"),0);
 assert.equal(await truth("(member? cafe (current-locations))"),0);
 assert.equal(await truth("(member? dock (current-locations))"),0);
 assert.equal(await vm.evaluateNumber(`(length (opening-life 'arrivals-at "老街酒馆"))`),1);
 await vm.evaluate(`((caddr (car (opening-life 'arrivals-at "老街酒馆"))))`);
 assert.equal(await truth("(opening-life 'routes-open?)"),1);
 assert.equal(await truth("(member? cafe (current-locations))"),1);
 assert.equal(await truth("(member? dock (current-locations))"),1);
 assert.equal(await truth("(member? old-street-tavern (current-locations))"),1);
 assert.equal(await vm.evaluateNumber(`(length (cafe-work 'nodes-at "街角咖啡馆"))`),1);
 assert.equal(await vm.evaluateNumber(`(length (photo-case 'nodes-at "街角咖啡馆"))`),1);
 assert.equal(await vm.evaluateNumber(`(length (photo-case 'nodes-at "老街酒馆"))`),0);
 await vm.evaluate(`(photo-case 'hear-rumours! 2)`);
 await vm.evaluate(`(photo-case 'hear-rumours! 2)`);
 assert.equal(await vm.evaluateNumber(`(length (photo-case 'nodes-at "街角咖啡馆"))`),1,
   "completed cafe rumour clock becomes a new private-investigation commission");
 assert.equal(await vm.evaluateNumber(`(length (photo-case 'nodes-at "老街酒馆"))`),0);
 const saved=await vm.evaluateNumber(`(if (assoc-get (opening-life 'save) "routes-known?" #f) 1 0)`);
 assert.equal(saved,1,"same unlock is saved and restored");
});
