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
      card.requires.some(x=>x.type==="die"))state=await nextRound();
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
      await page.getByTestId("end-turn").click({timeout:90000});
      await settled();await story();
      state=await read();
    }
    assert.deepEqual(state.path,["家"]);
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
  state=await action("潜入货运大楼");
  assert.equal(state.mode,"scenario","The original city case must open cargo infiltration");
  assert(state.entries.some(x=>x.id==="破解门铃"));
  state=await action("破解门铃");
  state=await action("小心翼翼地打开铁门");
  state=await action("小心翼翼地打开铁门");
  assert(state.entries.some(x=>x.id==="混进装卸队"),"Gate must unlock route choice");
  state=await action("混进装卸队");
  state=await action("抢搬重货");
  state=await action("抢搬重货");
  assert(state.entries.some(x=>x.id==="文件柜"),"Cargo work must open the archives");
  for(const container of ["文件柜","办公桌","外寄纸箱"]){
    state=await read();
    if(state.entries.some(x=>x.id==="与罗科谈判"))break;
    state=await action(container);
    state=await action(container);
    assert.equal(state.mode,"scenario","Archives must remain open until negotiation");
  }
  assert(state.entries.some(x=>x.id==="与罗科谈判"),
    "Legitimate searched records must permit negotiating with Rocco");
  state=await action("与罗科谈判");
  assert.equal(state.mode,"world","Authored negotiation returns to city");
  assert.equal(state.day,15);
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
  assert.deepEqual(errors,[],"No browser errors in the native third-act success route");
  console.log("PASS: real day-14 save -> five original recognition rolls -> manager verification -> night -> cargo archives -> Rocco agreement -> manager report -> authored dossier closed");
}finally{
  await browser?.close();await server.close();
}
