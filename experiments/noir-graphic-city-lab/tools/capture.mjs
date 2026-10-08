import { chromium } from 'playwright';
import { spawn, execFileSync } from 'node:child_process';
import { mkdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { parseArgs } from 'node:util';
import sharp from 'sharp';
import { sourceFingerprint } from './source-fingerprint.mjs';

const ROOT=process.cwd();
const {values}=parseArgs({options:{
  shot:{type:'string'},mode:{type:'string'},out:{type:'string'},port:{type:'string',default:'4174'},
}});
const OUT=path.resolve(ROOT,values.out??'captures/latest');
if(!OUT.startsWith(path.join(ROOT,'captures')+path.sep)) throw new Error('--out must be inside captures/.');
const PORT=Number(values.port);
const URL=`http://127.0.0.1:${PORT}/?capture=1`;

const allShots=[['01-city-plaza',0],['02-city-plaza-offset',1]];
const allModes=[
  {name:'structure',type:'jpeg',quality:88,viewport:{width:960,height:540}},
  {name:'clean',type:'jpeg',quality:90,viewport:{width:1280,height:720}},
  {name:'final',type:'png',viewport:{width:1600,height:900}},
];
const shots=values.shot?allShots.filter(([n])=>n===values.shot):allShots;
const modes=values.mode?allModes.filter(m=>m.name===values.mode):allModes;
if(!shots.length||!modes.length) throw new Error('Unknown --shot or --mode');

const sourceSha256=await sourceFingerprint(ROOT);
let gitSha=process.env.GITHUB_SHA??null;
try{gitSha??=execFileSync('git',['rev-parse','HEAD'],{encoding:'utf8',stdio:['ignore','pipe','ignore']}).trim();}catch{}

function startServer(){
  return spawn(process.execPath,[
    path.join(ROOT,'node_modules/vite/bin/vite.js'),
    'preview','--host','127.0.0.1','--port',String(PORT),'--strictPort'
  ],{cwd:ROOT,stdio:['ignore','pipe','pipe']});
}

async function waitReady(page){
  for(let i=0;i<80;i++){
    try{const r=await fetch(URL,{signal:AbortSignal.timeout(1200)});if(r.ok)break;}catch{}
    await new Promise(r=>setTimeout(r,300));
  }
  await page.goto(URL,{waitUntil:'domcontentloaded',timeout:120000});
  for(let i=0;i<160;i++){
    if(await page.evaluate(()=>window.__graphicCityLab?.ready===true).catch(()=>false))return;
    await new Promise(r=>setTimeout(r,200));
  }
  throw new Error('Graphic City Lab page did not become ready.');
}

await rm(OUT,{recursive:true,force:true});
await mkdir(OUT,{recursive:true});
const server=startServer();
let serverLog='';
server.stdout.on('data',d=>serverLog+=d.toString());
server.stderr.on('data',d=>serverLog+=d.toString());
let browser;

try{
  browser=await chromium.launch({
    headless:true,
    executablePath:process.env.CHROME_PATH||undefined,
    args:process.env.CI?['--no-sandbox','--disable-dev-shm-usage']:[],
  });
  const page=await browser.newPage({viewport:modes[0].viewport,deviceScaleFactor:1});
  const errors=[];
  page.on('pageerror',e=>errors.push(e.stack??String(e)));
  page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
  page.on('response',r=>{if(r.status()>=400)errors.push(`HTTP ${r.status()} ${r.url()}`);});
  const healthy=()=>{if(errors.length)throw new Error(errors.join('\n'));};

  await waitReady(page);
  await page.evaluate(()=>document.querySelector('.hud')?.remove());

  const outputs={},timings={};
  const started=performance.now();

  for(const mode of modes){
    outputs[mode.name]={};timings[mode.name]={};
    await page.setViewportSize(mode.viewport);
    await page.evaluate(()=>new Promise(resolve=>{
      dispatchEvent(new Event('resize'));
      requestAnimationFrame(()=>{window.__graphicCityLab.render();requestAnimationFrame(resolve);});
    }));
    await page.evaluate(name=>window.__graphicCityLab.setMode(name),mode.name);
    const dir=path.join(OUT,mode.name);await mkdir(dir,{recursive:true});

    for(const [shotName,index] of shots){
      await page.evaluate(i=>{window.__graphicCityLab.setShot(i);window.__graphicCityLab.render();},index);
      const t0=performance.now();
      const ext=mode.type==='jpeg'?'jpg':'png';
      const file=path.join(dir,`${shotName}.${ext}`);
      await page.screenshot({path:file,type:mode.type,quality:mode.type==='jpeg'?mode.quality:undefined,timeout:120000});
      healthy();
      outputs[mode.name][shotName]=file;
      timings[mode.name][shotName]=Math.round(performance.now()-t0);
    }
  }

  const order=['structure','clean','final'].filter(x=>outputs[x]);
  const tw=480,th=270;
  const sheet=sharp({create:{width:tw*order.length,height:th*shots.length,channels:3,background:{r:7,g:11,b:22}}});
  const comps=[];
  for(let col=0;col<order.length;col++){
    for(let row=0;row<shots.length;row++){
      const mode=order[col],shot=shots[row][0];
      const label=Buffer.from(`<svg width="${tw}" height="${th}" xmlns="http://www.w3.org/2000/svg"><rect width="300" height="27" fill="rgba(0,0,0,.76)"/><text x="8" y="18" fill="#eee9df" font-size="12" font-family="Arial">${mode.toUpperCase()} · ${shot}</text></svg>`);
      const input=await sharp(outputs[mode][shot]).resize(tw,th,{fit:'cover'}).composite([{input:label,top:0,left:0}]).jpeg({quality:91}).toBuffer();
      comps.push({input,left:col*tw,top:row*th});
    }
  }
  await sheet.composite(comps).jpeg({quality:92}).toFile(path.join(OUT,'contact-sheet.jpg'));

  // Full-resolution scrutiny crops for the primary beauty frame.
  if(outputs.final?.['01-city-plaza']){
    const detailDir=path.join(OUT,'details');
    await mkdir(detailDir,{recursive:true});
    const source=outputs.final['01-city-plaza'];
    const crops=[
      ['hero',720,330,430,540],
      ['diner',430,270,720,390],
      ['ground',40,470,840,410],
    ];
    for(const [name,left,top,width,height] of crops){
      await sharp(source).extract({left,top,width,height}).png().toFile(path.join(detailDir,`${name}.png`));
    }
  }

  const report=[
    '# Graphic City capture report','',
    'This report deliberately avoids aesthetic scores. Numeric image statistics are not acceptance criteria for this experiment.',
    '',
    '## Review protocol','',
    'Judge the actual images in this order:',
    '1. Does the frame look deliberately art-directed before inspecting technique?',
    '2. Are silhouettes, negative space and focal hierarchy clear at thumbnail size?',
    '3. Do lines describe architecture and form rather than read as generic wireframe?',
    '4. Are dark surfaces rich but clean, with texture subordinate to form?',
    '5. Do bloom, grain and atmosphere support the image without becoming the image?',
    '6. Does the offset shot remain coherent, proving the style is a system rather than a single-shot cheat?',
    '',
    '## Technical diagnostics only','',
    `Total capture: ${((performance.now()-started)/1000).toFixed(1)}s`,
  ];
  for(const mode of order)for(const [shot] of shots)report.push(`- ${mode} / ${shot}: ${timings[mode][shot]}ms`);
  await writeFile(path.join(OUT,'report.md'),report.join('\n')+'\n');

  const info=await page.evaluate(()=>window.__graphicCityLab.info());
  await writeFile(path.join(OUT,'manifest.json'),JSON.stringify({
    generatedAt:new Date().toISOString(),gitSha,sourceSha256,browser:await browser.version(),
    shots:shots.map(([name,index])=>({name,index})),
    modes:Object.fromEntries(modes.map(m=>[m.name,{type:m.type,viewport:m.viewport}])),
    info,timings,
  },null,2)+'\n');
}catch(error){
  process.stderr.write(serverLog);throw error;
}finally{
  await browser?.close();server.kill('SIGTERM');
}
