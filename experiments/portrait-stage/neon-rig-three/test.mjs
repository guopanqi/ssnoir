import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {getAutoBind} from './rig.js';
import {SCENES,sampleScene} from './scenes.js';
const names=['尼尔','尼尔_醉站','尼尔_醉行','尼尔_靠墙','码头工人','码头工人_奔跑','黑影'];
const model={};
for(const name of names)model[name]=JSON.parse(readFileSync(new URL('./data/'+name+'.neon.json',import.meta.url),'utf8'));

test('all used paths are valid official Neon line data',()=>{
  for(const [name,src] of Object.entries(model)){
    assert.ok(Array.isArray(src.paths)&&src.paths.length>8,name);
    assert.equal(src.skeleton_size,1024,name);
    for(const path of src.paths){
      assert.ok(path.length>=2,name);
      for(const v of path){
        assert.equal(v.length,2,name);
        assert.ok(v.every(c=>Number.isFinite(c)&&c>=0&&c<=1),name);
      }
    }
    const b=getAutoBind(src);
    assert.ok(b.head.y<b.neck.y&&b.neck.y<b.hip.y&&b.hip.y<b.ankleL.y,name);
  }
});
test('each time value produces finite deterministic stage state',()=>{
  for(const [id,meta] of Object.entries(SCENES)){
    for(let t=0;t<=meta.duration;t+=.25){
      const a=sampleScene(id,t);
      assert.deepEqual(a,sampleScene(id,t),'must seek without state');
      for(const actor of [a.neil,a.worker]){
        assert.ok(actor.source in model,actor.source);
        assert.ok(Number.isFinite(actor.x)&&Number.isFinite(actor.y));
        assert.ok(Number.isFinite(actor.scale)&&(actor.scale>0));
        for(const n of Object.values(actor.pose))assert.ok(Number.isFinite(n));
      }
      assert.equal(typeof a.beat,'string');
    }
  }
});
test('the two stories retain essential readable beats',()=>{
  assert.match(sampleScene('drunk',10).beat,/扶住墙/);
  assert.match(sampleScene('chase',2).beat,/信封/);
  assert.match(sampleScene('chase',20).beat,/巷子/);
  assert.match(sampleScene('chase',21).beat,/喘了/);
  assert.ok(sampleScene('drunk',10).neil.source.includes('靠墙'));
  assert.ok(sampleScene('chase',18.5).worker.scale<1);
});
