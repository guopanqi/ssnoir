import { chromium } from 'playwright';
import { spawn, execFileSync } from 'node:child_process';
import { mkdir, rm, writeFile, readFile } from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';
import { sourceFingerprint } from './source-fingerprint.mjs';

const ROOT=process.cwd();
const OUT=path.join(ROOT,'captures','latest');
const PORT=4186;
const URL=`http://127.0.0.1:${PORT}/?capture=1`;

function startServer(){
  return spawn(process.execPath,[
    path.join(ROOT,'node_modules/vite/bin/vite.js'),
    'preview','--host','127.0.0.1','--port',String(PORT),'--strictPort'
  ],{cwd:ROOT,stdio:['ignore','pipe','pipe']});
}

async function waitReady(page){
  for(let i=0;i<80;i++){
    try{
      const r=await fetch(URL,{signal:AbortSignal.timeout(1200)});
      if(r.ok) break;
    }catch{}
    await new Promise(r=>setTimeout(r,250));
  }
  await page.goto(URL,{waitUntil:'domcontentloaded',timeout:120000});
  for(let i=0;i<160;i++){
    const ready=await page.evaluate(()=>window.__NOIR_LAB__?.ready===true).catch(()=>false);
    if(ready) return;
    await new Promise(r=>setTimeout(r,200));
  }
  throw new Error('Application never became ready');
}

async function metrics(file){
  const {data,info}=await sharp(file).removeAlpha().raw().toBuffer({resolveWithObject:true});
  let sum=0,dark=0,bright=0;
  const bins=new Array(16).fill(0);
  for(let i=0;i<data.length;i+=info.channels){
    const y=.2126*data[i]+.7152*data[i+1]+.0722*data[i+2];
    sum+=y;
    if(y<38) dark++;
    if(y>205) bright++;
    bins[Math.min(15,Math.floor(y/16))]++;
  }
  const pixels=info.width*info.height;
  return {
    width:info.width,height:info.height,
    meanLuma:Number((sum/pixels).toFixed(2)),
    darkFraction:Number((dark/pixels).toFixed(4)),
    brightFraction:Number((bright/pixels).toFixed(4)),
    histogram16:bins
  };
}

await rm(OUT,{recursive:true,force:true});
await mkdir(OUT,{recursive:true});
const sourceSha256=await sourceFingerprint(ROOT);
let gitSha=process.env.GITHUB_SHA??null;
try{gitSha??=execFileSync('git',['rev-parse','HEAD'],{encoding:'utf8'}).trim();}catch{}

const server=startServer();
let serverLog='';
server.stdout.on('data',d=>serverLog+=d.toString());
server.stderr.on('data',d=>serverLog+=d.toString());

let browser;
try{
  browser=await chromium.launch({
    headless:true,
    executablePath:process.env.CHROME_PATH||undefined,
    args:process.env.CI?['--no-sandbox','--disable-dev-shm-usage','--use-gl=angle','--use-angle=swiftshader-webgl']:[]
  });
  const page=await browser.newPage({viewport:{width:1440,height:900},deviceScaleFactor:1});
  const errors=[];
  page.on('pageerror',e=>errors.push(e.stack??String(e)));
  page.on('console',m=>{
    if(m.type()!=='error') return;
    if(m.text().startsWith('Failed to load resource:')) return;
    errors.push(m.text());
  });
  page.on('response',r=>{
    if(r.status()<400) return;
    const u=new URL(r.url());
    if(u.pathname==='/favicon.ico') return;
    errors.push(`HTTP ${r.status()} ${r.url()}`);
  });

  await waitReady(page);
  await page.evaluate(()=>document.querySelectorAll('[data-capture-hide]').forEach(el=>el.remove()));

  let generatedManifest=[];
  try{
    generatedManifest=JSON.parse(await readFile(path.join(ROOT,'src/assets/generated/manifest.generated.json'),'utf8'));
  }catch{}

  const captures=[
    {name:'final-wide',scene:'cafe',shot:'wide',evaluation:'final'},
    {name:'final-street',scene:'cafe',shot:'street',evaluation:'final'},
    {name:'final-detail',scene:'cafe',shot:'detail',evaluation:'final'},
    {name:'final-alley',scene:'cafe',shot:'alley',evaluation:'final'},
    {name:'terminal-wide',scene:'terminal',shot:'wide',evaluation:'final'},
    {name:'terminal-street',scene:'terminal',shot:'street',evaluation:'final'},
    {name:'terminal-detail',scene:'terminal',shot:'detail',evaluation:'final'},
    {name:'audition-detective-walk',scene:'audition',shot:'asset',evaluation:'final',assetId:'character.detective.walk'},
    {name:'shape-wide',scene:'cafe',shot:'wide',evaluation:'shape'},
    {name:'line-wide',scene:'cafe',shot:'wide',evaluation:'line'},
    ...generatedManifest.map(entry=>({
      name:`generated-${String(entry.id).replace(/[^a-z0-9._-]+/gi,'-').toLowerCase()}`,
      scene:'audition',
      shot:'asset',
      evaluation:'final',
      assetId:entry.id,
      generatedStatus:entry.status??'candidate'
    }))
  ];

  const evidence=[];
  for(const item of captures){
    await page.evaluate(async args=>{
      await window.__NOIR_LAB__.prepareCapture(args);
    },item);
    const file=path.join(OUT,`${item.name}.png`);
    await page.screenshot({path:file,type:'png',fullPage:false,timeout:120000});
    evidence.push({
      ...item,
      file:`${item.name}.png`,
      metrics:await metrics(file)
    });
  }

  if(errors.length) throw new Error(errors.join('\n'));
  const appInfo=await page.evaluate(()=>window.__NOIR_LAB__.info());

  const manifest={
    generatedAt:new Date().toISOString(),
    gitSha,sourceSha256,
    browser:await browser.version(),
    viewport:{width:1440,height:900},
    appInfo,evidence,errors
  };
  await writeFile(path.join(OUT,'manifest.json'),JSON.stringify(manifest,null,2)+'\n');

  const report=[
    '# Noir World Kernel Capture',
    '',
    `Commit: ${gitSha}`,
    '',
    `Source fingerprint: ${sourceSha256}`,
    '',
    `Browser: ${manifest.browser}`,
    '',
    '## Visual evidence',
    '',
    ...evidence.map(x=>`- ${x.file} — ${x.scene} / ${x.shot} / ${x.evaluation}${x.assetId?` / ${x.assetId}`:''}; mean luma ${x.metrics.meanLuma}, dark ${x.metrics.darkFraction}, bright ${x.metrics.brightFraction}`),
    '',
    'Metrics are diagnostics only, not an aesthetic score.',
    '',
    '## Review order',
    '',
    '1. final-wide.png — composition, value hierarchy, focal point.',
    '2. final-street.png — whether the world reads from a gameplay-height camera.',
    '3. final-detail.png — whether vector characters and props survive closer inspection.',
    '4. final-alley.png — whether the cafe corner survives a reverse view.',
    '5. terminal-wide.png — whether the same systems create a second location rather than a reskin.',
    '6. terminal-street.png — gameplay-height readability in the second location.',
    '7. terminal-detail.png — vector-kit quality under closer inspection.',
    '8. audition-detective-walk.png — neutral-stage close inspection of a known-good SVG asset.',
    '9. generated-*.png — every generated candidate is automatically auditioned before scene use.',
    '10. shape-wide.png — silhouette and massing only.',
    '11. line-wide.png — line density and hierarchy only.',
    '',
    errors.length?`Runtime errors: ${errors.length}`:'No browser runtime errors detected.'
  ];
  await writeFile(path.join(OUT,'report.md'),report.join('\n')+'\n');
} catch(error){
  process.stderr.write(serverLog);
  throw error;
} finally {
  await browser?.close();
  server.kill('SIGTERM');
}
