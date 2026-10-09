#!/usr/bin/env node
/** Playwright QA of SSNoir v2 research workspace.
 * Tests public feedback/replies and GitHub mutation against mocked API only,
 * never requires or transmits a real PAT.
 */
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { chromium } from "../../../Game/node_modules/playwright/index.mjs";
import { execFileSync } from "node:child_process";

const root=path.resolve(import.meta.dirname,"../../..");
const html=path.join(root,"docs/实验/机制研究/研究总览.html");
const artifact=path.join(root,"artifacts/research-field-station");
fs.mkdirSync(artifact,{recursive:true});
const inbox="docs/实验/机制研究/反馈/inbox";
const reviewDir="docs/实验/机制研究/反馈/reviews";
const fbName="SSNoir-feedback-R54-20261009-062528-43765ce6ee.json";
const full=inbox+"/"+fbName;
const reviewPath=reviewDir+"/"+fbName;
const fbSeed=fs.readFileSync(path.join(root,full),"utf8");
const reviewSeed=fs.readFileSync(path.join(root,reviewPath),"utf8");
const browser=await chromium.launch({headless:true,args:["--no-sandbox"]});
let sequence=0;
const remote=new Map([[full,{body:fbSeed,sha:"cf209caf55c498c5f6236ecd68c2a98ca8b608f1"}]]);
const reviews=new Map([[reviewPath,{body:reviewSeed,sha:"review-sha"}]]);
function routeGitHub(page){
 return page.route("https://api.github.com/**",async route=>{
  const req=route.request(),u=new URL(req.url()),p=decodeURIComponent(u.pathname);
  const method=req.method();
  const respond=(status,data)=>route.fulfill({status,contentType:"application/json",
   headers:{"access-control-allow-origin":"*","access-control-allow-headers":"*"},
   body:JSON.stringify(data)});
  if(p==="/user"&&method==="GET")return respond(200,{login:"research-test-user"});
  const prefix="/repos/guopanqi/ssnoir/contents/";
  if(!p.startsWith(prefix))return respond(404,{message:"Unknown mock path "+p});
  const f=p.slice(prefix.length);
  if(method==="GET"&&(f===inbox||f===reviewDir)){
   const entries=f===inbox?remote:reviews;
   return respond(200,[...entries].map(([q,v])=>({
    type:"file",name:q.slice(f.length+1),path:q,sha:v.sha
   })).sort((a,b)=>a.name.localeCompare(b.name)));
  }
  if(method==="GET"){
   const item=remote.get(f)||reviews.get(f);
   return item?respond(200,{path:f,sha:item.sha,content:Buffer.from(item.body).toString("base64")}):
    respond(404,{message:"Not Found"});
  }
  if(method==="PUT"){
   assert(f.startsWith(inbox+"/"),"feedback writes must stay inside inbox");
   const body=JSON.parse(req.postData()),old=remote.get(f);
   if(old&&body.sha!==old.sha)return respond(409,{message:"Conflict"});
   if(!old&&body.sha)return respond(422,{message:"No matching content"});
   const decoded=Buffer.from(body.content,"base64").toString("utf8");
   const submitted=JSON.parse(decoded);
   assert.equal(submitted.schema,"ssnoir.mechanism-feedback/v1");
   assert(!decoded.includes("github_pat_research_test"));
   const sha="new-feedback-sha-"+(++sequence);
   remote.set(f,{sha,body:decoded});
   return respond(old?200:201,{content:{sha,path:f},commit:{sha:"commit-"+sequence}});
  }
  return respond(405,{message:"Method not allowed"});
 });
}
const row=(page,id)=>page.locator('#featured-tasks .experiment-row[aria-label^="'+id+' "]');
async function choose(page,id){await page.locator("#study-filter").selectOption("all");await row(page,id).click()}
async function openExtra(page){
 const x=page.locator(".feedback-panel > details.quiet-details");
 if(!(await x.evaluate(e=>e.open)))await x.locator("summary").click();
}
async function start(page){await page.getByRole("button",{name:/开始四骰实验/}).click()}
async function play(page,goalFn){
 let n=0;
 while(await page.getByRole("button",{name:"执行这一手 →"}).count()){
  await page.locator("#game .die:not([disabled])").first().click();
  await page.locator("#game .goal").nth(goalFn(n)).click();
  await page.getByRole("button",{name:"执行这一手 →"}).click();
  if(++n>5)throw Error("play more than five dice");
 }
 return n;
}
async function download(page){
 await openExtra(page);
 const promise=page.waitForEvent("download");
 await page.locator("#export").click();
 const item=await promise,filepath=path.join(artifact,item.suggestedFilename());
 fs.copyFileSync(await item.path(),filepath);
 execFileSync("python3",["tools/content-validator/experiments/validate_mechanism_feedback.py",filepath],{cwd:root});
 return {filepath,data:JSON.parse(fs.readFileSync(filepath,"utf8"))};
}
async function waitSync(page){await page.waitForFunction(()=>document.querySelector("#list-count")?.textContent.includes("11 / 11"));}
try{
 const context=await browser.newContext({viewport:{width:1370,height:940},acceptDownloads:true});
 const page=await context.newPage(),errors=[];
 page.on("pageerror",e=>errors.push(e.stack||e.message));
 page.on("console",m=>{if(m.type()==="error")errors.push(m.text())});
 await routeGitHub(page);
 await page.goto(pathToFileURL(html).href,{waitUntil:"load"});
 assert.equal(await page.locator("#overview:not(.hidden)").count(),1,"overview opens first");
 assert.equal(await page.locator("#wb-focus-title").textContent()!=="",true);
 await page.locator('.desk-nav a[href="#map"]').click();
 assert.equal(await page.locator("#map:not(.hidden) .wb-pattern").count(),6);
 await page.locator('.wb-filter[data-filter="untried"]').click();
 assert.equal(await page.locator("#map:not(.hidden) .wb-pattern").count(),3);
 await page.locator('.desk-nav a[href="#experiments"]').click();
 assert.equal(await page.locator("#experiments:not(.hidden)").count(),1);
 await waitSync(page);
 assert.equal(await row(page,"R54").count(),1);
 assert.equal(await page.locator("#featured-tasks .experiment-row").count(),11);
 // R50 is now the default, first-priority human playtest; inspect the prior R54
 // reply explicitly instead of assuming a reviewed experiment is initially active.
 await choose(page,"R54");
 await page.waitForFunction(()=>document.querySelector("#selected-state").textContent.includes("回复"));
 assert.match(await page.locator("#research-reply").innerText(),/开局取舍成立/);
 assert.match(await page.locator("#research-reply").innerText(),/不等于机制失败/);
 const first=await page.locator("#featured-tasks .experiment-row").first().getAttribute("aria-label");
 assert(first.startsWith("R50 "), "already-reviewed R54 must not top the needs-attention list");
 await page.locator("#study-filter").selectOption("reviewed");
 assert.equal(await page.locator("#featured-tasks .experiment-row").count(),1);
 assert.equal(await row(page,"R54").count(),1);
 await page.locator("#study-filter").selectOption("pending");
 assert.equal(await page.locator("#featured-tasks .experiment-row").count(),0);
 await page.locator("#study-filter").selectOption("all");
 await page.locator("#study-search").fill("动作回声");
 assert.equal(await row(page,"R50").count(),1);
 await page.locator("#study-search").fill("");
 // Direct browser R54: clear editor, play and export.
 await choose(page,"R54");
 await start(page);
 assert.equal(await page.locator("#game .goal").count(),2);
 assert.equal(await page.locator("#game .die").count(),4);
 await play(page,i=>i%2);
 await page.locator("#fb-mechanicalFeeling").fill("开局选甲还是选乙让我思考过。");
 const saved=await download(page);
 assert.equal(saved.data.studyId,"R54");
 assert.equal(saved.data.runs.length,1);
 assert.equal(saved.data.runs[0].handSize,4);
 // Capacity ablation browser path: fifth die exists, recorded separately; old
 // four-die feedback files and GitHub researcher replies stay compatible.
 await page.locator("#capacity").selectOption("5");
 await page.getByRole("button",{name:/再玩一局/}).click();
 assert.equal(await page.locator("#game .die").count(),5);
 assert.match(await page.locator("#game .game-header").innerText(),/0 \/ 5/);
 await play(page,i=>i%2);
 const compared=await download(page);
 assert.equal(compared.data.studyId,"R54");
 assert.equal(compared.data.runs.length,2);
 assert.equal(compared.data.runs[0].handSize,4);
 assert.equal(compared.data.runs[1].handSize,5);
 assert.equal(compared.data.runs[1].initialDice.length,5);
 // R50: same/alternate dynamic feedback remains correctly implemented.
 await choose(page,"R50");
 await start(page);
 await page.locator("#game .die:not([disabled])").first().click();
 await page.locator("#game .goal").nth(1).click();
 assert.match(await page.locator("#game .preview").innerText(),/修正 0/);
 await page.getByRole("button",{name:"执行这一手 →"}).click();
 await page.locator("#game .die:not([disabled])").first().click();
 await page.locator("#game .goal").nth(1).click();
 assert.match(await page.locator("#game .preview").innerText(),/修正 -1/);
 await page.locator("#game .goal").nth(0).click();
 assert.match(await page.locator("#game .preview").innerText(),/修正 \+1/);
 await page.getByRole("button",{name:"执行这一手 →"}).click();
 await play(page,i=>i%2);
 await page.locator("#fb-mechanicalFeeling").fill("继续与换线之间有不同的判定收益。");
 const r50=await download(page);
 assert.equal(r50.data.studyId,"R50");assert.equal(r50.data.runs.length,1);
 await choose(page,"R37");
 assert.match(await page.locator("#game").innerText(),/C#/);
 assert.equal(await page.getByRole("button",{name:/开始四骰实验/}).count(),0);
 // Log in with fake token, reopen existing human report and update exactly same path.
 await openExtra(page);
 await page.locator("#gh-token").fill("github_pat_research_test");
 await page.locator("#gh-connect").click();
 await page.waitForFunction(()=>document.querySelector("#gh-user").textContent.includes("research-test-user"));
 await choose(page,"R54");
 await page.locator("#gh-active button").click();
 await page.waitForFunction(()=>document.querySelector("#fb-mechanicalFeeling").value.includes("骰子的"));
 await page.locator("#fb-mechanicalFeeling").fill("开局甲/乙都有意义。我想补充：有些局行动容量不足。");
 await page.locator("#gh-save").click();
 await page.waitForFunction(()=>document.querySelector("#gh-operation").textContent.includes("已提交："),null,{timeout:7000}).catch(async err=>{throw new Error("Saving existing R54 failed: "+(await page.locator("#gh-operation").innerText())+"; remote mutations="+sequence+"; original="+err.message)});
 assert.equal(remote.size,1,"same review must update same feedback JSON");
 assert.equal(sequence,1);
 assert.match(JSON.parse(remote.get(full).body).responses.mechanicalFeeling,/容量不足/);
 // The old researcher reply remains visible, marked stale against new blob SHA.
 await page.waitForFunction(()=>document.querySelector("#selected-state").textContent.includes("待研究"));
 assert.match(await page.locator("#research-reply").innerText(),/上一版反馈/);
 await page.locator("#study-filter").selectOption("pending");
 assert.equal(await row(page,"R54").count(),1);
 // Refresh and re-open without a second token entry.
 await page.reload({waitUntil:"load"});
 await page.locator('.desk-nav a[href="#experiments"]').click();
 await waitSync(page);
 await page.waitForFunction(()=>document.querySelector("#gh-user").textContent.includes("research-test-user"));
 await page.locator("#study-filter").selectOption("all");
 await choose(page,"R54");
 // On the same browser origin, the active file is restored automatically. Only
 // open the old file if the user had explicitly switched to a new draft.
 if(await page.locator("#gh-active button").count())await page.locator("#gh-active button").click();
 await page.waitForFunction(()=>document.querySelector("#fb-mechanicalFeeling").value.includes("容量不足"));
 const currentSha=remote.get(full).sha;
 remote.set(full,{...remote.get(full),sha:"modified-other-device"});
 await page.locator("#gh-save").click();
 await page.waitForFunction(()=>document.querySelector("#gh-operation").textContent.includes("保存冲突"));
 assert.equal(sequence,1);
 assert.equal(currentSha,"new-feedback-sha-1");
 // Expected 409/Conflict console request is not an application crash.
 assert(!errors.some(x=>!x.includes("409")),errors.join("\n"));
 errors.length=0;
 await openExtra(page);
 await page.locator("#gh-disconnect").click();
 assert.equal(await page.evaluate(()=>localStorage.getItem("ssnoir/github-feedback-token/v1")),null);
 await page.screenshot({path:path.join(artifact,"desktop.png"),fullPage:true});
 await context.close();

 const mobile=await browser.newContext({viewport:{width:390,height:844},isMobile:true,hasTouch:true});
 const mp=await mobile.newPage(),mobileErrors=[];mp.on("pageerror",e=>mobileErrors.push(e.message));
 await routeGitHub(mp);
 await mp.goto(pathToFileURL(html).href,{waitUntil:"load"});
 assert.equal(await mp.locator("#overview:not(.hidden)").count(),1);
 await mp.locator('.desk-nav a[href="#experiments"]').click();
 await waitSync(mp);
 await choose(mp,"R50");await start(mp);
 await mp.locator("#game .die:not([disabled])").first().click();
 await mp.locator("#game .goal").first().click();
 assert.match(await mp.locator("#game .preview").innerText(),/骰面/);
 const over=await mp.evaluate(()=>document.documentElement.scrollWidth-window.innerWidth);
 assert(over<=2,"horizontal overflow "+over);
 assert.equal(mobileErrors.length,0,mobileErrors.join("\n"));
 await mp.screenshot({path:path.join(artifact,"mobile.png"),fullPage:true});
 await mobile.close();
 console.log("PASS: overview, evidence map, 11-item status list; R54 human reply and edit invalidation; R50/R54 4-v-5 play; feedback SHA updates; reload and mobile");
}catch(e){console.error(e);process.exitCode=1}finally{await browser.close();}
