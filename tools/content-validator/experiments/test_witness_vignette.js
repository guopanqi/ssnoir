#!/usr/bin/env node
"use strict";
/* Offline DOM simulation. This is NOT human observation or browser visual QA. */
const assert=require("node:assert/strict"),fs=require("node:fs"),vm=require("node:vm"),path=require("node:path");
const html=fs.readFileSync(path.resolve(__dirname,"../../../docs/实验/机制研究/批次/2026-10-13-情境选择与注意力验证/证词与时限.html"),"utf8");
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
assert.equal(src.length,1,"one inline script");
new vm.Script(src[0][1]);

function newDom(){
 const els=new Map();
 function element(id=""){
  const obj={id,value:"",disabled:false,textContent:"",onclick:null,style:{},children:[],type:"",setAttribute(){},
   classList:{items:new Set(),add(x){this.items.add(x)},remove(x){this.items.delete(x)},contains(x){return this.items.has(x)}},
   appendChild(node){this.children.push(node);return node},
   get innerHTML(){return this._html||""},
   set innerHTML(v){this._html=v;this.children=[];}
  };
  return obj;
 }
 const document={getElementById(id){if(!els.has(id))els.set(id,element(id));return els.get(id)},
 createElement(name){const el=element();el.tagName=name;return el}};
 vm.runInNewContext(src[0][1],{document,Math,navigator:{clipboard:{writeText:async()=>{}}}}, {timeout:4000});
 const get=id=>document.getElementById(id);
 return {get,select(id){
    const node=get("choices").children.find(e=>e.id==="choice-"+id);
    assert.ok(node,"missing choice "+id);
    return node;
 }};
}
const endings={};let cases=0;
for(const mode of ["hard","soft"]){
 for(const prep of ["records","witness"]){
  for(const decision of ["redact","delay","release"]){
   const app=newDom();app.get(mode==="hard"?"hardStart":"softStart").onclick();
   assert.match(app.get("scene").textContent,/听证/);
   assert.equal(app.select("records").disabled,false);
   assert.equal(app.select("witness").disabled,false);
   app.get("reason").value="我优先考虑当下能做的事";
   app.select(prep).onclick();
   assert.match(app.get("scene").textContent,/姓名/);
   assert.equal(app.get("chapter").textContent,"第二幕 · 21:30");
   const choice=app.select(decision);
   if(mode==="hard" && decision==="release"){
      assert.equal(choice.disabled,true,"hard mode cannot act on sealed testimony");
      assert.equal(choice.onclick,null,"disabled must not have handler");
      continue;
   }
   assert.equal(choice.disabled,false);
   app.get("reason").value="我觉得接下来会有代价";
   choice.onclick();
   const text=app.get("scene").textContent;
   assert.match(app.get("trail").textContent,/结局：/);
   assert.match(app.get("trail").textContent,/当时的想法：我优先考虑/);
   assert.match(app.get("trail").textContent,/当时的想法：我觉得/);
   assert.equal(app.get("choices").children.length,0,"choices close after finish");
   assert.equal(app.get("reason").disabled,true);
   assert.ok(!/undefined|NaN/.test(text));
   if(!endings[prep+":"+decision]) endings[prep+":"+decision]=text;
   else assert.equal(text,endings[prep+":"+decision],"shared outcomes must use identical narrative text across hard/soft");
   cases++;
  }
 }
}
assert.equal(cases,10);
assert.equal(Object.keys(endings).length,6);
assert.equal(newDom().get("play").classList.contains("invisible"),false,"DOM stub initial classList does not parse HTML");
console.log("PASS X03 witness vignette: 10 reachable endings, 6 distinct narratives, 2 unavailable actions, shared-outcome parity");
