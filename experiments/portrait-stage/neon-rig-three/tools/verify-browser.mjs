import {chromium} from 'playwright';
import {mkdirSync} from 'node:fs';
import assert from 'node:assert/strict';

const origin=process.env.STAGE_URL||'http://127.0.0.1:5189';
mkdirSync('captures',{recursive:true});
const browser=await chromium.launch({headless:true,args:['--no-sandbox','--use-gl=angle','--use-angle=swiftshader','--enable-webgl']});
try {
 const page=await browser.newPage({viewport:{width:1440,height:1040},deviceScaleFactor:1});
 const exceptions=[];
 page.on('pageerror',error=>exceptions.push(error.message));
 page.on('console',msg=>{if(msg.type()==='error')exceptions.push(msg.text())});
 page.on('response',response=>{if(!response.ok()&&new URL(response.url()).pathname.endsWith('.neon.json'))exceptions.push('Asset HTTP '+response.status())});
 await page.goto(origin+'/?scene=drunk&t=0&pause=1',{waitUntil:'domcontentloaded'});
 await page.waitForFunction(()=>!!window.__neonTheatre,{timeout:50000});
 const views={
  drunk:[0,4.5,10,15.5],
  chase:[3.3,7.5,12,20.5,23.5]
 };
 for(const [name,times] of Object.entries(views)){
   await page.evaluate(name=>window.__neonTheatre.setScene(name),name);
   for(const t of times){
     await page.evaluate(t=>window.__neonTheatre.seek(t),t);
     await page.locator('#stage-canvas').screenshot({
       path:'captures/'+name+'-'+String(t).replace('.','_')+'.png'
     });
     const state=await page.evaluate(()=>window.__neonTheatre.state);
     assert.equal(state.scene,name);
     assert.ok(Math.abs(state.time-t)<1e-5);
   }
 }
 await page.locator('#rig').uncheck();
 await page.evaluate(()=>window.__neonTheatre.seek(10));
 await page.locator('#stage-canvas').screenshot({path:'captures/drunk-10-rigid.png'});
 await page.locator('#rig').check();
 await page.locator('#joints').check();
 await page.locator('#stage-canvas').screenshot({path:'captures/drunk-10-joints.png'});
 assert.deepEqual(exceptions,[],'Browser runtime errors: '+exceptions.join(' | '));
 console.log('PASS: real Three.js WebGL theatre, 2 scenes, seek, toggles, 11 captured frames');
}finally{
 await browser.close();
}
