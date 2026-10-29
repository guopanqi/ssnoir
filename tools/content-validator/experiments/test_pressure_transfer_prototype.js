#!/usr/bin/env node
"use strict";
/* Abstract UI correctness check only. Not human play or psychological proof. */
const assert=require("node:assert/strict");
const fs=require("node:fs"),vm=require("node:vm"),path=require("node:path");
const html=fs.readFileSync(path.resolve(__dirname,"../../../experiments/抽象决策关系研究/批次/2026-10-13-压力转移与隐藏触发/压力分配.html"),"utf8");
const scripts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
assert.equal(scripts.length,1);
const script=new vm.Script(scripts[0][1]);
const BUTTONS={"WORK_A":"work-a","WORK_B":"work-b","MOVE_A_TO_B":"move-a",
 "MOVE_B_TO_A":"move-b","ERASE_A":"erase-a","ERASE_B":"erase-b","SIDE":"small"};
function replay(mode,audits,actions){
 const elements=new Map();
 const document={getElementById(id){
  if(!elements.has(id))elements.set(id,{id,value:"",textContent:"",style:{},disabled:false,onclick:null});
  return elements.get(id);
 }};
 let nth=0;
 const random=Object.create(Math);
 random.random=()=>audits[nth++]==="A"?0:0.999999;
 script.runInNewContext({document,Math:random,navigator:{clipboard:{writeText:async()=>{}}}},{timeout:2000});
 const get=id=>document.getElementById(id);
 get("mode").value=mode;get("risk").value="0.5";
 get("begin").onclick();
 assert.match(get("record").textContent,/A、B初始进度均1\/2/);
 for(const action of actions){
  const btn=get(BUTTONS[action]);
  assert.equal(btn.disabled,false,"illegal "+mode+" "+actions.join("/")+" aud:"+audits.join(""));
  get("why").value="这是第"+(actions.indexOf(action)+1)+"次思考";
  btn.onclick();
 }
 const m=get("record").textContent.match(/总成果=(\d+)；A=(\d+) \/ 2，B=(\d+) \/ 2，SIDE=(\d+)/);
 if(actions.length===4)assert.ok(m,"no ending "+mode+" "+actions);
 return {get,score:m?Number(m[1]):null,terminal:m?[...m].slice(2).map(Number):null,trace:get("record").textContent};
}
function expected(actions,audits){
 let a=1,b=1,ha=0,hb=0,side=0;
 for(let i=0;i<actions.length;i++){
  const act=actions[i];
  if(act==="WORK_A"){a=Math.min(2,a+1);ha=Math.min(2,ha+1);}
  if(act==="WORK_B"){b=Math.min(2,b+1);hb=Math.min(2,hb+1);}
  if(act==="MOVE_A_TO_B"){ha--;hb=Math.min(2,hb+1);}
  if(act==="MOVE_B_TO_A"){hb--;ha=Math.min(2,ha+1);}
  if(act==="ERASE_A")ha--;
  if(act==="ERASE_B")hb--;
  if(act==="SIDE")side++;
  if(audits[i]==="A"&&ha>0){a=Math.max(0,a-1);ha--;}
  if(audits[i]==="B"&&hb>0){b=Math.max(0,b-1);hb--;}
 }
 return [side+(a===2?12:0)+(b===2?9:0),a,b,side];
}
let total=0;
function all(mode,audits,prefix=[]){
 const r=replay(mode,audits,prefix);
 if(prefix.length===4){
  assert.deepEqual([r.score,...r.terminal],expected(prefix,audits));
  total++;
  return;
 }
 for(const [action,id] of Object.entries(BUTTONS)){
  if(!r.get(id).disabled)all(mode,audits,prefix.concat(action));
 }
}
for(const mode of ["move","none","erase","both"]){
 for(const sequence of ["AAAA","BBBB","ABAB","BABA"]){
  all(mode,sequence.split(""));
 }
}
const example=replay("move",["B","A","A","B"],["WORK_A","MOVE_A_TO_B"]);
assert.match(example.trace,/第2手：MOVE_A_TO_B/);
assert.equal(replay("move",["A","A","A","A"],["WORK_A"]).get("move-a").disabled,true,
  "after A is audited, there should be no remaining A heat to move");
assert.ok(total>=300,"expected broad legal history coverage, got "+total);
console.log("PASS PT01 pressure prototype: "+total+" four-action legal histories across all modes and fixed/alternating audits");
