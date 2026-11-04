import assert from "node:assert/strict";
import { mkdirSync } from "node:fs";
import { chromium } from "playwright";
import { createServer } from "vite";

const server=await createServer({configFile:"vite.config.ts",
  server:{host:"127.0.0.1",port:4173,strictPort:true}});
let browser;
try {
  await server.listen();
  browser=await chromium.launch({headless:true,args:["--no-sandbox",
    "--enable-webgl","--enable-unsafe-swiftshader","--use-gl=angle","--use-angle=swiftshader"]});
  const page=await browser.newPage({viewport:{width:1440,height:850}});
  const errors=[];
  page.on("pageerror",error=>errors.push("pageerror: "+String(error)));
  page.on("console",msg=>{
    if(msg.type()==="error")errors.push("console: "+msg.text());
    if(msg.text().startsWith("[SSNoir] web "))
      console.log("BROWSER: "+msg.text());
  });
  const read=()=>page.evaluate(()=>window.__SSNOIR_WEB__.snapshot());
  const settled=()=>page.waitForFunction(()=>{
    const s=window.__SSNOIR_WEB__?.snapshot();
    return s&&!s.busy;
  },null,{timeout:90000});
  async function drainStory(){
    await settled();
    for(let i=0;i<250;i++){
      const state=await read();
      if(!state.story)return;
      await page.getByTestId("story-continue").click();
    }
    throw new Error("Story contains more than 250 blocking frames");
  }
  async function clickNode(id){
    const card=page.locator('[data-testid="game-entry"][data-node-id='+JSON.stringify(id)+']');
    await card.click({timeout:90000});
    await settled();
    await drainStory();
    const s=await read();
    assert.equal(s.error,"","Scheme command failed after "+id);
    return s;
  }
  mkdirSync("artifacts",{recursive:true});
  await page.goto("http://127.0.0.1:4173/?seed=42",{waitUntil:"domcontentloaded"});
  await page.getByTestId("game-ready").waitFor({timeout:90000});
  await drainStory();
  const initial=await read();
  assert.equal(initial.mode,"world");
  assert.equal(initial.day,1);
  assert(initial.entries.some(x=>x.id==="家"&&x.isPlace),"Original city must contain the hotel");
  assert(initial.restBlocked,"The authored first-morning event must block resting");
  await page.screenshot({path:"artifacts/web-game-city.png"});
  let state=await clickNode("家");
  assert.deepEqual(state.path,["家"]);
  assert(state.entries.some(x=>x.id==="旅馆清晨"));
  await page.screenshot({path:"artifacts/web-game-place.png"});
  state=await clickNode("旅馆清晨");
  assert.equal(state.day,1);
  assert.equal(state.restBlocked,false,"Completing the authored morning releases rest blockers");
  await page.getByTestId("back").click();
  await settled();
  state=await read();
  assert.deepEqual(state.path,[],"Hotel exit should return to the city");
  assert(state.entries.some(x=>x.id==="老街酒馆"),"First-morning action should unlock the tavern");
  state=await clickNode("老街酒馆");
  assert.equal(state.path[0],"老街酒馆");
  assert(state.entries.some(x=>x.id==="看场子"),"Tavern must offer its real authored job");
  const oldDice=state.dice.length,oldMoney=state.money;
  const card=page.locator('[data-testid="game-entry"][data-node-id="看场子"]');
  await card.click();
  await settled();
  state=await read();
  assert.equal(state.selectedAction,"看场子");
  assert.equal(state.requiredDice,1);
  await page.locator(".die:not([disabled])").first().click();
  await settled();
  await drainStory();
  state=await read();
  assert.equal(state.error,"","Tavern work must be resolved by the original Scheme runtime");
  assert.equal(state.selectedAction,null);
  assert.equal(state.dice.length,oldDice-1,"One player die must be spent");
  assert(state.money>=oldMoney,"Original first tavern job cannot remove cash");
  await page.screenshot({path:"artifacts/web-game-action.png"});
  await page.getByTestId("save-game").click();
  await page.waitForFunction(()=>Boolean(localStorage.getItem("ssnoir-web-v1")),null,{timeout:30000});
  const saved=await page.evaluate(()=>JSON.parse(localStorage.getItem("ssnoir-web-v1")));
  assert.equal(saved.format,"ssnoir-world");
  assert.equal(saved.state.globals["世界日"],1);
  const cashAtSave=state.money;
  await page.getByTestId("end-turn").click();
  await settled();
  await drainStory();
  state=await read();
  assert.equal(state.day,1,"The top shortcut must navigate home, not skip the authored sleeping action");
  assert.deepEqual(state.path,["家"]);
  state=await clickNode("住宿");
  assert(state.entries.some(x=>x.id==="睡觉"&&!x.disabled),"The actual Scheme sleep action must be available");
  state=await clickNode("睡觉");
  assert.equal(state.day,2,"The authored sleep node must advance the world exactly one day");
  assert.equal(state.mode,"world");
  assert.deepEqual(state.path,[],"A new day returns to the city");
  assert(state.dice.length>oldDice-1,"The new day must roll a fresh action pool");
  await page.screenshot({path:"artifacts/web-game-next-day.png"});
  console.log("WEB SMOKE: restoring saved world from day 2");
  await page.getByTestId("load-game").click({timeout:120000});
  await page.waitForFunction(() => {
    const s=window.__SSNOIR_WEB__?.snapshot();
    return s && !s.busy && (s.day===1 || Boolean(s.error));
  },null,{timeout:150000});
  await drainStory();
  state=await read();
  assert.equal(state.error,"");
  assert.equal(state.day,1,"Loading must restore the actual authored calendar");
  assert.equal(state.money,cashAtSave,"Loading must restore the runtime inventory");
  assert.deepEqual(state.path,[]);
  await page.screenshot({path:"artifacts/web-game-restored.png"});
  assert.deepEqual(errors,[],"No runtime or browser errors");
  console.log("PASS: real Scheme city -> hotel morning -> tavern work/die -> save -> sleep/day 2 -> restore day 1");
} finally {
  await browser?.close();
  await server.close();
}
