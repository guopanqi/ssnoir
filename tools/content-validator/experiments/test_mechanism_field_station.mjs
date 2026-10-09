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
 console.log("PASS: desktop, R54 full play, R50 modifier + export, R37 official-only, import, mobile, screenshots");
}catch(err){console.error(err);process.exitCode=1}finally{await browser.close()}
