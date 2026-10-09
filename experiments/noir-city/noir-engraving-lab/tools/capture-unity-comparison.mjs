// Temporary capture overrides do not modify the scene source or the user's open tab.
import {chromium} from 'playwright';
import {readdir,access,mkdir,writeFile,copyFile} from 'node:fs/promises';
import path from 'node:path';
const out=path.resolve('../../city-box/prefabs/review/世界/Unity夜城迁移/对照');
await mkdir(out,{recursive:true});
const cache='/Users/usr/Library/Caches/ms-playwright';let executablePath;
for(const folder of (await readdir(cache)).filter(n=>n.startsWith('chromium-')).sort().reverse()){
 for(const rel of ['chrome-mac-arm64/Chromium.app/Contents/MacOS/Chromium','chrome-mac/Chromium.app/Contents/MacOS/Chromium']){
  const p=path.join(cache,folder,rel);try{await access(p);executablePath=p;break;}catch{}
 }if(executablePath)break;
}
if(!executablePath)executablePath='/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const browser=await chromium.launch({executablePath,headless:true});
try{
 for(const [name,fov] of [['threejs-selected',22.87],['threejs-matched-camera',26.991466522216798]]){
  const page=await browser.newPage({viewport:{width:1600,height:900},deviceScaleFactor:1});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/src/night-restraint/main.js*',async route=>{
   const response=await route.fetch();let text=await response.text();
   if(!text.includes('new THREE.PerspectiveCamera(22.87'))throw Error('Camera capture hook missing');
   text=text.replace('new THREE.PerspectiveCamera(22.87',`new THREE.PerspectiveCamera(${fov}`)
     .replace('scene.add(landmarks.group);','landmarks.group.visible=false;scene.add(landmarks.group);')
     .replace('landmarks.tick(camera,dt,viewport);',`landmarks.group.visible=false;camera.fov=${fov};camera.updateProjectionMatrix();`);
   await route.fulfill({response,body:text});
  });
  await page.goto('http://127.0.0.1:5173/night-restraint.html?view=world',{waitUntil:'networkidle'});
  await page.locator('canvas[data-ready="true"]').waitFor({timeout:60000});
  await page.addStyleTag({content:'body>*:not(canvas){visibility:hidden!important}'});
  await page.waitForTimeout(1200);
  if(errors.length)throw Error(errors.join('\n'));
  await page.locator('canvas[data-ready="true"]').screenshot({path:path.join(out,name+'.png')});
  await page.close();
 }
 await copyFile(path.resolve('../../city-box/prefabs/review/世界/Unity夜城迁移/校准02/世界.png'),path.join(out,'unity-current.png'));
 await writeFile(path.join(out,'capture.json'),JSON.stringify({date:new Date().toISOString(),threeSource:'night-restraint.html?view=world',unitySource:'校准02/世界.png',selectedFov:22.87,matchedFov:26.991466522216798,viewport:[1600,900],uiHidden:true,sourceModified:false},null,2));
 console.log(out);
}finally{await browser.close();}
