#!/usr/bin/env node
"use strict";
/* Offline DOM-action simulation for REL-01. Not a human or browser UX test. */
const assert=require("node:assert/strict");
const fs=require("node:fs");
const vm=require("node:vm");
const path=require("node:path");
const html=fs.readFileSync(path.resolve(__dirname,"../../../docs/实验/机制研究/批次/2026-10-13-关系许可与成果保全/试玩.html"),"utf8");
const parts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
assert.equal(parts.length,1);
const source=parts[0][1];
new vm.Script(source);
function run(variant,high,moves){
 const els=new Map();
 const document={getElementById(id){
  if(!els.has(id))els.set(id,{id,style:{},textContent:"",disabled:false,value:null,onclick:null});
  return els.get(id);
 }};
 const ctx={document,navigator:{clipboard:{writeText:async()=>{}}},console,Math};
 vm.runInNewContext(source,ctx,{timeout:2000});
 const get=id=>document.getElementById(id);
 get("variant").value=variant;
 get(high?"high":"low").onclick();
 for(const move of moves){
  const elem=get("act-"+move.toLowerCase());
  assert.equal(elem.disabled,false,variant+" "+high+" "+moves.join("/"));
  elem.onclick();
 }
 const trace=get("log").textContent;
 if(moves.length===4){
  const found=trace.match(/结束：你的成果=(\d+)；对方成果=(\d+)/);
  assert.ok(found,variant+" "+high+" "+trace);
  return {player:Number(found[1]),partner:Number(found[2]),trace,document};
 }
 return {trace,document};
}
const samples=[
 ["public-secure",1,["USE","SECURE","SIDE","SIDE"],9,12],
 ["public-secure",0,["USE","HELP","USE","SIDE"],18,10],
 ["public-plain",1,["SIDE","SIDE","SIDE","SIDE"],8,12],
 ["public-plain",0,["SIDE","HELP","USE","USE"],18,10],
 ["hidden-secure",1,["HELP","SIDE","SIDE","SIDE"],6,12],
 ["hidden-secure",0,["HELP","SIDE","USE","USE"],18,10],
 ["hidden-plain",1,["HELP","SIDE","SIDE","SIDE"],6,12],
 ["hidden-plain",0,["HELP","SIDE","USE","USE"],18,10],
 ["hidden-secure",1,["USE","SECURE","SIDE","SIDE"],9,12]
];
for(const [v,h,m,p,q] of samples){
 const r=run(v,h,m);
 assert.deepEqual([r.player,r.partner],[p,q],v+" "+h+" "+m);
}
const closed=run("public-secure",0,["USE","SECURE"]);
assert.equal(closed.document.getElementById("act-use").disabled,true,"SECURE must permanently close A");
assert.equal(run("public-plain",0,["USE"]).document.getElementById("act-secure").disabled,true);
// Every legal 4-turn button history under either public/hidden signal
// should terminate and present well-formed payoff records.
let count=0;
const all=["USE","HELP","SIDE","SECURE"];
function exhaust(prefix,variant,high){
 if(prefix.length===4){const r=run(variant,high,prefix);assert.ok(r.player>=0);count++;return;}
 const probe=run(variant,high,prefix);
 for(const action of all){
  if(!probe.document.getElementById("act-"+action.toLowerCase()).disabled)
   exhaust(prefix.concat(action),variant,high);
 }
}
for(const variant of ["public-secure","public-plain","hidden-secure","hidden-plain"])
 for(const high of [0,1])exhaust([],variant,high);
assert.ok(count>=80,"insufficient exhaustive UI trajectories");
console.log("PASS REL-01 offline UI: 9 reference trajectories, closed-route invariant, "+count+" legal histories");
