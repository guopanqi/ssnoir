import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {convertNode} from "../src/runtime/node-converter.ts";
const root="../UnityClient/Assets/Resources/Content",sources={};
function scan(dir){
  for(const item of readdirSync(join(root,dir),{withFileTypes:true})){
    const key=join(dir,item.name).replaceAll("\\","/");
    if(item.isDirectory())scan(key);
    else if(item.isFile()&&key.endsWith(".scm"))
      sources[key]=readFileSync(join(root,key),"utf8");
  }
}
scan("scripts");scan("scenes");
const standard=readFileSync("node_modules/lips/dist/std.xcb");
async function setup(){
  const state=new GameRuntimeState();
  const vm=await GameScriptSession.create("act-two-relations",state,sources,standard);
  await vm.loadFile("scenes/world/world.scm");
  return {vm,state};
}
async function singer(vm,day){
  await vm.evaluate(`(第一幕 'load! (list
     (list "opened" #t) (list "tavern" #t) (list "act" 2)
     (list "singer" 2) (list "rehearsal-seen" #t)))`);
  await vm.evaluate("(set! world-day "+day+")");
}
async function count(vm,expr){return await vm.evaluateNumber("(length "+expr+")");}
test("theatre relation scenes are optional, only open with a real shared history",async()=>{
  const {vm}=await setup();
  await vm.evaluate("(set! world-day 9)");
  assert.equal(await count(vm,`(第二幕剧院 'nodes-at "剧院")`),0);
  await singer(vm,9);
  assert.equal(await count(vm,`(第二幕剧院 'arrivals-at "剧院")`),1);
  assert.equal(await count(vm,`(第二幕剧院 'nodes-at "剧院")`),1);
  await vm.evaluate('(第二幕剧院 \'load! (list (list "listening" "酒馆") (list "care" #t)))');
  assert.equal(await count(vm,`(第二幕剧院 'nodes-at "剧院")`),0);
});
test("sponsor replay has its own date window; if skipped, Nightingale still performed",async()=>{
  const {vm}=await setup();
  await singer(vm,10);
  assert.equal(await count(vm,`(第二幕剧院 'arrivals-at "剧院")`),1);
  await vm.evaluate('(第二幕剧院 \'load! (list (list "sponsor-intro" #t)))');
  assert.equal(await count(vm,`(第二幕剧院 'nodes-at "剧院")`),1);
  await vm.evaluate("(set! world-day 12)");
  await vm.evaluate('(第二幕剧院 \'load! (list (list "sponsor-result" "错过")))');
  assert.equal(await count(vm,`(第二幕剧院 'arrivals-at "剧院")`),1);
  assert.equal(await count(vm,`(第二幕剧院 'nodes-at "剧院")`),1);
  await vm.evaluate("(set! world-day 14)");
  assert.equal(await count(vm,`(第二幕剧院 'nodes-at "剧院")`),0);
});
test("wage and school paths do not show intimate aftermath without having met people",async()=>{
  const {vm}=await setup();
  await vm.evaluate("(set! world-day 13)");
  assert.equal(await count(vm,`(第二幕余波 'arrivals-at "码头")`),0);
  assert.equal(await count(vm,`(第二幕余波 'arrivals-at "街角咖啡馆")`),0);
  await vm.evaluate('(第一幕 \'on-work! "码头" \'好)');
  await vm.evaluate('(第一幕 \'on-work! "街角咖啡馆" \'好)');
  assert.equal(await count(vm,`(第二幕余波 'arrivals-at "码头")`),1);
  assert.equal(await count(vm,`(第二幕余波 'arrivals-at "街角咖啡馆")`),1);
  await vm.evaluate('(第二幕余波 \'load! (list (list "eddie-seen" #t) (list "lucy-seen" #t)))');
  assert.equal(await count(vm,`(第二幕余波 'arrivals-at "码头")`),0);
  assert.equal(await count(vm,`(第二幕余波 'arrivals-at "街角咖啡馆")`),0);
});
test("sponsor encounter has one deadline, progress and three distinct helping methods",async()=>{
  const {vm}=await setup();
  await vm.loadFile("scenes/encounters/赞助人试演.scm");
  const node=convertNode(await vm.evaluate("(get-render-data)"));
  assert.equal(node.name,"赞助人试演");
  const names=node.children.map(x=>x.name);
  assert.ok(names.includes("检修伴奏"));
  assert.ok(names.includes("找旧乐手救场"));
  assert.ok(names.includes("争取清唱"));
  assert.ok(names.includes("交还给夜莺"));
  const clocks=node.children.filter(x=>x.resolve?.noteClock).map(x=>x.resolve.noteClock.label);
  assert.ok(clocks.includes("试演剩余时间"));
  assert.ok(clocks.includes("抢修进展"));
});

test("city announcement is public, but intimate reactions follow separately known characters",async()=>{
  const {vm}=await setup();
  await vm.evaluate("(set! world-day 13)");
  assert.equal(await count(vm,`(第二幕公告 'arrivals-at "剧院")`),0);
  assert.equal(await count(vm,`(第二幕公告 'arrivals-at "码头")`),0);
  assert.equal(await count(vm,`(第二幕公告 'arrivals-at "街角咖啡馆")`),0);
  await singer(vm,13);
  await vm.evaluate('(第一幕 \'on-work! "码头" \'好)');
  await vm.evaluate('(第一幕 \'on-work! "街角咖啡馆" \'好)');
  assert.equal(await count(vm,`(第二幕公告 'arrivals-at "剧院")`),1);
  assert.equal(await count(vm,`(第二幕公告 'arrivals-at "码头")`),1);
  assert.equal(await count(vm,`(第二幕公告 'arrivals-at "街角咖啡馆")`),1);
  await vm.evaluate('(第二幕公告 \'load! (list (list "eddie" #t) (list "lucy" #t) (list "singer" #t) (list "answer" "质疑")))');
  assert.equal(await count(vm,`(第二幕公告 'arrivals-at "剧院")`),0);
  assert.equal(await count(vm,`(第二幕公告 'arrivals-at "码头")`),0);
  assert.equal(await count(vm,`(第二幕公告 'nodes-at "剧院")`),0);
  assert.equal(await vm.evaluateNumber('(if (equal? (第二幕公告 \'singer-answer) "质疑") 1 0)'),1);
});
test("wage dispute and school application require an actual introduction",async()=>{
  const {vm}=await setup();
  await singer(vm,9);
  assert.equal(await count(vm,`(第二幕 'nodes-at "码头")`),0);
  assert.equal(await count(vm,`(第二幕 'nodes-at "街角咖啡馆")`),0);
  await vm.evaluate('(第一幕 \'on-work! "码头" \'好)');
  await vm.evaluate('(第一幕 \'on-work! "街角咖啡馆" \'好)');
  assert.equal(await count(vm,`(第二幕 'arrivals-at "码头")`),1);
  assert.equal(await count(vm,`(第二幕 'arrivals-at "街角咖啡馆")`),1);
  await vm.evaluate('(第二幕 \'load! (list (list "wage-introduced" #t) (list "lucy-introduced" #t)))');
  assert.equal(await count(vm,`(第二幕 'nodes-at "码头")`),1);
  assert.equal(await count(vm,`(第二幕 'nodes-at "街角咖啡馆")`),2);
  await vm.evaluate("(set! world-day 12)");
  assert.equal(await count(vm,`(第二幕 'nodes-at "码头")`),0);
});

test("a strong wage roll does not skip the worker's job-at-risk decision",async()=>{
  const {vm,state}=await setup();
  await vm.loadFile("scenes/encounters/少了十块钱.scm");
  await vm.evaluate("(on-encounter-enter)");
  state.beginActionReport();
  await vm.evaluate("(推进! 3 0)");
  state.takeActionReport();
  assert.equal(await vm.evaluateNumber("(if 危机? 1 0)"),1);
  const node=convertNode(await vm.evaluate("(get-render-data)"));
  const names=node.children.map(x=>x.name);
  assert.ok(names.includes("先让艾迪退开"));
  assert.ok(names.includes("请弗兰克出面"));
  assert.ok(names.includes("继续坚持补钱"));
});


test("act two adds accessible street and theater routines without friendship gate",async()=>{
  const {vm}=await setup();
  assert.equal(await count(vm,`(第二幕生活 'nodes-at "码头居民区")`),0);
  await singer(vm,7);
  assert.equal(await count(vm,`(第二幕生活 'nodes-at "码头居民区")`),4);
  assert.equal(await count(vm,`(第二幕生活 'nodes-at "剧院")`),1);
  assert.equal(await count(vm,`(第二幕生活 'arrivals-at "码头居民区")`),1);
  await vm.evaluate("(set! world-day 8)");
  assert.equal(await count(vm,`(第二幕生活 'arrivals-at "码头居民区")`),2);
  await vm.evaluate(`(第二幕生活 'load! (list (list "street" #t) (list "frank" #t) (list "theater" #t) (list "soup-day" 8)))`);
  assert.equal(await count(vm,`(第二幕生活 'arrivals-at "码头居民区")`),0);
  assert.equal(await count(vm,`(第二幕生活 'arrivals-at "剧院")`),0);
});

test("second act things-to-do distinguishes public plans from private matters",async()=>{
  const {vm}=await setup();
  await singer(vm,7);
  assert.equal(await count(vm,`(第二幕 'dossier)`),0);
  assert.equal(await count(vm,`(第二幕生活 'dossier)`),2);
  assert.equal(await count(vm,`(第二幕剧院 'dossier)`),1);
  assert.equal(await count(vm,`(第二幕生活 'nodes-at "家")`),1);
  await vm.evaluate('(第二幕生活 \'load! (list (list "remembered-card" #t)))');
  assert.equal(await count(vm,`(第二幕生活 'nodes-at "家")`),0);
  await vm.evaluate("(set! world-day 9)");
  assert.equal(await count(vm,`(第二幕 'dossier)`),0);
  await vm.evaluate('(第二幕 \'load! (list (list "wage-introduced" #t) (list "lucy-introduced" #t)))');
  assert.equal(await count(vm,`(第二幕 'dossier)`),2);
  await vm.evaluate('(第二幕剧院 \'load! (list (list "sponsor-intro" #t)))');
  assert.equal(await count(vm,`(第二幕剧院 'dossier)`),1);
});
