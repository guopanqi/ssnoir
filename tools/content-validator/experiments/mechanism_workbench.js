
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
 const queue=tasks.tasks.map(t=>Object.assign({},t,progress[t.id]||{status:"todo"})).filter(t=>t.status!=="reviewed").sort((a,b)=>(a.status==="pending"?0:1)-(b.status==="pending"?0:1)||a.rank-b.rank);
 if(!queue.length){make(box,"p","wb-empty","目前没有等待审阅或体验的实验。你仍可继续补充旧反馈。");return;}
 queue.slice(0,4).forEach(t=>{
  const a=make(box,"a","wb-attention-item");a.href=studyLink(t.id);
  make(a,"span","wb-num",t.id);const copy=make(a,"span");
  make(copy,"strong",null,t.title);make(copy,"small",null,t.status==="pending"?"已有反馈，等待研究者对本版审阅":t.purpose);
  make(a,"span","wb-pill"+(t.status==="pending"?" pending":""),t.status==="pending"?"待研究回复":t.mode==="browser-demo"?"可在线试玩":"正式原型");
 });
}
function home(){
 $("wb-updated").textContent="资料更新 "+atlas.updated+" · 不代表 Agent 实时运行";
 $("wb-summary").textContent=atlas.summary;
 const focus=tasks.tasks[0];
 $("wb-focus-title").textContent=focus.title;$("wb-focus-description").textContent=focus.purpose;
 $("wb-focus-meta").textContent=focus.id+" · "+focus.estimatedMinutes;$("wb-focus-link").href=studyLink(focus.id);
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
function route(){
 const value=decodeURIComponent(location.hash||"#overview");
 const match=/^#study-(R\d+)$/.exec(value);
 const page=match||value==="#experiments"?"experiments":value==="#map"?"map":"overview";
 ["overview","experiments","map"].forEach(id=>$(id).classList.toggle("hidden",id!==page));
 document.querySelectorAll(".desk-nav a").forEach(a=>a.setAttribute("aria-current",a.getAttribute("href")==="#"+page?"page":"false"));
 if(match)app.selectStudy(match[1]);
 window.scrollTo(0,0);
}
document.querySelectorAll(".wb-filter").forEach(b=>b.addEventListener("click",()=>{patternFilter=b.dataset.filter;map();}));
window.addEventListener("ssnoir:feedback-progress",e=>{progress=e.detail.progress||null;error=e.detail.error||"";attention();});
window.addEventListener("hashchange",route);
home();map();attention();route();
})();
