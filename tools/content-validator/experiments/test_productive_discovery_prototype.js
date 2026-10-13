#!/usr/bin/env node
"use strict";
/* Browser-free interaction check, NOT an actual human or visual playtest. */
const fs=require("node:fs"),assert=require("node:assert/strict"),
      vm=require("node:vm"),path=require("node:path");
const html=fs.readFileSync(path.resolve(__dirname,"../../../docs/实验/机制研究/批次/2026-10-13-目标生成与生产性发现/试玩.html"),"utf8");
const scripts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
assert.equal(scripts.length,1);
new vm.Script(scripts[0][1]);
const button={A:"work-a",B:"work-b",INSPECT:"inspect",SIDE:"side",ADAPT:"adapt"};
function replay(variant,truth,actions=[]){
 const elems=new Map();
 const document={getElementById(id){
  if(!elems.has(id))elems.set(id,{id,onclick:null,textContent:"",value:"",style:{},disabled:false});
  return elems.get(id);
 }};
 vm.runInNewContext(scripts[0][1],{document,Math,navigator:{clipboard:{writeText:async()=>{}}}},{timeout:3000});
 const $=id=>document.getElementById(id);
 $("variant").value=variant;
 $(truth==="A"?"force-a":"force-b").onclick();
 assert.match($("info").textContent,/未知|不知道/);
 for(const name of actions){
  const id=button[name];
  assert.ok(id,"unknown action "+name);
  assert.equal($(id).disabled,false,variant+" "+truth+" "+actions.join("/"));
  $(id).onclick();
 }
 const log=$("trace").textContent;
 const score=actions.length===3?Number(log.match(/最终成果=(\d+)/)?.[1]):null;
 if(actions.length===3)assert.ok(Number.isFinite(score),log);
 return {document,$,log,score};
}
const reference=[
 ["early-free","A",["A","A","SIDE"],10],
 ["early-free","B",["A","B","B"],8],
 ["early-free","A",["A","B","B"],0],
 ["late-free","A",["INSPECT","A","A"],8],
 ["late-free","B",["INSPECT","B","B"],8],
 ["early-tax","A",["INSPECT","A","A"],8],
 ["early-tax","B",["INSPECT","B","B"],8],
 ["early-tax","B",["A","SIDE","SIDE"],4]
];
for(const [version,truth,actions,expected] of reference)
 assert.equal(replay(version,truth,actions).score,expected);
assert.equal((10+8)/2,9);
assert.equal((8+8)/2,8);
const early=replay("early-free","B",["A"]);
assert.match(early.log,/正确目标是 B/);
const late=replay("late-free","B",["A"]);
assert.doesNotMatch(late.log,/正确目标是 B/);
assert.match(replay("late-free","B",["A","A"]).log,/正确目标是 B/);
assert.match(replay("late-free","B",["INSPECT"]).log,/正确目标是 B/);
const taxed=replay("early-tax","B",["A"]);
assert.equal(taxed.$("work-b").disabled,true,"switch forbidden before ADAPT");
assert.equal(taxed.$("adapt").disabled,false);
assert.equal(replay("early-tax","B",["A","ADAPT"]).$("work-b").disabled,false);
const normal=replay("early-free","B",["A"]);
assert.equal(normal.$("work-b").disabled,false);
let total=0;
const actions=Object.keys(button);
function exhaust(variant,truth,prefix=[]){
 if(prefix.length===3){
  const t=replay(variant,truth,prefix);
  assert.ok(t.score>=0&&t.score<=14);
  total++;
  return;
 }
 const probe=replay(variant,truth,prefix);
 for(const a of actions)
  if(!probe.$(button[a]).disabled)exhaust(variant,truth,prefix.concat(a));
}
for(const v of ["early-free","late-free","early-tax"])
 for(const truth of ["A","B"])exhaust(v,truth);
assert.ok(total>=100,"insufficient legal trajectory coverage: "+total);
console.log("PASS DISC-01 DOM: "+reference.length+" reference paths, timely observation, switch legality, "+total+" legal three-action histories");
