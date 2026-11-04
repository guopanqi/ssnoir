import assert from 'node:assert/strict';
import {readFileSync,readdirSync} from 'node:fs';
import {join} from 'node:path';
import {GameSceneDirector} from '../src/runtime/scene-director.ts';
import {WorldSaveController} from '../src/runtime/world-save.ts';
const root='../UnityClient/Assets/Resources/Content',sources={};
function scan(dir){for(const entry of readdirSync(join(root,dir),{withFileTypes:true})){
  const path=join(dir,entry.name).replaceAll('\\','/');
  if(entry.isDirectory())scan(path);
  else if(path.endsWith('.scm'))sources[path]=readFileSync(join(root,path),'utf8');
}}
scan('scripts');scan('scenes');
async function timed(label,action){const t=performance.now(),value=await action();
  console.log('SSNOIR_PERF '+JSON.stringify({label,ms:Math.round(performance.now()-t)}));return value;}
const director=await timed('bootstrap',()=>GameSceneDirector.create(sources,()=>.4));
const before=new WorldSaveController(director.state,director.world);
const saved=await before.capture();
for(let i=0;i<3;i++){
  const tree=await timed('full-authoritative-tree-'+i,()=>director.worldActions.prepare());
  assert.equal(tree.name,'世界');
  const after=await before.capture();
  assert.deepEqual(after,saved,'Materialization must not change authored or native state');
}
await timed('enter-place',()=>director.worldActions.enterPlace('家'));
await timed('return-to-map',()=>director.worldActions.enterPlace('世界'));
const data=await timed('save',()=>before.capture());
const fresh=await timed('fresh-restore-vm',()=>GameSceneDirector.create(sources,()=>.4,false));
await timed('restore-modules',()=>WorldSaveController.restore(data,async()=>({state:fresh.state,session:fresh.world})));
await timed('restored-authoritative-tree',()=>fresh.render());
assert.deepEqual(await new WorldSaveController(fresh.state,fresh.world).capture(),data);
