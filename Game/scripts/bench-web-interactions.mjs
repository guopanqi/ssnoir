import assert from 'node:assert/strict';
import {mkdirSync,writeFileSync,readFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {chromium} from 'playwright';
import {createServer} from 'vite';

// Also runs unchanged against a git-archive of the baseline Game directory.
const appRoot=resolve(process.env.SSNOIR_BENCH_ROOT??'.');
const port=Number(process.env.SSNOIR_BENCH_PORT??4174);
const output=resolve(process.env.SSNOIR_BENCH_OUTPUT??'artifacts/web-interactions.json');
const server=await createServer({root:appRoot,configFile:resolve(appRoot,'vite.config.ts'),
  cacheDir:resolve(appRoot,'node_modules/.vite-perf'),
  optimizeDeps:{force:true},server:{host:'127.0.0.1',port,strictPort:true}});
let browser;
try{
  await server.listen();
  browser=await chromium.launch({headless:true,args:['--no-sandbox','--enable-webgl',
    '--enable-unsafe-swiftshader','--use-gl=angle','--use-angle=swiftshader']});
  const page=await browser.newPage({viewport:{width:1440,height:850}});
  const errors=[],stages=[],snapshots=[],timings=[];
  page.on('pageerror',e=>errors.push(String(e)));
  page.on('console',msg=>{
    if(msg.type()==='error')errors.push(msg.text());
    if(msg.text().startsWith('[SSNoir]'))stages.push(msg.text());
  });
  const read=()=>page.evaluate(()=>window.__SSNOIR_WEB__.snapshot());
  async function settled(){
    await page.waitForFunction(()=>{const s=window.__SSNOIR_WEB__?.snapshot();return s&&!s.busy;},null,{timeout:180000});
    await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
    const state=await read();assert.equal(state.error,'');return state;
  }
  async function story(){for(let i=0;i<250;i++){
    if(!(await read()).story)return;
    await page.getByTestId('story-continue').click();await settled();
  }throw new Error('Story did not settle');}
  function stable(state){
    const fields=['mode','day','money','health','dice','actors','restBlocked','restReasons','entries','path'];
    return Object.fromEntries(fields.map(key=>[key,state[key]]));
  }
  async function timed(label,action){
    const start=await page.evaluate(()=>performance.now());
    await action();const state=await settled();
    const elapsed=await page.evaluate(t=>performance.now()-t,start);
    timings.push({label,ms:Math.round(elapsed)});snapshots.push({label,state:stable(state)});
    console.log('WEB_INTERACTION '+JSON.stringify(timings.at(-1)));return state;
  }
  // World locations are now spatial map pins; only actions remain game-entry
  // cards in the right rail. Inspect the authoritative UI snapshot rather
  // than benchmarking a stale selector for three minutes.
  async function node(id){
    const state=await read();
    const isWorldPlace=state.mode==='world'&&state.path.length===0&&
      state.entries.some(x=>x.id===id&&x.isPlace);
    const selector=isWorldPlace?'map-place':'game-entry';
    const locator=page.locator('[data-testid="'+selector+'"][data-node-id='+JSON.stringify(id)+']');
    await locator.waitFor({state:'visible',timeout:15000});
    assert.equal(await locator.count(),1,
      'One UI target per authored node: '+JSON.stringify({id,selector,path:state.path}));
    await locator.click({timeout:30000});
  }
  await page.goto(`http://127.0.0.1:${port}/?seed=42`,{waitUntil:'domcontentloaded'});
  await page.getByTestId('game-ready').waitFor({timeout:180000});await settled();await story();
  await timed('hotel-first',()=>node('家'));
  await timed('world-return',()=>page.getByTestId('back').click());
  await timed('hotel-reentry',()=>node('家'));
  await node('旅馆清晨');await settled();await story();
  await page.getByTestId('back').click();await settled();
  await timed('tavern-first',()=>node('老街酒馆'));await story();
  await timed('tavern-return',()=>page.getByTestId('back').click());
  await timed('tavern-reentry',()=>node('老街酒馆'));await story();
  await timed('save',()=>page.getByTestId('save-game').click());
  const saved=await page.evaluate(()=>JSON.parse(localStorage.getItem('ssnoir-web-v1')));
  assert.equal(saved.format,'ssnoir-world');
  if(process.env.SSNOIR_BENCH_LEGACY_SAVE){
    const legacy=JSON.parse(readFileSync(process.env.SSNOIR_BENCH_LEGACY_SAVE,'utf8')).saved;
    assert.deepEqual(saved,legacy,'New VM must emit the identical portable save envelope');
    await page.evaluate(text=>localStorage.setItem('ssnoir-web-v1',text),JSON.stringify(legacy));
  }
  await timed('restore',()=>page.getByTestId('load-game').click({timeout:180000}));
  mkdirSync(resolve(output,'..'),{recursive:true});
  await page.screenshot({path:output.replace(/\.json$/,'.png')});
  assert.deepEqual(errors,[]);
  if(process.env.SSNOIR_BENCH_ASSERT_BUDGET==='1'){
    for(const timing of timings)assert(timing.ms<(timing.label==='restore'?4000:2000),
      'Interaction exceeded coarse CI budget: '+JSON.stringify(timing));
  }
  const result={environment:{browser:browser.version(),node:process.version,viewport:[1440,850]},timings,snapshots,saved,stages};
  mkdirSync(resolve(output,'..'),{recursive:true});writeFileSync(output,JSON.stringify(result,null,2)+'\n');
}finally{await browser?.close();await server.close();}
