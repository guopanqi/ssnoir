
/* Research Workbench: a presentation layer over versioned research JSON and field station. */
(function(){
"use strict";
const station=window.SSNoirFieldStation;
if(!station)throw Error("Research workbench requires the existing field station");
const atlas=JSON.parse(document.getElementById("atlas-data").textContent);
const config=JSON.parse(document.getElementById("tasks-data").textContent);
const SOURCE="https://github.com/guopanqi/ssnoir/blob/main/docs/实验/机制研究/";
const $=id=>document.getElementById(id);
const make=(parent,tag,cls,value)=>{
 const el=document.createElement(tag);
 if(cls)el.className=cls;
 if(value!==undefined)el.textContent=value;
 parent.appendChild(el);return el;
};
const external=path=>/^(https?:|#)/.test(path)?path:new URL(path,SOURCE).href;
const studyHref=id=>"#study-"+id;
let progress=null,loadError="",patternFilter="all";
function renderAttention(){
 const root=$("wb-attention");
 root.replaceChildren();
 if(!progress){
  $("wb-attention-status").textContent=loadError?"无法读取远端反馈状态":"正在核对已提交反馈及研究回复";
  make(root,"p","wb-empty",loadError||"同步完成后，此处将只提示你需要参与的实验，不会把已完成的实验重新当作待办。");
  return;
 }
 $("wb-attention-status").textContent="由 GitHub 公开反馈记录判断 · 不代表 Agent 正在实时运行";
 const pending=config.tasks.map(t=>Object.assign({},t,progress[t.id]||{status:"todo"}))
  .filter(t=>t.status!=="reviewed")
  .sort((a,b)=>(a.status==="pending"?0:1)-(b.status==="pending"?0:1)||a.rank-b.rank);
 if(!pending.length){make(root,"p","wb-empty","当前没有尚待审阅或体验的实验。你仍可以返回已有实验继续讨论。");return;}
 for(const t of pending.slice(0,4)){
  const a=make(root,"a","wb-attention-item");a.href=studyHref(t.id);
  make(a,"span","wb-num",t.id);
  const info=make(a,"div");make(info,"strong",null,t.title);
  make(info,"small",null,t.status==="pending"?"反馈已经提交；研究者尚未审阅这一版本。":t.intro);
  make(a,"span","wb-pill "+t.status,t.status==="pending"?"待研究回复":t.mode==="browser-demo"?"网页可试玩":"正式原型");
 }
 if(pending.length>4){const a=make(root,"a","wb-cta alt","浏览全部实验 →");a.href="#experiments";a.style.marginTop="19px";}
}
function renderHome(){
 $("wb-updated").textContent="研究资料更新："+atlas.updated;
 $("wb-summary").textContent=atlas.summary;
 const focus=config.tasks[0];
 $("wb-focus-title").textContent=focus.title;
 $("wb-focus-description").textContent=focus.purpose;
 $("wb-focus-meta").textContent=focus.id+" · "+focus.estimatedMinutes;
 $("wb-focus-link").href=studyHref(focus.id);
 const notes=$("wb-notes");notes.replaceChildren();
 for(const id of ["reactive","echo","pressure"]){
  const p=atlas.patterns.find(x=>x.id===id);if(!p)continue;
  const row=make(notes,"article","wb-note");
  make(row,"div","wb-kicker",p.focus);make(row,"h3",null,p.name);
  make(row,"p",null,p.next);
 }
 renderAttention();
}
function evidence(p){
 const s=[];if(p.evidence.math)s.push("数学");if(p.evidence.native)s.push("正式规则");if(p.evidence.human)s.push("真人反馈");
 return s.join(" · ");
}
function renderMap(){
 const root=$("wb-map-grid");root.replaceChildren();
 const patterns=atlas.patterns.filter(p=>patternFilter==="all"||(patternFilter==="human"&&p.evidence.human)||(patternFilter==="untried"&&!p.evidence.human));
 $("wb-map-count").textContent=patterns.length+" / "+atlas.patterns.length+" 个候选";
 document.querySelectorAll(".wb-filter").forEach(btn=>btn.setAttribute("aria-pressed",String(btn.dataset.filter===patternFilter)));
 for(const p of patterns){
  const card=make(root,"article","wb-pattern");
  make(card,"div","wb-kicker",p.family+" / "+p.theme);
  make(card,"h3",null,p.name);make(card,"p","wb-decision",p.decision);
  make(card,"div","wb-next","下一步 · "+p.next);
  make(card,"div","wb-evidence","证据： "+evidence(p)+"（不等于玩法好坏评价）");
  const details=make(card,"details");make(details,"summary",null,"研究判断、反证与边界");
  [["为什么值得研究",p.why],["可能退化",p.counter],["适用边界",p.boundary],["研究者的判断",p.potential],["证据来源",p.evidenceNote]].forEach(([key,val])=>{
   make(details,"strong",null,key);make(details,"p",null,val);
  });
  const links=make(card,"div","wb-links");
  const task=config.tasks.find(t=>t.patternId===p.id);
  if(task){const a=make(links,"a",null,"进入 "+task.id+" 实验 →");a.href=studyHref(task.id);}
  p.links.slice(0,2).forEach(link=>{
   const a=make(links,"a",null,link.label+" ↗");a.href=external(link.href);
   a.target="_blank";a.rel="noopener noreferrer";
  });
 }
}
function renderResearch(){
 atlas.nextQuestions.forEach(q=>make($("wb-question-list"),"div","wb-question",q));
 config.nonPromoted.forEach(item=>{
  const el=make($("wb-stop-list"),"article");
  make(el,"h3",null,item.id+" · "+item.title);
  make(el,"p",null,item.summary);
 });
 renderMap();
}
function route(){
 const hash=decodeURIComponent(location.hash.replace(/^#/,""));
 const target=/^study-R\d+$/.test(hash)?"experiments":
  ["today","workshop"].includes(hash)?"experiments":
  ["research-log","library"].includes(hash)?"map":
  ["overview","experiments","map"].includes(hash)?hash:"overview";
 for(const name of ["overview","experiments","map"])$(name).classList.toggle("hidden",name!==target);
 document.querySelectorAll(".desk-nav a").forEach(a=>{
  if(a.hash==="#"+target)a.setAttribute("aria-current","page");
  else a.removeAttribute("aria-current");
 });
 if(hash.startsWith("study-")&&station.getTask(hash.slice(6))&&station.currentStudy()!==hash.slice(6)){
  station.selectStudy(hash.slice(6));
 }else{
  window.scrollTo(0,0);
 }
}
document.querySelectorAll(".wb-filter").forEach(btn=>btn.addEventListener("click",()=>{
 patternFilter=btn.dataset.filter;renderMap();
}));
window.addEventListener("ssnoir:feedback-progress",event=>{
 progress=event.detail?.progress||null;
 loadError=event.detail?.error||"";
 renderAttention();
});
window.addEventListener("ssnoir:study-change",event=>{
 const id=event.detail?.studyId;
 if(id&&location.hash!==studyHref(id))location.hash=studyHref(id);
});
window.addEventListener("hashchange",route);
renderHome();renderResearch();route();
})();
