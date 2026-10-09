import path from 'node:path';
const base = path.resolve(import.meta.dirname, '..');
import { chromium } from 'playwright';
import assert from 'node:assert/strict';
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const errors=[];
try {
  const page=await browser.newPage({viewport:{width:1440,height:1000}});
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5178');
  await page.waitForFunction(()=>window.stageLab?.player.state==='dialogue');
  await page.evaluate(()=>stageLab.player.pause(true));
  const before=await page.locator('.words').innerText();await page.waitForTimeout(180);assert.equal(await page.locator('.words').innerText(),before);
  await page.evaluate(()=>stageLab.player.pause(false));
  const counts = await page.evaluate(()=>stageLab.stories.map(s=>s.variants?.length||1));
  for(let scene=0;scene<counts.length;scene++) for(let variant=0;variant<counts[scene];variant++) {
    await page.evaluate(([s,v])=>stageLab.load(s,0,v),[scene,variant]);
    let steps=0;
    while(await page.evaluate(()=>stageLab.player.state!=='ended')) {
      assert.notEqual(await page.evaluate(()=>stageLab.player.state),'error');
      assert.ok(steps++<500,'演出未结束');
      await page.evaluate(()=>stageLab.player.advance());
      await page.waitForTimeout(40);
    }
    console.log(`Scene ${scene+1} variant ${variant+1} completed.`);
  }
  await page.evaluate(()=>stageLab.load(8,2));
  await page.waitForFunction(()=>stageLab.player.state==='dialogue');
  assert.equal(await page.evaluate(()=>stageLab.stage.props.get('信').x),490,'跳拍还原信件位置');
  await page.evaluate(()=>stageLab.load(9,3));
  await page.waitForFunction(()=>stageLab.player.state==='dialogue');
  assert.equal(await page.locator('[data-actor="守门人"]').count(),0,'黑场中的退场被还原');
  assert.equal(await page.locator('.fx').evaluate(el=>el.classList.contains('black')),false,'跳拍后恢复灯光');
  await page.locator('#sound').click();
  await page.evaluate(()=>{stageLab.stage.sound.cue('steps');stageLab.stage.sound.cue('impact');});
  await page.locator('#sound').click();
  await page.evaluate(()=>stageLab.load(3,2,1));
  await page.waitForFunction(()=>stageLab.player.state==='dialogue');
  assert.equal(await page.locator('[data-prop="烟盒"]').count(),1,'跳拍重建道具');
  await page.evaluate(()=>stageLab.load(4,4,1));
  await page.waitForFunction(()=>stageLab.player.state==='dialogue');
  assert.equal(await page.evaluate(()=>stageLab.stage.get('夜莺').x),955,'B 退开');
  await page.evaluate(()=>stageLab.load(4,4,0));
  await page.waitForFunction(()=>stageLab.player.state==='dialogue');
  assert.equal(await page.evaluate(()=>stageLab.stage.get('夜莺').x),850,'A 不退开');
  await page.locator('#hide-captions').check();
  await page.waitForFunction(()=>stageLab.player.state==='dialogue');
  assert.equal(await page.locator('.dialogue').evaluate(el=>getComputedStyle(el).visibility),'hidden');
  assert.equal(await page.evaluate(()=>stageLab.player.auto),true);
  await page.waitForTimeout(4500);
  assert.ok(await page.evaluate(()=>stageLab.player.beat)>0,'无点击自动推进');
  await page.evaluate(()=>stageLab.player.pause(true));
  const pausedBeat=await page.evaluate(()=>stageLab.player.beat);
  await page.waitForTimeout(600);
  assert.equal(await page.evaluate(()=>stageLab.player.beat),pausedBeat,'自动模式暂停');
  await page.locator('#hide-captions').uncheck();
  await page.waitForFunction(()=>stageLab.player.state==='dialogue');
  assert.equal(await page.evaluate(()=>stageLab.player.auto),false);
  await page.evaluate(()=>stageLab.load(1,8));await page.waitForFunction(()=>stageLab.player.state==='dialogue');
  const snapshot=await page.evaluate(()=>stageLab.stage.snapshot());
  assert.equal(snapshot.find(a=>a.id==='夜莺').x,1000);assert.equal(snapshot.find(a=>a.id==='尼尔').pose,'摘帽');
  await page.evaluate(()=>stageLab.load(0,2));await page.waitForFunction(()=>stageLab.player.state==='dialogue');await page.waitForTimeout(650);
  await page.screenshot({path:path.join(base,'art/preview-rain.png')});
  await page.evaluate(()=>stageLab.load(1,10));await page.waitForFunction(()=>stageLab.player.state==='dialogue');await page.waitForTimeout(1000);await page.screenshot({path:path.join(base,'art/preview-room.png')});
  await page.setViewportSize({width:844,height:600});await page.screenshot({path:path.join(base,'art/preview-compact.png')});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,'横向溢出');
  assert.deepEqual(errors,[]);
  console.log('Passed: all scenes and variants, hidden-caption autoplay, pause, prop reconstruction, A/B distance, compact width, no runtime errors.');
} finally {await browser.close();}
