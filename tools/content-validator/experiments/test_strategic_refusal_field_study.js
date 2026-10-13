#!/usr/bin/env node
"use strict";
// Non-browser DOM action simulation: checks information hiding, 2-stage input,
// partner response and copied trail. Not evidence of human comprehension.
const fs=require("node:fs"),vm=require("node:vm"),assert=require("node:assert/strict"),path=require("node:path");
const html=fs.readFileSync(path.resolve(__dirname,"../../../docs/实验/机制研究/批次/2026-10-13-策略回应与危机可达性/协商试玩.html"),"utf8");
const scripts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
assert.equal(scripts.length,1);
new vm.Script(scripts[0][1]);
function trial(mode,type,offer,counter=null){
 const els=new Map();
 const document={getElementById(id){if(!els.has(id))els.set(id,{id,textContent:"",value:"",disabled:false,style:{},onclick:null});return els.get(id);}};
 const ctx={document,Math,navigator:{clipboard:{writeText:async()=>{}}},console};
 vm.runInNewContext(scripts[0][1],ctx,{timeout:3000});
 const e=id=>document.getElementById(id);
 e((mode==="open"?"force-":"bound-")+(type===2?"low":"high")).onclick();
 assert.ok(!e("visibleState").textContent.includes("真实外部"));
 assert.match(e("trace").textContent,/外部价值：尚未公开/);
 e("reason").value="我猜对方的选择";
 e("offer"+offer).onclick();
 if(counter&&e("counterButtons").style.display==="flex")e(counter).onclick();
 const out=e("trace").textContent;
 const m=out.match(/玩家=(\d+)；对方=(\d+)/);
 assert.ok(m,"trial did not end "+mode+" "+type+" "+offer+" "+counter+"\n"+out);
 assert.match(out,/首轮理由：我猜对方的选择/);
 assert.match(out,new RegExp("真实外部价值="+type));
 return {p:Number(m[1]),partner:Number(m[2]),text:out,document};
}
const checks=[
 ["open",2,2,"raise",7,4],
 ["open",4,2,"raise",7,4],
 ["open",2,2,"walk",0,2],
 ["open",4,2,"walk",0,4],
 ["open",2,4,null,8,4],
 ["open",4,4,null,8,4],
 ["bound",2,2,null,10,2],
 ["bound",4,2,null,0,4],
 ["bound",2,4,null,8,4],
 ["bound",4,4,null,8,4],
];
for(const [mode,type,first,next,p,q] of checks){
 const got=trial(mode,type,first,next);
 assert.deepEqual([got.p,got.partner],[p,q]);
 if(mode==="open"&&first===2)assert.match(got.text,/对方拒绝了首轮报价/);
}
assert.match(trial("open",2,2,"raise").text,/第二轮理由：/);
console.log("PASS NEG03 offline field study: 10 complete strategic-response routes + hidden-value checks");
