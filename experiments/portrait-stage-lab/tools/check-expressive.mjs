import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
const base=fileURLToPath(new URL('../',import.meta.url));const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const page=await browser.newPage({viewport:{width:1440,height:1150}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:5178/third.html');await page.waitForFunction(()=>window.thirdLab?.player.story);
 await page.evaluate(()=>{window.thirdLab.load(2,1);});await page.waitForFunction(()=>window.thirdLab.player.waiting);
 // Preserve real action delays; bypass only dialogue waits to reach the two silences.
 await page.evaluate(()=>{const p=window.thirdLab.player;p.advance();p.advance();p.dialogue=async c=>{p.stage.say(c);p.stage.text(c.text);};});
 await page.waitForFunction(()=>[...window.thirdLab.stage.animations].some(a=>a.effect.target.matches('[data-pool="路灯"]')));
 await page.locator('#pause').click();await page.evaluate(async()=>{await new Promise(requestAnimationFrame);await new Promise(requestAnimationFrame);});
 const opacity=await page.locator('[data-pool="路灯"]').evaluate(el=>getComputedStyle(el).opacity);await page.waitForTimeout(200);
 assert.equal(await page.locator('[data-pool="路灯"]').evaluate(el=>getComputedStyle(el).opacity),opacity);
 await page.screenshot({path:`${base}art/third/preview-streetlamp.png`});
 await page.locator('#pause').click();await page.waitForFunction(()=>window.thirdLab.stage.get('夜莺').x===1080);
 assert.equal(await page.evaluate(()=>window.thirdLab.stage.get('尼尔').x),410);
 await page.locator('#pause').click();await page.screenshot({path:`${base}art/third/preview-follow-delay.png`});
 // Node reconstruction puts a stable lamp back, with no replayed one-shot flicker.
 await page.evaluate(()=>{window.thirdLab.load(2,3);window.thirdLab.player.pause(true);});
 assert.equal(await page.locator('[data-pool="路灯"]').evaluate(el=>Number(getComputedStyle(el).opacity)),.16);
 await page.evaluate(()=>{window.thirdLab.load(4,3);window.thirdLab.player.pause(true);});
 await page.locator('#pause').click();await page.waitForFunction(()=>window.thirdLab.player.state==='ended');
 assert.equal(await page.evaluate(()=>window.thirdLab.stage.people.has('经理')),false);
 assert.equal(await page.evaluate(()=>window.thirdLab.stage.props.get('电话').x),855);
 assert.equal(await page.evaluate(()=>window.thirdLab.stage.poolStates.get('电话').strength),.18);
 await page.locator('.end-card').evaluate(el=>el.hidden=true);await page.screenshot({path:`${base}art/third/preview-phone-left.png`});
 assert.deepEqual(errors,[]);console.log('PASS: lamp flicker pauses and restores, she leaves before he follows, manager exits and leaves the telephone.');
}finally{await browser.close();}
