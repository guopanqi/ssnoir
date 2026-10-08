import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { execFileSync } from 'node:child_process';
import { mkdir, rm, writeFile, copyFile, readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';
import { createHash } from 'node:crypto';
const startedAt=performance.now();

const ROOT=process.cwd();
const OUT=path.join(ROOT,'captures','latest');
const REVIEW=path.join(ROOT,'captures','review');
const PORT=4174;
const URL=`http://127.0.0.1:${PORT}/?capture=1`;
const shots=['01-city-overlook','02-city-landmark','03-alley-mouth','04-alley-figure'];
const availableModes=[
  {name:'shape',type:'jpeg',quality:84,width:960,height:540},
  {name:'light',type:'jpeg',quality:84,width:960,height:540},
  {name:'line',type:'jpeg',quality:84,width:960,height:540},
  {name:'final',type:'png',width:1440,height:810},
];

const requested=(process.env.CAPTURE_MODES||availableModes.map(x=>x.name).join(',')).split(',');
if(requested.some(n=>!availableModes.some(m=>m.name===n))||new Set(requested).size!==requested.length) throw new Error('CAPTURE_MODES must contain unique supported modes');
const modes=requested.map(n=>availableModes.find(m=>m.name===n));

async function sourceIdentity(){
 const sourceHash=createHash('sha256');
 const files=['package-lock.json',...(await readdir(path.join(ROOT,'src'))).sort().map(n=>'src/'+n),'tools/capture.mjs'];
 for(const file of files)sourceHash.update(file).update(await readFile(path.join(ROOT,file)));
 const assets={};
 for(const file of (await readdir(path.join(ROOT,'assets/models'))).sort())assets[file]=createHash('sha256').update(await readFile(path.join(ROOT,'assets/models',file))).digest('hex');
 let gitSha=process.env.GITHUB_SHA||null;
 if(!gitSha){try{gitSha=execFileSync('git',['rev-parse','HEAD'],{cwd:ROOT,encoding:'utf8',stdio:['ignore','pipe','ignore']}).trim();}catch{}}
 return {gitSha,sourceHash:sourceHash.digest('hex'),assets};
}

function server(){return spawn(process.execPath,[path.join(ROOT,'node_modules/vite/bin/vite.js'),'--host','127.0.0.1','--port',String(PORT),'--strictPort'],{cwd:ROOT,stdio:['ignore','pipe','pipe']});}
async function wait(page,getPageError=()=>null){
  let available=false;
  for(let i=0;i<80;i++){
    try{const r=await fetch(URL,{signal:AbortSignal.timeout(1200)});if(r.ok){available=true;break;}}catch{}
    await new Promise(r=>setTimeout(r,400));
  }
  if(!available) throw new Error('Noir Theatre Lab dev server did not start');
  await page.goto(URL,{waitUntil:'domcontentloaded',timeout:45000});
  const started=Date.now();
  while(Date.now()-started<45000){
    const error=getPageError();
    if(error) throw error;
    if(await page.evaluate(()=>window.__noirTheatreLab?.ready===true)) return;
    await new Promise(r=>setTimeout(r,250));
  }
  throw new Error('Noir Theatre Lab application did not become ready');
}
function percentile(a,p){return a[Math.min(a.length-1,Math.floor((a.length-1)*p))]||0;}
async function metrics(buffer){
  const {data,info}=await sharp(buffer).resize({width:360,withoutEnlargement:true}).removeAlpha().raw().toBuffer({resolveWithObject:true});
  const ys=[]; let black=0,bright=0,edgeCount=0,sum=0; const w=info.width,h=info.height,c=info.channels;
  const luma=(i)=>(.2126*data[i]+.7152*data[i+1]+.0722*data[i+2])/255;
  for(let y=0;y<h;y++)for(let x=0;x<w;x++){const i=(y*w+x)*c,v=luma(i);ys.push(v);sum+=v;if(v<.035)black++;if(v>.55)bright++;if(x>0&&y>0){const dx=Math.abs(v-luma(i-c));const dy=Math.abs(v-luma(i-w*c));if(dx+dy>.24)edgeCount++;}}
  ys.sort((a,b)=>a-b); const n=ys.length;
  return {mean:sum/n,p50:percentile(ys,.5),p90:percentile(ys,.9),black:black/n,bright:bright/n,edgeDensity:edgeCount/n};
}

await rm(OUT,{recursive:true,force:true}); await mkdir(OUT,{recursive:true}); await mkdir(REVIEW,{recursive:true});
const s=server(); let log='';s.stdout.on('data',d=>log+=d);s.stderr.on('data',d=>log+=d);let browser;
try{
  browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||undefined,args:process.env.CI?['--no-sandbox','--disable-dev-shm-usage']:[]});
  const page=await browser.newPage({viewport:{width:960,height:540}});
  let pageError=null;
  page.on('pageerror',e=>{pageError=e;process.stderr.write(`[browser] ${e.stack||e}\n`);});
  page.on('console',message=>{
    if(message.type()==='error') pageError=new Error(`Browser console: ${message.text()}`);
  });
  const assertHealthy=()=>{if(pageError) throw pageError;};
  await wait(page,()=>pageError);
  await page.evaluate(()=>document.querySelector('#hud')?.remove());
  const all={}; const outputs={};
  for(const mode of modes){
    await page.setViewportSize({width:mode.width,height:mode.height}); await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
    await page.evaluate(n=>window.__noirTheatreLab.setMode(n),mode.name); all[mode.name]={}; outputs[mode.name]={}; const dir=path.join(OUT,mode.name);await mkdir(dir,{recursive:true});
    for(let i=0;i<shots.length;i++){
      await page.evaluate(i=>window.__noirTheatreLab.setShot(i),i);
      const ext=mode.type==='png'?'png':'jpg';const file=path.join(dir,`${shots[i]}.${ext}`);
      const buf=await page.screenshot({path:file,type:mode.type,quality:mode.type==='jpeg'?mode.quality:undefined,timeout:45000}); outputs[mode.name][shots[i]]=file; all[mode.name][shots[i]]=await metrics(buf);
      assertHealthy();
      process.stdout.write(`${mode.name}/${shots[i]} captured\n`);
    }
  }
  const tw=420,th=236,cols=modes.length,rows=shots.length;
  const base=sharp({create:{width:tw*cols,height:th*rows,channels:3,background:{r:5,g:5,b:5}}}); const composite=[];
  for(let c=0;c<cols;c++)for(let r=0;r<rows;r++){
    const mode=modes[c].name,shot=shots[r],label=`${mode.toUpperCase()} · ${shot}`;
    const svg=Buffer.from(`<svg width="${tw}" height="${th}" xmlns="http://www.w3.org/2000/svg"><rect width="${tw}" height="25" fill="rgba(0,0,0,.82)"/><text x="8" y="17" fill="#eee" font-size="12" font-family="Arial">${label}</text></svg>`);
    const input=await sharp(outputs[mode][shot]).resize(tw,th,{fit:'cover'}).composite([{input:svg,left:0,top:0}]).jpeg({quality:86}).toBuffer(); composite.push({input,left:c*tw,top:r*th});
  }
  const sheet=path.join(OUT,'contact-sheet.jpg'); await base.composite(composite).jpeg({quality:89}).toFile(sheet); if(modes.length===availableModes.length) await copyFile(sheet,path.join(REVIEW,'contact-sheet.jpg'));
  if(outputs.final){
    const panels=[];
    for(let i=0;i<shots.length;i++){
      const label=Buffer.from(`<svg width="720" height="405" xmlns="http://www.w3.org/2000/svg"><rect width="720" height="27" fill="#050505"/><text x="12" y="19" fill="#ddd" font-size="14" font-family="Arial">${shots[i]}</text></svg>`);
      const input=await sharp(outputs.final[shots[i]]).resize(720,405).composite([{input:label,left:0,top:0}]).jpeg({quality:90}).toBuffer();
      panels.push({input,left:(i%2)*720,top:Math.floor(i/2)*405});
    }
    const board=path.join(OUT,'final-board.jpg');
    await sharp({create:{width:1440,height:810,channels:3,background:'#050505'}}).composite(panels).jpeg({quality:92}).toFile(board);
    if(modes.length===availableModes.length)await copyFile(board,path.join(REVIEW,'final-board.jpg'));
  }
  const report=['# Noir Theatre capture report','',`Modes: ${requested.join(', ')}. Elapsed: ${((performance.now()-startedAt)/1000).toFixed(1)}s.`,'','| mode | shot | mean | p50 | p90 | black<3.5% | bright>55% | edge density |','|---|---|---:|---:|---:|---:|---:|---:|'];
  for(const m of modes)for(const shot of shots){const x=all[m.name][shot];report.push(`| ${m.name} | ${shot} | ${x.mean.toFixed(3)} | ${x.p50.toFixed(3)} | ${x.p90.toFixed(3)} | ${(x.black*100).toFixed(1)}% | ${(x.bright*100).toFixed(1)}% | ${(x.edgeDensity*100).toFixed(1)}% |`)}
  await writeFile(path.join(OUT,'report.md'),report.join('\n')+'\n');
  assertHealthy();
  const manifest={generatedAt:new Date().toISOString(),...(await sourceIdentity()),durationSeconds:(performance.now()-startedAt)/1000,modes:requested,metrics:all,info:await page.evaluate(()=>window.__noirTheatreLab.info())};
  await writeFile(path.join(OUT,'manifest.json'),JSON.stringify(manifest,null,2)+'\n'); if(modes.length===availableModes.length) await writeFile(path.join(REVIEW,'metrics.json'),JSON.stringify(manifest,null,2)+'\n');
}catch(e){process.stderr.write(log);throw e;}finally{await browser?.close();s.kill('SIGTERM');}

