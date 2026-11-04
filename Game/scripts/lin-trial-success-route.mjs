import assert from "node:assert/strict";
import {readFileSync,mkdirSync} from "node:fs";
import {chromium} from "playwright";
import {createServer} from "vite";

// Source: the saved world immediately before the ORIGINAL dock trial, produced
// by the long new-game -> chapter-two Chromium route. Two player approaches,
// one authentic starting save; no direct Scheme flags, clocks or date edits.
const fixture=readFileSync("artifacts/web-game-lin-before-trial-save.json","utf8");
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
    const s=await read(),entry=s.entries.find(x=>x.id===id);
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
      await page.locator(".die:not([disabled])").first().click({timeout:90000});
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
    const startDay=s.day,priorMoney=s.money;
    if(!s.path.length)s=await click("码头");
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
    assert.equal(s.day,startDay,"Successful Scenario does not advance the city calendar");
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
    outcomes.push({route:scenario.route,spent,rounds,journal});
  }
  assert.notDeepEqual(outcomes[0].journal,outcomes[1].journal,
    "Different ways of saving the trial must produce different durable stories");
  mkdirSync("artifacts",{recursive:true});
  await page.screenshot({path:"artifacts/web-game-lin-trial-success.png"});
  assert.deepEqual(errors,[],"No browser errors in the actual dock trial successes");
  console.log("PASS: same authentic Lin pre-trial save -> parameter vs worker-experience success -> distinct authored journal -> fresh VM restore",
    JSON.stringify(outcomes.map(({route,spent,rounds})=>({route,spent,rounds}))));
}finally{await browser?.close();await server.close();}
