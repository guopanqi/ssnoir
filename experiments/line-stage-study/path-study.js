const $=s=>document.querySelector(s),NS='http://www.w3.org/2000/svg';
const data=await fetch('./assets/path-study/night.json').then(r=>{if(!r.ok)throw Error('人物路径加载失败');return r.json()});
const tracks=[];
const actorStart=.36,actorDuration=.8,blendDuration=.3,actorReady=actorStart+actorDuration;
const backdropReady=3.8,exitStart=14,exitDrawStart=exitStart+blendDuration,actorExitEnd=exitDrawStart+actorDuration,endTime=22;
function makePath(parent,d,color,width,start,duration){
 const el=document.createElementNS(NS,'path');el.setAttribute('d',d);el.setAttribute('stroke',color);el.setAttribute('stroke-width',width);parent.append(el);
 const length=el.getTotalLength();el.style.strokeDasharray=`${length} ${length}`;tracks.push({el,length,start,duration});return el;
}
function line(points){return points.map((p,i)=>`${i?'L':'M'}${p[0]},${p[1]}`).join(' ')}
const world=[];
world.push([[180,790],[1410,790]],[[420,790],[420,140],[1200,140],[1200,790]],[[370,140],[1250,140]],[[660,790],[660,455],[960,455],[960,790]]);
for(let row=0;row<3;row++)for(let col=0;col<6;col++){let x=465+col*118,y=185+row*82;world.push([[x,y],[x+55,y],[x+55,y+55],[x,y+55],[x,y]])}
world.push([[680,790],[680,478],[940,478],[940,790]],[[1020,570],[1080,570],[1080,665],[1020,665],[1020,570]],[[1028,580],[1072,580]],[[1028,590],[1072,590]]);
world.forEach((p,i)=>makePath($('#world'),line(p),i<4?'#61738b':'#3e526c',1.5,i*.12,.8));
// One compound contour preserves holes. Its opacity follows the actor's total visible path length.
const outline=document.createElementNS(NS,'path');outline.setAttribute('d',data.outline.map(p=>line(p)+' Z').join(' '));outline.style.fill='#070b12';outline.setAttribute('fill-rule','evenodd');$('#backing').append(outline);
// All character strokes share one clock, just as a set of backdrop lines can draw in parallel.
for(const p of data.paths)makePath($('#actor'),line(p.points),p.color,3,actorStart,actorDuration);
const original=document.createElementNS(NS,'image');original.setAttribute('href','./assets/path-study/night-original.png');original.setAttribute('width',1024);original.setAttribute('height',1024);$('#original').append(original);
let time=0,playing=true,last=performance.now();
function render(){
 // Backdrop reverses its construction clock; the actor owns a separate exit clock.
 const construction=time>=endTime?0:time<=exitDrawStart?Math.min(time,backdropReady):backdropReady*(endTime-time)/(endTime-exitDrawStart);
 let visible=0,actorLength=0;
 for(const t of tracks){const isActor=t.el.parentNode===$('#actor');let k=isActor&&time>=exitDrawStart?Math.max(0,1-(time-exitDrawStart)/actorDuration):Math.max(0,Math.min(1,(construction-t.start)/t.duration));t.el.style.strokeDashoffset=t.length*(1-k);t.el.style.visibility=k>0?'visible':'hidden';if(isActor){visible+=k*t.length;actorLength+=t.length}}
 const imageMix=$('#use-original').checked?Math.max(0,Math.min(1,(time-actorReady)/blendDuration,(exitDrawStart-time)/blendDuration)):0;
 $('#original').style.opacity=imageMix;$('#actor').style.opacity=1-imageMix;
 outline.style.opacity=$('#mask').checked?Math.min(1,visible/actorLength*5):0;
 $('#actor').style.filter=$('#glow-toggle').checked?'drop-shadow(0 0 2px #7186ff)':'none';
 $('#phase').textContent=time<actorStart?'布景 · 开始建立':time<actorReady?'门框与夜莺 · 同步描出':time<actorReady+blendDuration?'夜莺 · 路径 → 原图':time<backdropReady?'人物已在场 · 窗户继续建立':time<exitStart?($('#use-original').checked?'原图 · 定场':'路径 · 定场'):time<exitDrawStart?'原图 → 路径':time<actorExitEnd?'人物与布景 · 独立收线':time<endTime?'人物已退场 · 布景继续收线':'退场完成';
 $('#readout').textContent=`${time.toFixed(1)} / 22 秒`;$('#time').value=time;$('#play').textContent=playing?'暂停':'播放';
}
$('#play').onclick=()=>{if(time>=22)time=0;playing=!playing;render()};$('#restart').onclick=()=>{time=0;playing=true;render()};$('#time').oninput=e=>{time=Number(e.target.value);playing=false;render()};$('#use-original').onchange=render;$('#mask').onchange=render;$('#glow-toggle').onchange=render;
function tick(now){if(playing){time=Math.min(22,time+(now-last)/1000*Number($('#speed').value));if(time>=22)playing=false}last=now;render();requestAnimationFrame(tick)}requestAnimationFrame(tick);
