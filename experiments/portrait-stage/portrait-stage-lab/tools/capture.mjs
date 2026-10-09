import path from 'node:path';
const base = path.resolve(import.meta.dirname, '..');
import {chromium} from 'playwright';
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const page=await browser.newPage({viewport:{width:1440,height:1000}});
 await page.goto('http://127.0.0.1:5178');await page.waitForFunction(()=>window.stageLab?.player.state==='dialogue');
 for(const [scene,beat,name] of [[0,2,'rain'],[0,7,'waiting'],[1,10,'room'],[3,2,'cigarette'],[4,4,'distance'],[5,3,'manager'],[6,3,'farewell'],[7,2,'blocked'],[8,2,'letter'],[9,3,'blackout']]) {
  await page.evaluate(([s,b])=>stageLab.load(s,b,s>=3&&s<=6?1:0),[scene,beat]);
  await page.waitForFunction(()=>stageLab.player.state==='dialogue');await page.waitForTimeout(1000);
  await page.screenshot({path:path.join(base,`art/preview-${name}.png`)});
 }
 await page.setViewportSize({width:844,height:600});await page.screenshot({path:path.join(base,'art/preview-compact.png')});
} finally {await browser.close();}
