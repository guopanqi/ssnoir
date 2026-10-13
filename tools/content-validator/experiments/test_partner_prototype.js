#!/usr/bin/env node
"use strict";
/* Dependency-free offline browser-logic smoke test for the ACT-01 study.
 * Checks declared A/B/HOLD/OUT transitions in HTML against exact Python model.
 */
const assert=require("node:assert/strict");
const fs=require("node:fs");
const vm=require("node:vm");
const path=require("node:path");
const file=path.resolve(__dirname,"../../../docs/实验/机制研究/批次/2026-10-13-行动权利与自主他者/试玩.html");
const html=fs.readFileSync(file,"utf8");
const scripts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
assert.equal(scripts.length,1,"standalone study must include one script");
const script=scripts[0][1];
new vm.Script(script,{filename:"ACT-01-inline.js"});
function play(signal,sequence){
 const els=new Map();
 const document={getElementById(id){
   if(!els.has(id))els.set(id,{id,textContent:"",style:{},onclick:null});
   return els.get(id);
 }};
 const sandbox={document,navigator:{clipboard:{writeText:async()=>{}}},Math,console};
 vm.runInNewContext(script,sandbox,{timeout:2500,filename:"ACT-01-inline.js"});
 const get=id=>document.getElementById(id);
 get(signal?"present":"absent").onclick();
 for(const a of sequence)get(a==="A"?"doA":"doB").onclick();
 assert.match(get("roundTitle").textContent,/结束/);
 return get("trace").textContent;
}
const absent=play(0,["A","A","A"]);
const present=play(1,["A","B","B"]);
assert.match(absent,/第1轮  你：A  协作者：HOLD/);
assert.match(absent,/第3轮  你：A  协作者：A/);
assert.match(absent,/你的成果=6  协作者成果=5/);
assert.match(present,/第2轮  你：B  协作者：HOLD/);
assert.match(present,/第3轮  你：B  协作者：OUT/);
assert.match(present,/你的成果=1  协作者成果=12/);
for(const signal of [0,1]){
  for(const a of ["A","B"])for(const b of ["A","B"])for(const d of ["A","B"]){
    const trace=play(signal,[a,b,d]);
    assert.match(trace,/第3轮  你：[AB]  协作者：(HOLD|A|B|OUT)/);
    assert.match(trace,/结束  你的成果=\\d+  协作者成果=\\d+/);
  }
}
console.log("PASS ACT-01 offline UI smoke: exact reference paths and all 16 player choice/offer combinations");
