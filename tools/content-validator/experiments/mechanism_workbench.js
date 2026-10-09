
/* Research presentation from versioned JSON; no duplicate decision authority. */
(function(){
"use strict";
const app=window.SSNoirFieldStation;
if(!app)throw Error("Field station bridge unavailable");
const atlas=JSON.parse(document.getElementById("atlas-data").textContent);
const tasks=JSON.parse(document.getElementById("tasks-data").textContent);
const SOURCE="https://github.com/guopanqi/ssnoir/blob/main/docs/实验/机制研究/";
const $=id=>document.getElementById(id);
const make=(p,t,c,v)=>{const e=document.createElement(t);if(c)e.className=c;if(v!==undefined)e.textContent=v;p.appendChild(e);return e};
const external=s=>/^(https?:|#)/.test(s)?s:new URL(s,SOURCE).href;
let progress=null,error="",patternFilter="all";
const studyLink=id=>"#study-"+id;
function attention(){
 const box=$("wb-attention");box.replaceChildren();
 if(!progress){$("wb-attention-status").textContent=error?"同步失败":"正在同步";make(box,"p","wb-empty",error?"远端反馈状态不可用；仍可进行本地试玩并保留草稿。":"正在读取已提交反馈与研究回复…");return;}
 $("wb-attention-status").textContent="依据 GitHub 反馈记录";
 const queue=tasks.tasks.map(t=>Object.assign({},t,progress[t.id]||{status:"todo"})).filter(t=>t.status==="todo").sort((a,b)=>a.rank-b.rank);
 if(!queue.length){make(box,"p","wb-empty","当前优先实验已提交反馈，暂无必须完成的试玩。你可以继续补充旧反馈，研究者会在下一轮研读。");return;}
 queue.slice(0,4).forEach(t=>{
  const a=make(box,"a","wb-attention-item");a.href=studyLink(t.id);
  make(a,"span","wb-num",t.id);const copy=make(a,"span");
  make(copy,"strong",null,t.title);make(copy,"small",null,t.purpose);
  make(a,"span","wb-pill",t.mode==="browser-demo"?"可在线试玩":"正式原型");
 });
}
function focusCard(){
 const focus=progress?tasks.tasks.find(t=>(progress[t.id]||{status:"todo"}).status==="todo"):null;
 if(!progress){
  $("wb-focus-title").textContent="正在核对你的研究记录";
  $("wb-focus-description").textContent="读取 GitHub 已提交反馈后，显示真正需要你参与的实验，不会重复催促已完成的试玩。";
  $("wb-focus-meta").textContent="反馈记录同步中";
  $("wb-focus-link").href="#experiments";$("wb-focus-link").textContent="浏览实验室 ↗";return;
 }
 if(!focus){
  $("wb-focus-title").textContent="下一步等待研究判断";
  $("wb-focus-description").textContent="优先试玩均已有反馈；现在可以回看研究者的回复，或自由补充新的观察。";
  $("wb-focus-meta").textContent="暂无强制试玩";
  $("wb-focus-link").href="#experiments";$("wb-focus-link").textContent="回到实验室 ↗";return;
 }
 $("wb-focus-title").textContent=focus.title;
 $("wb-focus-description").textContent=focus.purpose;
 $("wb-focus-meta").textContent=focus.id+" · "+focus.estimatedMinutes;
 $("wb-focus-link").href=studyLink(focus.id);
 $("wb-focus-link").textContent="进入实验 ↗";
}
function home(){
 $("wb-updated").textContent="资料更新 "+atlas.updated+" · 不代表 Agent 实时运行";
 $("wb-summary").textContent=atlas.summary;
 focusCard();
 tasks.current.slice(0,4).forEach((s,i)=>{const c=make($("wb-notes"),"article","wb-note");make(c,"span","wb-kicker","进展 / "+String(i+1).padStart(2,"0"));make(c,"p",null,s);});
 atlas.nextQuestions.forEach((s,i)=>{const c=make($("wb-question-list"),"div","wb-question");make(c,"span","wb-kicker","QUESTION "+String(i+1).padStart(2,"0"));make(c,"div",null,s);});
 tasks.nonPromoted.forEach(x=>{const c=make($("wb-stop-list"),"article");make(c,"h3",null,x.id+" · "+x.title);make(c,"p",null,x.summary);});
}
function evidence(p){return [["math","数学 / 反证"],["native","正式运行"],["human","真人反馈"]].filter(x=>p.evidence[x[0]]).map(x=>x[1]).join(" · ");}
function map(){
 const arr=atlas.patterns.filter(p=>patternFilter==="all"||(patternFilter==="human"&&p.evidence.human)||(patternFilter==="untried"&&!p.evidence.human));
 $("wb-map-count").textContent=arr.length+" / "+atlas.patterns.length+" 个研究候选";
 document.querySelectorAll(".wb-filter").forEach(b=>b.setAttribute("aria-pressed",String(b.dataset.filter===patternFilter)));
 const root=$("wb-map-grid");root.replaceChildren();
 for(const p of arr){
  const c=make(root,"article","wb-pattern");make(c,"span","wb-kicker",p.family+" / "+p.theme);make(c,"h3",null,p.name);
  make(c,"p","wb-decision",p.decision);make(c,"div","wb-evidence","证据 · "+evidence(p)+"；不等于玩法好坏");
  make(c,"div","wb-next","下一步｜"+p.next);
  const d=make(c,"details");make(d,"summary",null,"查看判断、反例和适用边界");
  [["为什么值得",p.why],["可能退化",p.counter],["适用边界",p.boundary],["目前的判断",p.potential],["证据说明",p.evidenceNote]].forEach(([a,b])=>{make(d,"strong",null,a);make(d,"p",null,b);});
  const links=make(c,"div","wb-links");
  tasks.tasks.filter(t=>t.patternId===p.id).forEach(t=>{const a=make(links,"a",null,t.id+" 试玩 ↗");a.href=studyLink(t.id);});
  p.links.forEach(l=>{const a=make(links,"a",null,l.label+" ↗");a.href=external(l.href);a.target="_blank";a.rel="noopener noreferrer";});
 }
}
function journal(){
 $("field-journal-date").textContent="资料更新 "+tasks.updated;
 const events=$("field-journal-events");events.replaceChildren();
 tasks.current.forEach((entry,i)=>{
  const card=make(events,"article","field-journal-event");
  make(card,"span","field-journal-event-number",String(i+1).padStart(2,"0"));
  const copy=make(card,"div");
  make(copy,"small",null,"研究进展 / "+tasks.updated);
  make(copy,"p",null,entry);
 });
 const questions=$("field-journal-open");questions.replaceChildren();
 atlas.nextQuestions.forEach((q,i)=>{
  const line=make(questions,"div","field-journal-open-item");
  make(line,"i",null,String(i+1).padStart(2,"0"));
  make(line,"span",null,q);
 });
 const boundaries=$("field-journal-boundaries");boundaries.replaceChildren();
 tasks.nonPromoted.forEach((entry)=>{
  const item=make(boundaries,"article","field-journal-boundary");
  make(item,"strong",null,entry.id+" · "+entry.title);
  make(item,"p",null,entry.summary);
 });
 $("field-letter-question").textContent=atlas.nextQuestions[0]||atlas.summary;
}
function route(){
 const value=decodeURIComponent(location.hash||"#overview");
 const match=/^#study-(R\d+)$/.exec(value);
 const page=match||value==="#experiments"||value==="#research-log"?"experiments":value==="#map"||value==="#research-findings"?"map":value==="#journal"?"journal":"overview";
 ["overview","experiments","map","journal"].forEach(id=>$(id).classList.toggle("hidden",id!==page));
 $("field-breadcrumb").textContent=({overview:"研究概览",experiments:"实验室",map:"研究地图",journal:"研究日志"})[page];
 document.querySelectorAll(".desk-nav a").forEach(a=>a.setAttribute("aria-current",a.getAttribute("href")==="#"+page?"page":"false"));
 if(match)app.selectStudy(match[1]);
 window.scrollTo(0,0);
}
document.querySelectorAll(".wb-filter").forEach(b=>b.addEventListener("click",()=>{patternFilter=b.dataset.filter;map();}));
window.addEventListener("ssnoir:feedback-progress",e=>{progress=e.detail.progress||null;error=e.detail.error||"";attention();focusCard();});
window.addEventListener("hashchange",route);
home();map();journal();attention();route();
})();
