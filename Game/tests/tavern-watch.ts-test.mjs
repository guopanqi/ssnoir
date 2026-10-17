import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { GameRuntimeState } from "../src/runtime/game-state.ts";
import { GameScriptSession } from "../src/runtime/script-session.ts";
import { convertNode } from "../src/runtime/node-converter.ts";

const root="../UnityClient/Assets/Resources/Content";
const sources={};
function walk(path){
  for(const entry of readdirSync(join(root,path),{withFileTypes:true})){
    const key=join(path,entry.name).replaceAll("\\","/");
    if(entry.isDirectory())walk(key);
    else if(entry.isFile()&&key.endsWith(".scm"))
      sources[key]=readFileSync(join(root,key),"utf8");
  }
}
walk("scripts");walk("scenes");
const standard=readFileSync("node_modules/lips/dist/std.xcb");
let id=0;
async function world(){
  const vm=await GameScriptSession.create("tavern-watch-"+(++id),new GameRuntimeState(),sources,standard);
  await vm.loadFile("scenes/world/world.scm");
  return vm;
}
async function tavern(vm){
  return convertNode(await vm.evaluate("(car (old-street-tavern 'render-data))"));
}
test("tavern trouble exposes urgency and the SAME clock before and after entering its container",async()=>{
  const vm=await world();
  await vm.evaluate('(old-street-tavern \'load! (list (list "watch-first?" #f) (list "watch-trouble-day" 1) (list "watch-trouble" "醉客纠缠") (list "watch-progress" 2)))');
  const scene=await tavern(vm);
  const node=scene.children.find(x=>x.name==="闹事：醉客纠缠");
  assert.ok(node,"ongoing bouncer trouble should be present");
  assert.deepEqual(node.tags,["紧急"],"parent container must expose urgency to the plate renderer");
  assert.equal(node.clocks.length,1,"collapsed container should show the progress badge");
  assert.equal(node.clocks[0].label,"处理麻烦");
  const clocks=node.children.filter(x=>x.resolve?.noteClock?.label==="处理麻烦");
  assert.equal(clocks.length,1,"expanded container should show a single child clock");
  assert.equal(node.clocks[0].current,2);
  assert.equal(clocks[0].resolve.noteClock.current,2);
  assert.equal(node.clocks[0].max,3);
  assert.equal(clocks[0].resolve.noteClock.max,3);
  assert.deepEqual(node.clocks[0],clocks[0].resolve.noteClock,"two views of the same clock must agree");
  assert.deepEqual(node.children.filter(x=>x.resolve?.kind==="roll").map(x=>x.name),
    ["劝他离席","请他出去"]);
  await vm.evaluate('(old-street-tavern \'load! (list (list "watch-first?" #f) (list "watch-trouble" "") (list "watch-progress" 0)))');
  assert.ok(!(await tavern(vm)).children.some(x=>x.name==="闹事：醉客纠缠"));
});
test("every watch and foreman disturbance has authored spatial noise; first explains duty through narration",()=>{
  const content=sources["scenes/world/老街酒馆.scm"];
  for(const trouble of ["醉客纠缠","门口争执","赖账客人","醉客闹事","账目短缺","后门斗殴"])
    assert.ok(content.includes(`(equal? kind "${trouble}")`),`missing noise variant: ${trouble}`);
  assert.match(content,/play-watch-trouble-noise! watch-trouble first\?/);
  assert.match(content,/play-foreman-trouble-noise! tavern-trouble/);
  assert.match(content,/play-narration! "酒馆看场职责"/);
  assert.doesNotMatch(content,/你负责看场，这种事就得你去处理/);
  const narration=JSON.parse(readFileSync("../UnityClient/Assets/Resources/Narrations/酒馆看场职责.json","utf8"));
  assert.match(narration.cues[0].text,/这事得我摆平/);
  assert.match(narration.cues[0].text,/扣我五块钱/);
  assert.match(content,/:at "老街酒馆-工作"/);
  assert.match(content,/:at "老街酒馆"/);
});
test("rainy encounter opens without old intertitle and finishes with voiced epilogue narration",()=>{
  const source=sources["scenes/world/三封信.scm"];
  const start=source.indexOf("(define (receive-night-visit!)");
  const end=source.indexOf("(define (node-answer-door)",start);
  const visit=source.slice(start,end);
  assert.ok(start>=0&&end>start);
  assert.doesNotMatch(visit,/play-intertitle!/);
  assert.ok(visit.indexOf("(rainy-door-theatre!)")<visit.indexOf("(commission-dialogue!)"));
  assert.match(source,/\(play-narration! "序章接案后"\)/);
  const cues=JSON.parse(readFileSync("../UnityClient/Assets/Resources/Narrations/序章接案后.json","utf8")).cues;
  assert.ok(cues.some(x=>x.text.includes("她的确需要帮助")));
  assert.ok(cues.some(x=>x.text.includes("那是我人生的转折点")));
  assert.ok(cues.every(x=>x.audio),"current narration has newly generated cue audio");
});
