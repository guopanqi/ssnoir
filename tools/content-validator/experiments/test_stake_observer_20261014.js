#!/usr/bin/env node
/* Offline smoke test of the self-selected-goal observational page.
   Execute exact inline JS in a minimal DOM-shaped Node vm, not a real browser.
   Does NOT certify human usability, layout, or GitHub Pages deployment. */
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const page='docs/实验/机制研究/批次/2026-10-14-策略性伪装与自选成果/试玩.html';
const html=fs.readFileSync(page,'utf8');
const js=html.match(/<script>([\s\S]*?)<\/script>/);
assert(js,'page should contain exactly one inline JS game');
function boot(random){
  const el=new Map(),fresh=()=>({value:'',hidden:false,textContent:'',children:[],
    replaceChildren(){this.children=[]},append(child){this.children.push(child)},
    focus(){}});
  const document={getElementById(id){if(!el.has(id))el.set(id,fresh());return el.get(id)},
    createElement(){return fresh()}};
  const math=Object.create(Math);math.random=()=>random;
  const navigator={clipboard:{writeText:async(t)=>{el.get('captured').textContent=t}}};
  el.set('captured',fresh());
  vm.runInNewContext(js[1],{document,Math:math,navigator},{timeout:1000});
  const $=id=>document.getElementById(id);
  function choose(label){
    const b=$('actions').children.find(x=>x.textContent===label);
    assert(b && !b.disabled,'missing/unavailable: '+label+'; list='+$('actions').children.map(x=>x.textContent));
    b.onclick();
  }
  return {$,choose};
}
{
  const p=boot(0.2);p.$('priority').value='A';
  p.$('why').value='希望保住A';p.$('variant').value='ordinary';p.$('start').onclick();
  assert.equal(p.$('setup').hidden,true);
  assert.equal(p.$('requirement').textContent.includes('尚未知道'),true);
  p.choose('做一份 A 工作');p.choose('做一份 B 工作');
  assert(p.$('requirement').textContent.includes('需要 A'));
  assert.equal(p.$('actions').children.find(x=>x.textContent.startsWith('提交')).disabled,true);
  p.choose('做一份 A 工作');
  p.choose('提交本次需求匹配的完整成果');
  assert(p.$('outcome').textContent.includes('C 完整'));
  assert(p.$('outcome').textContent.includes('原规则综合值：16'));
  assert(p.$('outcome').textContent.includes('你选定的目标得到保留'));
  assert(p.$('timeline').textContent.includes('第3手')); // stage redraw occurs before final action
  p.$('reflection').value='自行选择了保A';p.$('copy').onclick();
}
{
  const p=boot(0.8);p.$('priority').value='A';p.$('variant').value='safe';p.$('start').onclick();
  p.choose('做一份 A 工作');p.choose('做一份 A 工作');
  assert(p.$('requirement').textContent.includes('需要 B'));
  p.choose('SAFE：一手完整处理 C');p.choose('取得 SIDE 小成果');
  assert(p.$('outcome').textContent.includes('原规则综合值：17'));
  assert(p.$('outcome').textContent.includes('A 完成'));
  assert(p.$('outcome').textContent.includes('B 未完成'));
  p.$('again').onclick();assert.equal(p.$('setup').hidden,false);
}
{
  const p=boot(0.8);p.$('priority').value='FULL_C';p.$('start').onclick();
  p.choose('取得 SIDE 小成果');p.choose('取得 SIDE 小成果');
  p.choose('直接处理 C（两次才能得到部分成果）');
  p.choose('直接处理 C（两次才能得到部分成果）');
  assert(p.$('outcome').textContent.includes('C 部分'));
  assert(p.$('outcome').textContent.includes('你选定的目标没有保住'));
}
console.log('PASS 3 independent no-answer observation traces: matching submission, universal SAFE, non-full direct result');
