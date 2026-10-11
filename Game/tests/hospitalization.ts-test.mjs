import test from "node:test";
import assert from "node:assert/strict";
import { GameRuntimeState } from "../src/runtime/game-state.ts";
import { createNativeBridge } from "../src/runtime/native-bridge.ts";

test("7/7 injury resolves hospitalization immediately; wealth pays 80 and leaves one permanent scar",()=>{
 const s=new GameRuntimeState();s.setGlobal("世界日",4);s.setItemCount("金钱",100);
 const n=createNativeBridge(s);n["__injure!"](7);
 assert.equal(s.hasPendingHospitalization(),true);
 assert.equal(s.injury.severity,0);
 assert.equal(s.injury.scarCount(),1);
 assert.equal(s.injury.scars[0].day,4);
 assert.equal(s.getItemCount("金钱"),20);
 assert.equal(s.team.composure("player"),0);
 assert.equal(n["__hospitalization-pending?"](),true);
 assert.match(s.consumeHospitalization().message,/80 金/);
 assert.equal(s.hasPendingHospitalization(),false);
});
test("can't afford treatment: cash zero, doubled same-part scars; pending blocks more injury",()=>{
 const s=new GameRuntimeState(),n=createNativeBridge(s);
 n["__injure!"](7);
 assert.equal(s.getItemCount("金钱"),0);
 assert.equal(s.injury.scarCount(),2);
 assert.equal(s.injury.scars[0].part,s.injury.scars[1].part);
 assert.throws(()=>n["__injure!"](1),/hospitalization must end/);
 assert.equal(s.consumeHospitalization().paid,15);
});
test("protagonist composure overflow is 1:1 injury; companions withdraw without player injury",()=>{
 const s=new GameRuntimeState(),n=createNativeBridge(s);
 s.team.recruit("eddie","艾迪",{violence:0,knowledge:0,sharpness:0,social:0});
 n["__spend-actor-composure!"]("eddie",2);
 assert.equal(s.team.status("eddie"),"away");
 assert.equal(s.injury.severity,0);
 n["__spend-actor-composure!"]("player",6);
 assert.equal(s.team.composure("player"),0);
 assert.equal(s.injury.severity,1);
});
