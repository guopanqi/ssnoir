import assert from "node:assert/strict";
import {readFileSync,writeFileSync,mkdirSync} from "node:fs";
import {chromium} from "playwright";
import {createServer} from "vite";

/**
 * Separate genuine player choices from ONE real chapter-two save made by
 * third-act-route.mjs. No debugger presets, direct Scheme mutation, date
 * injection, fabricated inventory, or scenario result callbacks.
 */
const source=readFileSync("artifacts/web-game-before-banquet-save.json","utf8");
const server=await createServer({configFile:"vite.config.ts",
  server:{host:"127.0.0.1",port:4178,strictPort:true}});
let browser;
try{
  await server.listen();
  browser=await chromium.launch({headless:true,args:["--no-sandbox",
    "--enable-webgl","--enable-unsafe-swiftshader",
    "--use-gl=angle","--use-angle=swiftshader"]});
  const page=await browser.newPage({viewport:{width:1440,height:850}});
  const errors=[],stories=[];
  page.on("pageerror",e=>errors.push(String(e)));
  page.on("console",m=>{if(m.type()==="error")errors.push(m.text());});
  const read=()=>page.evaluate(()=>window.__SSNOIR_WEB__.snapshot());
  const settled=()=>page.waitForFunction(()=>{
    const s=window.__SSNOIR_WEB__?.snapshot();return Boolean(s&&!s.busy);
  },null,{timeout:90000});
  async function story(){
    for(let i=0;i<350;i++){
      await settled();
      const s=await read();
      if(!s.story)return;
      stories.push({kind:s.story.kind,text:s.story.text,speaker:s.story.speaker});
      await page.getByTestId("story-continue").click({timeout:90000});
    }
    throw new Error("Story queue did not drain");
  }
  async function state(){
    await settled();await story();
    const s=await read();
    assert.equal(s.error,"","No native Scheme runtime error");
    return s;
  }
  async function go(id){
    const s=await state();
    const row=s.entries.find(x=>x.id===id)||
      s.displayCards.find(x=>x.id===id)||
      s.mapPlaces.find(x=>x.id===id);
    assert(row&&!row.disabled,"Unavailable native node "+id+" "+
      JSON.stringify({day:s.day,mode:s.mode,path:s.path,
        entries:s.entries.map(x=>x.id),cards:s.displayCards.map(x=>x.id)}));
    const map=s.mode==="world"&&row.isPlace;
    await page.locator('[data-testid="'+(map?"map-place":"game-entry")+
      '"][data-node-id='+JSON.stringify(id)+']').click({timeout:90000});
    return state();
  }
  async function act(id){
    let s=await go(id);
    if(s.selectedAction===id){
      const die=s.dice[0];
      assert(die,"No genuine die to spend for "+id);
      const before=s.dice.length;
      await page.locator(".die:not([disabled])").first().click({timeout:90000});
      s=await state();
      assert.equal(s.selectedAction,null);
      if(s.mode==="scenario")assert.equal(s.dice.length,before-1);
    }
    return s;
  }
  async function round(){
    await page.getByTestId("end-turn").click({timeout:90000});
    return state();
  }
  async function dossiers(){
    await page.getByTestId("dossier-toggle").click({timeout:90000});
    await settled();
    const rows=(await read()).dossiers;
    await page.getByTestId("dossier-close").click({timeout:90000});
    await settled();
    return rows;
  }
  async function sleep(){
    const start=await state();let s=start;
    if(s.path[0]!=="家")s=await go("家");
    assert.equal(s.mode,"world");
    s=await go("住宿");
    const rest=["睡觉","蜷缩在门口"].find(x=>
      s.entries.some(n=>n.id===x&&!n.disabled));
    assert(rest,"Player must resolve their actual rest before time may advance "+
      JSON.stringify({day:s.day,restReasons:s.restReasons}));
    s=await act(rest);
    assert.equal(s.day,start.day+1);
    return s;
  }
  async function save(){
    await page.getByTestId("save-game").click({timeout:90000});
    await settled();
    const v=await page.evaluate(()=>localStorage.getItem("ssnoir-web-v1"));
    assert(v);return v;
  }
  async function load(value){
    await page.evaluate(v=>localStorage.setItem("ssnoir-web-v1",v),value);
    await page.getByTestId("load-game").click({timeout:90000});
    return state();
  }
  await page.goto("http://127.0.0.1:4178/?seed=42&fixture=max-roll",
    {waitUntil:"domcontentloaded"});
  await page.getByTestId("game-ready").waitFor({timeout:90000});
  await story();
  let s=await load(source);
  assert.equal(s.mode,"world");
  const beforeBanquet=s.day;
  s=await sleep();
  assert.equal(s.day,beforeBanquet+1);
  s=await go("格兰德酒店");
  const banquetDay=s.day; // The authored meeting date, not a later hospitalization date.
  s=await act("赴晚宴");
  assert.equal(s.mode,"scenario");
  // Allow the authored introductions to reach the second half. Do not
  // fabricate "met Walter": each real social action pushes his own Clock.
  for(let turn=1;turn<=3;turn++){
    s=await round();
    assert.equal(s.mode,"scenario");
  }
  assert(s.entries.some(x=>x.id==="陪他喝一杯"&&!x.disabled),
    "Walter must be a real second-half social card");
  let rolls=0;
  const decisions=[];
  for(let turn=4;turn<=6;turn++){
    for(let n=0;n<8;n++){
      s=await state();
      const card=s.entries.find(x=>x.id==="陪他喝一杯");
      if(!card||card.disabled||s.dice.length===0)break;
      s=await act("陪他喝一杯");
      rolls++;
      const clock=s.entries.find(x=>x.id==="陪他喝一杯")?.clocks?.[0];
      decisions.push({turn,outcome:s.lastAction?.result,dice:s.dice.length,
        clock,disabled:s.entries.find(x=>x.id==="陪他喝一杯")?.disabled});
    }
    s=await round();
  }
  assert.equal(s.mode,"world");
  assert(rolls>=3,"The six-point Walter connection costs real action dice");
  const allDossiers=await dossiers();
  const pending=allDossiers.find(x=>x.id==="陪沃尔特验货");
  console.log("WALTER BANQUET TRACE",JSON.stringify({rolls,decisions,
    dossiers:allDossiers.map(x=>({id:x.id,status:x.status})),
    finalStory:stories.slice(-12)}));
  assert.equal(pending?.status,"进行中",
    "Walter must be remembered by the authored city callback: "+JSON.stringify({rolls,decisions,
      dossierIds:allDossiers.map(x=>x.id)}));
  // Three real conversations can exhaust an injured detective. If the
  // Scenario hospitalizes him on the final dance, hospitalization itself
  // advances the calendar. It does NOT grant permission to sleep through
  // the next day's mandatory newspaper confrontation.
  assert(s.day===banquetDay||s.day===banquetDay+1,
    "The banquet may advance one day only through a real hospital collapse");
  if(s.day===banquetDay+1){
    assert(stories.some(x=>String(x.text).includes("诊所里醒过来")),
      "A banquet calendar jump must have an actual hospitalization narrative");
  }else{
    s=await sleep();
  }
  assert.equal(s.day,banquetDay+1,
    "The next authored obligation belongs on the day after the banquet");
  s=await go("剧院");
  assert(s.entries.some(x=>x.id==="见夜莺"&&!x.disabled));
  s=await act("见夜莺");
  assert(!s.restReasons.some(x=>x.node==="见夜莺"));
  s=await sleep();
  assert.equal(s.day,banquetDay+2,
    "Walter appears two days after the banquet, not two days after hospitalization");
  s=await go("格兰德酒店");
  assert(stories.some(x=>String(x.text).includes("沃尔特")),
    "The delayed hotel arrival must actually deliver Walter's dialogue");
  assert((await dossiers()).some(x=>x.id==="陪沃尔特验货"));
  // The claim is earned, not granted for free. Use original dock records.
  s=await go("码头");
  assert(s.entries.some(x=>x.id==="翻账房的记录"&&!x.disabled));
  for(let attempt=0;attempt<4;attempt++){
    const card=s.entries.find(x=>x.id==="翻账房的记录");
    if(!card||card.disabled)break;
    s=await act("翻账房的记录");
  }
  s=await go("格兰德酒店");
  assert(s.entries.some(x=>x.id==="陪沃尔特核赔"&&!x.disabled),
    "Real dock ledger progress must open the claim scenario");
  const claimSource=await save();
  const outcomes=[];
  for(const choice of ["坐实骗保","替他放过这一笔"]){
    s=await load(claimSource);
    s=await go("格兰德酒店");
    s=await act("陪沃尔特核赔");
    assert.equal(s.mode,"scenario");
    for(let n=0;n<4;n++){
      s=await act("核对伤势与工时");
      if(s.entries.some(x=>x.id==="坐实骗保"&&!x.disabled))break;
      if(s.dice.length===0)s=await round();
    }
    assert(s.entries.some(x=>x.id==="坐实骗保"&&!x.disabled),
      "The original evidence Clock must be satisfied before a verdict");
    assert(s.entries.some(x=>x.id===choice&&!x.disabled),
      "This claim option must be a genuine available player choice");
    s=await act(choice);
    assert.equal(s.mode,"world","The authored claim callback must return to city");
    const claim=(await dossiers()).find(x=>x.id==="陪沃尔特验货");
    assert.equal(claim?.status,"了结");
    assert(claim.steps.every(x=>x.done),
      "Meeting, ledger and claim verdict must have all happened");
    s=await go("格兰德酒店");
    assert(s.entries.some(x=>x.id==="替沃尔特跑外勤"&&!x.disabled),
      "The verdict must open genuine repeatable work");
    const pre=s.money;
    s=await act("替沃尔特跑外勤");
    assert(s.money>pre,"An actual fieldwork action pays through native inventory");
    const afterSave=await save();
    s=await load(afterSave);
    assert.equal((await dossiers()).find(x=>x.id==="陪沃尔特验货")?.status,"了结");
    const result={choice,day:s.day,pay:s.money-pre,claim:claim.log?.at(-1)?.text};
    outcomes.push(result);
    mkdirSync("artifacts",{recursive:true});
    writeFileSync("artifacts/web-game-walter-"+(choice==="坐实骗保"?"confirm":"mercy")+
      "-save.json",afterSave,"utf8");
  }
  assert.notDeepEqual(outcomes[0].claim,outcomes[1].claim,
    "The two lawful verdicts must have different authored journals");
  assert.notEqual(outcomes[0].pay,outcomes[1].pay,
    "Confirming vs sparing the claimant changes the actual paid work");
  assert.deepEqual(errors,[],"No console/page errors along either Walter route");
  await page.screenshot({path:"artifacts/web-game-walter-claim.png"});
  console.log("PASS: authentic banquet Walter relationship -> delayed hotel meeting -> "+
    "real dock manifest -> confirm/mercy claim -> distinct city fieldwork -> portable restore",
    JSON.stringify(outcomes));
}finally{
  await browser?.close();await server.close();
}
