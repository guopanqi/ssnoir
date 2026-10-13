
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
 $("wb-main-question").textContent=dialogue.questions[0]?.title||tasks.headline;
 $("wb-main-link").href="#library";
 $("wb-main-link").textContent="从模式目录挑选结构 ↗";
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

/* Designer-facing catalog is a reading view over the complete, evidence-aware map.
   It never promotes or changes canonical research status. */
const graph=JSON.parse(document.getElementById("pattern-map-data").textContent);
const dialogue=JSON.parse(document.getElementById("research-dialogue-data").textContent);
const nodeById=new Map(graph.nodes.map(n=>[n.id,n]));
let chosenId="CS2-08";
const picked=new Set();
let libSearch="",libLayer="all",libOrigin="all",libPickedOnly=false;
const graphUrl=p=>"https://github.com/guopanqi/ssnoir/blob/main/"+p.split("/").map(encodeURIComponent).join("/");
const layerName=n=>{
 if(n.layer==="encounter-structure")return "交锋结构";
 if(n.layer==="decision-relation")return "基础决策";
 if(n.layer==="negative-boundary")return "反证边界";
 if(n.layer==="open-hypothesis")return "开放问题";
 if((n.layer||"").includes("combination"))return "组合关系";
 return "研究关系";
};
const sourceName=n=>n.origin==="published-game-analysis"?"成熟作品设计归纳":n.origin==="ssnoir-research"?"SSNoir研究":"组合分析";
function libraryVisible(n){
 if(libPickedOnly&&!picked.has(n.id))return false;
 if(libLayer!=="all"&&(libLayer==="combination"?!(["combination","boundary","hypothesis","variation"].some(t=>(n.layer||"").includes(t))):n.layer!==libLayer))return false;
 if(libOrigin!=="all"&&(libOrigin==="other"?["published-game-analysis","ssnoir-research"].includes(n.origin):n.origin!==libOrigin))return false;
 if(libSearch&&![n.id,n.name,n.idea,n.family,n.stage].filter(Boolean).join(" ").toLocaleLowerCase().includes(libSearch))return false;
 return true;
}
function setChosen(id){
 if(!nodeById.has(id))return;
 chosenId=id;
 drawLibrary();
}
function pick(id){
 if(picked.has(id))picked.delete(id);else picked.add(id);
 drawLibrary();
}
function drawLibrary(){
 const list=$("lib-list");list.replaceChildren();
 const visible=graph.nodes.filter(libraryVisible);
 $("lib-count").textContent=visible.length+"/"+graph.nodes.length+" 项";
 visible.forEach(n=>{
  const item=make(list,"button","lib-item"+(n.id===chosenId?" active":""));
  item.type="button";item.setAttribute("aria-pressed",String(n.id===chosenId));
  const head=make(item,"div","lib-item-head");
  make(head,"span","lib-id",n.id);
  make(head,"span","lib-origin",sourceName(n));
  make(item,"strong",null,n.name);
  make(item,"small",null,n.idea||"");
  item.addEventListener("click",()=>setChosen(n.id));
 });
 if(!visible.length)make(list,"p","wb-empty","没有匹配条目；可尝试清除筛选。");
 drawLibraryDetail();drawPicked();
}
function drawLibraryDetail(){
 const root=$("lib-detail");root.replaceChildren();
 const n=nodeById.get(chosenId);if(!n)return;
 make(root,"span","wb-kicker",sourceName(n)+" / "+layerName(n));
 make(root,"h2",null,n.name);
 make(root,"p","lib-idea",n.idea||"目前只有关联证据，尚未提炼独立玩法描述。");
 const info=make(root,"div","lib-metadata");
 make(info,"span",null,"研究编号："+n.id);
 make(info,"span",null,"记录身份："+(n.stage||"待复查"));
 if(n.curatedDisplay)make(info,"span",null,"收录于精选展示（不代表通用认证）");
 if(n.evidence?.humanFeedbackAvailable||n.evidence?.humanRelated)make(info,"span",null,"存在相关真人反馈（不等于受控验证）");
 const p=make(root,"p","lib-explainer",n.note||"目前缺乏可以外推到情境设计的完整体验证据。");
 const btn=make(root,"button","button "+(picked.has(n.id)?"ghost":""));
 btn.type="button";btn.textContent=picked.has(n.id)?"从情境组合中移除":"加入情境组合";
 btn.addEventListener("click",()=>pick(n.id));
 const related=graph.edges.filter(e=>e.from===n.id||e.to===n.id);
 if(related.length){
  make(root,"h3",null,"相关关系 · "+related.length);
  const refs=make(root,"div","lib-relations");
  related.forEach(e=>{
   const other=nodeById.get(e.from===n.id?e.to:e.from);
   if(!other)return;
   const line=make(refs,"button","lib-relation");line.type="button";
   make(line,"strong",null,other.name+" · "+e.relation);
   make(line,"span",null,e.reason);
   line.addEventListener("click",()=>setChosen(other.id));
  });
 }
 make(root,"h3",null,"可追溯来源");
 const sources=make(root,"div","wb-links");
 (n.sources||[]).forEach(source=>{
  const a=make(sources,"a",null,source.split("/").slice(-1)[0]+" ↗");
  a.href=graphUrl(source);a.target="_blank";a.rel="noopener noreferrer";
 });
 if(n.id.startsWith("CS2-"))make(root,"p","minor","CS2九类是设计归纳，并非官方分类或每条模式独立的受控玩家验证。");
}
function drawPicked(){
 const root=$("lib-picked");root.replaceChildren();
 if(!picked.size){make(root,"p","minor","尚未选用模式。打开左侧任一项，可加入设计组合。");return;}
 [...picked].forEach(id=>{
  const n=nodeById.get(id);
  const b=make(root,"button","lib-chip",n.name+" ×");b.type="button";
  b.title="移除 "+n.name;b.addEventListener("click",()=>pick(id));
 });
}
function buildBrief(){
 const name=$("lib-scenario").value.trim()||"（待填写情境）";
 const selection=[...picked].map(id=>nodeById.get(id));
 const interactions=graph.edges.filter(e=>picked.has(e.from)&&picked.has(e.to));
 const blocks=[
  "SSNoir · 交锋设计材料（研究知识，不是实现要求）",
  "情境："+name,"选用模式："+selection.map(n=>n.name+" ["+n.id+"]").join("、"),
  ...selection.flatMap(n=>[
   "\n"+n.id+" · "+n.name+"（"+sourceName(n)+"）",
   "玩家可能进行的判断："+(n.idea||"仍需定义"),
   "研究状态："+(n.stage||"未知"),
   "限制："+(n.note||"尚待体验验证")
  ]),
  "\n已知的相互作用："+(interactions.length?interactions.map(e=>e.from+" → "+e.to+"："+e.reason).join("；"):"本知识图暂未记录这些模式之间经过检查的相互作用。"),
  "\n设计时仍需回答：这些动作会怎样影响彼此？玩家在什么时刻重新考虑？最强固定策略是什么？缺少哪些体验证据？"
 ];
 return blocks.join("\n");
}
async function copyBrief(){
 if(!picked.size){$("lib-copy-state").textContent="请先选择至少一项模式。";return;}
 const txt=buildBrief();
 try{
  if(navigator.clipboard?.writeText)await navigator.clipboard.writeText(txt);
  else{
   const area=document.createElement("textarea");area.value=txt;document.body.append(area);
   area.select();if(!document.execCommand("copy"))throw Error("copy unavailable");area.remove();
  }
  $("lib-copy-state").textContent="已复制设计简报。它保留研究来源、尚未验证的部分和组合问题。";
 }catch(err){$("lib-copy-state").textContent="浏览器未允许复制。可在HTTPS页面重试。";}
}
function drawDialogue(){
 const root=$("wb-dialogue-requests");root.replaceChildren();
 dialogue.questions.forEach(q=>{
  const section=make(root,"article","dialogue-card");
  const h=make(section,"div","dialogue-heading");make(h,"span","wb-kicker",q.priority+" / "+q.id);make(h,"h3",null,q.title);
  make(section,"p",null,q.context);
  make(section,"p","dialogue-question",q.ask);
  const links=make(section,"div","lib-dialogue-links");
  q.related.forEach(id=>{const n=nodeById.get(id);if(!n)return;const a=make(links,"a",null,n.name+" ↗");a.href="#library";a.addEventListener("click",()=>setChosen(id));});
  const field=make(section,"textarea","dialogue-answer");field.rows=3;field.placeholder="描述实际经历、判断或设计需求即可，不用打分。";field.setAttribute("aria-label",q.title+"的回答");
  const controls=make(section,"div","dialogue-actions");
  const submit=make(controls,"button","button tiny","提交给研究 Agent");submit.type="button";
  const backup=make(controls,"button","button ghost tiny","导出回答 JSON");backup.type="button";
  const result=make(section,"p","minor");result.setAttribute("role","status");
  const response=()=>({schema:"ssnoir.research-dialogue/v1",questionId:q.id,questionTitle:q.title,createdAt:new Date().toISOString(),related:q.related,answer:field.value.trim(),context:q.context});
  const validate=()=>{if(!field.value.trim()){result.textContent="先填写一个具体观察或问题。";return false;}return true;};
  backup.addEventListener("click",()=>{
   if(!validate())return;
   const file=new Blob([JSON.stringify(response(),null,2)+"\n"],{type:"application/json"});
   const url=URL.createObjectURL(file);const a=document.createElement("a");
   a.href=url;a.download="SSNoir-dialogue-"+q.id+"-"+new Date().toISOString().slice(0,10)+".json";
   a.click();URL.revokeObjectURL(url);result.textContent="JSON已导出。可上传到仓库的研究反馈 questions 目录。";
  });
  submit.addEventListener("click",async()=>{
   if(!validate())return;
   submit.disabled=true;result.textContent="提交中…";
   try{if(!window.SSNoirResearchDialogue)throw Error("研究反馈连接尚未加载");const r=await window.SSNoirResearchDialogue.submit(response());result.textContent="已提交到GitHub："+r;}
   catch(err){result.textContent="未提交："+err.message+"。你仍可导出JSON；首次使用需在实验室连接GitHub。";}
   finally{submit.disabled=false;}
  });
 });
}
["lib-search","lib-layer","lib-origin","lib-picked-only"].forEach(id=>{
 const element=$(id);
 element.addEventListener(id==="lib-search"?"input":"change",()=>{
  libSearch=$("lib-search").value.trim().toLocaleLowerCase();
  libLayer=$("lib-layer").value;libOrigin=$("lib-origin").value;
  libPickedOnly=$("lib-picked-only").checked;drawLibrary();
 });
});
$("lib-copy").addEventListener("click",copyBrief);

function route(){
 const h=decodeURIComponent(location.hash||"#overview"),m=/^#study-(R\d+)$/.exec(h);
 const page=m||h==="#experiments"||h==="#research-log"?"experiments":h==="#map"||h==="#research-findings"?"map":h==="#library"?"library":h==="#journal"?"journal":"overview";
 ["overview","experiments","map","library","journal"].forEach(id=>$(id).classList.toggle("hidden",id!==page));
 $("field-breadcrumb").textContent=({overview:"概览",experiments:"实验室",map:"研究地图",library:"模式目录",journal:"研究日志"})[page];
 document.querySelectorAll(".desk-nav a").forEach(a=>a.setAttribute("aria-current",a.getAttribute("href")==="#"+page?"page":"false"));
 if(m)app.selectStudy(m[1]);
 window.scrollTo(0,0);
}
window.addEventListener("ssnoir:feedback-progress",e=>{progress=e.detail.progress||null;syncError=e.detail.error||"";attention();});
window.addEventListener("hashchange",route);
home();map();journal();drawLibrary();drawDialogue();attention();route();
})();
