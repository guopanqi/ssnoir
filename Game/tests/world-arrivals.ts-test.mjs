import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {GameActionRunner} from "../src/runtime/action-runner.ts";
import {ActionTurnState} from "../src/runtime/turn-state.ts";

const base="../UnityClient/Assets/Resources/Content";
const sources={};
function scan(directory){
  for(const entry of readdirSync(join(base,directory),{withFileTypes:true})){
    const key=join(directory,entry.name).replaceAll("\\","/");
    if(entry.isDirectory())scan(key);
    else if(entry.isFile()&&key.endsWith(".scm"))
      sources[key]=readFileSync(join(base,key),"utf8");
  }
}
scan("scripts");scan("scenes");

test("day-five world root arrival announces the farewell once without changing dice",async()=>{
  const scene=await GameSceneDirector.create(sources,()=>0.5);
  const dice=scene.state.team.diceSnapshot();
  await scene.world.evaluate("(set! world-day 5) (set-global! '世界日 5)");
  const root=await scene.worldActions.enterPlace("世界");
  assert.equal(root.name,"世界");
  const events=scene.state.takeStoryEvents();
  assert.match(JSON.stringify(events),/明晚是夜莺在这里的最后一场演出/);
  assert.deepEqual(scene.state.team.diceSnapshot(),dice);
  await scene.worldActions.enterPlace("世界");
  assert.deepEqual(scene.state.takeStoryEvents(),[],"The one-shot announcement cannot repeat");
});

test("an authored arrival may reset a clock and collect rent once without losing inventory or narration",async()=>{
  const state=new GameRuntimeState();
  const scripts=await GameScriptSession.create("arrival-clock",state,{
    "scripts/stdlib.scm":sources["scripts/stdlib.scm"],
    "scripts/engine.scm":sources["scripts/engine.scm"],
    "scripts/theatre.scm":sources["scripts/theatre.scm"]
  });
  const actions=await GameActionRunner.create(scripts,state,new ActionTurnState(()=>0.5));
  await scripts.evaluate("(define test-rent-clock (make-clock \"住宿期限\" 4 'countdown \"下一次房租\" \"住宿期限\"))\n(test-rent-clock 'set! 0)\n(define test-rent-collected? #f)\n(define (get-render-data)\n  (node \"世界\" :children\n    (list (place \"家\"\n      :arrivals (if test-rent-collected? '()\n        (list (arrival \"旅馆收租\" (lambda ()\n          (set! test-rent-collected? #t)\n          (test-rent-clock 'set! 4)\n          (remove-item! \"金钱\" 10)\n          (play-dialogue! (line \"旅馆老板\" \"收下十元租金。\"))))))\n      :children '()))))");
  const diceBefore=state.team.diceSnapshot();
  await actions.prepare();
  await actions.enterPlace("家");
  assert.equal(state.getItemCount("金钱"),5,"The actual rent cost must commit");
  assert.equal(String(await scripts.evaluate("(test-rent-clock 'current)")),"4");
  assert.equal(state.isActionReporting(),false);
  assert.deepEqual(state.team.diceSnapshot(),diceBefore,
    "Navigation does not consume action dice");
  assert.match(JSON.stringify(state.takeStoryEvents()),/收下十元租金/,
    "The original rent dialogue must be delivered");
  await actions.enterPlace("家");
  assert.equal(state.getItemCount("金钱"),5,"Collection happens once");
  assert.deepEqual(state.takeStoryEvents(),[]);
});
