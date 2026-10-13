#!/usr/bin/env node
"use strict";
const fs=require("node:fs"),vm=require("node:vm"),path=require("node:path"),assert=require("node:assert/strict");
const file=path.resolve(__dirname,"../../../docs/实验/机制研究/批次/2026-10-13-策略回应与危机可达性/危机试玩.html");
const html=fs.readFileSync(file,"utf8");
const scripts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
assert.equal(scripts.length,1);
new vm.Script(scripts[0][1]);
const mapping={B:"prepB",SIDE:"prepSide",A:"workA",FIX:"fix"};
function simulate(prob,crisis,path){
 const elements=new Map();
 const document={getElementById(id){
   if(!elements.has(id))elements.set(id,{id,style:{},textContent:"",value:"",disabled:false,onclick:null});
   return elements.get(id);
 }};
 const sandbox={document,Math,navigator:{clipboard:{writeText:async()=>{}}},console};
 vm.runInNewContext(scripts[0][1],sandbox,{timeout:3500});
 const el=id=>document.getElementById(id);
 const begin=(prob===.25?"low-":"high-")+(crisis?"crisis":"clear");
 el(begin).onclick();
 assert.match(el("trace").textContent,/危机结果尚未揭晓/);
 for(let i=0;i<path.length;i++){
   const action=path[i];const id=i<2?mapping[action]:({A:"workA",B:"workB",FIX:"fix",SIDE:"side"})[action];
   assert.ok(id,"unknown move "+action);
   assert.equal(el(id).disabled,false,"illegal "+path.join("/")+" at "+i);
   el(id).onclick();
 }
 const trace=el("trace").textContent;
 const m=trace.match(/最终成果=(\d+)；A完成=(true|false)；B完成=(true|false)/);
 if(path.length===4)assert.ok(m,path.join("/")+"\n"+trace);
 return {score:m?Number(m[1]):null,trace,el};
}
const examples=[
 [.25,true,["SIDE","SIDE","FIX","A"],16],
 [.25,false,["SIDE","SIDE","A","SIDE"],18],
 [.75,true,["B","B","B","B"],17],
 [.75,false,["B","B","B","B"],17],
 [.25,true,["B","SIDE","FIX","A"],12],
 [.25,false,["B","SIDE","A","SIDE"],14]
];
for(const [prob,crisis,path,expect] of examples){
 const r=simulate(prob,crisis,path);
 assert.equal(r.score,expect);
}
assert.equal(simulate(.25,true,["SIDE","SIDE"]).el("workA").disabled,true);
assert.equal(simulate(.25,false,["SIDE","SIDE"]).el("workA").disabled,false);
assert.match(simulate(.75,true,["B","B"]).trace,/危机发生/);
assert.match(simulate(.25,false,["B","B"]).trace,/没有危机/);
let count=0;
for(const prob of [.25,.75])for(const crisis of [false,true]){
 for(const x of ["B","SIDE"])for(const y of ["B","SIDE"]){
  const prefix=[x,y],next=simulate(prob,crisis,prefix);
  const first=["A","B","FIX","SIDE"].filter(a=>!next.el(({A:"workA",B:"workB",FIX:"fix",SIDE:"side"})[a]).disabled);
  for(const u of first){
   const after=simulate(prob,crisis,prefix.concat(u));
   const second=["A","B","FIX","SIDE"].filter(a=>!after.el(({A:"workA",B:"workB",FIX:"fix",SIDE:"side"})[a]).disabled);
   for(const v of second){
    const result=simulate(prob,crisis,prefix.concat(u,v));
    assert.ok(result.score>=0);
    count++;
   }
  }
 }
}
assert.ok(count>=100);
console.log("PASS X01 full-opening UI, "+count+" legal trajectories, fixed crisis/payoff controls");
