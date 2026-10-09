#!/usr/bin/env node
/** Browser-level QA of the generated, offline SSNoir human field station.

Runs two complete browser play sketches, verifies feedback JSON export/import,
official-only routing, link behavior, and responsive layout. Does not claim the
sketches are the canonical C# / Scheme engine.
*/
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { chromium } from "../../../Game/node_modules/playwright/index.mjs";
import { execFileSync } from "node:child_process";

const root=path.resolve(import.meta.dirname,"../../..");
const htmlPath=path.join(root,"docs/实验/机制研究/研究总览.html");
const artifacts=path.join(root,"artifacts/research-field-station");
fs.mkdirSync(artifacts,{recursive:true});

const browser=await chromium.launch({headless:true,args:["--no-sandbox"]});
async function selectFirstDie(page){
  await page.locator("#game .die:not([disabled])").first().click();
}
async function playOne(page,chooseGoal){
  let actions=0;
  while(await page.getByRole("button",{name:"执行这一手 →"}).count()>0){
    await selectFirstDie(page);
    await page.locator("#game .goal").nth(chooseGoal(actions)).click();
    await page.getByRole("button",{name:"执行这一手 →"}).click();
    actions++;
    assert(actions<=4,"one browser play sketch must finish within four dice");
  }
  return actions;
}
async function expectNoErrors(errors){
  assert.equal(errors.length,0,"Console/browser errors: "+errors.join("\n"));
}
async function validatedDownload(download){
  // Playwright's sandbox path lacks the original .json suffix.
  // Restore the suggested filename before passing it to the real inbox validator.
  const original=await download.path();
  const named=path.join(artifacts,download.suggestedFilename());
  fs.copyFileSync(original,named);
  execFileSync("python3",["tools/content-validator/experiments/validate_mechanism_feedback.py",named],{cwd:root});
  return {file:named,data:JSON.parse(fs.readFileSync(named,"utf8"))};
}
try{
 const context=await browser.newContext({viewport:{width:1280,height:920},acceptDownloads:true});
 const page=await context.newPage();
 const errors=[];
 page.on("pageerror",e=>errors.push("pageerror: "+e.message));
 page.on("console",m=>{if(m.type()==="error")errors.push("console: "+m.text())});
 await page.goto(pathToFileURL(htmlPath).href,{waitUntil:"load"});
 await page.locator("#featured-tasks .task-card").first().waitFor();
 assert.equal(await page.locator("#featured-tasks .task-card").count(),3);
 assert.equal(await page.locator("#patterns .pattern").count(),6);
 assert.match(await page.locator("#featured-tasks").innerText(),/R54/);
 assert.match(await page.locator("#featured-tasks").innerText(),/R50/);
 assert.match(await page.locator("#featured-tasks").innerText(),/R37/);

 // R54 browser sketch: 4 dice, candidate targets, actual completion and export.
 await page.locator("#featured-tasks .task-card").first().getByRole("button").click();
 assert.match(await page.locator("#selected-title").innerText(),/追击领先目标/);
 await page.getByRole("button",{name:/开始四骰实验/}).click();
 assert.equal(await page.locator("#game .goal").count(),2);
 assert.equal(await page.locator("#game .die").count(),4);
 await playOne(page,step=>step%2);
 assert.match(await page.locator("#run-summary").innerText(),/1 局/);
 await page.locator("#fb-decisiveMoment").fill("第二手之后我才重新判断方向");
 let downloadEvent=page.waitForEvent("download");
 await page.locator("#export").click();
 let download=await downloadEvent;
 let exported=await validatedDownload(download);
 let file=exported.file;
 const feedback1=exported.data;
 assert.equal(feedback1.schema,"ssnoir.mechanism-feedback/v1");
 assert.equal(feedback1.studyId,"R54");
 assert.equal(feedback1.environment,"browser-sketch");
 assert.equal(feedback1.runs.length,1);
 assert(feedback1.runs[0].actions.length<=4);
 assert.match(feedback1.responses.decisiveMoment,/第二手/);
 assert.match(await page.locator("#upload-link").getAttribute("href"),/upload\/main/);

 // R50: first decision must have modifier 0, then same-goal -1 / switch +1.
 await page.locator("#featured-tasks .task-card").nth(1).getByRole("button").click();
 await page.getByRole("button",{name:/开始四骰实验/}).click();
 await selectFirstDie(page);
 await page.locator("#game .goal").nth(1).click();
 assert.match(await page.locator("#game .preview").innerText(),/修正 0/);
 await page.locator("#hesitate").check();
 await page.locator("#step-note").fill("这里我先选择乙，观察接下来的切换");
 await page.getByRole("button",{name:"执行这一手 →"}).click();
 await selectFirstDie(page);
 await page.locator("#game .goal").nth(1).click();
 assert.match(await page.locator("#game .preview").innerText(),/修正 -1/);
 await page.locator("#game .goal").nth(0).click();
 assert.match(await page.locator("#game .preview").innerText(),/修正 \+1/);
 await page.getByRole("button",{name:"执行这一手 →"}).click();
 // Finish any available turns.
 await playOne(page,step=>step%2);
 assert.match(await page.locator("#run-summary").innerText(),/1 局/);
 downloadEvent=page.waitForEvent("download");await page.locator("#export").click();
 download=await downloadEvent;
 const feedback2=(await validatedDownload(download)).data;
 assert.equal(feedback2.studyId,"R50");
 assert.equal(feedback2.runs.length,1);
 assert(feedback2.runs[0].actions.some(a=>a.hesitated));
 assert(feedback2.runs[0].actions.some(a=>a.modifier===1));

 // Official-only experiment visibly avoids fake browser simulation.
 await page.locator("#featured-tasks .task-card").nth(2).getByRole("button").click();
 assert.match(await page.locator("#game").innerText(),/C#/);
 assert.equal(await page.getByRole("button",{name:/开始四骰实验/}).count(),0);
 await page.locator("#fb-ideas").fill("压力预算：仍然想尝试多人情况");
 downloadEvent=page.waitForEvent("download");await page.locator("#export").click();
 download=await downloadEvent;
 const feedback3=(await validatedDownload(download)).data;
 assert.equal(feedback3.studyId,"R37");
 assert.equal(feedback3.environment,"official-runtime");
 assert.equal(feedback3.runs.length,0);

 // Import without modifying the original research conclusions.
 await page.locator("#import-file").setInputFiles(file);
 await page.waitForFunction(()=>document.querySelector("#selected-title").textContent.includes("追击领先目标"));
 assert.match(await page.locator("#selected-title").innerText(),/追击领先目标/);
 assert.match(await page.locator("#fb-decisiveMoment").inputValue(),/第二手之后/);


 // GitHub Contents API mock: new file, optimistic-sha update, persistent token,
 // reload existing record and conflict rejection. No real credentials are used.
 const remote=new Map();
 let mutations=0;
 await page.route("https://api.github.com/**", async route=>{
  const req=route.request(),u=new URL(req.url()),p=decodeURIComponent(u.pathname);
  const method=req.method();
  const respond=(status,json)=>route.fulfill({status,contentType:"application/json",body:JSON.stringify(json),
    headers:{"access-control-allow-origin":"*","access-control-allow-headers":"*"}});
  if(p==="/user"&&method==="GET")return respond(200,{login:"research-test-user"});
  const marker="/repos/guopanqi/ssnoir/contents/";
  if(!p.startsWith(marker))return respond(404,{message:"Unknown mock GitHub API route"});
  const file=p.slice(marker.length);
  const dir="docs/实验/机制研究/反馈/inbox";
  if(file===dir&&method==="GET"){
   return respond(200,[{type:"file",name:".gitkeep",path:dir+"/.gitkeep"},
    ...Array.from(remote.entries(),([path,entry])=>({type:"file",name:path.slice(dir.length+1),path,sha:entry.sha}))]);
  }
  if(method==="GET"){
   const record=remote.get(file);
   return record?respond(200,{content:Buffer.from(record.body).toString("base64"),sha:record.sha,path:file}):
    respond(404,{message:"Not Found"});
  }
  if(method==="PUT"){
   const body=JSON.parse(req.postData());
   const old=remote.get(file);
   if(old&&body.sha!==old.sha)return respond(409,{message:"Content conflict"});
   if(!old&&body.sha)return respond(422,{message:"File does not exist"});
   const bytes=Buffer.from(body.content,"base64").toString("utf8");
   const value=JSON.parse(bytes);
   assert.equal(value.schema,"ssnoir.mechanism-feedback/v1");
   assert(!bytes.includes("github_pat_research_test"));
   mutations++;
   const sha="mock-sha-"+mutations;
   remote.set(file,{sha,body:bytes});
   return respond(old?200:201,{content:{sha,path:file},commit:{sha:"commit-"+mutations}});
  }
  return respond(405,{message:"Method not allowed"});
 });
 await page.locator("#gh-token").fill("github_pat_research_test");
 await page.locator("#gh-connect").click();
 await page.locator("#cloud-connected").waitFor({state:"visible"});
 assert.match(await page.locator("#gh-user").innerText(),/research-test-user/);
 await page.locator("#gh-save").click();
 await page.waitForFunction(()=>document.querySelector("#gh-operation").textContent.includes("已提交："));
 assert.equal(remote.size,1,"first submission creates one record");
 let [remoteFile,remoteValue]=Array.from(remote.entries())[0];
 assert.equal(JSON.parse(remoteValue.body).studyId,"R54");
 await page.locator("#gh-add-note").fill("下一局我尝试把甲作为诱饵");
 await page.locator("#gh-save").click();
 await page.waitForFunction(()=>document.querySelector("#gh-operation").textContent.includes("已提交："));
 assert.equal(remote.size,1,"update must preserve original file path");
 assert.equal(mutations,2);
 assert.equal(JSON.parse(remote.get(remoteFile).body).notes.length,1);
 assert.match(JSON.parse(remote.get(remoteFile).body).notes[0],/诱饵/);
 // Token survives reload on the same Origin; reading an existing record restores its values.
 await page.reload({waitUntil:"load"});
 await page.locator("#cloud-connected").waitFor({state:"visible"});
 await page.locator("#gh-list .feedback-file").first().getByRole("button",{name:/继续编辑|重新载入/}).click();
 await page.waitForFunction(()=>document.querySelector("#fb-decisiveMoment").value.includes("第二手之后"));
 assert.match(await page.locator("#gh-active").innerText(),/正在编辑/);
 assert.match(await page.locator("#gh-user").innerText(),/research-test-user/);
 // Concurrent writer modifies the same sha in the mock remote.
 let existing=remote.get(remoteFile);
 remote.set(remoteFile,{...existing,sha:"someone-else-newer-sha"});
 await page.locator("#gh-add-note").fill("这句应该因为版本冲突而不写入");
 await page.locator("#gh-save").click();
 await page.waitForFunction(()=>document.querySelector("#gh-operation").textContent.includes("保存冲突"));
 assert.equal(mutations,2,"conflict must not silently overwrite newer remote");
 assert(!remote.get(remoteFile).body.includes("版本冲突"));
 await page.locator("#gh-disconnect").click();
 assert.equal(await page.locator("#cloud-connected").isVisible(),false);
 assert.equal(await page.evaluate(()=>localStorage.getItem("ssnoir/github-feedback-token/v1")),null);

 await page.screenshot({path:path.join(artifacts,"desktop.png"),fullPage:true});
 await expectNoErrors(errors);
 await context.close();

 // Mobile: focus, reading order, no horizontal overflow, play controls usable.
 const mobile=await browser.newContext({viewport:{width:390,height:844},acceptDownloads:true,isMobile:true,hasTouch:true});
 const mp=await mobile.newPage(),merrors=[];
 mp.on("pageerror",e=>merrors.push(e.message));
 mp.on("console",m=>{if(m.type()==="error")merrors.push(m.text())});
 await mp.goto(pathToFileURL(htmlPath).href,{waitUntil:"load"});
 await mp.locator("#featured-tasks .task-card").first().getByRole("button").click();
 await mp.getByRole("button",{name:/开始四骰实验/}).click();
 await selectFirstDie(mp);
 await mp.locator("#game .goal").first().click();
 assert.match(await mp.locator("#game .preview").innerText(),/骰面/);
 const overflow=await mp.evaluate(()=>document.documentElement.scrollWidth-window.innerWidth);
 assert(overflow<=2,"Mobile should not require horizontal scrolling: "+overflow);
 await mp.screenshot({path:path.join(artifacts,"mobile.png"),fullPage:true});
 await expectNoErrors(merrors);
 await mobile.close();
 console.log("PASS: browser play, real JSON schema, mocked GitHub create/update/reload/conflict, logout, mobile");
}catch(err){console.error(err);process.exitCode=1}finally{await browser.close()}
