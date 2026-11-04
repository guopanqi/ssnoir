import assert from "node:assert/strict";
import {readFileSync,mkdirSync} from "node:fs";
import {chromium} from "playwright";
import {createServer} from "vite";

/**
 * Starts from the literal day-four portable save produced by the main
 * first-act + chapter-two route, never from hand-set chapter flags.
 * Exercises the optional deception/investigation and a genuine dead end.
 */
const paperSave=readFileSync("artifacts/web-game-paper-investigation-save.json","utf8");
const original=JSON.parse(paperSave);
assert.equal(original.format,"ssnoir-world");
const server=await createServer({configFile:"vite.config.ts",
  server:{host:"127.0.0.1",port:4175,strictPort:true}});
let browser;
try{
  await server.listen();
  browser=await chromium.launch({headless:true,args:["--no-sandbox",
    "--enable-webgl","--enable-unsafe-swiftshader","--use-gl=angle","--use-angle=swiftshader"]});
  const page=await browser.newPage({viewport:{width:1440,height:850}});
  const errors=[];
  page.on("pageerror",error=>errors.push(String(error)));
  page.on("console",msg=>{if(msg.type()==="error")errors.push(msg.text());});
  const read=()=>page.evaluate(()=>window.__SSNOIR_WEB__.snapshot());
  const settled=()=>page.waitForFunction(()=>{
    const s=window.__SSNOIR_WEB__?.snapshot();return s&&!s.busy;
  },null,{timeout:90000});
  async function story(){
    for(let i=0;i<400;i++){
      await settled();const view=await read();
      if(!view.story)return;
      await page.getByTestId("story-continue").click({timeout:90000});
    }
    throw new Error("Optional investigation story queue did not drain");
  }
  async function visit(place){
    const view=await read();
    assert.equal(view.mode,"world");
    assert(view.mapPlaces.some(x=>x.id===place&&!x.disabled),
      "Location must genuinely be available "+place);
    await page.locator('[data-testid="map-place"][data-node-id='+JSON.stringify(place)+']')
      .click({timeout:90000});
    await settled();await story();
    const result=await read();
    assert.equal(result.error,"","Place entry "+place);
    assert.equal(result.path[0],place);
    return result;
  }
  async function perform(id){
    const prior=await read();
    const node=prior.entries.find(x=>x.id===id&&!x.disabled);
    assert(node,"Unopened native action "+id+" at "+JSON.stringify({
      day:prior.day,path:prior.path,entries:prior.entries.map(x=>[x.id,x.disabled])}));
    await page.locator('[data-testid="game-entry"][data-node-id='+JSON.stringify(id)+']')
      .click({timeout:90000});
    await settled();await story();
    let result=await read();
    if(result.selectedAction===id){
      assert(result.dice.length>0,"Native action requires a die "+id);
      await page.locator(".die:not([disabled])").first().click({timeout:90000});
      await settled();await story();
      result=await read();
      assert.equal(result.selectedAction,null,"Roll must resolve "+id);
    }
    assert.equal(result.error,"","Real authored action "+id);
    return result;
  }
  async function sleep(){
    let view=await read();
    if(view.path[0]!=="家")view=await visit("家");
    if(view.path.at(-1)!=="住宿"){
      await page.locator('[data-testid="game-entry"][data-node-id="住宿"]')
        .click({timeout:90000});
      await settled();await story();
    }
    view=await read();
    const rest=view.entries.find(x=>["睡觉","蜷缩在门口"].includes(x.id)&&!x.disabled);
    assert(rest,"The real city must provide an affordable sleep action on "+view.day);
    const day=view.day;
    view=await perform(rest.id);
    assert.equal(view.day,day+1,"Only native Scheme end-of-day changes the date");
    assert.equal(view.mode,"world");
    assert.deepEqual(view.path,[]);
    return view;
  }
  async function dossier(){
    await page.getByTestId("dossier-toggle").click({timeout:90000});
    await settled();
    const entries=(await read()).dossiers;
    await page.getByTestId("dossier-close").click({timeout:90000});
    await settled();
    return entries;
  }
  async function rumorAt(place,id,step){
    for(let i=0;i<10;i++){
      let view=await visit(place);
      if(!view.entries.some(x=>x.id===id&&!x.disabled)){
        const row=(await dossier()).find(x=>x.id==="谁出卖了夜莺");
        assert.equal(row?.steps[step]?.done,true,
          "Rumor vanishes only when its authoritative Clock is filled");
        return;
      }
      if(view.dice.length===0){await sleep();continue;}
      const before=view.dice.length;
      view=await perform(id);
      assert.equal(view.dice.length,before-1,"Each rumor uses a genuine daily die");
    }
    throw new Error("Could not fill the original authored rumor Clock: "+id);
  }
  await page.goto("http://127.0.0.1:4175/?seed=42&fixture=max-roll",
    {waitUntil:"domcontentloaded"});
  await page.getByTestId("game-ready").waitFor({timeout:90000});
  await story();
  await page.evaluate(text=>localStorage.setItem("ssnoir-web-v1",text),paperSave);
  await page.getByTestId("load-game").click({timeout:90000});
  await settled();await story();
  let view=await read();
  assert.equal(view.mode,"world");
  const initialDay=view.day;
  let investigation=(await dossier()).find(x=>x.id==="谁出卖了夜莺");
  assert.equal(investigation?.status,"进行中");
  assert.equal(investigation.steps[0].done,false);
  assert.equal(investigation.steps[1].done,false);

  await rumorAt("剧院","放给剧院",0);
  await rumorAt("格兰德酒店","留给酒店",1);
  investigation=(await dossier()).find(x=>x.id==="谁出卖了夜莺");
  assert.equal(investigation?.steps[0].done,true);
  assert.equal(investigation?.steps[1].done,true);
  assert.match(investigation.now,/等两天|等早报/,
    "Both planted rumors start the clock; no client-triggered reveal");
  const plantedDay=(await read()).day;
  for(let i=0;i<2;i++)await sleep();
  view=await read();
  assert.equal(view.day,plantedDay+2,"The forged paper arrives after two real world days");
  await visit("家");
  view=await perform("看早报");
  assert.equal(view.mode,"world");
  investigation=(await dossier()).find(x=>x.id==="谁出卖了夜莺");
  assert.equal(investigation?.steps[2].done,true);
  assert.match(investigation.now,/尾随|剧院/,
    "Reading the paper must direct the player to the theater, not solve the case");

  await visit("剧院");
  view=await perform("等取件人");
  assert.equal(view.mode,"scenario","The original five-stage tail must actually start");
  assert(view.entries.some(x=>x.id==="跟紧"),"The real scenario roll action must exist");
  const encounterDay=view.day;
  for(let round=1;round<=3&&view.mode==="scenario";round++){
    await page.getByTestId("end-turn").click({timeout:90000});
    await settled();await story();
    view=await read();
    assert.equal(view.error,"","A missed courier cannot strand the scenario");
  }
  assert.equal(view.mode,"world","The authored loss-of-trail clock must close the scene");
  assert(view.day>=encounterDay&&view.day<=encounterDay+1,
    "Only hospitalization is permitted to advance the city calendar");
  investigation=(await dossier()).find(x=>x.id==="谁出卖了夜莺");
  assert.equal(investigation?.status,"了结",
    "An unsuccessful optional investigation must settle, not soft-lock the main game");
  assert.match(investigation.now,/尾随失败|封死|跟丢/);
  await page.getByTestId("save-game").click({timeout:90000});
  await settled();
  await page.getByTestId("load-game").click({timeout:90000});
  await settled();await story();
  assert.equal((await dossier()).find(x=>x.id==="谁出卖了夜莺")?.status,"了结",
    "A failed trail must persist through a complete isolated Scheme VM restore");
  mkdirSync("artifacts",{recursive:true});
  await page.screenshot({path:"artifacts/web-game-paper-trail-failed.png"});
  assert.deepEqual(errors,[],"No browser errors in the optional investigation route");
  console.log("PASS: authentic day-four chapter-two save -> both forged rumors -> two world days -> newspaper -> five-stage courier scene -> lawful failed trail -> portable restore",
    JSON.stringify({initialDay,plantedDay,encounterDay,endedDay:view.day}));
}finally{
  await browser?.close();await server.close();
}
