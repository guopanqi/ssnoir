
/* Research information view. All factual content is read from versioned research JSON. */
(function(){
"use strict";
const app=window.SSNoirFieldStation;if(!app)throw Error("Research app not initialized");
const atlas=JSON.parse(document.getElementById("atlas-data").textContent);
const tasks=JSON.parse(document.getElementById("tasks-data").textContent);
const SOURCE="https://github.com/guopanqi/ssnoir/blob/main/docs/实验/机制研究/";
const $=id=>document.getElementById(id);
const make=(p,t,c,v)=>{const e=document.createElement(t);if(c)e.className=c;if(v!==undefined)e.textContent=v;p.appendChild(e);return e};
const external=s=>/^(https?:|#)/.test(s)?s:new URL(s,SOURCE).href;
const studyLink=id=>"#study-"+id;
let progress=null,syncError="",selectedPattern="echo";
const branches=[
 {title:"资源与目标",question:"有限行动先争取什么？",ids:["risk","scarce"]},
 {title:"压力与响应",question:"情况变化后是否调整计划？",ids:["pressure","reactive"]},
 {title:"信息与行动",question:"新信息或上一手如何影响下一手？",ids:["reveal","echo"]}
];
const patterns=new Map(atlas.patterns.map(p=>[p.id,p]));
function attention(){
 const root=$("wb-attention");root.replaceChildren();
 if(!progress){$("wb-attention-status").textContent=syncError?"同步失败":"同步中";make(root,"p","wb-empty",syncError?"反馈记录暂不可用；仍可直接试玩和保存草稿。":"正在读取已提交反馈…");return;}
 $("wb-attention-status").textContent="尚未提交反馈";
 const queue=tasks.tasks.filter(t=>(progress[t.id]||{status:"todo"}).status==="todo").sort((a,b)=>a.rank-b.rank);
 if(!queue.length){make(root,"p","wb-empty","当前没有需要重复提交的实验。");return;}
 queue.slice(0,3).forEach(t=>{const a=make(root,"a","wb-attention-item");a.href=studyLink(t.id);make(a,"span","wb-num",t.id);make(a,"strong",null,t.title);make(a,"span","wb-pill",t.mode==="browser-demo"?"网页可玩":"正式原型");});
}
function home(){
 $("wb-updated").textContent=tasks.updated+" 更新";
 $("wb-main-question").textContent=atlas.nextQuestions[0]||tasks.headline;
 $("wb-main-link").href=studyLink(tasks.tasks[0].id);
 $("wb-main-link").textContent="查看 "+tasks.tasks[0].id+" · "+tasks.tasks[0].title+" ↗";
 const root=$("wb-notes");root.replaceChildren();
 tasks.current.slice(0,3).forEach((v,i)=>{const a=make(root,"article","wb-note");make(a,"span","wb-kicker","记录 "+String(i+1).padStart(2,"0"));make(a,"p",null,v);});
}
function inspector(){
 const root=$("wb-map-inspector");root.replaceChildren();
 const p=patterns.get(selectedPattern);if(!p)return;
 make(root,"span","wb-kicker",p.family+" / "+p.theme);make(root,"h2",null,p.name);
 make(root,"p",null,p.decision);
 const e=make(root,"div","network-evidence");
 [["math","数学"],["native","正式引擎"],["human","真人体验"]].forEach(([k,l])=>make(e,"span",p.evidence[k]?"on":"",l+(p.evidence[k]?" ✓":" · 待验证")));
 make(root,"h3",null,"已发现");make(root,"p",null,p.dynamic);
 make(root,"h3",null,"下一步");make(root,"p",null,p.next);
 const d=make(root,"details");make(d,"summary",null,"研究判断与局限");
 [["可能退化",p.counter],["适用边界",p.boundary],["研究判断",p.potential],["证据说明",p.evidenceNote]].forEach(([t,v])=>{make(d,"strong",null,t);make(d,"p",null,v);});
 const links=make(root,"div","wb-links");
 tasks.tasks.filter(t=>t.patternId===p.id).forEach(t=>{const a=make(links,"a",null,"进入 "+t.id+" · "+t.title+" ↗");a.href=studyLink(t.id);});
 p.links.forEach(l=>{const a=make(links,"a",null,l.label+" ↗");a.href=external(l.href);a.target="_blank";a.rel="noopener noreferrer";});
 document.querySelectorAll(".wb-pattern-node").forEach(el=>el.setAttribute("aria-pressed",String(el.dataset.id===selectedPattern)));
}
function map(){
 const root=$("wb-map-grid");root.replaceChildren();
 $("wb-map-count").textContent=atlas.patterns.length+" 个保留候选";
 const start=make(root,"div","network-root");make(start,"small",null,"研究起点");make(start,"strong",null,atlas.subtitle);
 const flow=make(root,"div","network-branches");
 branches.forEach(b=>{
  const group=make(flow,"div","network-branch"),caption=make(group,"div","network-branch-caption");
  make(caption,"strong",null,b.title);make(caption,"small",null,b.question);
  const nodes=make(group,"div","network-branch-nodes");
  b.ids.forEach(id=>{
   const p=patterns.get(id);if(!p)return;
   const node=make(nodes,"button","wb-pattern-node wb-pattern");node.type="button";node.dataset.id=id;node.setAttribute("aria-pressed",String(id===selectedPattern));
   make(node,"strong",null,p.name);make(node,"small",null,p.evidence.human?"已有真人反馈":"待真人体验");
   node.addEventListener("click",()=>{selectedPattern=id;inspector();});
  });
 });
 inspector();
 $("wb-stop-count").textContent="("+tasks.nonPromoted.length+")";
 const stopped=$("wb-stop-list");stopped.replaceChildren();
 tasks.nonPromoted.forEach(x=>{const item=make(stopped,"article");make(item,"h3",null,x.id+" · "+x.title);make(item,"p",null,x.summary);});
}
function journal(){
 const root=$("field-journal-events");root.replaceChildren();
 make(root,"div","plain-log-day",tasks.updated);
 tasks.current.forEach((v,i)=>{const entry=make(root,"article");make(entry,"span","log-number",String(i+1).padStart(2,"0"));make(entry,"p",null,v);});
}
function route(){
 const h=decodeURIComponent(location.hash||"#overview"),m=/^#study-(R\d+)$/.exec(h);
 const page=m||h==="#experiments"||h==="#research-log"?"experiments":h==="#map"||h==="#research-findings"?"map":h==="#journal"?"journal":"overview";
 ["overview","experiments","map","journal"].forEach(id=>$(id).classList.toggle("hidden",id!==page));
 $("field-breadcrumb").textContent=({overview:"概览",experiments:"实验室",map:"研究地图",journal:"研究日志"})[page];
 document.querySelectorAll(".desk-nav a").forEach(a=>a.setAttribute("aria-current",a.getAttribute("href")==="#"+page?"page":"false"));
 if(m)app.selectStudy(m[1]);
 window.scrollTo(0,0);
}
window.addEventListener("ssnoir:feedback-progress",e=>{progress=e.detail.progress||null;syncError=e.detail.error||"";attention();});
window.addEventListener("hashchange",route);
home();map();journal();attention();route();
})();
