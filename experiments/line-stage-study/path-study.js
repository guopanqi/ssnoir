const $=s=>document.querySelector(s),NS='http://www.w3.org/2000/svg';
const data=await fetch('./assets/path-study/night.json').then(r=>{if(!r.ok)throw Error('人物路径加载失败');return r.json()});
const tracks=[];
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
const total=data.paths.reduce((sum,p)=>sum+p.points.slice(1).reduce((n,pt,i)=>n+Math.hypot(pt[0]-p.points[i][0],pt[1]-p.points[i][1]),0),0);
let cursor=4;
for(const p of data.paths){const length=p.points.slice(1).reduce((n,pt,i)=>n+Math.hypot(pt[0]-p.points[i][0],pt[1]-p.points[i][1]),0),duration=length/total*6;makePath($('#actor'),line(p.points),p.color,3,cursor,duration);cursor+=duration}
let time=0,playing=true,last=performance.now();
function render(){
 // Reverse the exact construction clock; each stroke retracts toward its own starting point.
 const construction=time<=14?Math.min(time,10):Math.max(0,10-(time-14)*1.25);
 let visible=0,actorLength=0;
 for(const t of tracks){let k=Math.max(0,Math.min(1,(construction-t.start)/t.duration));t.el.style.strokeDashoffset=t.length*(1-k);t.el.style.visibility=k>0?'visible':'hidden';if(t.start>=4){visible+=k*t.length;actorLength+=t.length}}
 outline.style.opacity=$('#mask').checked?Math.min(1,visible/actorLength*5):0;
 $('#actor').style.filter=$('#glow-toggle').checked?'drop-shadow(0 0 2px #7186ff)':'none';
 $('#phase').textContent=time<4?'布景 · 描出':time<10?'夜莺 · 路径入场':time<14?'定场':time<18.8?'夜莺 · 原路收线':time<22?'布景 · 原路收线':'退场完成';
 $('#readout').textContent=`${time.toFixed(1)} / 22 秒`;$('#time').value=time;$('#play').textContent=playing?'暂停':'播放';
}
$('#play').onclick=()=>{if(time>=22)time=0;playing=!playing;render()};$('#restart').onclick=()=>{time=0;playing=true;render()};$('#time').oninput=e=>{time=Number(e.target.value);playing=false;render()};$('#mask').onchange=render;$('#glow-toggle').onchange=render;
function tick(now){if(playing){time=Math.min(22,time+(now-last)/1000*Number($('#speed').value));if(time>=22)playing=false}last=now;render();requestAnimationFrame(tick)}requestAnimationFrame(tick);
