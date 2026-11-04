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
    const node=(prior.entries.find(x=>x.id===id&&!x.disabled)??
      prior.displayCards.find(x=>x.id===id&&!x.disabled));
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
    if(view.restReasons.some(x=>x.id==="醉客互相叫骂")){
      // This obligation is created by the player's second tavern shift.
      // It cannot be skipped by resting; resolve the original two-desk
      // Scenario. The outcome (including injury) remains authoritative.
      view=await visit("老街酒馆");
      view=await perform("醉客互相叫骂");
      assert.equal(view.mode,"scenario");
      for(let turn=0;turn<12&&view.mode==="scenario";turn++){
        for(let move=0;move<8&&view.mode==="scenario"&&view.dice.length;move++){
          const calm=["东边这桌：劝","西边这桌：劝"].find(id=>
            view.displayCards.some(x=>x.id===id&&!x.disabled));
          if(!calm)break;
          view=await perform(calm);
        }
        if(view.mode==="scenario"){
          await page.getByTestId("end-turn").click({timeout:90000});
          await settled();await story();
          view=await read();
        }
      }
      assert.equal(view.mode,"world","The tavern's authored brawl must exit normally or by clinic");
      assert(!view.restReasons.some(x=>x.id==="醉客互相叫骂"),
        "The genuine Scenario callback must release the mandatory tavern duty");
      console.log("PASS: optional investigation's second tavern shift -> native two-table brawl -> rest obligation released");
      view=await visit("家");
    }
    if(view.path.at(-1)!=="住宿"){
      await page.locator('[data-testid="game-entry"][data-node-id="住宿"]')
        .click({timeout:90000});
      await settled();await story();
    }
    view=await read();
    let rest=view.entries.find(x=>["睡觉","蜷缩在门口"].includes(x.id)&&!x.disabled);
    if(!rest&&!view.restBlocked){
      // A day already slept cannot be slept again without leaving the house.
      // Opening the home while already inside is not an exit. Follow the
      // author's explicit "出门后才能再睡" rule through the actual map.
      console.log("REST REQUIRES OUTING",JSON.stringify({day:view.day,
        path:view.path,money:view.money,restReasons:view.restReasons,
        lodging:view.entries.map(x=>[x.id,x.disabled,x.subtitle])}));
      await page.locator(".city-map-art").click({position:{x:600,y:300}});
      await settled();await story();
      const city=await read();
      assert.deepEqual(city.path,[],"The map outing must return to city");
      view=await visit("家");
      await page.locator('[data-testid="game-entry"][data-node-id="住宿"]')
        .click({timeout:90000});
      await settled();await story();
      view=await read();
      rest=view.entries.find(x=>["睡觉","蜷缩在门口"].includes(x.id)&&!x.disabled);
    }
    assert(rest,"The real city must provide an affordable sleep action on "+
      view.day+" "+JSON.stringify({restBlocked:view.restBlocked,restReasons:view.restReasons,
        money:view.money,path:view.path,entries:view.entries.map(x=>[x.id,x.disabled,x.subtitle])}));
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

  // The same genuine portable city snapshot now follows the contrasting
  // successful path. No authored flags, dates, clocks or injury are injected.
  // Preparing with paid tavern work and a drink is a PLAYER choice needed to
  // survive five rounds when the day-four save has low composure and injuries.
  await page.evaluate(text=>localStorage.setItem("ssnoir-web-v1",text),paperSave);
  await page.getByTestId("load-game").click({timeout:90000});
  await settled();await story();
  view=await read();
  assert.equal(view.day,initialDay);
  await rumorAt("剧院","放给剧院",0);
  await rumorAt("格兰德酒店","留给酒店",1);
  for(let i=0;i<2;i++)await sleep();
  await visit("家");
  await perform("看早报");
  view=await visit("老街酒馆");
  for(let attempt=0;attempt<3&&view.money<15;attempt++){
    assert(view.entries.some(x=>x.id==="看场子"&&!x.disabled),
      "The city must offer a legal paid shift before costly recovery");
    view=await perform("看场子");
  }
  assert(view.money>=15,"Two ordinary paid shifts must fund a recovery drink");
  const beforeDrink={money:view.money,composure:view.actors.find(x=>x.id==="player")?.composure};
  view=await perform("点一杯酒");
  assert.equal(view.money,beforeDrink.money-15);
  assert(view.actors.find(x=>x.id==="player")?.composure>beforeDrink.composure,
    "A real purchased drink restores composure before five dangerous rounds");
  view=await visit("剧院");
  view=await perform("等取件人");
  assert.equal(view.mode,"scenario");
  const successfulTrailStart=view.day;
  for(let round=1;round<=5&&view.mode==="scenario";round++){
    let moves=0;
    while(moves<3&&view.mode==="scenario"&&view.dice.length>0){
      view=await perform("跟紧");
      moves++;
    }
    assert.equal(view.error,"","Successful trail cannot produce a runtime error");
    if(view.mode==="scenario"){
      await page.getByTestId("end-turn").click({timeout:90000});
      await settled();await story();
      view=await read();
      assert.equal(view.error,"","Five-stage courier transition must succeed");
    }
    console.log("COURIER SUCCESS ROUND",JSON.stringify({
      round,mode:view.mode,day:view.day,health:view.health,composure:view.actors.find(x=>x.id==="player")?.composure}));
  }
  assert.equal(view.mode,"world","Following the real courier must finish after five authored stages");
  const successDossier=(await dossier()).find(x=>x.id==="谁出卖了夜莺");
  assert.equal(successDossier?.status,"了结");
  assert.match(successDossier.now,/科尔|渠道已经查清/,
    "A real successful tail must identify the source and preserve its journal");
  const sequel=(await dossier()).find(x=>x.id==="查早报供稿人");
  assert.equal(sequel?.status,"进行中",
    "Courier success opens the earlier-paper investigation, not a fake solved state");
  view=await visit("报社");
  assert(view.entries.some(x=>x.id==="问十一点零七分"),
    "The original report desk must expose the follow-up action");
  view=await perform("问十一点零七分");
  assert(view.entries.some(x=>x.id==="下废稿间"),
    "The source's own entry action must open the archive without a debug shortcut");
  await page.getByTestId("save-game").click({timeout:90000});
  await settled();
  await page.getByTestId("load-game").click({timeout:90000});
  await settled();await story();
  view=await read();
  assert.equal(view.mode,"world");
  assert.equal((await dossier()).find(x=>x.id==="查早报供稿人")?.status,"进行中");
  mkdirSync("artifacts",{recursive:true});
  await page.screenshot({path:"artifacts/web-game-paper-trail-success.png"});
  assert.deepEqual(errors,[],"Both player outcomes must be browser-error-free");
  console.log("PASS: same authored save -> paid preparation -> five courier stages -> identified Koer -> earlier-paper investigation opens -> portable restore",JSON.stringify({
    initialDay,successfulTrailStart,finishedDay:view.day,remainingMoney:view.money}));

  // Continue from the same accepted save through the entire original
  // "11:07" paper archive. Its three stages only advance after opponent
  // turns; the two different final evidence piles must BOTH be recovered.
  // If already injured, a lawful clinic collapse is not a dead end: the
  // authored case remains open, and the player may try again the next day.
  let archiveComplete=false,archiveAttempts=0,archivePlayerTurns=0;
  const paperClock=(v,label)=>{
    const matches=v.displayCards.flatMap(x=>x.clocks).filter(c=>
      c.label===label||c.displayLabel===label);
    assert(matches.length===1,
      "Every archive objective must expose one authoritative Clock "+label+
      " "+JSON.stringify(v.displayCards.map(x=>[x.id,x.clocks.map(c=>c.label)])));
    return matches[0];
  };
  for(let attempt=1;attempt<=3&&!archiveComplete;attempt++){
    archiveAttempts=attempt;
    view=await visit("报社");
    assert(view.entries.some(x=>x.id==="下废稿间"),
      "The journal's real archive entry must survive clinic recovery");
    view=await perform("下废稿间");
    assert.equal(view.mode,"scenario","The original basement must open in its own Scheme VM");
    const entryDay=view.day;
    for(let step=0;step<70&&view.mode==="scenario";step++){
      const available=id=>view.displayCards.some(x=>x.id===id&&!x.disabled);
      let goal=null,clockLabel=null;
      if(available("按日期整理")){
        goal="按日期整理";clockLabel="理清这批废稿";
      }else if(available("核对收件章")||available("按稿号归档")){
        const inbound=paperClock(view,"分清外来材料");
        const outgoing=paperClock(view,"分清报社稿件");
        goal=inbound.current<inbound.max?"核对收件章":"按稿号归档";
        clockLabel=inbound.current<inbound.max?"分清外来材料":"分清报社稿件";
        if(outgoing.current===outgoing.max&&inbound.current===inbound.max)goal=null;
      }else if(available("查首演日收件章")||available("寻找稿号四一七")){
        const envelope=paperClock(view,"剧院酒店来件");
        const proof=paperClock(view,"四号桌校样");
        goal=envelope.current<envelope.max?"查首演日收件章":"寻找稿号四一七";
        clockLabel=envelope.current<envelope.max?"剧院酒店来件":"四号桌校样";
        if(envelope.current===envelope.max&&proof.current===proof.max)goal=null;
      }
      if(available("排列两件证据")){
        view=await perform("排列两件证据");
        archiveComplete=view.mode==="world";
        break;
      }
      if(goal&&paperClock(view,clockLabel).current<paperClock(view,clockLabel).max&&
          view.dice.length>0){
        view=await perform(goal);
      }else{
        await page.getByTestId("end-turn").click({timeout:90000});
        await settled();await story();
        view=await read();
        archivePlayerTurns++;
      }
      assert.equal(view.error,"","The original archive must never fail a normal action");
    }
    if(archiveComplete)break;
    assert.equal(view.mode,"world",
      "An incomplete injured attempt must exit via the authored clinic collapse");
    assert.equal(view.day,entryDay+1,
      "Only a genuine hospitalization may leave the archive without evidence");
    const open=(await dossier()).find(x=>x.id==="查早报供稿人");
    assert.equal(open?.status,"进行中",
      "A collapsed investigation cannot fake a completed evidence chain");
    console.log("ARCHIVE CLINIC RECOVERY",JSON.stringify({attempt,day:view.day,health:view.health}));
  }
  assert(archiveComplete,"The player must be able to finish the full archive after lawful recovery");
  let earlyEvidence=(await dossier()).find(x=>x.id==="查早报供稿人");
  assert.equal(earlyEvidence?.status,"了结",
    "Both 11:07 and 11:35 papers must complete the real authored investigation");
  assert(earlyEvidence.steps.every(x=>x.done),
    "The journal should show a checked reporter ledger AND identified archive evidence");
  assert.match(earlyEvidence.now,/早于|供稿人|已经证实|已证实/,
    "The final dossier must distinguish evidence-before-letter from the anonymous source");
  await page.getByTestId("save-game").click({timeout:90000});
  await settled();
  await page.getByTestId("load-game").click({timeout:90000});
  await settled();await story();
  const restored=(await dossier()).find(x=>x.id==="查早报供稿人");
  assert.equal(restored?.status,"了结",
    "Final paper findings must persist through an isolated fresh VM");
  mkdirSync("artifacts",{recursive:true});
  await page.screenshot({path:"artifacts/web-game-early-paper-evidence.png"});
  assert.deepEqual(errors,[],"The full paper success path must not log browser errors");
  console.log("PASS: genuine Koer route -> reporter ledger -> archive three stages -> 11:07 envelope and 11:35 proof -> authored evidence-before-letter close -> save/restore",
    JSON.stringify({archiveAttempts,archivePlayerTurns,day:(await read()).day}));

  // Next first unverified cross-branch path: having *solved* the optional
  // investigation must not distort the city's independent machine calendar.
  // Sleep only through native world actions; the machine day must still
  // impose its genuine obligation and honour the investigation history.
  let machineDay=null;
  for(let elapsed=0;elapsed<12;elapsed++){
    view=await read();
    if(view.restReasons.some(x=>x.node==="到跳板去")){
      machineDay=view.day;break;
    }
    assert.equal(view.mode,"world");
    view=await sleep();
    assert.equal(view.error,"","Optional solved investigation cannot block unrelated city nights");
  }
  assert(machineDay!==null,
    "Even with early-paper proof, the city's machine arrival must still occur on schedule");
  const beforeMachine=(await dossier()).find(x=>x.id==="查早报供稿人");
  assert.equal(beforeMachine?.status,"了结");
  view=await visit("码头");
  view=await perform("到跳板去");
  assert(view.entries.some(x=>x.id==="看着这一天过完"&&!x.disabled),
    "The original terminal city action remains available after a solved paper branch");
  view=await perform("看着这一天过完");
  assert.equal(view.day,machineDay,
    "The machine encounter is an authored city event, not an extra world day");
  let all=await dossier();
  assert.equal(all.find(x=>x.id==="机器进入老街")?.status,"了结");
  assert.equal(all.find(x=>x.id==="查早报供稿人")?.status,"了结",
    "Independent city climax must not erase the earlier newspaper evidence");
  await page.getByTestId("save-game").click({timeout:90000});
  await settled();
  await page.getByTestId("load-game").click({timeout:90000});
  await settled();await story();
  all=await dossier();
  assert.equal(all.find(x=>x.id==="查早报供稿人")?.status,"了结");
  assert.equal(all.find(x=>x.id==="机器进入老街")?.status,"了结");
  view=await sleep();
  assert.equal(view.day,machineDay+1);
  assert(!view.restReasons.some(x=>x.node==="看着这一天过完"),
    "A finished city's climactic blocker must not regenerate after the next day");
  assert.deepEqual(errors,[],"Following the optional mystery may not break the mainline ending");
  console.log("PASS: successfully solved optional investigation -> normal independent machine day -> two settled dossiers -> portable save and next-day play",
    JSON.stringify({archiveAttempts,machineDay,followingDay:view.day}));



}finally{
  await browser?.close();await server.close();
}
