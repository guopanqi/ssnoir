import {CutoutRig} from './rig.js';
import {CLIPS,sampleScene} from './scenes.js';
const el=id=>document.getElementById(id);
const canvas=el('stage'),ctx=canvas.getContext('2d',{alpha:false});
if(!ctx)throw Error('Canvas 2D unavailable');
const W=1600,H=900;
const query=new URL(location.href).searchParams;
let scene=CLIPS[query.get('scene')]?query.get('scene'):'drunk';
const references={};
let t=Number(query.get('t')||0),speed=1,playing=!query.has('pause'),last=0,rig=null;
const checks={rigged:el('rigged'),coat:el('coat'),joints:el('joints')};
const view=el('view'),clock=el('clock'),range=el('timeline'),subtitle=el('subtitle'),playBtn=el('play');
if(['compare','stage','exploded'].includes(query.get('view')))view.value=query.get('view');
const fail=e=>{el('error').hidden=false;el('error').textContent='加载失败：'+e.message;console.error(e);};
const image=url=>new Promise((resolve,reject)=>{const i=new Image();i.onload=()=>resolve(i);i.onerror=()=>reject(Error('Missing image '+url));i.src=url;});
const rgba=(v,a)=>`rgba(${v},${a})`;
function line(points,color='#56606c',width=2){ctx.beginPath();ctx.moveTo(...points[0]);for(const p of points.slice(1))ctx.lineTo(...p);ctx.lineWidth=width;ctx.strokeStyle=color;ctx.stroke();}
function rect(x,y,w,h,fill,stroke,width=1){ctx.fillStyle=fill;ctx.fillRect(x,y,w,h);if(stroke){ctx.lineWidth=width;ctx.strokeStyle=stroke;ctx.strokeRect(x,y,w,h)}}
function text(label,x,y,size=16,opacity=1,align='left'){
  ctx.save();ctx.fillStyle=`rgba(220,211,194,${opacity})`;ctx.textAlign=align;ctx.font=`${size}px Georgia,serif`;ctx.fillText(label,x,y);ctx.restore();
}
function wallGrain(time,brightness=1){
  // Deterministic strokes: no frame-to-frame random flicker.
  for(let i=0;i<45;i++){
    const px=(i*372.1)%W,py=(i*177.15)%790;
    ctx.strokeStyle=`rgba(189,200,210,${(.02+(i%4)*.008)*brightness})`;
    ctx.beginPath();ctx.moveTo(px,py);ctx.lineTo(px+((i*23)%78),py);ctx.stroke();
  }
}
function backdrop(state){
  const grad=ctx.createLinearGradient(0,0,0,H);
  grad.addColorStop(0,'#08090b');grad.addColorStop(.60,'#12151a');grad.addColorStop(1,'#070809');
  ctx.fillStyle=grad;ctx.fillRect(0,0,W,H);
  // Architectural strokes, intentionally subordinate to the character.
  if(scene==='drunk'){
    rect(55,130,580,645,'#0e1012','#5b6064',2);
    line([[55,252],[635,252]],'#5c6369',3);
    rect(185,362,267,430,'#111416','#9a9a88',2);
    rect(202,379,240,413,'#08090d','#3a4149',3);
    line([[370,410],[370,777]],'#9f9070',2);
    text('T A V E R N',340,220,44,.70,'center');
    const glow=ctx.createRadialGradient(355,383,0,355,383,310);
    glow.addColorStop(0,'rgba(220,176,108,.18)');glow.addColorStop(1,'rgba(220,176,108,0)');
    ctx.fillStyle=glow;ctx.fillRect(45,135,660,660);
    ctx.beginPath();ctx.arc(350,370,8,0,Math.PI*2);ctx.fillStyle='#d0b47a';ctx.fill();
    line([[1110,165],[1110,780]],'#58636d',3);line([[1110,165],[1310,165]],'#58636d',2);
    for(let yy=220;yy<785;yy+=68)line([[1018,yy],[1430,yy]],'#343c43',1);
    for(let xx=1030;xx<1460;xx+=145)line([[xx,155],[xx,800]],'#252b31',1);
  }else if(scene==='brace'){
    rect(935,90,570,710,'#0e1014','#58606b',3);
    for(let y=155;y<785;y+=70){line([[950,y],[1500,y]],'#404852',2);
      for(let x=950+((y/70)%2)*75;x<1500;x+=150)line([[x,y],[x,y+70]],'#2d343e',2)}
    line([[935,85],[935,790]],'#a9acb1',5);
    rect(250,115,250,280,'#11151b','#42515c',2);
  }else{
    const offset=state.speed*120;
    for(let i=0;i<16;i++){
      const x=((i*220-state.t*150*state.speed+3000)%2000)-250;
      const h=210+(i*79)%290;
      rect(x,780-h,110+(i%3)*40,h,'#101318',i%4===0?'#343e48':null,1);
      if(i%3===0)line([[x+35,780-h+55],[x+100,780-h+55]],'#627084',2);
    }
    rect(160,505,130,285,'#11171d','#63707e',3);
    line([[145,509],[303,509]],'#a6a8a4',3);
    rect(985,130,310,665,'#0a0b0f','#708093',3);
    line([[995,130],[1080,790]],'#637080',2);line([[1280,130],[1185,790]],'#637080',2);
    for(let j=0;j<13;j++){const yy=260+((j*37+state.t*230*state.speed)%500);
      line([[60+j*120,yy],[185+j*120,yy]],'#8894a2',2);}
  }
  line([[0,784],[1600,784]],'#89929b',2);
  line([[0,807],[1600,807]],'#2e333a',2);
  wallGrain(state.t);
}
function portraitShadow(x,y,scale,opacity=.6){
  ctx.save();ctx.translate(x,y);ctx.scale(scale,1);
  const grad=ctx.createRadialGradient(0,0,0,0,0,220);
  grad.addColorStop(0,`rgba(0,0,0,${opacity})`);grad.addColorStop(1,'transparent');
  ctx.fillStyle=grad;ctx.beginPath();ctx.ellipse(0,0,220,22,0,0,Math.PI*2);ctx.fill();ctx.restore();
}
function drawReference(name,x,y,height=530){
  const img=references[name];
  if(!img)return false;
  const w=img.width*height/img.height;
  ctx.save();ctx.globalAlpha=.92;ctx.drawImage(img,x-w/2,y-height,w,height);ctx.restore();
  return true;
}
function drawPuppet(state,x,footY,scale,staticPose=false,explode=0){
  const allowed=checks.rigged.checked&&!staticPose;
  const angles=allowed?{...state.angles}:{};
  if(!checks.coat.checked){delete angles.coatL;delete angles.coatR;}
  ctx.save();
  ctx.translate(x,footY+(staticPose?0:state.bob));
  if(!staticPose)ctx.rotate(state.tilt);
  portraitShadow(0,14,1.2,state.shadow*.65);
  rig.render(ctx,{x:0,y:0,scale,angles,showJoints:checks.joints.checked,explode});
  ctx.restore();
}
function render(){
  if(!rig)return;
  t=Math.max(0,Math.min(CLIPS[scene].duration,t));
  const state=sampleScene(scene,t);
  if(view.value==='stage'){
    backdrop(state);
    const x=state.x*W;
    drawPuppet(state,x,783,.46,false,0);
    text(CLIPS[scene].title,74,100,19,.57);
    line([[70,110],[225,110]],'#987c5a',2);
  }else if(view.value==='compare'){
    backdrop({...state,speed:0});
    ctx.fillStyle='rgba(4,6,8,.55)';ctx.fillRect(0,0,W,H);
    line([[800,104],[800,816]],'#59626a',2);
    const ref=sampleScene(scene,0);
    if(!drawReference(scene,405,790,510))drawPuppet(ref,405,795,.45,true);
    drawPuppet(state,1175,795,.45,false);
    text('KEY POSE · GENERATED REFERENCE',405,132,16,.78,'center');
    text('RIGGED CUTOUT · MOTION',1175,132,16,.78,'center');
  }else{
    backdrop({...state,speed:0});ctx.fillStyle='rgba(3,5,8,.72)';ctx.fillRect(0,0,W,H);
    const rest=sampleScene(scene,0);
    drawPuppet(rest,800,780,.44,true,1);
    text('13 INDEPENDENT PIECES / EXPLICIT JOINTS',800,115,21,.8,'center');
    text('HEAD · TORSO · PELVIS · 2× UPPER ARM · 2× FOREARM · 2× THIGH · 2× SHIN · 2× COAT',800,150,13,.65,'center');
  }
  subtitle.textContent=state.beat;
  clock.textContent=`${sec(t)} / ${sec(CLIPS[scene].duration)}`;
  playBtn.textContent=playing?'暂停':'播放';
  range.value=String(t);
  el('frame-label').textContent=scene.toUpperCase()+' / '+CLIPS[scene].duration+'s';
}
const sec=t=>'00:'+String(Math.floor(t)).padStart(2,'0');
function setScene(name){
  if(!CLIPS[name])throw Error('Unknown scene '+name);
  scene=name;t=0;range.max=String(CLIPS[name].duration);
  document.querySelectorAll('[data-scene]').forEach(b=>b.classList.toggle('selected',b.dataset.scene===name));
  render();
}
function toggle(){playing=!playing;render();}
document.querySelectorAll('[data-scene]').forEach(b=>b.addEventListener('click',()=>{setScene(b.dataset.scene);playing=true;}));
playBtn.addEventListener('click',toggle);
el('restart').addEventListener('click',()=>{t=0;playing=true;render();});
range.addEventListener('input',()=>{t=Number(range.value);render();});
view.addEventListener('change',render);
Object.values(checks).forEach(c=>c.addEventListener('change',render));
el('speed').addEventListener('change',e=>speed=Number(e.target.value));
document.addEventListener('keydown',e=>{
  if(['INPUT','SELECT','BUTTON'].includes(document.activeElement?.tagName))return;
  if(e.code==='Space'){e.preventDefault();toggle();}
  else if(e.code==='ArrowRight'){e.preventDefault();t+=.10;render();}
  else if(e.code==='ArrowLeft'){e.preventDefault();t-=.10;render();}
});
function frame(ms){
  if(rig){
    const dt=Math.min(.065,(ms-last)/1000||0);last=ms;
    if(playing){t+=dt*speed;if(t>=CLIPS[scene].duration){t=CLIPS[scene].duration;playing=false;}render();}
  }
  requestAnimationFrame(frame);
}
async function init(){
  try{
    const [spec,atlas]=await Promise.all([fetch('./rig.json').then(r=>{if(!r.ok)throw Error('Missing rig.json');return r.json()}),image('./assets/puppet-atlas.png')]);
    rig=new CutoutRig(spec,atlas);
    const keys=['base','drunk','brace','run'];
    await Promise.all(keys.map(async key=>{try{references[key]=await image('./assets/reference-'+key+'.png')}catch(e){console.warn('Optional reference image unavailable',key)}}));
    setScene(scene);
    if(query.has('t')){t=Number(query.get('t'));render();}
    window.__cutout={setScene,seek:s=>{t=Number(s);render();},setView:s=>{view.value=s;render();},sample:()=>sampleScene(scene,t),get state(){return {scene,time:t,playing,parts:rig.nodes.size}}};
    window.dispatchEvent(new Event('cutout-ready'));
  }catch(e){fail(e);}
}
init();requestAnimationFrame(frame);
