import test from "node:test";
import assert from "node:assert/strict";
import { TeamState } from "../src/runtime/team-state.ts";
import { GameRuntimeState } from "../src/runtime/game-state.ts";
import { createNativeBridge } from "../src/runtime/native-bridge.ts";

const stats={violence:1,knowledge:0,sharpness:-1,social:2};
test("C# protagonist initial stats, five composure and stable actor identity",()=>{
  const team=new TeamState();
  assert.equal(team.stat("player","violence"),-1);
  assert.equal(team.stat("player","knowledge"),1);
  assert.equal(team.composure("player"),5);
  assert.deepEqual(team.roster()[0].actionDice,[]);
  assert.throws(()=>team.dismiss("player"));
});
test("companion time budget, away status and daily refill; protagonist retains lost composure",()=>{
  const team=new TeamState();
  team.recruit("eddie","艾迪",stats);
  assert.equal(team.composure("eddie"),2);
  assert.equal(team.spend("eddie",2),0);
  assert.equal(team.status("eddie"),"away");
  assert.equal(team.spend("player",7),2);
  assert.equal(team.composure("player"),0);
  assert.deepEqual(team.beginCityDay(),["艾迪"]);
  assert.equal(team.composure("eddie"),2);
  assert.equal(team.composure("player"),0);
  team.restoreComposure("eddie",1);
  assert.equal(team.composure("eddie"),2);
});
test("relationship support is unique, first auto-carried; cannot carry unearned",()=>{
  const team=new TeamState();team.grantSupport("林");team.grantSupport("林");team.grantSupport("夜莺");
  assert.deepEqual(team.getSupports(),["林","夜莺"]);
  assert.equal(team.getCarriedSupport(),"林");
  assert.throws(()=>team.setCarriedSupport("弗兰克"));
  team.setCarriedSupport("夜莺");assert.equal(team.getCarriedSupport(),"夜莺");
});
test("companion die profile and permanent penalties preserve save and reject corruption",()=>{
  const team=new TeamState();team.recruit("lin","林",stats);
  team.dieProfile("lin",2,4,"机器");
  team.permanentDiePenalty("lin","旧伤",-1);
  team.applyHangover();
  const snapshot=team.snapshot();const restore=new TeamState();restore.restore(JSON.parse(JSON.stringify(snapshot)));
  assert.deepEqual(restore.snapshot(),snapshot);
  assert.throws(()=>restore.restore({...snapshot,actors:snapshot.actors.map(a=>({...a,actionDice:[3],actionDiceSlotIds:[]}))}),/die value/);
  assert.deepEqual(restore.snapshot(),snapshot);
  assert.throws(()=>team.recruit("lin","another",stats));
  team.recruit("temp","支援",stats,true);
  assert.throws(()=>team.snapshot(),/temporary/);
  team.dismissTemporary();assert.ok(team.snapshot());
});
test("native actor and support bridge is shared with typed state",()=>{
  const state=new GameRuntimeState();
  const bridge=createNativeBridge(state);
  assert.equal(bridge["__actor-composure"]("player"),5);
  assert.equal(bridge["__actor-stat"]("player","knowledge"),1);
  bridge["__grant-support!"]("弗兰克");
  assert.equal(bridge["__has-support?"]("弗兰克"),true);
  assert.equal(bridge["__carried-support"](),"弗兰克");
  bridge["__spend-actor-composure!"]("player",2);
  assert.equal(state.team.composure("player"),3);
  const snap=state.snapshot();const copy=new GameRuntimeState();copy.restore(JSON.parse(JSON.stringify(snap)));
  assert.deepEqual(copy.snapshot(),snap);
});

test("authored prologue drunk state is saved and clears only on the next city dice roll",()=>{
  const team=new TeamState();
  const state=new GameRuntimeState();const bridge=createNativeBridge(state);
  assert.equal(state.team.require("player").isDrunk,false);
  assert.throws(()=>bridge["__apply-drunk!"]("unexpected"),/expects 0/);
  bridge["__apply-drunk!"]();
  assert.equal(state.team.require("player").isDrunk,true);
  const saved=state.snapshot();
  const copy=new GameRuntimeState();
  copy.restore(JSON.parse(JSON.stringify(saved)));
  assert.equal(copy.team.require("player").isDrunk,true,"C# IsDrunk must survive world save");
  copy.team.rollDice(()=>0.5,true,true);
  assert.equal(copy.team.require("player").isDrunk,true,"encounter dice do not clear drink");
  copy.team.rollDice(()=>0.5,false,false);
  assert.equal(copy.team.require("player").isDrunk,true,"non-consuming roll does not clear drink");
  copy.team.rollDice(()=>0.5,false,true);
  assert.equal(copy.team.require("player").isDrunk,false,"next city roll ends drunkenness");
});
