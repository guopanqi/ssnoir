const $ = s => document.querySelector(s);
const game = $('#org-game');
const descriptions = {
 stack:['以对象收拢行动','对象标题、进度和截止状态只出现一次，行动向下排列。观察：关联是否清楚，以及六组同时出现时是否遮住过多场景。'],
 wings:['围绕场景对象展开行动','两侧行动卡连接场景对象，下方目标牌集中说明对象与进度。其他对象保留独立入口，点击切换焦点；虚线仅表达跨对象影响。'],
 columns:['地点提供边界，对象各自成列','同一地点内，每个对象保留自己的进度和行动。观察：不同目标是否容易区分，以及横向排列是否削弱空间感。']
};
let mode='wings', sample='shared', width=1280, focus=null, selected=5, funds=40, objects=[], placed=new Map(), results=new Map();
const action=(id,name,skill,text,targets,cost=0)=>({id,name,skill,text,targets,cost});
function reset(){
 focus=null;funds=40;selected=5;placed.clear();results.clear();
 objects=[{id:'school',name:'去学校的时间',value:2,max:4,desc:'替她接下几段晚班，老板才肯让她去学校。',deadline:'报名截止 3/4',anchor:[.5,.24],actions:[action('cover','帮她顶班','交际 0','坏无进展，中1格，好2格；不拿工资。',[['school',1]]),action('pay','请人代班','','付20美元，安排剩余代班。',[['school',2]],20)]}];
 if(sample!=='shared') objects.push({id:'door',name:'后门',value:1,max:3,desc:'看清门锁和后巷的出入情况。',anchor:[.24,.30],actions:[action('lock','查看门锁','观察 0','检查锁孔留下的痕迹。',[['door',1]])]});
 if(sample==='cross'){
 objects[0].actions[0].targets.push(['door',1]);
 objects[0].actions[0].text='接下晚班，同时请同事留意后门。';
 }
 if(sample==='dense'){
 const names=['柜台账目','夜班司机','货物去向','屋内争执'];
 const verbs=['查账','打听车牌','核对货单','打圆场'];
 names.forEach((name,i)=>objects.push({id:'extra'+i,name,value:i%3,max:4,desc:'查看现场，找到下一步的线索。',anchor:[[.77,.28],[.20,.52],[.50,.46],[.80,.52]][i],actions:[action('extra-action'+i,verbs[i],'观察 0','投入一次行动，推进1格。',[['extra'+i,1]])]}));
 }
 render();
}
function die(value){const points={1:[4],2:[0,8],3:[0,4,8],4:[0,2,6,8],5:[0,2,4,6,8],6:[0,2,3,5,6,8]};return `<span class="die" aria-hidden="true">${Array.from({length:9},(_,i)=>`<i class="pip ${points[value].includes(i)?'':'off'}"></i>`).join('')}</span>`;}
function clock(o){return `<div class="org-clock"><div class="segments">${Array.from({length:o.max},(_,i)=>`<i class="segment ${i<o.value?'filled':''}"></i>`).join('')}</div><span class="org-count">${o.value} / ${o.max}</span></div>`;}
function row(a){const value=placed.get(a.id);const result=results.get(a.id);const secondary=a.targets.slice(1).map(([id,n])=>`${objects.find(o=>o.id===id).name} +${n}`).join('、');return `<div class="org-action" data-action="${a.id}"><div class="org-action-heading"><h3>${a.name}</h3><span class="org-skill">${a.skill}</span></div><div class="org-action-body"><p>${a.text}</p><button class="org-slot ${a.cost?'money':value?'':'empty'}" data-slot="${a.id}" aria-label="${a.name}：${a.cost?'支付20美元':value?'取回骰子':'放入所选骰子'}">${a.cost?'$20':die(value||5)}</button></div>${secondary?`<div class="org-impact">同时影响：${secondary}</div>`:''}${value||a.cost?`<button class="org-execute" data-execute="${a.id}" ${a.cost>funds?'disabled':''}>${a.cost?'支付并安排':'执行 · 示意推进1格'}</button>`:''}${result?`<div class="org-result" role="status">${result}</div>`:''}</div>`;}
function header(o){return `<header><button data-focus="${o.id}" aria-label="聚焦${o.name}"><span>${o.name}</span><span class="org-focus-label">查看</span></button><p class="org-desc">${o.desc}</p>${clock(o)}${o.deadline?`<div class="org-deadline">${o.deadline}</div>`:''}</header>`;}
function object(o,x,y,w,compact=false,rows=true){return `<article class="org-object ${compact?'compact':''}" data-object="${o.id}" style="left:${x}px;top:${y}px;width:${w}px">${header(o)}${rows?(compact?o.actions.map(a=>`<button class="org-summary" data-focus="${o.id}">${a.name}<span>${a.cost?'$20':a.skill}</span></button>`).join(''):o.actions.map(row).join('')):''}</article>`;}
function render(){
 const visible=focus?objects.filter(o=>o.id===focus||sample==='cross'):objects;
 const dense=visible.length>3;
 game.style.width=width+'px';game.className='game org-game org-mode-'+mode;
 let html=`<div class="org-top"><span>世界 / 街角餐馆${focus?' / '+objects.find(o=>o.id===focus).name:''}</span>${focus?'<button id="org-back">返回全场</button>':'<span>第 6 天</span>'}</div><div class="org-context">${dense?'六个对象 · 点对象标题进入局部':sample==='cross'?'一个行动同时推进两个目标；主归属不因此合并':sample==='separate'?'同一地点，两个独立对象':'同一目标，两种办法'}</div><svg class="org-lines" aria-hidden="true"></svg>`;
 visible.forEach(o=>html+=`<div class="org-anchor" data-anchor="${o.id}" style="left:${o.anchor[0]*width}px;top:${o.anchor[1]*720}px"><span>${o.name}</span></div>`);
 if(mode==='wings'&&!dense){
   const main=visible.find(o=>o.id===focus)||visible[0];
   const targetWidth=430;
   html+=object(main,(width-targetWidth)/2,488,targetWidth,false,false);
   main.actions.forEach((a,j)=>{
     const w=350;
     const x=j===0?40:width-w-40;
     html+=`<article class="org-wing" data-owner="${main.id}" data-side="${j===0?'left':'right'}" style="left:${x}px;top:270px;width:${w}px">${row(a)}</article>`;
   });
   visible.filter(o=>o.id!==main.id).forEach((o,i)=>{
     html+=object(o,width-310,94+i*135,270,true,false);
   });

 }else{
   const cols=dense?3:visible.length===1?1:visible.length;
   const gap=mode==='columns'?18:26;
   const available=width-80;
   const cardWidth=dense?(available-gap*2)/3:Math.min(500,(available-gap*(cols-1))/cols);
   const total=cardWidth*cols+gap*(cols-1);const start=(width-total)/2;
   visible.forEach((o,i)=>{
     const x=start+(i%cols)*(cardWidth+gap);
     const y=dense?108+Math.floor(i/cols)*233:mode==='columns'?190:200;
     html+=object(o,x,y,cardWidth,dense);
   });
   if(mode==='columns')html+='<div class="org-container-title">街角餐馆 / 对象与行动</div>';
 }
 html+=`<div class="org-hand"><strong>尼尔</strong>${[3,5,6].map(v=>`<button data-die="${v}" aria-label="选择${v}点骰子" aria-pressed="${selected===v}">${die(v)}</button>`).join('')}<span style="font-size:13px;color:#b4b5ad">选择后点骰槽</span></div><div class="org-funds">现金　$${funds}</div>`;
 game.innerHTML=html;
 document.querySelectorAll('[data-layout]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.layout===mode)));
 $('#org-title').textContent=descriptions[mode][0];$('#org-note').textContent=descriptions[mode][1];
 fit();requestAnimationFrame(lines);
}
function lines(){
 const svg=game.querySelector('.org-lines');if(!svg)return;svg.replaceChildren();
 const scale=game.getBoundingClientRect().width/width;
 const base=game.getBoundingClientRect();
 const bounds=el=>{const r=el.getBoundingClientRect();return {x:(r.x-base.x)/scale,y:(r.y-base.y)/scale,w:r.width/scale,h:r.height/scale};};
 function line(from,to,secondary=false){
  const ns='http://www.w3.org/2000/svg';
  const mid=(from.y+to.y)/2;
  const shape=`M${from.x},${from.y} L${from.x},${mid} L${to.x},${mid} L${to.x},${to.y}`;
  const group=document.createElementNS(ns,'g');
  for(const kind of ['underlay','relation']){
   const path=document.createElementNS(ns,'path');
   path.setAttribute('class',kind==='underlay'?'org-line-underlay':'org-line'+(secondary?' side':''));
   path.setAttribute('d',shape);group.append(path);
  }
  for(const [point,radius] of [[from,3],[to,7]]){
   const dot=document.createElementNS(ns,'circle');
   dot.setAttribute('cx',point.x);dot.setAttribute('cy',point.y);dot.setAttribute('r',radius);
   dot.setAttribute('class','org-line-end'+(secondary?' side':''));group.append(dot);
  }
  svg.append(group);
 }

 game.querySelectorAll('[data-object]').forEach(el=>{const o=objects.find(o=>o.id===el.dataset.object);const r=bounds(el);line({x:o.anchor[0]*width,y:o.anchor[1]*720},{x:r.x+r.w/2,y:r.y});});
 game.querySelectorAll('[data-owner]').forEach(el=>{const target=game.querySelector(`[data-object="${el.dataset.owner}"]`);const a=bounds(el);const o=objects.find(o=>o.id===el.dataset.owner);
 const edge=el.dataset.side==='left'?a.x+a.w:a.x;
 line({x:edge,y:a.y+43},{x:o.anchor[0]*width,y:o.anchor[1]*720});});
 if(sample==='cross'){
 const source=game.querySelector('[data-action="cover"]');const target=game.querySelector('[data-object="door"]');
 if(source&&target){const a=bounds(source),b=bounds(target);line({x:a.x+a.w,y:a.y+a.h/2},{x:b.x,y:b.y+b.h/2},true);}
 }
}
function fit(){const scale=$('#org-viewport').clientWidth/width;game.style.transform=`scale(${scale})`;$('#org-viewport').style.height=720*scale+'px';}
game.addEventListener('click',e=>{
 const dieButton=e.target.closest('[data-die]');if(dieButton){selected=+dieButton.dataset.die;render();return;}
 if(e.target.closest('#org-back')){focus=null;render();return;}
 const focusButton=e.target.closest('[data-focus]');if(focusButton){focus=focusButton.dataset.focus;render();return;}
 const slot=e.target.closest('[data-slot]');if(slot){const id=slot.dataset.slot;const a=objects.flatMap(o=>o.actions).find(a=>a.id===id);if(a.cost)return;if(placed.has(id))placed.delete(id);else placed.set(id,selected);results.delete(id);render();return;}
 const execute=e.target.closest('[data-execute]');if(!execute)return;
 const a=objects.flatMap(o=>o.actions).find(a=>a.id===execute.dataset.execute);
 if(a.cost>funds||(!a.cost&&!placed.has(a.id)))throw Error('行动成本不满足');
 funds-=a.cost;placed.delete(a.id);
 const changed=[];
 for(const [id,delta] of a.targets){const target=objects.find(o=>o.id===id);const before=target.value;target.value=Math.min(target.max,target.value+delta);changed.push(`${target.name} +${target.value-before}`);}
 results.set(a.id,changed.join('；'));$('#org-status').textContent=a.name+'：'+changed.join('；');render();
 a.targets.forEach(([id])=>game.querySelector(`[data-object="${id}"]`)?.classList.add('received'));
});
document.querySelectorAll('[data-layout]').forEach(b=>b.onclick=()=>{mode=b.dataset.layout;render();});
$('#sample').onchange=e=>{sample=e.target.value;$('#org-status').textContent='';reset();};
$('#aspect').onchange=e=>{width=+e.target.value;render();};
$('#restore').onclick=()=>{$('#org-status').textContent='';reset();};
new ResizeObserver(()=>{fit();lines();}).observe($('#org-viewport'));
reset();
