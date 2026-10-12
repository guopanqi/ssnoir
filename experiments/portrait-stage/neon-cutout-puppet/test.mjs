import test from 'node:test';import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';import {sampleScene,CLIPS} from './scenes.js';
const rig=JSON.parse(readFileSync(new URL('./rig.json',import.meta.url),'utf8'));
test('explicit cutout hierarchy, crops, pivots and joints',()=>{
  assert.equal(rig.parts.length,13);
  const seen=new Set();for(const p of rig.parts){assert.ok(!seen.has(p.id));seen.add(p.id);}
  for(const p of rig.parts){if(p.parent)assert.ok(seen.has(p.parent),p.id);assert.equal(p.crop.length,4);assert.equal(p.joint.length,2);assert.equal(p.sourcePivot.length,2);
    assert.ok(p.crop.every(Number.isFinite));assert.ok(p.crop[2]>0&&p.crop[3]>0);
  }
  for(const p of rig.parts){let id=p.parent,k=0;while(id){assert.ok(k++<13,'Cycle');id=rig.parts.find(q=>q.id===id)?.parent;}}
});
test('deterministic independent timelines and finite joint commands',()=>{
 for(const [name,meta] of Object.entries(CLIPS)){
  for(let t=0;t<=meta.duration;t+=.125){
    const s=sampleScene(name,t);assert.deepEqual(s,sampleScene(name,t));
    assert.ok(Number.isFinite(s.x)&&Number.isFinite(s.tilt)&&Number.isFinite(s.bob));
    for(const [key,val] of Object.entries(s.angles)){assert.ok(rig.parts.some(p=>p.id===key),key);assert.ok(Number.isFinite(val));}
  }
 }
});
test('three distinct acting relations',()=>{
  assert.ok(Math.abs(sampleScene('drunk',3).angles.thighL)>.05);
  assert.ok(sampleScene('brace',6).angles.armUpperR < -.8);
  assert.ok(sampleScene('run',4).angles.coatR!==sampleScene('run',5).angles.coatR);
});
