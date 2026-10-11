import test from "node:test";
import assert from "node:assert/strict";
import {parseTheatre} from "../src/runtime/theatre-contract.ts";
import {TheatrePlayback} from "../src/runtime/theatre-playback.ts";

function scene(program){
  const object=["尼尔","","group",[],"#FFFFFF","","",[["x",10]]];
  return parseTheatre([[1600,900,"#0A0B0C",[object]],program]);
}
test("deterministic theatre timeline tweens and parallel tasks respect original time budget",()=>{
  const player=new TheatrePlayback(scene(["sequence",
    ["parallel",["tween","尼尔","x",[2,110,"linear"]],["wait",1]],
    ["wait",1]
  ]));
  player.start();
  assert.equal(player.objects.get("尼尔").values.x,10);
  player.tick(1);
  assert.equal(player.objects.get("尼尔").values.x,60);
  assert.equal(player.complete,false);
  player.tick(1);
  assert.equal(player.objects.get("尼尔").values.x,110);
  assert.equal(player.complete,false);
  player.tick(1);
  assert.equal(player.complete,true);
});
test("theatre say pauses until acknowledgment and reveals all text before progressing",()=>{
  const player=new TheatrePlayback(scene(["sequence",
    ["say","夜莺","今天下雨了。"],["wait",1]]));
  player.start();player.tick(10);
  assert.equal(player.complete,false);
  assert.equal(player.caption.text,"今天下雨了。");
  player.advance();
  assert.equal(player.complete,false);
  assert.equal(player.visibleCharacters,player.caption.text.length);
  player.advance();player.tick(1);
  assert.equal(player.complete,true);
});
test("during stops finite/infinite background once main cue completes",()=>{
  const player=new TheatrePlayback(scene(["during",["wait",2],
    ["loop",["sequence",["tween","尼尔","x",[0.5,20,"linear"]],["tween","尼尔","x",[0.5,10,"linear"]]]]
  ]));
  player.start();player.tick(2);
  assert.equal(player.complete,true);
});
test("timed caption and image swap retain audio operations in authored order",()=>{
  const player=new TheatrePlayback(scene(["sequence",
    ["caption-for","夜莺","别走",1,"#FFFFFF"],
    ["sound","footsteps","Sounds/step",true,0.5,-0.2],
    ["stop-sound","footsteps"]]));
  player.start();player.tick(1);
  assert.equal(player.complete,true);
  assert.equal(player.audioEvents.length,2);
  assert.deepEqual(player.audioEvents.map(e=>e.kind),["sound","stop-sound"]);
});
