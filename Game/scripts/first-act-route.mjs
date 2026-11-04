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
  const cashBeforeRehearsal=state.money;
  const diceBeforeRehearsal=state.dice.map(x=>({actorId:x.actorId,slotId:x.slotId,value:x.value}));
  await page.getByTestId("save-game").click({timeout:120000});
  await page.waitForFunction(()=>Boolean(localStorage.getItem("ssnoir-web-v1")),null,{timeout:90000});
  const dayEightSave=await page.evaluate(()=>localStorage.getItem("ssnoir-web-v1"));
  assert.equal(JSON.parse(dayEightSave).state.globals["世界日"],8);
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
  // The city save must be recoverable from a live Scenario without leaving
  // scenario-only dice, actions or temporary state in the world.
  assert(await page.getByTestId("save-game").isDisabled(),
    "A scenario must not be saved as a normal city world snapshot");
  assert(await page.getByTestId("load-game").isEnabled(),
    "The player must be able to recover from a scenario with a prior save");
  await page.getByTestId("load-game").click({timeout:180000});
  await settled();await story();
  state=await read();
  assert.equal(state.error,"","Restoring during a rehearsal must succeed");
  assert.equal(state.mode,"world","Restoring a city save abandons the active Scenario");
  assert.equal(state.day,8);
  assert.deepEqual(state.path,[]);
  assert.equal(state.money,cashBeforeRehearsal);
  assert.deepEqual(state.dice.map(x=>({actorId:x.actorId,slotId:x.slotId,value:x.value})),
    diceBeforeRehearsal,"The saved city dice must be restored without scenario contamination");
  assert(state.restReasons.some(x=>x.id==="观看正式彩排"),
    "The saved mandatory rehearsal must be available again");
  await page.screenshot({path:"artifacts/web-game-rehearsal-restored.png"});
  // Reenter the authored Scene and deliberately miss both delivery deadlines:
  // a single stage-work action cannot fill six scenery and eight light slots.
  await node("剧院");
  ({s:state}=await node("观看正式彩排"));
  assert.equal(state.mode,"scenario","Rehearsal must be reenterable after city restore");
  ({s:state}=await node("搬运布景"));
  assert.equal(state.selectedAction,"搬运布景");
  await page.locator(".die:not([disabled])").first().click({timeout:90000});
  await settled();await story();
  state=await read();
  assert.equal(state.selectedAction,null);
  assert.equal(state.error,"");
  for(let round=1;round<=5;round++){
    await page.getByTestId("end-turn").click({timeout:120000});
    await settled();
    await story();
    state=await read();
    assert.equal(state.error,"","Scenario failed on round "+round);
    assert.equal(state.lastAction?.title,"回合结算",
      "Round transition should expose its own authoritative consequences");
    assert(state.lastAction.changes.some(x=>x.label==="冷静"||x.label==="伤势"),
      "The authored time tax must be visible to the player");
    console.log("REHEARSAL ROUND",round,JSON.stringify({
      mode:state.mode,day:state.day,dice:state.dice.length,
      blockers:state.restReasons.map(x=>x.id)}));
    if(round<5)assert.equal(state.mode,"scenario","Rehearsal must last five rounds");
  }
  assert.equal(state.mode,"world","Authored fifth round must return to the city");
  assert.equal(state.day,8,"Scenario rounds must not advance the city date");
  assert(!state.restReasons.some(x=>x.id==="观看正式彩排"),
    "Completed rehearsal must clear the mandatory visit");
  assert.equal(state.money,cashBeforeRehearsal,
    "Missing both original authored delivery deadlines must award no rehearsal wages");
  assert.deepEqual(state.dice.map(x=>({actorId:x.actorId,slotId:x.slotId,value:x.value})),
    diceBeforeRehearsal,"Scenario end must restore the saved city's action dice slots");
  await page.screenshot({path:"artifacts/web-game-rehearsal-finished.png"});
  assert.deepEqual(exceptions,[],"No browser exceptions through the theatre scenario");
  console.log("PASS: farewell -> day-eight required rehearsal -> scenario dice work -> restore city during scenario -> reenter -> five-round missed deliveries -> city without wages or dice contamination");

  // The next required story beat is authored on day 13. Proceed by ordinary
  // sleeping/doorway-rest actions, never by setting a Scheme global.
  for(let day=8;day<13;day++){
    await endDay(day);
    state=await read();
    console.log("ACT TWO DAY",day+1,JSON.stringify({
      restReasons:state.restReasons.map(x=>x.id),mode:state.mode
    }));
  }
  assert.equal(state.day,13);
  assert(state.restReasons.some(x=>x.id==="赴试唱之约"),
    "The sponsor performance must be an actual day-thirteen obligation");
  await page.screenshot({path:"artifacts/web-game-sponsor-day13.png"});
  await node("剧院");
  const sponsor=await node("赴试唱之约");
  state=sponsor.s;
  assert.equal(state.error,"");
  assert.equal(state.mode,"scenario","The sponsor performance must transition into pursuit");
  assert(state.entries.some(x=>x.id==="公共走廊")&&
         state.entries.some(x=>x.id==="工作通道"),
    "The authored first-stage pursuit must offer two explorable routes");
  // Choose the route indicated by the narrated evidence the player actually
  // received; this does not inspect Scheme variables or force an outcome.
  const evidence=sponsor.lines.join("\n");
  const route=evidence.includes("工作通道的窄门正在回摆")?"工作通道":
    evidence.includes("公共走廊的地毯上有新踩出的湿脚印")?"公共走廊":null;
  assert(route,"The authored route clue must be delivered before decisions");
  const search=route==="工作通道"?"穿过工作通道":"沿走廊寻找";
  for(let attempt=0;attempt<24;attempt++){
    state=await read();
    if(state.mode==="scenario"&&
      state.entries.some(x=>x.id==="打听离场去向"))break;
    assert.equal(state.mode,"scenario","Pursuit must not prematurely exit phase one");
    if(state.path.length===0){
      await node(route);
      state=await read();
    }
    if(state.dice.length===0){
      await page.getByTestId("end-turn").click({timeout:120000});
      await settled();await story();
      assert.equal((await read()).error,"");
      continue;
    }
    const result=await node(search);
    assert.equal(result.s.selectedAction,search);
    const strongest=[...result.s.dice].sort((x,y)=>y.value-x.value)[0];
    assert(strongest,"No die available for the pursuit action");
    await page.getByTitle(strongest.actorName+" · "+strongest.value).first()
      .click({timeout:120000});
    await settled();await story();
    state=await read();
    assert.equal(state.error,"","Pursuit search action failed");
  }
  state=await read();
  assert(state.entries.some(x=>x.id==="打听离场去向"),
    "The player must be able to reach backstage by using authored route clues");
  await page.screenshot({path:"artifacts/web-game-pursuit-backstage.png"});
  // Intentionally miss the departure by spending all three remaining rounds
  // without following the car. This tests the narrative's legitimate failure.
  for(let round=1;round<=3;round++){
    await page.getByTestId("end-turn").click({timeout:120000});
    await settled();await story();
    state=await read();
    assert.equal(state.error,"","Backstage countdown failed on round "+round);
    if(round<3)assert.equal(state.mode,"scenario");
  }
  assert.equal(state.mode,"world","The missed-car result must return to the city");
  assert.equal(state.day,13);
  assert(!state.restReasons.some(x=>x.id==="赴试唱之约"));
  await page.getByTestId("dossier-toggle").click({timeout:120000});
  await settled();
  state=await read();
  const result=state.dossiers.find(x=>x.id==="最后一排的观众");
  assert(result&&/没记下车牌/.test(result.now),
    "Failed pursuit must authoritatively record the missing license plate");
  await page.getByTestId("dossier-close").click();
  await settled();
  await page.screenshot({path:"artifacts/web-game-pursuit-missed.png"});
  assert.deepEqual(exceptions,[],"No browser errors during sponsor pursuit");
  console.log("PASS: day-thirteen sponsor performance -> evidence-directed search -> backstage deadline -> missed vehicle -> city and dossier");

} finally {
  await browser?.close();
  await server.close();
}
