import assert from "node:assert/strict";
import {readFileSync,mkdirSync} from "node:fs";
import {chromium} from "playwright";
import {createServer} from "vite";

/**
 * Continues from the exact city save produced by first-act-route.mjs.
 * The dev-only max-roll input controls randomness, not Scheme events, clocks,
 * outcomes, dates or decisions. Every transition uses the real browser UI.
 */
const day14Save=readFileSync("artifacts/web-game-day14-save.json","utf8");
assert.equal(JSON.parse(day14Save).state.globals["世界日"],14);
const server=await createServer({configFile:"vite.config.ts",
  server:{host:"127.0.0.1",port:4174,strictPort:true}});
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
      await settled();const state=await read();
      if(!state.story)return;
      await page.getByTestId("story-continue").click({timeout:90000});
    }
    throw new Error("Authored story did not drain");
  }
  async function node(id){
    const before=await read();
    const entry=before.entries.find(x=>x.id===id);
    assert(entry&&!entry.disabled,
      "Unavailable authored node "+id+" at "+JSON.stringify({
        day:before.day,mode:before.mode,path:before.path,
        entries:before.entries.map(x=>[x.id,x.disabled])}));
    const map=before.mode==="world"&&!before.path.length&&entry.isPlace;
    const testId=map?"map-place":"game-entry";
    await page.locator('[data-testid="'+testId+'"][data-node-id='+JSON.stringify(id)+']')
      .click({timeout:90000});
    await settled();await story();
    const state=await read();
    assert.equal(state.error,"","Original authored "+id+" failed");
    return state;
  }
  async function nextRound(){
    await page.getByTestId("end-turn").click({timeout:90000});
    await settled();await story();
    const state=await read();
    assert.equal(state.error,"","Scenario round failed");
    return state;
  }
  async function action(id){
    let state=await read();
    const card=state.entries.find(x=>x.id===id);
    assert(card,"Missing authored action "+id);
    while(state.mode==="scenario"&&state.dice.length===0&&
      card.requires.some(x=>x.type==="die")){
      state=await nextRound();
      // A legitimate collapse/timeout can end the scenario while rerolling.
      if(state.mode!=="scenario")return state;
    }
    state=await node(id);
    if(state.selectedAction===id){
      assert(state.dice.length>0,"Action lacks a real die "+id);
      await page.locator(".die:not([disabled])").first().click({timeout:90000});
      await settled();await story();
      state=await read();
      assert.equal(state.error,"","Die resolution failed for "+id);
      assert.equal(state.selectedAction,null,"Die must settle chosen action "+id);
    }
    return state;
  }
  async function sleep(){
    let state=await read();
    if(state.path[0]!=="家"){
      // The location board is now a direct route: no intermediate world exit.
      await page.locator('[data-testid="map-place"][data-node-id="家"]')
        .click({timeout:90000});
      await settled();await story();
      state=await read();
    }
    assert.equal(state.error,"","Going to lodging through the city board must succeed");
    assert.deepEqual(state.path,["家"],"City map must open the authored home directly");
    state=await node("住宿");
    const rest=state.entries.find(x=>
      (x.id==="睡觉"||x.id==="蜷缩在门口")&&!x.disabled);
    assert(rest,"No legitimate night action on day "+state.day);
    const day=state.day;
    state=await action(rest.id);
    assert.equal(state.day,day+1);
    assert.equal(state.mode,"world");
    assert.deepEqual(state.path,[]);
    return state;
  }

  await page.goto("http://127.0.0.1:4174/?seed=42&fixture=max-roll",
    {waitUntil:"domcontentloaded"});
  await page.getByTestId("game-ready").waitFor({timeout:90000});
  await story();
  await page.evaluate(text=>localStorage.setItem("ssnoir-web-v1",text),day14Save);
  await page.getByTestId("load-game").click({timeout:90000});
  await settled();await story();
  let state=await read();
  assert.equal(state.error,"");
  assert.equal(state.mode,"world");assert.equal(state.day,14);
  assert.deepEqual(state.path,[]);
  assert(state.entries.some(x=>x.id==="剧院"));
  await node("剧院");
  state=await action("等待黑衣人");
  assert.equal(state.mode,"scenario");
  for(let n=0;n<5;n++){
    state=await action("辨认车辆特征");
    assert.equal(state.error,"");
    if(n<4)assert.equal(state.mode,"scenario",
      "The original 10-point identification clock cannot finish early");
  }
  assert.equal(state.mode,"world","The fifth successful die must end the original chase");
  assert.equal(state.day,14,"Encounter rounds do not consume city days");
  await node("剧院");
  state=await action("交回跟车记录");
  assert.equal(state.mode,"world");
  state=await sleep();
  assert.equal(state.day,15);
  await node("剧院");
  state=await action("询问核查结果");
  assert.equal(state.mode,"world");
  state=await action("问夜莺旧事");
  assert.equal(state.mode,"world");
  await page.getByTestId("back").click({timeout:90000});
  await settled();await story();
  state=await read();assert.deepEqual(state.path,[]);
  await node("货运公司");
  state=await action("调查罗科租处");
  assert.equal(state.mode,"world");
  let caseSettled=false;
  for(let attempt=1;attempt<=3 && !caseSettled;attempt++){
    state=await action("潜入货运大楼");
    assert.equal(state.mode,"scenario","The city case must open actual infiltration");
    assert(state.entries.some(x=>x.id==="破解门铃"));
    const initialHealth=state.health;
    console.log("CARGO ATTEMPT",attempt,JSON.stringify({day:state.day,health:initialHealth,dice:state.dice.length}));
    // The original injury and time rules can cause a hospital collapse.
    // Never force the stage or heal. A failed attempt remains retryable.
    for(const id of ["破解门铃","小心翼翼地打开铁门",
      "小心翼翼地打开铁门","混进装卸队",
      "抢搬重货","抢搬重货"]){
      if(state.mode!=="scenario")break;
      state=await action(id);
    }
    if(state.mode==="scenario"){
      assert(state.entries.some(x=>x.id==="文件柜"),
        "Legitimate cargo work must lead to the archives");
      for(const container of ["文件柜","办公桌","外寄纸箱"]){
        if(state.mode!=="scenario"||state.entries.some(x=>x.id==="与罗科谈判"))break;
        state=await action(container);
        if(state.mode!=="scenario")break;
        state=await action(container);
      }
    }
    if(state.mode==="scenario"){
      assert(state.entries.some(x=>x.id==="与罗科谈判"),
        "The original documents must permit the agreement");
      state=await action("与罗科谈判");
      assert.equal(state.mode,"world","Authored negotiation returns to city");
      caseSettled=true;
      break;
    }
    console.log("CARGO LEGITIMATE EXIT",JSON.stringify({
      day:state.day,health:state.health,mode:state.mode}));
    assert.equal(state.mode,"world","A failed infiltration must return to the city");
    assert(state.day>=15&&state.day<=17,"A failed scenario cannot jump calendar dates");
    assert(state.entries.some(x=>x.id==="货运公司"),
      "Failed infiltration cannot remove the company from the city");
    // Normal authored retry: re-enter on the next legal opportunity.
    await node("货运公司");
  }
  assert(caseSettled,"Hospitalization and ordinary failure must not strand Rocco case");
  assert(state.day>=15&&state.day<=17);
  await node("剧院");
  state=await action("提交调查报告");
  await page.getByTestId("dossier-toggle").click({timeout:90000});
  await settled();
  state=await read();
  const caseEntry=state.dossiers.find(x=>x.id==="银色打火机");
  assert.equal(caseEntry?.status,"了结",
    "The manager's original settlement must close the silver-lighter case");
  await page.getByTestId("dossier-close").click({timeout:90000});
  await settled();
  mkdirSync("artifacts",{recursive:true});
  await page.screenshot({path:"artifacts/web-game-rocco-resolved.png"});
  // The first chapter must lead into chapter two by regular authored actions,
  // not by calling the debugging preset or editing the calendar.
  await page.getByTestId("back").click({timeout:90000});
  await settled();await story();
  assert.deepEqual((await read()).path,[]);
  state=await node("剧院");
  assert(state.entries.some(x=>x.id==="和夜莺去河边"&&!x.disabled),
    "The manager report must lead to the original Nightingale invitation");
  state=await action("和夜莺去河边");
  assert.equal(state.mode,"world");
  assert(!state.mapPlaces.some(x=>x.id==="格兰德酒店"),
    "Chapter two must not start before the next authored world day");
  const firstChapterFinalDay=state.day;
  state=await sleep();
  assert.equal(state.day,firstChapterFinalDay+1);
  assert(state.mapPlaces.some(x=>x.id==="格兰德酒店"),
    "A natural sleep after completing chapter one must unlock chapter-two locations");
  await page.getByTestId("dossier-toggle").click({timeout:90000});
  await settled();
  state=await read();
  assert(state.dossiers.some(x=>x.id==="格兰德酒店晚宴"&&x.primary),
    "Chapter-two opening must expose its real banquet dossier");
  await page.getByTestId("dossier-close").click({timeout:90000});
  await settled();
  await page.screenshot({path:"artifacts/web-game-natural-chapter2.png"});
  console.log("PASS: real day-14 save -> original Rocco case -> manager report -> Nightingale river invitation -> natural next-day chapter-two opening");

  // FIRST UNSIGNED PLAYER PATH AFTER THE PREVIOUSLY ACCEPTED CHAPTER-ONE ROUTE:
  // let the actual city calendar announce the banquet, enter through the map,
  // spend real dice, let the six authored opponent turns finish the event,
  // and verify the city's resulting dossier and portable save.
  const chapterTwoStart=state.day;
  assert(state.dossiers.some(x=>x.id==="格兰德酒店晚宴"&&x.status==="进行中"));
  state=await sleep();
  assert.equal(state.day,chapterTwoStart+1,"The second chapter's second day must be real");
  await page.getByTestId("save-game").click({timeout:90000});
  await settled();
  const preBanquet=await read();
  const banquetSave=await page.evaluate(()=>localStorage.getItem("ssnoir-web-v1"));
  assert(banquetSave,"Chapter-two city snapshot must be persistable");
  await page.getByTestId("load-game").click({timeout:90000});
  await settled();await story();
  state=await read();
  assert.equal(state.day,preBanquet.day);
  assert.equal(state.mode,"world");
  assert.deepEqual(state.mapPlaces,preBanquet.mapPlaces,
    "A chapter-two save/restore must preserve the authored map and unlocks");
  state=await sleep();
  assert.equal(state.day,chapterTwoStart+2,"The third chapter-two day is the banquet night");
  assert(state.restReasons.some(x=>x.location==="格兰德酒店"),
    "The genuine banquet-night obligation must block sleeping");
  const banquetDate=state.day;
  await node("格兰德酒店");
  state=await action("赴晚宴");
  assert.equal(state.mode,"scenario","The authentic banquet entrance must open a scenario");
  assert.equal(state.day,banquetDate,"A scene entrance cannot advance the city calendar");
  assert(state.actors.some(x=>x.name==="夜莺"),
    "Nightingale must be present at the beginning of the banquet");
  assert(state.entries.some(x=>x.id==="加入赞助人"&&!x.disabled),
    "Banquet's first phase must expose social groups");

  const banquetRounds=[];
  for(let round=1;round<=6;round++){
    state=await read();
    assert.equal(state.mode,"scenario","Banquet must remain active for round "+round);
    const firstAct=["加入赞助人","加入剧院那桌","听他们谈钱","走进舞池"];
    const secondAct=["和薇拉谈","陪他喝一杯","和贝恩斯谈","和记者谈"];
    const choices=[...secondAct,...firstAct].filter(id=>
      state.entries.some(x=>x.id===id&&!x.disabled));
    assert(choices.length>0,"The banquet must offer a real decision in round "+round);
    if(state.dice.length>0){
      const choice=choices[(round-1)%choices.length];
      const diceBefore=state.dice.length;
      state=await action(choice);
      assert.equal(state.mode,"scenario");
      assert.equal(state.dice.length,diceBefore-1,
        "Talking to "+choice+" must consume a genuine die");
      banquetRounds.push({round,choice});
      const recovery=state.entries.find(x=>
        ["去露台透气","在餐桌边停下"].includes(x.id)&&!x.disabled);
      if(recovery&&state.dice.length>0){
        const health=state.health;
        state=await action(recovery.id);
        assert.equal(state.mode,"scenario");
        assert(state.health<=health,
          "A rest action cannot inflict an injury outside the authored opponent turn");
      }
    }
    state=await nextRound();
    if(round<6)assert.equal(state.mode,"scenario",
      "The fixed six-dance countdown cannot end in an earlier round");
  }
  assert.equal(state.mode,"world","The original six-dance event must return to the city");
  assert.equal(state.day,banquetDate,"The banquet is one evening, not six world days");
  assert.equal(state.failure,null,"Completing the banquet must not end the whole game");
  assert(state.dice.length>0,"Returning to the city must restore its saved die pool");
  await page.getByTestId("dossier-toggle").click({timeout:90000});
  await settled();
  state=await read();
  const banquetEntry=state.dossiers.find(x=>x.id==="格兰德酒店晚宴");
  assert.equal(banquetEntry?.status,"了结",
    "The authored Scenario result must close the city's banquet dossier");
  assert(banquetEntry?.log?.length>0,
    "The guest outcomes must be recorded by the city callback");
  await page.getByTestId("dossier-close").click({timeout:90000});
  await settled();
  await page.getByTestId("save-game").click({timeout:90000});
  await settled();
  const closedBanquetSave=await page.evaluate(()=>localStorage.getItem("ssnoir-web-v1"));
  assert(closedBanquetSave&&closedBanquetSave!==banquetSave,
    "Banquet outcomes must enter the persistent Scheme world datum");
  await page.getByTestId("load-game").click({timeout:90000});
  await settled();await story();
  state=await read();
  assert.equal(state.mode,"world");
  assert.equal(state.day,banquetDate);
  await page.getByTestId("dossier-toggle").click({timeout:90000});
  await settled();
  state=await read();
  assert.equal(state.dossiers.find(x=>x.id==="格兰德酒店晚宴")?.status,"了结",
    "Banquet settlement must persist across VM isolation and reload");
  await page.getByTestId("dossier-close").click({timeout:90000});
  await settled();
  await page.screenshot({path:"artifacts/web-game-banquet-finished.png"});
  assert.deepEqual(errors,[],"Chapter-two opening and banquet produced no browser errors");
  console.log("PASS: chapter-two natural countdown -> banquet six real rounds -> callback dossier settlement -> save/restore",JSON.stringify(banquetRounds));
  // The next mainline beat is the leaked newspaper, not a debug preset or
  // an automatic dossier completion. The player must attend at the theater.
  state=await sleep();
  assert.equal(state.day,chapterTwoStart+3,"The cover story appears on chapter-two day four");
  assert(state.restReasons.some(x=>x.node==="见夜莺"),
    "The leaked newspaper must create a real before-rest obligation");
  state=await node("剧院");
  assert(state.entries.some(x=>x.id==="见夜莺"&&!x.disabled),
    "The event must be playable at the authored location");
  state=await action("见夜莺");
  assert.equal(state.mode,"world");
  assert(!state.restReasons.some(x=>x.node==="见夜莺"),
    "Talking to Nightingale must release the author's rest requirement");
  await page.getByTestId("dossier-toggle").click({timeout:90000});
  await settled();
  state=await read();
  assert.equal(state.dossiers.find(x=>x.id==="封面上的夜莺")?.status,"了结",
    "The printed cover story must be marked settled only after the scene");
  const inquiry=state.dossiers.find(x=>x.id.includes("追查")||x.id.includes("泄密"));
  assert(inquiry,"The paper leak must unlock an independently playable investigation");
  await page.getByTestId("dossier-close").click({timeout:90000});
  await settled();
  console.log("PASS: chapter-two fourth-day newspaper -> authored theater confrontation -> separate investigation opened");

  // The new harbor plan is a public calendar event independent of whether
  // the player pursues the optional investigation or supporting cast.
  for(let index=4;index<=8;index++){
    state=await sleep();
    assert.equal(state.day,chapterTwoStart+index,
      "Each new chapter-two date must arise from a legitimate night");
  }
  await page.getByTestId("dossier-toggle").click({timeout:90000});
  await settled();
  state=await read();
  assert.equal(state.dossiers.find(x=>x.id==="机器进入老街")?.status,"进行中",
    "Phase B must publish the real machine-arrival mainline dossier");
  await page.getByTestId("dossier-close").click({timeout:90000});
  await settled();
  state=await node("码头");
  assert(state.displayCards.some(x=>x.id==="标注：码头施工"),
    "Phase B city must visually show the published construction, not only a hidden flag");
  console.log("PASS: natural chapter-two day nine -> public new-harbor plan -> city visibly changes");

  // The chapter's current climax happens exactly six ordinary days after
  // phase B. A visit alone must NOT settle the day; the player must actively
  // choose when to stop intervening at the gangway.
  for(let index=9;index<=14;index++){
    state=await sleep();
    assert.equal(state.day,chapterTwoStart+index);
  }
  assert(state.restReasons.some(x=>x.node==="到跳板去"),
    "Machine arrival must block the day until the player handles its scenes");
  state=await node("码头");
  state=await action("到跳板去");
  assert.equal(state.mode,"world","Gangway is an authored city event, not a debug scenario");
  assert(state.restReasons.some(x=>x.node==="看着这一天过完"),
    "Arrival alone cannot silently bypass the rest of the machine-day choices");
  const availableInterventions=["拦住艾迪","跟林停机","自己查设备","劝人群退"]
    .filter(id=>state.entries.some(x=>x.id===id&&!x.disabled));
  if(availableInterventions.length&&state.dice.length){
    const id=availableInterventions[0];
    const before=state.dice.length;
    state=await action(id);
    assert.equal(state.dice.length,before-1,
      "A selected optional intervention has a real opportunity cost");
  }
  assert(state.entries.some(x=>x.id==="看着这一天过完"&&!x.disabled));
  state=await action("看着这一天过完");
  assert.equal(state.mode,"world");
  assert.equal(state.day,chapterTwoStart+14);
  assert(!state.restReasons.some(x=>x.node==="看着这一天过完"),
    "The player-chosen ending must release the machine-day rest blocker");
  await page.getByTestId("dossier-toggle").click({timeout:90000});
  await settled();
  state=await read();
  const machines=state.dossiers.find(x=>x.id==="机器进入老街");
  assert.equal(machines?.status,"了结",
    "Machine-day resolution must settle the authored chapter climax");
  assert(machines?.log?.length>0,"Scheme must record what occurred at the dock");
  await page.getByTestId("dossier-close").click({timeout:90000});
  await settled();
  await page.getByTestId("save-game").click({timeout:90000});
  await settled();
  await page.getByTestId("load-game").click({timeout:90000});
  await settled();await story();
  state=await read();
  assert.equal(state.day,chapterTwoStart+14);
  await page.getByTestId("dossier-toggle").click({timeout:90000});
  await settled();
  state=await read();
  assert.equal(state.dossiers.find(x=>x.id==="机器进入老街")?.status,"了结",
    "The chapter ending must survive a full isolated VM restore");
  await page.getByTestId("dossier-close").click({timeout:90000});
  await settled();
  await page.screenshot({path:"artifacts/web-game-machine-arrival-resolved.png"});
  assert.deepEqual(errors,[],"No browser errors from the beginning through the machine climax");
  console.log("PASS: natural day-four leak -> phase B -> six-day deployment -> optional intervention -> machine-day finish -> restore");
}finally{
  await browser?.close();await server.close();
}
