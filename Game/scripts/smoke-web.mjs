import assert from "node:assert/strict";
import { mkdirSync } from "node:fs";
import { chromium } from "playwright";
import { createServer } from "vite";

const liveBase=process.env.SSNOIR_WEB_BASE_URL;
const server=liveBase?null:await createServer({configFile:"vite.config.ts",
  server:{host:"127.0.0.1",port:4173,strictPort:true}});
let browser;
try {
  await server?.listen();
  browser=await chromium.launch({headless:true,args:["--no-sandbox",
    "--enable-webgl","--enable-unsafe-swiftshader","--use-gl=angle","--use-angle=swiftshader"]});
  const page=await browser.newPage({viewport:{width:1440,height:850}});
  const errors=[];
  page.on("pageerror",error=>errors.push("pageerror: "+String(error)));
  page.on("console",msg=>{
    if(msg.type()==="error")errors.push("console: "+msg.text());
    if(msg.text().startsWith("[SSNoir] web ") || msg.text().startsWith("[SSNoir] enter place"))
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
    const current=await read();
    const mapPlace=current.mode==="world" && current.path.length===0 &&
      current.entries.some(entry=>entry.id===id && entry.isPlace);
    const testId=mapPlace?"map-place":"game-entry";
    const card=page.locator('[data-testid="'+testId+'"][data-node-id='+JSON.stringify(id)+']');
    await card.click({timeout:90000});
    await settled();
    await drainStory();
    const s=await read();
    assert.equal(s.error,"","Scheme command failed after "+id);
    return s;
  }
  mkdirSync("artifacts",{recursive:true});
  const gameUrl=liveBase?new URL("?seed=42",liveBase.endsWith("/")?liveBase:liveBase+"/").href
    :"http://127.0.0.1:4173/?seed=42";
  await page.goto(gameUrl,{waitUntil:"domcontentloaded"});
  await page.getByTestId("game-ready").waitFor({timeout:90000});
  await drainStory();
  const initial=await read();
  assert.equal(initial.mode,"world");
  assert.equal(initial.day,1);
  assert(initial.entries.some(x=>x.id==="家"&&x.isPlace),"Original city must contain the hotel");
  assert(initial.restBlocked,"The authored first-morning event must block resting");
  const worldPlaces=initial.entries.filter(x=>x.isPlace);
  assert.equal(await page.getByTestId("map-place").count(),worldPlaces.length,
    "Map pins must be exactly the authored current locations");
  const mapPin=page.locator('[data-testid="map-place"][data-node-id="家"]');
  assert(await mapPin.isVisible(),"The actual hotel must be visible on the board");
  assert(await mapPin.evaluate(el=>el.classList.contains("urgent")),
    "The required first-morning visit is visibly marked on the map");
  const diceLayout=await page.locator(".hud-dice").boundingBox();
  const mapLayout=await page.locator(".city-map").boundingBox();
  const inventoryLayout=await page.locator(".hud-inventory").boundingBox();
  assert(diceLayout&&mapLayout&&inventoryLayout);
  assert(diceLayout.y>=mapLayout.y+mapLayout.height-3,
    "Dice should be docked below the city surface");
  assert(inventoryLayout.x>diceLayout.x,
    "Inventory belongs at the right side of the resource HUD");
  assert.deepEqual(
    await page.locator(".hud-inventory .inventory-item").allTextContents(),
    initial.inventory.map(item=>item.id+"× "+item.quantity),
    "The inventory HUD must reflect native item quantities");

  await page.screenshot({path:"artifacts/web-game-city.png"});
  let state=await clickNode("家");
  assert.deepEqual(state.path,["家"]);
  assert(state.entries.some(x=>x.id==="旅馆清晨"));
  assert(await page.locator(".city-map .map-location.is-current").isVisible(),
    "Entered location remains identifiable on the city map");
  assert(await page.locator('[data-testid="map-place"][data-node-id="家"]').isDisabled(),
    "An entered map pin is no longer an illegal world navigation shortcut");
  await page.screenshot({path:"artifacts/web-game-place.png"});
  state=await clickNode("旅馆清晨");
  assert.equal(state.day,1);
  assert.equal(state.restBlocked,false,"Completing the authored morning releases rest blockers");
  await page.getByTestId("back").click();
  await settled();
  state=await read();
  assert.deepEqual(state.path,[],"Hotel exit should return to the city");
  assert(state.entries.some(x=>x.id==="老街酒馆"),"First-morning action should unlock the tavern");
  await page.getByTestId("dossier-toggle").click();
  await settled();
  state=await read();
  assert.equal(state.error,"","Read original Scheme dossier");
  assert.equal(state.dossierOpen,true);
  assert(state.dossiers.length>0,"Current authored first-act dossier must list a task");
  const preview=state.dossiers.find(x=>x.primary)??state.dossiers[0];
  assert(preview.id,"An authored dossier must have an id");
  if(preview.status!=="了结"&&preview.steps.some(x=>!x.done))
    assert(await page.getByText("下一步："+preview.steps.find(x=>!x.done).text).count()>0,
      "UI should reveal the next authored step");
  await page.screenshot({path:"artifacts/web-game-dossier.png"});
  await page.getByTestId("dossier-close").click();
  await settled();
  assert.equal((await read()).dossierOpen,false);
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
  assert(await page.getByTestId("allocation-panel").isVisible(),
    "Action selection must explain how to use the bottom dice bar");
  assert(await page.locator('[data-testid="game-entry"][data-node-id="看场子"]').evaluate(
    el=>el.classList.contains("selected")),
    "Pending action remains visually selected instead of looking unavailable");
  const diceBeforeCancel=state.dice;
  await page.getByTestId("cancel-action").click();
  await settled();
  state=await read();
  assert.equal(state.selectedAction,null,"Cancel must clear pending allocation");
  assert.deepEqual(state.dice,diceBeforeCancel,"Cancel must not spend any die");
  await card.click();
  await settled();
  assert.equal((await read()).selectedAction,"看场子");

  await page.locator(".die:not([disabled])").first().click();
  await settled();
  await drainStory();
  state=await read();
  assert.equal(state.error,"","Tavern work must be resolved by the original Scheme runtime");
  assert.equal(state.selectedAction,null);
  assert.equal(state.dice.length,oldDice-1,"One player die must be spent");
  assert.equal(await page.locator(".hud-dice .die").count(),state.dice.length,
    "Action HUD must remove the spent native slot");
  assert.deepEqual(
    await page.locator(".hud-inventory .inventory-item").allTextContents(),
    state.inventory.map(item=>item.id+"× "+item.quantity),
    "Inventory HUD must update after a genuine Scheme action");

  assert(state.money>=oldMoney,"Original first tavern job cannot remove cash");
  assert.equal(state.lastAction?.title,"看场子","Action consequence card must identify the authored action");
  assert(["成功","一般","失败"].includes(state.lastAction?.result),"Action outcome must be localized");
  assert(state.lastAction?.changes.some(x=>x.label==="行动骰"&&x.delta===-1),
    "Action consequence card must explain spent dice");
  await page.getByTestId("action-result").waitFor();
  await page.screenshot({path:"artifacts/web-game-action.png"});
  await page.getByTestId("save-game").click();
  await page.waitForFunction(()=>Boolean(localStorage.getItem("ssnoir-web-v1")),null,{timeout:30000});
  const saved=await page.evaluate(()=>JSON.parse(localStorage.getItem("ssnoir-web-v1")));
  assert.equal(saved.format,"ssnoir-world");
  assert.equal(saved.state.globals["世界日"],1);
  const cashAtSave=state.money;
  const diceAtSave=state.dice;
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
  // Exercise the real host's failed-candidate isolation before a valid restore.
  const beforeFailedLoad=state;
  const badSave=structuredClone(saved);
  badSave.state.globals["世界日"]=9;
  await page.evaluate(text=>localStorage.setItem("ssnoir-web-v1",text),JSON.stringify(badSave));
  const errorsBeforeFailedLoad=errors.length;
  await page.getByTestId("load-game").click({timeout:120000});
  await page.waitForFunction(()=>{
    const s=window.__SSNOIR_WEB__?.snapshot();
    return s&&!s.busy&&Boolean(s.error);
  },null,{timeout:150000});
  state=await read();
  assert.match(state.error,/save calendar mismatch/);
  for(const field of ["day","money","health","dice","actors","restBlocked","restReasons","entries","path"])
    assert.deepEqual(state[field],beforeFailedLoad[field],"A failed load must retain live "+field);
  const expectedErrors=errors.splice(errorsBeforeFailedLoad);
  assert.equal(expectedErrors.length,1,"Only the expected save rejection should be logged");
  assert.match(expectedErrors[0],/^console: \[SSNoir web\].*save calendar mismatch/);
  await page.evaluate(text=>localStorage.setItem("ssnoir-web-v1",text),JSON.stringify(saved));
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
  assert.deepEqual(state.dice,diceAtSave,"Loading must restore unspent dice and their slot identities");
  assert.deepEqual(state.path,[]);
  await page.screenshot({path:"artifacts/web-game-restored.png"});
  assert.deepEqual(errors,[],"No runtime or browser errors");
  console.log("PASS: real Scheme city -> hotel morning -> tavern work/die -> save -> sleep/day 2 -> reject invalid save without state loss -> restore day 1");
} finally {
  await browser?.close();
  await server?.close();
}
