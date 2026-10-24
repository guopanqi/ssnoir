import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {convertNode,schemeList} from "../src/runtime/node-converter.ts";

const root="../UnityClient/Assets/Resources/Content",sources={};
function scan(dir){
  for(const item of readdirSync(join(root,dir),{withFileTypes:true})){
    const key=join(dir,item.name).replaceAll("\\","/");
    if(item.isDirectory())scan(key);
    else if(item.isFile()&&key.endsWith(".scm"))sources[key]=readFileSync(join(root,key),"utf8");
  }
}
scan("scripts");scan("scenes");
const standard=readFileSync("node_modules/lips/dist/std.xcb");
async function setup(){
 const state=new GameRuntimeState();
 const vm=await GameScriptSession.create("first-act-current",state,sources,standard);
 await vm.loadFile("scenes/world/world.scm");
 return {state,vm};
}
test("first act starts from work and rent, not the retired blackmail plot",async()=>{
 const {vm}=await setup();
 assert.equal(await vm.evaluateNumber("world-day"),1);
 assert.equal(await vm.evaluateNumber("(第一幕 'act)"),1);
 const world=convertNode(await vm.evaluate("(get-render-data)"));
 assert.equal(world.name,"世界");
 assert.ok(world.children.length>0);
 assert.ok(Array.isArray(schemeList(await vm.evaluate("(get-dossier)"))));
 assert.equal(await vm.evaluateNumber("(if (bound? 'three-letters) 1 0)"),0);
});
test("Eddie becomes a dependable companion through actual dock work",async()=>{
 const {vm}=await setup();
 assert.equal(await vm.evaluateNumber("(if (第一幕 'eddie-met?) 1 0)"),0);
 await vm.evaluate('(第一幕 \'load! (list (list "opened" #t) (list "tavern" #t)))');
 await vm.evaluate('(第一幕 \'on-work! "码头" \'好)');
 assert.equal(await vm.evaluateNumber("(if (第一幕 'eddie-met?) 1 0)"),1);
 assert.equal(await vm.evaluateNumber("(第一幕 'eddie-stage)"),1);
 await vm.evaluate('(第一幕 \'on-work! "码头" \'好)');
 assert.equal(await vm.evaluateNumber("(第一幕 'eddie-stage)"),1);
});
test("rainy escort is optional and no longer repeats after the day-five window",async()=>{
 const {vm}=await setup();
 await vm.evaluate('(第一幕 \'load! (list (list "opened" #t) (list "tavern" #t) (list "singer" 1)))');
 await vm.evaluate("(set! world-day 4)");
 assert.equal(await vm.evaluateNumber('(length (first-act-walk \'nodes-at "老街酒馆"))'),1);
 await vm.evaluate("(set! world-day 7)");
 assert.equal(await vm.evaluateNumber('(length (first-act-walk \'nodes-at "老街酒馆"))'),0);
 await vm.evaluate('(first-act-walk \'load! (list (list "status" "已结束") (list "result" "抵达") (list "final-fear" 1)))');
 assert.equal(await vm.evaluateNumber("(if (first-act-walk 'walked?) 1 0)"),1);
});
test("the tavern song is once daily and can establish an introduction",async()=>{
 const {vm}=await setup();
 await vm.evaluate('(第一幕 \'load! (list (list "opened" #t) (list "tavern" #t)))');
 await vm.evaluate(`(let* ((cards (第一幕 'nodes-at "老街酒馆"))
   (song (car (reverse cards))) (resolve (get-kwarg (cddr song) :resolve #f)))
   ((cadr (cadr resolve))))`);
 assert.equal(await vm.evaluateNumber("(第一幕 'singer-stage)"),1);
 assert.equal(await vm.evaluateNumber("(if (= (第一幕 'listened-day) world-day) 1 0)"),1);
});
test("day-six farewell exists once and its fact survives save/load",async()=>{
 const {vm}=await setup();
 await vm.evaluate('(第一幕 \'load! (list (list "opened" #t) (list "tavern" #t) (list "singer" 1)))');
 await vm.evaluate("(set! world-day 6)");
 const before=await vm.evaluateNumber('(length (第一幕 \'nodes-at "老街酒馆"))');
 assert.ok(before>=1);
 await vm.evaluate('(第一幕 \'load! (list (list "opened" #t) (list "tavern" #t) (list "singer" 1) (list "farewell-seen" #t) (list "act" 2)))');
 const after=await vm.evaluateNumber('(length (第一幕 \'nodes-at "老街酒馆"))');
 assert.ok(after<before);
 assert.equal(await vm.evaluateNumber("(if (第一幕 'farewell-seen?) 1 0)"),1);
});
