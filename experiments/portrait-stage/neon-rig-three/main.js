import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.180.0/build/three.module.js';
import {makeNeonActor} from './rig.js';
import {SCENES,sampleScene} from './scenes.js';

const $=(s)=>document.querySelector(s);
const CANVAS=$('#stage-canvas');
const LABEL=$('#beat'),SCRUB=$('#scrub'),TIME=$('#time'),PLAY=$('#play'),SCENE=$('#scene');
const RIG=$('#rig'),JOINTS=$('#joints'),KEYS=$('#keyposes'),SPEED=$('#speed');
const error=(message)=>{$('#error').hidden=false;$('#error').textContent=message;};
const renderer=new THREE.WebGLRenderer({canvas:CANVAS,antialias:true,alpha:false});
renderer.setClearColor(0x080a0e,1);
renderer.setPixelRatio(Math.min(devicePixelRatio||1,2));
renderer.outputColorSpace=THREE.SRGBColorSpace;
const scene=new THREE.Scene();
const camera=new THREE.OrthographicCamera(-8.9,8.9,5.006,-5.006,.1,100);
camera.position.set(0,0,20);camera.lookAt(0,0,0);
const world=new THREE.Group();scene.add(world);
const venue=new THREE.Group(),road=new THREE.Group(),lane=new THREE.Group(),overlay=new THREE.Group();
world.add(venue,road,lane,overlay);
const mat=(color=0xe2ddc9,opacity=1)=>new THREE.LineBasicMaterial({
  color,transparent:true,opacity,depthWrite:false,depthTest:false
});
const wire=(parent,pts,color=0x8c98a4,opacity=.6,closed=false)=>{
  const arr=pts.map(v=>new THREE.Vector3(v[0],v[1],0));
  const geometry=new THREE.BufferGeometry().setFromPoints(closed?[...arr,arr[0]]:arr);
  const line=new THREE.Line(geometry,mat(color,opacity));parent.add(line);return line;
};
const rect=(parent,x,y,w,h,color=0x9da3b0,opacity=.7)=>{
  return wire(parent,[[x,y],[x+w,y],[x+w,y+h],[x,y+h]],color,opacity,true);
};
const fill=(parent,x,y,w,h,color,opacity=1)=>{
  const obj=new THREE.Mesh(new THREE.PlaneGeometry(w,h),
  new THREE.MeshBasicMaterial({color,transparent:true,opacity,depthWrite:false,depthTest:false}));
  obj.position.set(x,y,-.02);parent.add(obj);return obj;
};
const mkText=(parent,text,x,y,size=58,color='#b5ad98')=>{
  const canvas=document.createElement('canvas');canvas.width=640;canvas.height=148;
  const context=canvas.getContext('2d');
  context.textAlign='center';context.textBaseline='middle';
  context.font='600 '+size+'px Georgia, serif';context.letterSpacing='8px';
  context.fillStyle=color;context.fillText(text,320,75);
  const tex=new THREE.CanvasTexture(canvas);tex.colorSpace=THREE.SRGBColorSpace;
  const material=new THREE.MeshBasicMaterial({map:tex,transparent:true,depthTest:false,depthWrite:false});
  const object=new THREE.Mesh(new THREE.PlaneGeometry(4.0,.92),material);object.position.set(x,y,.01);
  parent.add(object);return object;
};
function sceneProps(){
  wire(venue,[[-8.9,-2.56],[8.9,-2.56]],0x8590a6,.44);
  fill(venue,-5.8,-.32,5.2,6.4,0x14171a,.92);
  wire(venue,[[-8.4,-2.55],[-8.4,2.92],[-2.8,2.92],[-2.8,-2.55]],0x737d82,.56);
  rect(venue,-5.03,-2.55,2.28,4.45,0xafafa3,.73);
  rect(venue,-4.85,-2.55,1.85,4.10,0x515661,.7);
  wire(venue,[[-3.22,.3],[-3.22,-2.2]],0xc8ad7c,.72);
  wire(venue,[[-8.4,1.97],[-2.80,1.97]],0x59616a,.6);
  mkText(venue,'TAVERN',-5.69,2.45,71,'#c5b38e');
  wire(venue,[[-5.0,1.9],[-4.95,1.35],[-4.55,1.35]],0xc5a36a,.6);
  const bulb=new THREE.Mesh(new THREE.CircleGeometry(.16,32),
    new THREE.MeshBasicMaterial({color:0xe2c08a,transparent:true,opacity:1,depthTest:false}));
  bulb.position.set(-4.55,1.33,0);venue.add(bulb);
  // The actor and the rest of the street are intentionally kept free of backdrop detail.
  wire(venue,[[-2.75,-2.55],[-2.2,-2.55]],0x777e88,.48);
  wire(venue,[[.30,-2.56],[.30,1.3],[1.30,1.3]],0x55606a,.36);
  wire(venue,[[7.60,-2.56],[7.60,2.75]],0x5b6270,.43);

  wire(road,[[-8.8,-2.57],[8.8,-2.57]],0x8c94a6,.51);
  // Mailbox, newspaper bench and simple street landmarks.
  rect(road,-4.75,-1.8,.78,1.42,0xc0bdaf,.76);
  wire(road,[[-4.84,-.4],[-3.91,-.4]],0xd6c7a9,.85);
  wire(road,[[-4.64,-1.0],[-4.12,-1.0]],0x8e989e,.72);
  wire(road,[[2.83,-1.95],[5.3,-1.95]],0x7d8697,.65);
  for(const x of[3.02,5.08])wire(road,[[x,-1.95],[x,-2.48]],0x7d8697,.65);
  mkText(road,'MAIL',-4.36,.10,38,'#929cae');
  for(let i=0;i<4;i++){
    const x=-7.5+i*5.15;
    wire(road,[[x,-2.57],[x,-2.57+(i%2)*.1]],0x5b6372,.6);
  }

  wire(lane,[[-1.5,-2.57],[-.79,2.73]],0x758294,.63);
  wire(lane,[[1.3,-2.57],[.66,2.73]],0x758294,.63);
  for(let k=0;k<5;k++){
    const y=-2.4+k*1.10, s=(y+2.5)/5.5;
    wire(lane,[[-1.5+s*.7,y],[1.3-s*.65,y]],0x4c5a69,.35);
  }
  fill(lane,0,-.4,2.2,6.2,0x090b0f,.55);
  mkText(lane,'ALLEY',.10,3.0,50,'#76849b');
  // Parallax geometry is translated in world-space on every update.
  for(let layer=0;layer<3;layer++){
    const g=new THREE.Group();g.userData.layer=layer;overlay.add(g);
    for(let i=0;i<13;i++){
      const x=-11+i*2.6+layer*.9;
      const h=2.7+((i*7+layer*3)%5)*.38;
      wire(g,[[x,-2.56],[x, h]],0x4f5e75,.12+layer*.06);
      if(i%3===0)wire(g,[[x,-.1],[x+1.1,-.1]],0x63738e,.10+layer*.04);
    }
  }
}
sceneProps();
// Small moving props make the two dramatic triggers visible, not only captioned.
const letter=new THREE.Group();road.add(letter);
rect(letter,-.30,-.20,.60,.40,0xd0c6b0,.94);
wire(letter,[[-.3,.2],[0,-.035],[.3,.2]],0xd0c6b0,.85);
const newspaper=new THREE.Group();road.add(newspaper);
rect(newspaper,-.51,-.31,1.02,.62,0xbfc2c5,.75);
for(let j=0;j<4;j++)wire(newspaper,[[-.35,.16-j*.12],[.33,.16-j*.12]],0xbfc2c5,.46);
const doorGlow=fill(venue,-3.91,-.33,1.65,4.1,0xb29256,.10);
// Local immutable snapshots of the Unity Neon paths: no game runtime dependency.
const sources=['尼尔','尼尔_醉站','尼尔_醉行','尼尔_靠墙','码头工人','码头工人_奔跑','黑影'];
const sourceStore={};
async function loadAssets() {
  await Promise.all(sources.map(async key=>{
    const result=await fetch('./data/'+encodeURIComponent(key)+'.neon.json');
    if(!result.ok)throw new Error('Cannot load formal Neon asset '+key+' ('+result.status+')');
    const data=await result.json();
    if(!Array.isArray(data.paths)||data.paths.length===0)throw new Error('Invalid paths '+key);
    sourceStore[key]=data;
  }));
}
const actors={};
function createActor(id,source,opt={}){
  const rig=makeNeonActor(THREE,sourceStore[source],opt);
  actors[id]=rig;world.add(rig.group);
  return rig;
}
let running=false,activeScene='drunk',clock=0,speed=1,last=0,scrubbing=false,loaded=false;
const url=new URL(location.href);
if(SCENES[url.searchParams.get('scene')])activeScene=url.searchParams.get('scene');
function refreshControls(){
  SCENE.value=activeScene;
  SCRUB.max=String(SCENES[activeScene].duration);
  $('#scene-title').textContent=SCENES[activeScene].title;
  $('#scene-description').textContent=SCENES[activeScene].detail;
}
const format=s=>String(Math.floor(s/60)).padStart(2,'0')+':'+String(Math.floor(s%60)).padStart(2,'0');
function drawFrame(){
  if(!loaded)return;
  const st=sampleScene(activeScene,clock);
  venue.visible=activeScene==='drunk';
  road.visible=activeScene==='chase';
  lane.visible=activeScene==='chase';
  overlay.visible=activeScene==='chase';
  lane.position.set(0,0,0);
  lane.scale.set(1,1,1);
  lane.children.forEach(child=>{if(child.material?.opacity!==undefined&&child.material?.map)child.material.opacity=st.alley;});
  lane.visible=activeScene==='chase'&&st.alley>.02;
  overlay.children.forEach((g,i)=>g.position.x=((st.scroll*(i+1)*-1.15+20)%5)-2.5);
  doorGlow.material.opacity=st.gateLight*.14;
  if(activeScene==='chase'){
    const moved=Math.max(0,Math.min(1,(clock-2.4)/1.25));
    letter.visible=clock<5.4;
    letter.position.set(-4.39+2.2*moved,-1.0+.63*moved,.26);
    letter.rotation.z=moved*.31;
    newspaper.visible=clock<4.4;
    newspaper.position.set(4.12,-.18-1.9*Math.max(0,Math.min(1,(clock-3.6)/.8)),.30);
    newspaper.rotation.z=-.04-clock*.16;
  }
  for(const [id,rig] of Object.entries(actors)){
    if(id==='neil'||id==='worker'){
      const state=st[id],key=KEYS.checked?state.source:(id==='neil'?'尼尔':'码头工人');
      rig.setData(sourceStore[key]);
      rig.group.visible=!!state.visible;
      rig.update(clock,state.pose,{
        x:state.x,y:state.y+(state.bob||0),z:id==='neil'?.24:.18,
        tilt:state.tilt,articulate:RIG.checked,showJoints:JOINTS.checked,
        stroke:id==='neil'?.022:.0205,unit:4.95
      });
      rig.group.scale.set(state.flip?-(state.scale||1):(state.scale||1),state.scale||1,1);
    }else{
      const n=Number(id.slice(5)),phase=(clock*5.2+n*1.47);
      const slide=((n*2.45+phase+16)%21)-10.5;
      rig.group.visible=activeScene==='chase'&&st.crowd>.03;
      rig.update(clock,{torso:.04*Math.sin(phase),legL:.15*Math.sin(phase*2),legR:-.15*Math.sin(phase*2)},
        {x:slide,y:-2.62+((n*3)%3)*.045,z:n%2?-.1:.13,articulate:RIG.checked,stroke:.036,unit:4.15});
      rig.group.scale.set(n%2?-1:1,.94+(.08*(n%3)),1);
      rig.group.children[1].material.opacity=st.crowd*.34;
    }
  }
  camera.position.x=st.focus;
  camera.updateProjectionMatrix();
  LABEL.textContent=st.beat;
  const total=SCENES[activeScene].duration;
  if(!scrubbing)SCRUB.value=String(clock);
  TIME.textContent=format(clock)+' / '+format(total);
  PLAY.textContent=running?'暂停':'播放';
  PLAY.setAttribute('aria-pressed',String(running));
  renderer.render(scene,camera);
}
function seek(t){
  clock=Math.max(0,Math.min(SCENES[activeScene].duration,Number(t)||0));drawFrame();
}
function pause(){running=false;drawFrame()}
function play(){if(clock>=SCENES[activeScene].duration)clock=0;running=true;last=performance.now();drawFrame()}
function loop(now){
  requestAnimationFrame(loop);
  const dt=Math.min(.075,(now-last)/1000||0);last=now;
  if(running&&loaded){
    clock+=dt*speed;
    if(clock>=SCENES[activeScene].duration){
      clock=SCENES[activeScene].duration;running=false;
    }
    drawFrame();
  }
}
PLAY.addEventListener('click',()=>running?pause():play());
$('#replay').addEventListener('click',()=>{seek(0);play()});
SCENE.addEventListener('change',()=>{
  activeScene=SCENE.value;seek(0);refreshControls();play();
});
SCRUB.addEventListener('pointerdown',()=>{scrubbing=true});
SCRUB.addEventListener('input',()=>{seek(SCRUB.value);SCRUB.value=String(clock)});
const stopScrub=()=>{scrubbing=false;last=performance.now()};
SCRUB.addEventListener('pointerup',stopScrub);SCRUB.addEventListener('change',stopScrub);
[RIG,JOINTS,KEYS].forEach(node=>node.addEventListener('change',drawFrame));
SPEED.addEventListener('change',()=>{speed=Number(SPEED.value)});
document.addEventListener('keydown',event=>{
  if(event.code==='Space'&&!['INPUT','SELECT','BUTTON'].includes(document.activeElement.tagName)){
    event.preventDefault();running?pause():play();
  }
  if(event.code==='ArrowRight')seek(clock+1/4);
  if(event.code==='ArrowLeft')seek(clock-1/4);
});
function resize(){
  const w=CANVAS.clientWidth||1280,h=CANVAS.clientHeight||720;
  renderer.setSize(w,h,false);drawFrame();
}
addEventListener('resize',resize);
async function start(){
  try{
    await loadAssets();
    createActor('neil','尼尔',{color:0xf2ebdc});
    createActor('worker','码头工人',{color:0xd0d9df});
    for(let i=0;i<6;i++)createActor('crowd'+i,'黑影',{color:0x7d8ca5,opacity:.1});
    loaded=true;refreshControls();
    if(url.searchParams.has('rig'))RIG.checked=url.searchParams.get('rig')!=='0';
    if(url.searchParams.has('joints'))JOINTS.checked=url.searchParams.get('joints')==='1';
    if(url.searchParams.has('keyposes'))KEYS.checked=url.searchParams.get('keyposes')!=='0';
    if(url.searchParams.has('t'))seek(Number(url.searchParams.get('t')));
    resize();
    window.__neonTheatre={seek,sample:()=>sampleScene(activeScene,clock),
      setScene:(next)=>{if(!SCENES[next])throw Error(next);activeScene=next;seek(0);refreshControls();},
      get state(){return {scene:activeScene,time:clock,running}}};
    dispatchEvent(new CustomEvent('neon-theatre-ready'));
    if(url.searchParams.get('pause')!=='1'&&!url.searchParams.has('t'))play();
  }catch(e){error('加载失败：'+e.message);console.error(e);}
}
requestAnimationFrame(loop);
start();
