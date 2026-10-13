#!/usr/bin/env node
"use strict";
/* Offline DOM simulation of action rights, branch consequences and 3 decisions.
   Does not claim human comprehension, visual accessibility or narrative quality. */
const assert=require("node:assert/strict"),fs=require("node:fs"),vm=require("node:vm"),path=require("node:path");
const html=fs.readFileSync(path.resolve(__dirname,"../../../docs/实验/机制研究/批次/2026-10-13-情境选择与注意力验证/证词与时限.html"),"utf8");
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
assert.equal(src.length,1);
new vm.Script(src[0][1]);
function newDom(flat=false){
 const els=new Map();
 function element(id=""){
  const el={id,value:"",disabled:false,textContent:"",onclick:null,style:{},children:[],type:"",setAttribute(){},
   classList:{items:new Set(),add(x){this.items.add(x)},remove(x){this.items.delete(x)},contains(x){return this.items.has(x)}},
   appendChild(node){this.children.push(node);return node},
   get innerHTML(){return this._html||""},
   set innerHTML(v){this._html=v;this.children=[]}
  };
  return el;
 }
 const document={getElementById(id){if(!els.has(id))els.set(id,element(id));return els.get(id)},
   createElement(name){const el=element();el.tagName=name;return el}};
 vm.runInNewContext(src[0][1],{document,Math,window:{location:{search:flat?"?flat-menu=1":""}},navigator:{clipboard:{writeText:async()=>{}}}}, {timeout:4000});
 return {get:id=>document.getElementById(id),choose(id){
    const node=document.getElementById("choices").children.find(e=>e.id==="choice-"+id);
    assert.ok(node,"missing choice "+id);
    return node;
 }};
}
const ENDINGS={},MENUS={normal:{},flat:{}},FINAL={normal:{},flat:{}};
let cases=0,followPaths=0,disabled=0;
for(const flat of [false,true]){
 const scope=flat?"flat":"normal";
 for(const mode of ["hard","soft"]){
  for(const prep of ["records","witness"]){
   for(const action of ["redact","delay","release"]){
    const initial=newDom(flat);initial.get(mode==="hard"?"hardStart":"softStart").onclick();
    assert.match(initial.get("scene").textContent,/听证/);
    initial.get("reason").value="我选择先做这件事";
    initial.choose(prep).onclick();
    assert.match(initial.get("scene").textContent,/认出来|名字|姓名|身份/);
    assert.equal(initial.get("chapter").textContent,"第二幕 · 21:30");
    const next=initial.choose(action);
    if(mode==="hard" && action==="release"){
       assert.equal(next.disabled,true,"hard must lock releasing a real name");
       assert.equal(next.onclick,null);
       disabled++;
       continue;
    }
    assert.equal(next.disabled,false);
    initial.get("reason").value="我在意将来的后果";
    next.onclick();
    const key=prep+":"+action;
    const intermediate=initial.get("scene").textContent;
    assert.match(initial.get("chapter").textContent,/第三幕/);
    assert.match(initial.get("trail").textContent,/已发生的后果：/);
    const menu=initial.get("choices").children.map(x=>x.id.replace("choice-",""));
    assert.equal(menu.length,2,"each history should have two future actions");
    assert.equal(new Set(menu).size,2);
    assert.ok(!menu.includes("release"),"release may not repeat after hearing");
    if(!ENDINGS[key])ENDINGS[key]=intermediate;
    else assert.equal(ENDINGS[key],intermediate,"identical history across variants must have identical immediate consequences");
    if(!MENUS[scope][key])MENUS[scope][key]=menu.join("|");
    else assert.equal(MENUS[scope][key],menu.join("|"),"hard and soft share affordances for common decisions");
    for(const third of menu){
      const dom=newDom(flat);dom.get(mode==="hard"?"hardStart":"softStart").onclick();
      dom.get("reason").value="第一手判断";dom.choose(prep).onclick();
      dom.get("reason").value="第二手判断";dom.choose(action).onclick();
      dom.get("reason").value="第三手判断";dom.choose(third).onclick();
      const trail=dom.get("trail").textContent;
      assert.match(trail,/第一幕行动/);assert.match(trail,/第二幕行动/);assert.match(trail,/第三幕行动/);
      assert.match(trail,/当时的想法：第一手判断/);
      assert.match(trail,/当时的想法：第二手判断/);
      assert.match(trail,/当时的想法：第三手判断/);
      assert.equal(dom.get("choices").children.length,0);
      assert.equal(dom.get("reason").disabled,true);
      assert.match(dom.get("scene").textContent,/最后选择/);
      if(!FINAL[scope][key+":"+third])FINAL[scope][key+":"+third]=dom.get("scene").textContent;
      else assert.equal(dom.get("scene").textContent,FINAL[scope][key+":"+third]);
      followPaths++;
    }
    cases++;
   }
  }
 }
}
assert.equal(disabled,4);
assert.equal(cases,20,"2 menu variants, each hard=4 and soft=6 legal histories");
assert.equal(followPaths,40,"each legal history must allow two subsequent actions");
assert.equal(Object.keys(ENDINGS).length,6);
assert.equal(new Set(Object.values(MENUS.normal)).size,5,"causal mode should preserve five distinct future action menus");
assert.equal(new Set(Object.values(MENUS.flat)).size,1,"cosmetic control must retain same outcomes but have just one next-day action menu");
assert.equal(Object.keys(FINAL.normal).length,12);
assert.equal(Object.keys(FINAL.flat).length,12);
assert.notEqual(MENUS.normal["records:redact"],MENUS.normal["witness:redact"]);
assert.notEqual(MENUS.normal["records:redact"],MENUS.normal["records:delay"]);
assert.equal(MENUS.normal["witness:redact"],MENUS.normal["witness:delay"],"one causal collision must be preserved as genuine negative");
for(const key of Object.keys(ENDINGS))
 assert.equal(MENUS.flat[key],"neighbors|summarize");
console.log("PASS X03 witness study: 2x hard/soft, 20 legal intermediate histories, 40 completed action paths, common six narrative outcomes, 5 vs 1 next-action menus");
