import {chromium} from 'playwright';
import assert from 'node:assert/strict';
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const page=await browser.newPage({viewport:{width:1440,height:1150}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:5178/world-stage.html');await page.waitForFunction(()=>window.worldStage?.ready);
 for(const style of ['02-emergent-light','01-soft-planes','03-ink-wash','04-screenprint','flat','neon']){
  await page.locator(`button[data-style="${style}"]`).click();assert.equal(await page.evaluate(()=>window.worldStage.state.cue),1);
  await page.locator('.night img').evaluate(i=>i.decode());await page.locator('.viewport').screenshot({path:`art/world-stage/compare-${style}.png`});
 }
 await page.locator('button[data-style="02-emergent-light"]').click();
 for(const set of ['street','door','room']){await page.locator('#set').selectOption(set);await page.locator('[data-cue="4"]').click();await page.waitForTimeout(1100);const rect=await page.locator('.letter').evaluate(e=>{const r=e.getBoundingClientRect();return{x:r.x,width:r.width}});assert.ok(rect.x>400&&rect.width>150,'Inspect prop must move to the stage centre');}
 await page.locator('#neil-style').selectOption('neil');assert.ok((await page.locator('.neil img').getAttribute('src')).endsWith('neil.png'));await page.locator('#neil-style').selectOption('neil-02');
 await page.locator('#world').selectOption('world');assert.ok((await page.locator('.world').getAttribute('src')).endsWith('world.png'));
 await page.locator('#tone').selectOption('warm');await page.locator('#opacity').fill('42');assert.equal(await page.locator('.curtain').evaluate(e=>getComputedStyle(e).opacity),'0.42');
 await page.locator('#world-only').check();assert.equal(await page.locator('.cast').evaluate(e=>getComputedStyle(e).visibility),'hidden');await page.locator('#world-only').uncheck();
 await page.locator('#world').selectOption('hotel');await page.locator('#set').selectOption('street');await page.locator('#tone').selectOption('cool');await page.locator('#opacity').fill('72');await page.locator('[data-cue="1"]').click();await page.locator('.viewport').screenshot({path:'art/world-stage/final.png'});
 await page.locator('#play').click();await page.waitForTimeout(350);await page.locator('#play').click();assert.equal(await page.evaluate(()=>window.worldStage.state.playing),false);
 await page.setViewportSize({width:844,height:650});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
 assert.deepEqual(errors,[]);console.log('PASS: six styles preserve cue, three geometric sets, centred prop, world/tone/alpha controls, layer toggles, playback stop and narrow layout.');
}finally{await browser.close();}
