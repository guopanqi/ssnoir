/* SSNoir human-feedback GitHub sync — embedded into generated HTML.
 * Only the reader's browser makes GitHub API requests. Personal access tokens
 * are opt-in, saved only in localStorage on this page origin, never in git.
 */
(function () {
"use strict";
const app = window.SSNoirFieldStation;
if (!app) throw new Error("SSNoir field station bridge missing");
const REPO = "guopanqi/ssnoir";
const DIR = "docs/实验/机制研究/反馈/inbox";
const REVIEWS = "docs/实验/机制研究/反馈/reviews";
const API = "https://api.github.com";
const TOKEN_KEY = "ssnoir/github-feedback-token/v1";
const ACTIVE_KEY = "ssnoir/github-feedback-active/v1";
const MAX_BYTES = 204800;
let token = "", user = "", active = null, records = [], reviews = new Map(), saving = false;
const $ = id => document.getElementById(id);
const LOCAL = {
  get(k) { try { return localStorage.getItem(k) || ""; } catch (_) { return ""; } },
  set(k,v) { try { localStorage.setItem(k,v); return true; } catch (_) { return false; } },
  del(k) { try { localStorage.removeItem(k); } catch (_) {} }
};
const msg = text => { $("gh-operation").textContent = text; };
const endpoint = path => path.split("/").map(encodeURIComponent).join("/");
const inInbox = path => typeof path === "string" &&
  path.startsWith(DIR + "/") &&
  /^SSNoir-feedback-R\d{2}-[a-zA-Z0-9._-]+\.json$/.test(path.slice(DIR.length + 1));
const fileLabel = path => path.slice(DIR.length + 1);
const safeJson = object => {
  const text = JSON.stringify(object, null, 2) + "\n";
  if (new TextEncoder().encode(text).length > MAX_BYTES)
    throw new Error("记录已超过 200 KiB。请先导出 JSON 备份，并创建另一份反馈。");
  return text;
};
function encoded(text) {
  const bytes = new TextEncoder().encode(text);
  let result = "";
  for (let i = 0; i < bytes.length; i += 8192)
    result += String.fromCharCode(...bytes.subarray(i,i+8192));
  return btoa(result);
}
function decoded(raw) {
  const string = atob(raw.replace(/\s/g, ""));
  const bytes = Uint8Array.from(string, c => c.charCodeAt(0));
  return new TextDecoder("utf-8",{fatal:true}).decode(bytes);
}
async function request(method, route, body, overrideToken) {
  const auth = overrideToken || token;
  const response = await fetch(API + route, {
    method,
    headers: {
      Accept: "application/vnd.github+json",
      ...(auth ? {Authorization: "Bearer " + auth} : {}),
      "X-GitHub-Api-Version": "2022-11-28",
      ...(body ? {"Content-Type":"application/json"} : {})
    },
    ...(body ? {body:JSON.stringify(body)} : {})
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const err = new Error("GitHub " + response.status + "：" +
      (data.message || response.statusText || "请求失败"));
    err.status = response.status;
    throw err;
  }
  return data;
}
function showConnection() {
  const connected = !!token && !!user;
  $("cloud-connect").classList.toggle("hidden", connected);
  $("cloud-connected").classList.toggle("hidden", !connected);
  $("cloud-status").textContent = connected ? "已连接 · " + user : token ? "验证连接中" : "未连接";
  $("gh-user").textContent = connected ? "@" + user : "";
  $("gh-save").disabled = !connected;
  $("gh-save").title = connected ? "提交到 GitHub，更新现有文件时保留版本" : "请先展开 GitHub 连接设置并保存 Token";
  renderActive();
}
function renderActive() {
  const e = $("gh-active");
  if (!e) return;
  e.replaceChildren();
  const current=app.currentStudy();
  if(active && active.studyId===current){
    e.appendChild(document.createTextNode("正在编辑已提交的反馈 · "+fileLabel(active.path)));
  } else {
    const found=records.find(x=>x.path.startsWith(DIR+"/SSNoir-feedback-"+current+"-"));
    if(found){
      e.appendChild(document.createTextNode("GitHub 已有这项实验的反馈。"));
      const b=document.createElement("button");b.type="button";b.className="button ghost tiny";
      b.style.marginLeft="8px";b.textContent="打开旧反馈继续编辑";
      b.addEventListener("click",()=>openRecord(found));e.appendChild(b);
    }else e.textContent="新反馈 · "+current+"；填写或试玩后保存。";
  }
}
function setActive(value){
 active=value;
 if(value)LOCAL.set(ACTIVE_KEY,JSON.stringify(value));
 else LOCAL.del(ACTIVE_KEY);
 renderActive();renderRecords();renderReply();
}
function reviewerFor(file){return file?reviews.get(file.path)||null:null}
function progress(){
 const status={};
 for(const t of app.tasks()){
  const matching=records.filter(x=>x.name.startsWith("SSNoir-feedback-"+t.id+"-"));
  let p={status:"todo",count:matching.length,updatedAt:matching[0]?.name||""};
  if(matching.length){
   p.status=matching.some(f=>reviewerFor(f)?.feedbackSha!==f.sha)?"pending":"reviewed";
  }
  status[t.id]=p;
 }
 app.updateStudyProgress(status);
 renderActive();
 renderReply();
}
function renderReply(){
 const root=$("research-reply");
 if(!root)return;
 root.replaceChildren();
 const id=app.currentStudy(),files=records.filter(x=>x.name.startsWith("SSNoir-feedback-"+id+"-"));
 const relevant=active&&active.studyId===id?records.find(x=>x.path===active.path):files[0];
 const data=reviewerFor(relevant);
 if(!data){
   root.textContent=files.length?
     "已收到你的反馈，正在等待研究者研读并回复。你可以继续编辑；一次体验不等于玩法好坏的裁决。":
     "尚无这项实验的人类反馈。试玩或记录想法后提交；研究者会在继续研究时回复，而不是由网页即时生成虚假的 AI 回复。";
   return;
 }
 const synced=relevant.sha===data.feedbackSha;
 const status=document.createElement("p");status.className=synced?"review-current":"review-stale";
 status.textContent=synced?"已审阅当前反馈版本 · "+(data.updatedAt||""):
  "这条回复针对上一版反馈。你已经修改了体验记录，研究者需要重新审阅；原有回复保留供对照。";
 root.appendChild(status);
 const title=document.createElement("h3");title.textContent=data.headline;root.appendChild(title);
 for(const key of ["acknowledgment","interpretation"]){
   if(data[key]){const p=document.createElement("p");p.textContent=data[key];root.appendChild(p);}
 }
 if(Array.isArray(data.observations)&&data.observations.length){
   const list=document.createElement("ul");data.observations.forEach(line=>{
    const li=document.createElement("li");li.textContent=line;list.appendChild(li);});root.appendChild(list);
 }
 if(data.question){
  const q=document.createElement("div");q.className="review-question";
  const label=document.createElement("strong");label.textContent="研究者想继续问你";q.appendChild(label);
  const p=document.createElement("p");p.textContent=data.question;q.appendChild(p);root.appendChild(q);
 }
 if(data.next){const note=document.createElement("p");note.className="minor";note.textContent="研究下一步："+data.next;root.appendChild(note);}
}
function renderRecords() {
  const root = $("gh-list");
  root.replaceChildren();
  if (!records.length) {
    const empty = document.createElement("div");
    empty.className = "feedback-empty";
    empty.textContent = "尚无已提交的反馈。完成试玩或写下一句感受后，可以直接提交。";
    root.appendChild(empty);
    return;
  }
  for (const file of records) {
    const box = document.createElement("div");
    box.className = "feedback-file" + (active?.path === file.path ? " current" : "");
    const info = document.createElement("div");
    const title = document.createElement("h4");
    const study = /SSNoir-feedback-(R\d+)-/.exec(file.name);
    title.textContent = (study ? study[1] + " · " : "") + "已提交体验";
    const sub = document.createElement("div");
    sub.className = "minor";
    sub.textContent = file.name + " · main";
    info.append(title, sub);
    const b = document.createElement("button");
    b.className = "button ghost tiny";
    b.type = "button";
    b.textContent = active?.path === file.path ? "重新载入" : "继续编辑";
    b.addEventListener("click", () => openRecord(file));
    box.append(info, b);
    root.appendChild(box);
  }
}
async function refresh() {
  try {
    msg("正在同步仓库的已提交反馈与研究回复……");
    const list = await request("GET", "/repos/"+REPO+"/contents/"+endpoint(DIR)+"?ref=main");
    if (!Array.isArray(list)) throw new Error("GitHub 未返回反馈目录");
    records = list.filter(f => f.type === "file" && inInbox(f.path))
      .sort((a,b) => b.name.localeCompare(a.name,"en"));
    reviews = new Map();
    try {
      const reviewFiles=await request("GET","/repos/"+REPO+"/contents/"+endpoint(REVIEWS)+"?ref=main");
      if(Array.isArray(reviewFiles)){
        await Promise.all(reviewFiles.filter(f=>f.name.endsWith(".json")).slice(0,80).map(async f=>{
          try {
            const remote=await request("GET","/repos/"+REPO+"/contents/"+endpoint(f.path)+"?ref=main");
            const data=JSON.parse(decoded(remote.content||""));
            if(data.schema==="ssnoir.mechanism-review/v1"&&
               data.sourcePath?.startsWith(DIR+"/")&&
               typeof data.feedbackSha==="string")reviews.set(data.sourcePath,data);
          }catch(err){console.warn("Review unavailable",f.name,err.message);}
        }));
      }
    }catch(err){if(err.status!==404)console.warn("Review index unavailable",err.message);}
    renderRecords();progress();
    msg("已同步 "+records.length+" 份反馈、"+reviews.size+" 条研究回复。");
  } catch (err) { msg("读取研究反馈失败："+err.message); }
}
async function connect(raw, persist) {
  const next = raw.trim();
  if (!next) { $("gh-connect-error").textContent = "请输入 Fine-grained Token。"; return; }
  $("gh-connect-error").textContent = "";
  $("cloud-status").textContent = "验证连接中";
  try {
    const identity = await request("GET", "/user", null, next);
    if (!identity.login) throw new Error("未获得 GitHub 用户身份");
    const directory = await request("GET",
      "/repos/"+REPO+"/contents/"+endpoint(DIR)+"?ref=main", null, next);
    if (!Array.isArray(directory)) throw new Error("当前 Token 无法读取反馈目录");
    user = identity.login;
    token = next;
    if (persist && !LOCAL.set(TOKEN_KEY, token))
      msg("本次连接有效，但浏览器拒绝永久保存 Token。");
    $("gh-token").value = "";
    showConnection();
    await refresh();
  } catch (err) {
    token = ""; user = "";
    showConnection();
    $("gh-connect-error").textContent = "连接失败：" + err.message;
    if (persist) LOCAL.del(TOKEN_KEY);
  }
}
function disconnect() {
  token = ""; user = ""; records = [];
  LOCAL.del(TOKEN_KEY); LOCAL.del(ACTIVE_KEY);
  active = null;
  $("gh-token").value = "";
  $("gh-operation").textContent = "已从当前浏览器清除 Token。GitHub 中的反馈不会被删除。";
  showConnection();
}
function validateFeedback(data) {
  if (!data || data.schema !== "ssnoir.mechanism-feedback/v1" ||
      !app.getTask(data.studyId)) throw new Error("反馈格式或实验编号无效");
  if (!["browser-sketch","official-runtime"].includes(data.environment))
    throw new Error("环境标识无效");
  if (!Array.isArray(data.runs) || data.runs.length > 20)
    throw new Error("试玩记录超过20局或格式错误");
  if (data.environment === "official-runtime" && data.runs.length)
    throw new Error("正式游戏反馈不能附带浏览器试玩轨迹");
  if (!data.responses || typeof data.responses !== "object")
    throw new Error("反馈文字格式错误");
  if (!data.source || data.source.officialEntry !== app.getTask(data.studyId).experiment)
    throw new Error("这份反馈的原型入口和当前研究不一致");
  safeJson(data);
}
async function openRecord(file) {
  if (!inInbox(file.path)) return;
  try {
    msg("正在载入 "+file.name+"……");
    const remote = await request("GET",
      "/repos/"+REPO+"/contents/"+endpoint(file.path)+"?ref=main");
    if (!remote.content || !remote.sha) throw new Error("GitHub 未返回文件内容与版本");
    const data = JSON.parse(decoded(remote.content));
    validateFeedback(data);
    // Loading does not mutate remote data. sha is kept for conflict-safe edits.
    app.loadFeedback(data);
    setActive({path:file.path,sha:remote.sha,studyId:data.studyId,
      createdAt:data.createdAt,source:data.source});
    $("gh-add-note").value = "";
    msg("已载入 "+file.name+"。可以继续试玩或修改文字，再提交更新。");
    renderReply();
  } catch (err) { msg("无法打开反馈："+err.message); }
}
function newFeedback() {
  setActive(null);
  $("gh-add-note").value = "";
  // Existing local draft remains, by design. It is a new file, not a destructive reset.
  msg("已切换为新反馈；当前本地试玩和文字仍保留。提交将创建另一份文件。");
  renderReply();
}
function makeFileName(study) {
  const stamp = new Date().toISOString().replace(/[-:]/g,"").replace(/\..+$/,"").replace("T","-");
  const random = crypto.getRandomValues(new Uint8Array(5));
  const tag = Array.from(random, x => x.toString(16).padStart(2,"0")).join("");
  return DIR+"/SSNoir-feedback-"+study+"-"+stamp+"-"+tag+".json";
}
function buildSubmission() {
  const data = app.currentFeedback();
  validateFeedback(data);
  if (active && active.studyId !== data.studyId)
    throw new Error("当前实验与打开的旧反馈不一致；请重新打开旧反馈或选择新反馈。");
  if (active) {
    data.createdAt = active.createdAt || data.createdAt;
    data.source = active.source || data.source;
    data.updatedAt = new Date().toISOString();
  }
  const extra = $("gh-add-note").value.trim();
  if (extra) {
    data.notes = [...(data.notes||[]), new Date().toISOString().slice(0,10)+" · "+extra];
  }
  if (!data.runs.length && !(data.notes||[]).length &&
      !Object.values(data.responses).some(v => typeof v === "string" && v.trim()))
    throw new Error("请至少试玩一局或填写一项实际感受。");
  validateFeedback(data);
  return data;
}
async function save() {
  if (saving || !token || !user) return;
  saving = true;
  $("gh-save").disabled = true;
  try {
    const data = buildSubmission();
    const filePath = active ? active.path : makeFileName(data.studyId);
    if (!inInbox(filePath)) throw new Error("非法反馈路径");
    const body = {message:(active ? "feedback: update " : "feedback: add ")+
      data.studyId+" playtest notes",content:encoded(safeJson(data)),branch:"main"};
    if (active) body.sha = active.sha; // Optimistic concurrency prevents silently overwriting.
    msg(active ? "正在更新原有反馈……" : "正在新建反馈文件……");
    const result = await request("PUT", "/repos/"+REPO+"/contents/"+endpoint(filePath),body);
    if (!result.content?.sha) throw new Error("GitHub 没有确认更新后的文件版本");
    // Keep the submitted note in the local draft so it won't disappear on later update.
    app.loadFeedback(data);
    setActive({path:filePath,sha:result.content.sha,studyId:data.studyId,
      createdAt:data.createdAt,source:data.source});
    $("gh-add-note").value = "";
    await refresh();
    msg("已提交："+fileLabel(filePath)+"。之后可以随时打开并继续修改。");
  } catch (err) {
    if (err.status === 409 || err.status === 422)
      msg("保存冲突：这份文件可能已在其他页面更新。先备份本地 JSON，再刷新已提交列表、重新载入，合并修改后保存。");
    else msg("保存失败："+err.message);
  } finally { saving=false; $("gh-save").disabled=false; }
}
async function restore() {
  const stored = LOCAL.get(TOKEN_KEY);
  const rawActive = LOCAL.get(ACTIVE_KEY);
  if (rawActive) {
    try {
      const parsed=JSON.parse(rawActive);
      if (inInbox(parsed.path) && parsed.sha && app.getTask(parsed.studyId)) active=parsed;
    } catch (_) { LOCAL.del(ACTIVE_KEY); }
  }
  showConnection();
  if (stored) {
    $("cloud-status").textContent = "正在恢复连接";
    await connect(stored,false);
  } else await refresh();
}
$("gh-connect").addEventListener("click",()=>connect($("gh-token").value,true));
$("gh-token").addEventListener("keydown",e=>{if(e.key==="Enter"){e.preventDefault();connect(e.currentTarget.value,true);}});
$("gh-refresh").addEventListener("click",refresh);
$("gh-disconnect").addEventListener("click",disconnect);
$("gh-save").addEventListener("click",save);
$("gh-new").addEventListener("click",newFeedback);
window.addEventListener("ssnoir:study-change",e=>{
  if (active && e.detail.studyId !== active.studyId) {
    // Selection changes should never rewrite an unrelated remote file.
    setActive(null);
  } else renderActive();
  renderReply();
});
restore();
})();