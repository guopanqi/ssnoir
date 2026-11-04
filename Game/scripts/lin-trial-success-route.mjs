import assert from "node:assert/strict";
import {readFileSync,mkdirSync} from "node:fs";
import {chromium} from "playwright";
import {createServer} from "vite";

// Source: the saved world immediately before the ORIGINAL dock trial, produced
// by the long new-game -> chapter-two Chromium route. Two player approaches,
// one authentic starting save; no direct Scheme flags, clocks or date edits.
const combined=process.env.SSNOIR_LIN_FROM_EVIDENCE==="1";
const fixture=readFileSync(combined?
  "artifacts/web-game-after-paper-evidence-save.json":
  "artifacts/web-game-lin-before-trial-save.json","utf8");
const server=await createServer({configFile:"vite.config.ts",
  server:{host:"127.0.0.1",port:4177,strictPort:true}});
let browser;
try{
  await server.listen();
  browser=await chromium.launch({headless:true,args:["--no-sandbox",
    "--enable-webgl","--enable-unsafe-swiftshader",
    "--use-gl=angle","--use-angle=swiftshader"]});
  const page=await browser.newPage({viewport:{width:1440,height:850}});
  const errors=[],stories=[];
  page.on("pageerror",e=>errors.push(String(e)));
  page.on("console",msg=>{if(msg.type()==="error")errors.push(msg.text());});
  const read=()=>page.evaluate(()=>window.__SSNOIR_WEB__.snapshot());
  const settled=()=>page.waitForFunction(()=>{
    const s=window.__SSNOIR_WEB__?.snapshot();return s&&!s.busy;
  },null,{timeout:90000});
  async function story(){
    for(let n=0;n<400;n++){
      await settled();const s=await read();
      if(!s.story)return;
      stories.push({speaker:s.story.speaker,kind:s.story.kind,text:s.story.text});
      await page.getByTestId("story-continue").click({timeout:90000});
    }
    throw new Error("Trial story queue never drained");
  }
  async function click(id){
    const s=await read(),entry=s.entries.find(x=>x.id===id)||
      s.displayCards.find(x=>x.id===id);
    assert(entry&&!entry.disabled,
      "Actual Scheme node unavailable: "+id+" "+JSON.stringify({
        mode:s.mode,path:s.path,entries:s.entries.map(x=>[x.id,x.disabled])}));
    const map=s.mode==="world"&&!s.path.length&&entry.isPlace;
    await page.locator('[data-testid="'+(map?"map-place":"game-entry")+
      '"][data-node-id='+JSON.stringify(id)+']').click({timeout:90000});
    await settled();await story();
    const result=await read();
    assert.equal(result.error,"","Original "+id+" must be executable");
    return result;
  }
  async function back(){
    await page.getByTestId("back").click({timeout:90000});
    await settled();await story();
  }
  async function roll(id){
    let s=await click(id);
    if(s.selectedAction===id){
      assert(s.dice.length>0,"Trial action must have its real available die");
      // Successful corrections require thoughtful allocation, not whichever
      // die happens to appear first in the HUD. Choose the best currently
      // available player die, exactly as a human can do through the interface.
      const options=s.dice.map((die,index)=>({...die,index}))
        .filter(die=>die.actorId==="player");
      assert(options.length>0,"Trial has no available player die");
      const best=options.reduce((a,b)=>b.value>a.value?b:a);
      await page.locator(".die:not([disabled])").nth(best.index).click({timeout:90000});
      await settled();await story();
      s=await read();
      assert.equal(s.error,"","Authoritative trial action failed");
      assert.equal(s.selectedAction,null);
    }
    return s;
  }
  async function endRound(){
    await page.getByTestId("end-turn").click({timeout:90000});
    await settled();await story();
    const s=await read();
    assert.equal(s.error,"","Unmodified opponent rule failed");
    return s;
  }
  async function records(){
    await page.getByTestId("dossier-toggle").click({timeout:90000});
    await settled();
    const ds=(await read()).dossiers;
    await page.getByTestId("dossier-close").click({timeout:90000});
    await settled();
    return ds;
  }
  // The evidence-route save can carry a second-tavern-shift brawl obligation.
  // Sleep is legitimately blocked until the original two-table Scenario ends:
  // do not reset an authored duty or advance the calendar in the harness.
  async function settleMandatoryTavernBrawl(){
    let view=await read();
    const due=view.restReasons.find(x=>x.id==="醉客互相叫骂");
    if(!due)return view;
    const initialDay=view.day;
    assert.equal(due.location,"老街酒馆",
      "A rest blocker must identify its real playable city location");
    await page.locator('[data-testid="map-place"][data-node-id="老街酒馆"]')
      .click({timeout:90000});
    await settled();await story();
    view=await click("醉客互相叫骂");
    assert.equal(view.mode,"scenario",
      "The bartender's required brawl must enter the original Scenario");
    let usedDice=0,turns=0;
    for(;turns<12&&view.mode==="scenario";turns++){
      for(let move=0;move<8&&view.mode==="scenario"&&view.dice.length;move++){
        const action=["东边这桌：劝","西边这桌：劝"].find(id=>
          view.displayCards.some(x=>x.id===id&&!x.disabled));
        if(!action)break;
        const before=view.dice.length;
        view=await roll(action);
        usedDice++;
        if(view.mode==="scenario")assert.equal(view.dice.length,before-1,
          "Tavern resolution must consume real authored action dice");
      }
      if(view.mode==="scenario")view=await endRound();
    }
    assert.equal(view.mode,"world","The real tavern duty must have a legal exit");
    assert(!view.restReasons.some(x=>x.id==="醉客互相叫骂"),
      "The original encounter callback must release the before-rest blocker");
    assert(view.day===initialDay||view.day===initialDay+1,
      "Only hospitalization may advance the day while resolving a brawl");
    console.log("PASS: saved evidence carries an authentic tavern obligation -> "+
      "player resolves both tables -> native rest blocker released",JSON.stringify({
        fromDay:initialDay,afterDay:view.day,turns,usedDice,
        health:view.health,remaining:view.restReasons}));
    return view;
  }
  async function sleep(){
    const initial=await read(),before=initial.day;
    let s=await read();
    if(s.restReasons.some(x=>x.id==="醉客互相叫骂")){
      s=await settleMandatoryTavernBrawl();
      if(s.day!==before){
        assert.equal(s.day,before+1,
          "Hospitalization is the only accepted day change during tavern resolution");
        return s;
      }
    }
    if(s.path[0]!=="家"){
      await page.locator('[data-testid="map-place"][data-node-id="家"]')
        .click({timeout:90000});
      await settled();await story();
      s=await read();
    }
    assert.deepEqual(s.path,["家"]);
    await click("住宿");
    s=await read();
    let rest=["睡觉","蜷缩在门口"].find(id=>
      s.entries.some(x=>x.id===id&&!x.disabled));
    if(!rest&&!s.restBlocked){
      // Authored anti-double-click sleep latch: leaving home is required.
      // Only the real map exit and re-entry may clear the latch.
      console.log("LIN SLEEP NEEDS OUTING",JSON.stringify({day:s.day,
        money:s.money,restReasons:s.restReasons,
        cards:s.entries.map(x=>[x.id,x.disabled,x.subtitle])}));
      await page.locator(".city-map-art").click({position:{x:600,y:300}});
      await settled();await story();
      assert.deepEqual((await read()).path,[]);
      await page.locator('[data-testid="map-place"][data-node-id="家"]')
        .click({timeout:90000});
      await settled();await story();
      await click("住宿");
      s=await read();
      rest=["睡觉","蜷缩在门口"].find(id=>
        s.entries.some(x=>x.id===id&&!x.disabled));
    }
    assert(rest,"The world must offer legal sleep or outdoor lodging "+
      JSON.stringify({day:s.day,path:s.path,money:s.money,
        restBlocked:s.restBlocked,restReasons:s.restReasons,
        lodging:s.entries.map(x=>[x.id,x.disabled,x.subtitle])}));
    s=await click(rest);
    assert.equal(s.day,before+1,"Only authored lodging advances the day");
    assert.deepEqual(s.path,[]);
    return s;
  }
  async function load(){
    await page.evaluate(text=>localStorage.setItem("ssnoir-web-v1",text),fixture);
    await page.getByTestId("load-game").click({timeout:90000});
    await settled();await story();
    const s=await read();
    assert.equal(s.mode,"world");
    assert.equal(s.error,"");
    assert(s.mapPlaces.some(x=>x.id==="码头"));
    return s;
  }
  await page.goto("http://127.0.0.1:4177/?seed=42&fixture=max-roll",
    {waitUntil:"domcontentloaded"});
  await page.getByTestId("game-ready").waitFor({timeout:90000});
  await story();
  const outcomes=[];
  const cases=[
    {route:"参数",actions:[
      ["处理偏载货箱","重算货箱配重"],
      ["处理钢索异响","检查张力曲线"],
      ["处理混装货物","重做货物分类"]]},
    {route:"经验",actions:[
      ["处理偏载货箱","看轮印和绑绳"],
      ["处理钢索异响","听钢索的声音"],
      ["处理混装货物","问过去怎么堆"]]}
  ];
  for(const scenario of cases){
    let s=await load();
    let startDay=s.day,priorMoney=s.money;
    if(combined){
      const startingEvidence=(await records()).find(x=>x.id==="查早报供稿人");
      assert.equal(startingEvidence?.status,"了结",
        "The combined scenario must start after the actual 11:07 evidence completion");
      assert(startingEvidence.steps.every(x=>x.done));
      while(s.day<25)s=await sleep();
      assert.equal(s.day,25,
        "The combined path must reach the real Phase B trial date by lodging");
      assert(s.mapPlaces.some(x=>x.id==="三号货栈工棚"),
        "Real evidence must not erase Lin's accessible workshop");
      s=await click("三号货栈工棚");
      assert.equal(s.error,"","Lin's moving-workshop arrival must remain playable");
      await page.locator('[data-testid="map-place"][data-node-id="码头"]')
        .click({timeout:90000});
      await settled();await story();
      s=await read();
      assert.deepEqual(s.path,["码头"],
        "The same city day must allow a genuine map journey from Lin's workshop to the dock");
      // The source evidence save is day 23. The required tavern brawl
      // may hospitalize the player and legitimately advance to day 24.
      // The authored trial is not started until day 25. Establish the
      // Scenario's actual calendar and starting resources at its entrance,
      // not at the earlier evidence-save checkpoint.
      startDay=s.day;
      priorMoney=s.money;
      assert.equal(startDay,25,"Only the original date progression opens this trial");
    }else if(!s.path.length)s=await click("码头");
    assert(s.entries.some(x=>x.id==="开始试运行"));
    s=await click("开始试运行");
    assert.equal(s.mode,"scenario");
    const clocks=[
      ["偏载货箱","处理偏载货箱"],
      ["钢索异响","处理钢索异响"],
      ["混装货物","处理混装货物"]
    ];
    let spent=0,rounds=0;
    // Authored 5-point gauges: each real high-roll gains 2, requiring 3
    // actions per anomaly. Skip disabled work only after actual clock success.
    for(const [group,work] of scenario.actions){
      for(let attempts=0;attempts<7&&s.mode==="scenario";attempts++){
        while(s.mode==="scenario"&&s.dice.length===0){
          s=await endRound();rounds++;
        }
        if(s.mode!=="scenario")break;
        if(s.path.length)await back();
        s=await read();
        const entry=s.entries.find(x=>x.id===group);
        if(!entry||entry.disabled)break; // Author's completed state.
        s=await click(group);
        const action=s.entries.find(x=>x.id===work&&!x.disabled);
        if(!action){
          if(s.mode==="scenario")await back();
          break;
        }
        const previousDice=s.dice.length;
        s=await roll(work);
        spent++;
        if(s.mode==="scenario")
          assert.equal(s.dice.length,previousDice-1,
            "Every correction must spend exactly one actual die");
        if(s.mode==="scenario"&&!s.path.length)continue;
      }
      if(s.mode!=="scenario")break;
    }
    assert.equal(s.mode,"world",
      "All three genuine correction clocks must finish before stability collapses");
    assert.equal(s.day,startDay,
      "A successful correction must not be confused with an injury-driven " +
      "hospitalization: "+JSON.stringify({route:scenario.route,combined,startDay,
        endDay:s.day,health:s.health,actors:s.actors,spent,rounds,
        lastAction:s.lastAction,restReasons:s.restReasons,
        stories:stories.slice(-14)}));
    assert.equal(s.money,priorMoney,"Trial corrections do not pay or charge city money");
    const lin=(await records()).find(x=>x.id==="林与新机器");
    assert.equal(lin?.steps?.[1]?.done,true,
      "A successful trial must complete Lin's test step");
    assert(lin.log.some(x=>x.text.includes("试运行成功")),
      "Successful trial result is journaled to Lin's authoritative world");
    const journal=lin.log.map(x=>x.text).join("\n");
    if(scenario.route==="经验"){
      assert.match(journal,/经验解决/,
        "Hands-on worker knowledge must produce an experience-dominant result");
    }else{
      assert.match(journal,/参数修正/,
        "The calculated route must produce parameter-dominant result");
    }
    await page.getByTestId("save-game").click({timeout:90000});
    await settled();
    const saved=await page.evaluate(()=>localStorage.getItem("ssnoir-web-v1"));
    assert(saved&&saved!==fixture,"The original trial result must have an independent save");
    await page.getByTestId("load-game").click({timeout:90000});
    await settled();await story();
    const restored=(await records()).find(x=>x.id==="林与新机器");
    assert.equal(restored.steps[1].done,true);
    assert.equal(restored.log.map(x=>x.text).join("\n"),journal,
      "The successful trial story must survive a fresh isolated Scheme VM");
    if(combined){
      const evidence=(await records()).find(x=>x.id==="查早报供稿人");
      assert.equal(evidence?.status,"了结",
        "A successful machine trial cannot reopen completed 11:07 evidence");
      assert(evidence.steps.every(x=>x.done));
    }

    // Actual consequences cross the story boundary: Lin's response to
    // machine safety during the independent public deployment is determined
    // by what he learned from this trial, not a frontend probability label.
    const machineDay=startDay+6;
    s=await read();
    while(s.day<machineDay)s=await sleep();
    assert.equal(s.day,machineDay);
    assert(s.restReasons.some(x=>x.node==="到跳板去"),
      "The public machine day must arrive independently of a successful trial");
    if(s.path[0]!=="码头"){
      await page.locator('[data-testid="map-place"][data-node-id="码头"]')
        .click({timeout:90000});
      await settled();await story();
      s=await read();
    }
    s=await click("到跳板去");
    const humane=s.entries.some(x=>x.id==="跟林停机"&&!x.disabled);
    const investigate=s.entries.some(x=>x.id==="自己查设备"&&!x.disabled);
    assert.notEqual(humane,investigate,
      "The two alternative machine-safety actions must never coexist");
    assert.equal(humane,scenario.route==="经验",
      "The authored human-cost lesson must change Lin's machine-day choice");
    assert.equal(investigate,scenario.route==="参数",
      "Pure parameter success must leave riskier inspection to the detective");
    const safety=humane?"跟林停机":"自己查设备";
    const dieCount=s.dice.length;
    s=await roll(safety);
    assert.equal(s.dice.length,dieCount-1,
      "Intervening in either safety branch consumes a real city die");
    assert(s.entries.some(x=>x.id==="看着这一天过完"&&!x.disabled));
    s=await click("看着这一天过完");
    assert.equal(s.day,machineDay);
    const resolved=(await records()).find(x=>x.id==="机器进入老街");
    assert.equal(resolved?.status,"了结");
    await page.getByTestId("save-game").click({timeout:90000});
    await settled();
    const endingSave=await page.evaluate(()=>localStorage.getItem("ssnoir-web-v1"));
    assert(endingSave!==fixture&&endingSave!==saved);
    await page.getByTestId("load-game").click({timeout:90000});
    await settled();await story();
    assert.equal((await records()).find(x=>x.id==="机器进入老街")?.status,"了结",
      "The experience-derived machine outcome must persist across fresh VM load");
    // Second independent continuation from the SAME successful trial save:
    // do not skip Joe's optional qualification. Complete the native 8-point
    // Clock with actual day-limited dice and check the original machine-day
    // character choice remains governed by Lin's earlier lived experience.
    await page.evaluate(text=>localStorage.setItem("ssnoir-web-v1",text),saved);
    await page.getByTestId("load-game").click({timeout:90000});
    await settled();await story();
    s=await read();
    assert.equal(s.day,startDay);
    s=await sleep();
    assert.equal(s.day,startDay+1);
    let training=(await records()).find(x=>x.id==="林与新机器");
    assert(training&&training.status!=="了结",
      "The day after a successful trial must offer real worker training");
    const trainingPlace=training.where;
    assert(["码头","港务技术区"].includes(trainingPlace),
      "Lin's company status owns the authored training place");
    s=await click(trainingPlace);
    s=await click("陪老乔培训");
    let trainingRolls=0,previousProgress=0;
    for(let attempt=0;attempt<15;attempt++){
      training=(await records()).find(x=>x.id==="林与新机器");
      if(training.status==="了结")break;
      s=await read();
      if(s.dice.length===0){
        s=await sleep();
        assert(s.day<machineDay,
          "The original training must be finishable before deployment");
        s=await click(trainingPlace);
        s=await click("陪老乔培训");
      }
      const realActions=["逐项解释按钮","拿码头设备作比",
        "拆开系统缩写","按真实货物还原",
        "陪他核对步骤","让他按习惯做"];
      s=await read();
      const choice=realActions.find(id=>s.entries.some(x=>x.id===id&&!x.disabled));
      assert(choice,"Each incomplete training phase must present an authored action");
      const beforeDice=s.dice.length;
      s=await roll(choice);
      trainingRolls++;
      assert.equal(s.dice.length,beforeDice-1,
        "Training consumes the actual city die, never free scripted progress");
      training=(await records()).find(x=>x.id==="林与新机器");
      const gauge=training.clocks.find(x=>x.label==="基础操作");
      if(gauge){
        assert(gauge.current>=previousProgress&&gauge.current<=8);
        previousProgress=gauge.current;
      }
    }
    training=(await records()).find(x=>x.id==="林与新机器");
    assert.equal(training?.status,"了结",
      "Training after the trial success must close the original worker dossier");
    assert(training.steps.every(x=>x.done),
      "Trial, training and completion must all be represented in the real dossier");
    await page.getByTestId("save-game").click({timeout:90000});
    await settled();
    const qualifiedSave=await page.evaluate(()=>localStorage.getItem("ssnoir-web-v1"));
    assert(qualifiedSave&&qualifiedSave!==saved);
    await page.getByTestId("load-game").click({timeout:90000});
    await settled();await story();
    training=(await records()).find(x=>x.id==="林与新机器");
    assert.equal(training.status,"了结",
      "Completed worker training survives a fresh world VM");
    s=await read();
    while(s.day<machineDay)s=await sleep();
    assert.equal(s.day,machineDay);
    s=await click("码头");
    s=await click("到跳板去");
    const qualifiedHumane=s.entries.some(x=>x.id==="跟林停机"&&!x.disabled);
    const qualifiedInspect=s.entries.some(x=>x.id==="自己查设备"&&!x.disabled);
    assert.equal(qualifiedHumane,scenario.route==="经验",
      "Finishing training cannot erase the prior lived-experience distinction");
    assert.equal(qualifiedInspect,scenario.route==="参数",
      "A parameter-led trial must still require Neil's riskier inspection");
    s=await roll(qualifiedHumane?"跟林停机":"自己查设备");
    s=await click("看着这一天过完");
    const qualifiedEnding=(await records()).find(x=>x.id==="机器进入老街");
    assert.equal(qualifiedEnding?.status,"了结");
    assert.equal((await records()).find(x=>x.id==="林与新机器")?.status,"了结",
      "A machine-day ending must not downgrade completed qualification");
    await page.getByTestId("save-game").click({timeout:90000});
    await settled();
    const qualifiedEndingSave=await page.evaluate(()=>localStorage.getItem("ssnoir-web-v1"));
    await page.getByTestId("load-game").click({timeout:90000});
    await settled();await story();
    assert.equal((await records()).find(x=>x.id==="林与新机器")?.status,"了结");
    assert.equal((await records()).find(x=>x.id==="机器进入老街")?.status,"了结");
    if(combined){
      const combinedDossiers=await records();
      for(const id of ["谁出卖了夜莺","查早报供稿人","林与新机器","机器进入老街"]){
        const d=combinedDossiers.find(x=>x.id===id);
        assert.equal(d?.status,"了结",
          "All four independent authored outcomes must survive the same isolated VM: "+id);
        assert(d.steps.every(x=>x.done),
          "The cross-branch checkpoint cannot forge or lose completed steps: "+id);
      }
      // Next playable checkpoint after the combined machine-day victory:
      // the city must permit another real night and preserve independent
      // dossiers after a complete isolated Scheme VM reconstruction.
      const closedIds=["谁出卖了夜莺","查早报供稿人","林与新机器","机器进入老街"];
      const historyBefore=Object.fromEntries(closedIds.map(id=>{
        const d=combinedDossiers.find(x=>x.id===id);
        return [id,{status:d.status,steps:d.steps,log:d.log}];
      }));
      assert.equal((await read()).day,machineDay);
      const tomorrow=await sleep();
      assert.equal(tomorrow.day,machineDay+1,
        "Only real lodging or lawful hospitalization advances the next chapter day");
      assert.equal(tomorrow.mode,"world");
      assert.equal(tomorrow.error,"");
      assert(!tomorrow.restReasons.some(x=>
        x.node==="到跳板去"||x.node==="看着这一天过完"),
        "A settled machine-day must never leave its rest blocker active");
      await page.getByTestId("save-game").click({timeout:90000});
      await settled();
      const nextDaySave=await page.evaluate(()=>localStorage.getItem("ssnoir-web-v1"));
      assert(nextDaySave&&nextDaySave!==qualifiedEndingSave,
        "Following-day world changes must persist in a new portable save");
      await page.getByTestId("load-game").click({timeout:90000});
      await settled();await story();
      assert.equal((await read()).day,machineDay+1);
      const historyAfter=await records();
      for(const id of closedIds){
        const d=historyAfter.find(x=>x.id===id);
        assert.deepEqual({status:d?.status,steps:d?.steps,log:d?.log},historyBefore[id],
          "Following-day progression cannot erase independently settled history: "+id);
      }
      console.log("PASS: original evidence + Lin safety and worker training -> "+
        "legal following night -> all four closed histories intact on VM restore",
        JSON.stringify({route:scenario.route,fromDay:machineDay,nextDay:tomorrow.day}));
    }
    assert(qualifiedEndingSave!==endingSave,
      "Optional training must produce a meaningfully distinct persisted character history");
    outcomes.push({route:scenario.route,spent,rounds,journal,
      humane,safety,machineDay,endingSave,trainingRolls,qualifiedEndingSave});
    console.log("PASS: authentic trial success -> completed Joe qualification -> genuine machine-day safety action -> two settled dossiers -> VM restore",
      JSON.stringify({route:scenario.route,trainingPlace,trainingRolls,machineDay,
        safety:qualifiedHumane?"跟林停机":"自己查设备"}));
  }
  assert.notDeepEqual(outcomes[0].journal,outcomes[1].journal,
    "Different ways of saving the trial must produce different durable stories");
  assert.notEqual(outcomes[0].safety,outcomes[1].safety,
    "Prior choices must change which original machine intervention is available");
  assert.notEqual(outcomes[0].endingSave,outcomes[1].endingSave,
    "The two character arcs must have genuinely distinct serialized outcomes");
  mkdirSync("artifacts",{recursive:true});
  await page.screenshot({path:"artifacts/web-game-lin-trial-success.png"});
  assert.deepEqual(errors,[],"No browser errors in the actual dock trial successes");
  console.log(combined?
    "PASS: single real save with 11:07 evidence -> two Lin trial successes -> Joe qualified -> machine settlement -> all four dossiers intact":
    "PASS: same authentic Lin pre-trial save -> parameter vs worker-experience success -> distinct authored journal -> fresh VM restore",
    JSON.stringify(outcomes.map(({route,spent,rounds,safety,machineDay})=>
      ({route,spent,rounds,safety,machineDay}))));
}finally{await browser?.close();await server.close();}
