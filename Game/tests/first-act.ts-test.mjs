import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {convertNode,schemeList} from "../src/runtime/node-converter.ts";
import {GameActionRunner} from "../src/runtime/action-runner.ts";
import {ActionTurnState} from "../src/runtime/turn-state.ts";

const root="../UnityClient/Assets/Resources/Content",sources={};
function scan(path){
  for(const item of readdirSync(join(root,path),{withFileTypes:true})){
    const key=join(path,item.name).replaceAll("\\","/");
    if(item.isDirectory())scan(key);
    else if(item.isFile()&&key.endsWith(".scm"))sources[key]=readFileSync(join(root,key),"utf8");
  }
}
scan("scripts");scan("scenes");
const standard=readFileSync("node_modules/lips/dist/std.xcb");
async function session(){
 const state=new GameRuntimeState();
 const vm=await GameScriptSession.create("first-act-smoke",state,sources,standard);
 await vm.loadFile("scenes/world/world.scm");
 return {state,vm};
}
test("new first-act world starts from livelihood, not the retired blackmail story",async()=>{
 const {vm}=await session();
 assert.equal(await vm.evaluateNumber("world-day"),1);
 assert.equal(await vm.evaluateNumber("(第一幕 'act)"),1);
 const rootNode=convertNode(await vm.evaluate("(get-render-data)"));
 assert.equal(rootNode.name,"世界");
 assert.ok(rootNode.children.length>0);
 const dossier=await vm.evaluate("(get-dossier)");
 assert.ok(Array.isArray(schemeList(dossier)));
 assert.equal(await vm.evaluateNumber("(if (bound? 'three-letters) 1 0)"),0);
});
test("Eddie can be met through work and optional rainy invitation is a date window",async()=>{
 const {vm}=await session();
 assert.equal(await vm.evaluateNumber("(if (第一幕 'eddie-met?) 1 0)"),0);
 await vm.evaluate('(第一幕 \'load! (list (list "opened" #t) (list "tavern" #t) (list "singer" 1)))');
 await vm.evaluate("(第一幕 'on-work! \"码头\" '好)");
 assert.equal(await vm.evaluateNumber("(if (第一幕 'eddie-met?) 1 0)"),1);
 await vm.evaluate("(set! world-day 4)");
 assert.equal(await vm.evaluateNumber("(length (first-act-walk 'nodes-at \"老街酒馆\"))"),1);
 await vm.evaluate("(set! world-day 7)");
 assert.equal(await vm.evaluateNumber("(length (first-act-walk 'nodes-at \"老街酒馆\"))"),0);
});

test("rainy aftermath follows Nightingale to the theatre after the tavern farewell",async()=>{
 const {vm}=await session();
 await vm.evaluate(`(first-act-walk 'load!
  (list (list "status" "已结束") (list "result" "抵达")
    (list "ignored?" #f) (list "cared?" #t)
    (list "aftermath?" #f) (list "final-fear" 1)))`);
 await vm.evaluate("(set! world-day 7)");
 assert.equal(await vm.evaluateNumber('(length (first-act-walk \'arrivals-at "老街酒馆"))'),0);
 assert.equal(await vm.evaluateNumber('(length (first-act-walk \'arrivals-at "剧院"))'),1);
});

test("first act: singing costs one die, once per day; it can advance a Nightingale scene",async()=>{
 const {vm}=await session();
 await vm.evaluate('(第一幕 \'load! (list (list "opened" #t) (list "tavern" #t)))');
 const before=await vm.evaluateNumber("(第一幕 'singer-stage)");
 assert.equal(before,0);
 await vm.evaluate(`
   (let* ((cards (第一幕 'nodes-at "老街酒馆"))
          (song (car (reverse cards)))
          (resolve (get-kwarg (cddr song) :resolve #f)))
     ((cadr (cadr resolve))))`);
 assert.equal(await vm.evaluateNumber("(第一幕 'singer-stage)"),1);
 assert.equal(await vm.evaluateNumber("(if (= (第一幕 'listened-day) world-day) 1 0)"),1);
 const disabled=await vm.evaluateNumber(`
   (let* ((cards (第一幕 'nodes-at "老街酒馆"))
          (song (car (reverse cards)))
          (disabled (get-kwarg (cddr song) :disabled #f)))
     (if disabled 1 0))`);
 assert.equal(disabled,1);
});
test("first act: day-six farewell really prevents ending the day until watched",async()=>{
 const {vm,state}=await session();
 await vm.evaluate('(第一幕 \'load! (list (list "opened" #t) (list "tavern" #t)))');
 await vm.evaluate("(set! world-day 6)");
 await vm.evaluate("(on-turn-start)");
 assert.ok(state.hasRestBlockers());
 await vm.evaluate(`
   (let* ((cards (第一幕 'nodes-at "老街酒馆"))
          (farewell (car (reverse cards)))
          (resolve (get-kwarg (cddr farewell) :resolve #f)))
     ((cadr (cadr resolve))))`);
 assert.equal(state.hasRestBlockers(),false);
 assert.equal(await vm.evaluateNumber("(if (第一幕 'farewell-seen?) 1 0)"),1);
});

test("Eddie gives a real rescue: -2 then +1 composure, once each working day",async()=>{
 const {vm,state}=await session();
 await vm.evaluate("(第一幕 'meet-eddie!)");
 assert.equal(await vm.evaluateNumber("(第一幕 'eddie-stage)"),1);
 const badHaul=`
   (let* ((place (car (dock 'render-data)))
          (haul (car (get-kwarg (cddr place) :children '())))
          (resolve (get-kwarg (cddr haul) :resolve #f))
          (bad-outcome (list-ref resolve 3)))
      ((cadr bad-outcome)))`;
 assert.equal(state.team.composure("player"),5);
 // The authored dock failure calls (current-actor). C# only exposes it
 // during SceneManager.ExecuteAction, never from an arbitrary Scheme REPL.
 // Execute the outcome under that same native action context.
 const runBadHaul=async()=>{
   state.beginActionContext("player","world");
   state.beginActionReport();
   try {
     await vm.evaluate(badHaul);
     state.takeActionReport();
   }catch(error){
     state.cancelActionReport();
     throw error;
   }finally{
     state.clearActionContext();
   }
 };
 await runBadHaul();
 assert.equal(state.team.composure("player"),4);
 assert.equal(await vm.evaluateNumber('(assoc-get (dock \'save) "help-day" 0)'),1);
 await runBadHaul();
 assert.equal(state.team.composure("player"),2);
 assert.throws(()=>state.currentActor(),/only valid inside action/);
});

// Full authored card integration: a dock failure must be resolved via the
// action runner, not by invoking the Scheme effect closure as an REPL command.
// This tests the same action-context + dice-spending path as the real UI.
test("real dock card: action runner binds current actor and Eddie saves one composure",async()=>{
 const {vm,state}=await session();
 await vm.evaluate("(第一幕 'meet-eddie!)");
 state.team.rollDice(()=>0,false,true,state.injury.actionSlotLoss);
 const runner=await GameActionRunner.create(vm,state,new ActionTurnState(()=>0));
 const root=await runner.prepare('(node "世界" :children (dock \'render-data))');
 const dock=root.children.find(x=>x.name==="码头");
 assert.ok(dock?.children.some(x=>x.name==="搬运"));
 const dice=state.team.dice("player");
 assert.equal(dice.length,4);
 const first=await runner.performSlots("搬运",[{type:"die",actorId:"player",slotId:dice[0].slotId}]);
 assert.equal(first.outcome,"fail");
 assert.equal(state.team.composure("player"),4);
 assert.equal(state.team.dice("player").length,3);
 assert.equal(await vm.evaluateNumber('(assoc-get (dock \'save) "help-day" 0)'),1);
 assert.throws(()=>state.currentActor(),/only valid inside action/);
 await runner.prepare('(node "世界" :children (dock \'render-data))');
 const second=await runner.performSlots("搬运",[{type:"die",actorId:"player",slotId:dice[1].slotId}]);
 assert.equal(second.outcome,"fail");
 assert.equal(state.team.composure("player"),2);
 assert.equal(state.team.dice("player").length,2);
});
