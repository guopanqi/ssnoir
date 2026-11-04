import assert from "node:assert/strict";
import {mkdirSync} from "node:fs";
import {chromium} from "playwright";
import {createServer} from "vite";

const server=await createServer({configFile:"vite.config.ts",
  server:{host:"127.0.0.1",port:4174,strictPort:true}});
let browser;
try {
  await server.listen();
  browser=await chromium.launch({headless:true,args:["--no-sandbox",
    "--enable-webgl","--enable-unsafe-swiftshader","--use-gl=angle","--use-angle=swiftshader"]});
  const page=await browser.newPage({viewport:{width:1440,height:850}});
  const exceptions=[];
  page.on("pageerror",e=>exceptions.push("pageerror: "+String(e)));
  page.on("console",msg=>{if(msg.type()==="error")exceptions.push("console: "+msg.text());});
  const read=()=>page.evaluate(()=>window.__SSNOIR_WEB__.snapshot());
  const settled=()=>page.waitForFunction(()=>{
    const state=window.__SSNOIR_WEB__?.snapshot();
    return state&&!state.busy;
  },null,{timeout:90000});
  async function story(){
    const lines=[];
    for(let i=0;i<350;i++){
      await settled();
      const state=await read();
      if(!state.story)return lines;
      lines.push(state.story.speaker+": "+state.story.text);
      await page.getByTestId("story-continue").click();
    }
    throw new Error("More than 350 story frames during an authored beat");
  }
  async function node(id){
    const before=await read();
    const card=before.entries.find(x=>x.id===id);
    if(!card)throw new Error("Missing authored node "+id+" at "+JSON.stringify({
      day:before.day,path:before.path,entries:before.entries.map(x=>x.id)}));
    if(card.disabled)throw new Error("Disabled authored node "+id+" at "+JSON.stringify({
      day:before.day,path:before.path,subtitle:card.subtitle,
      restReasons:before.restReasons}));
    await page.locator('[data-testid="game-entry"][data-node-id='+JSON.stringify(id)+']')
      .click({timeout:90000});
    await settled();
    const lines=await story();
    const s=await read();
    assert.equal(s.error,"","Authored action "+id+" failed");
    return {s,lines};
  }
  async function goHome(){
    const state=await read();
    if(state.path[0]==="家"){
      if(state.path.length!==1)throw new Error("Expected hotel root, got "+state.path);
    }else{
      await page.getByTestId("end-turn").click();
      await settled();await story();
      assert.deepEqual((await read()).path,["家"]);
    }
  }
  async function endDay(day){
    await goHome();
    const lodging=await node("住宿");
    // On rent arrears, the authored game allows sleeping at the locked door.
    // Do not grant money or force an unavailable bed.
    const sleep=lodging.s.entries.find(x=>
      (x.id==="睡觉"||x.id==="蜷缩在门口")&&!x.disabled);
    assert(sleep,"No playable sleep action after entering lodging on day "+day+
      ": "+JSON.stringify(lodging.s.entries.map(x=>({
        name:x.id,disabled:x.disabled,subtitle:x.subtitle}))));
    const finished=await node(sleep.id);
    assert.equal(finished.s.day,day+1,"Authored sleep must advance only one calendar day");
    assert.equal(finished.s.mode,"world");
    assert.deepEqual(finished.s.path,[],"New day returns to the city");
    return finished.lines;
  }
  mkdirSync("artifacts",{recursive:true});
  await page.goto("http://127.0.0.1:4174/?seed=42",{waitUntil:"domcontentloaded"});
  await page.getByTestId("game-ready").waitFor({timeout:90000});
  await story();
  let state=await read();
  assert.equal(state.day,1);
  assert(state.restReasons.some(x=>x.id==="旅馆清晨"),"First morning is obligatory");
  await node("家");
  ({s:state}=await node("旅馆清晨"));
  assert.equal(state.restBlocked,false);
  const dayStories={};
  for(let day=1;day<6;day++){
    const lines=await endDay(day);
    dayStories[day+1]=lines;
    const snapshot=await read();
    console.log("FIRST ACT DAY",day+1,
      JSON.stringify({restReasons:snapshot.restReasons,stories:lines.length,
        events:lines.filter(x=>x.includes("告别")||x.includes("告示"))}));
    if(day+1===5){
      await page.screenshot({path:"artifacts/web-game-first-act-day5.png"});
      assert(lines.some(x=>x.includes("明晚是夜莺在这里的最后一场演出")),
        "Day-5 world-root arrival must announce Nightingale's farewell");
    }
  }
  state=await read();
  assert.equal(state.day,6);
  assert(state.restReasons.some(x=>x.id==="夜莺最后一场演出"),
    "The authored final tavern show must block rest on day six");
  await node("老街酒馆");
  ({s:state}=await node("夜莺最后一场演出"));
  assert.equal(state.restBlocked,false,
    "Attending the farewell must clear day-six mandatory obligation");
  assert.equal(state.day,6);
  await page.screenshot({path:"artifacts/web-game-first-act-finale.png"});
  await page.getByTestId("dossier-toggle").click();
  await settled();
  state=await read();
  assert.equal(state.error,"");
  const first=state.dossiers.find(x=>x.id==="活下去");
  assert(first&&first.status==="了结",
    "Farewell's authored transition must close the first-act dossier");
  await page.getByTestId("dossier-close").click();
  await settled();
  assert.deepEqual(exceptions,[],"No browser exceptions during the first-act path");
  console.log("PASS: first-act fresh game -> five authored sleeps -> day-five world arrival -> day-six mandatory farewell -> act 2");

  // The farewell naturally opens the theatre; verify the next mandatory
  // objective is a genuine multi-round scenario, not a dialogue-only stub.
  await endDay(6);
  await endDay(7);
  state=await read();
  assert.equal(state.day,8);
  assert(state.restReasons.some(x=>x.id==="观看正式彩排"),
    "Day-eight first rehearsal must be an authored mandatory world action");
  await page.screenshot({path:"artifacts/web-game-rehearsal-day8.png"});
  await node("剧院");
  ({s:state}=await node("观看正式彩排"));
  assert.equal(state.error,"","The rehearsal must enter the current authored scenario");
  assert.equal(state.mode,"scenario","Rehearsal must start an isolated Scenario runtime");
  assert(state.entries.some(x=>x.id==="搬运布景"),"Backstage action cards must be playable");
  assert(state.entries.some(x=>x.id==="钟：歌曲进程" &&
    x.clocks.some(c=>c.label==="歌曲进程" && c.current===0 && c.max===5)),
    "The authored song clock must be visible as a live gauge, not just a note label");
  const beforeWorkDice=state.dice.length;
  ({s:state}=await node("搬运布景"));
  assert.equal(state.selectedAction,"搬运布景");
  await page.locator(".die:not([disabled])").first().click({timeout:90000});
  await settled();
  await story();
  state=await read();
  assert.equal(state.error,"","Rehearsal action must settle via original Scheme");
  assert.equal(state.selectedAction,null);
  assert.equal(state.dice.length,beforeWorkDice-1,"A real rehearsal action spends a die");
  await page.screenshot({path:"artifacts/web-game-rehearsal-action.png"});
  for(let round=1;round<=5;round++){
    await page.getByTestId("end-turn").click({timeout:120000});
    await settled();
    await story();
    state=await read();
    assert.equal(state.error,"","Scenario failed on round "+round);
    console.log("REHEARSAL ROUND",round,JSON.stringify({
      mode:state.mode,day:state.day,dice:state.dice.length,
      blockers:state.restReasons.map(x=>x.id)}));
    if(round<5)assert.equal(state.mode,"scenario","Rehearsal must last five rounds");
  }
  assert.equal(state.mode,"world","Authored fifth round must return to the city");
  assert.equal(state.day,8,"Scenario rounds must not advance the city date");
  assert(!state.restReasons.some(x=>x.id==="观看正式彩排"),
    "Completed rehearsal must clear the mandatory visit");
  await page.screenshot({path:"artifacts/web-game-rehearsal-finished.png"});
  assert.deepEqual(exceptions,[],"No browser exceptions through the theatre scenario");
  console.log("PASS: farewell -> day-eight required rehearsal -> dice work -> five rounds -> authored scenario result -> city");
} finally {
  await browser?.close();
  await server.close();
}
