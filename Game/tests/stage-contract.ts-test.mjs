import test from "node:test";
import assert from "node:assert/strict";
import {parseStage} from "../src/runtime/stage-contract.ts";
const beat=(...commands)=>commands;
test("Unity stage beats validate spawn, move, dialogue and removal",()=>{
  const stage=parseStage([[
    beat(["spawn","尼尔","Portraits/Neil",1,"front"]),
    beat(["move","尼尔",4,1.5]),
    beat(["say",["尼尔","请等等","",0,null,"尼尔"]]),
    beat(["remove","尼尔"])
  ]]);
  assert.equal(stage.length,4);
  assert.equal(stage[1].seconds,1.5);
  assert.equal(stage[2].requiresInput,true);
  assert.equal(stage[2].commands[0].speaker,"尼尔");
});
test("invalid concurrent stage edits, unspawned actor and impossible positions fail",()=>{
  assert.throws(()=>parseStage([[beat(["move","尼尔",5,1])]]),/unknown target/);
  assert.throws(()=>parseStage([[
    beat(["spawn","尼尔","Portraits/Neil",1,"front"]),
    beat(["move","尼尔",10,3],["move","尼尔",12,2])
  ]]),/concurrent write/);
  assert.throws(()=>parseStage([[beat(["spawn","尼尔","Portraits/Neil",31,"front"])]]),/invalid x/);
  assert.throws(()=>parseStage([[beat(["say",["尼尔","未上场","",0,null,"尼尔"]])]]),/not on stage/);
});
