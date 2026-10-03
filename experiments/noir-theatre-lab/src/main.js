import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import './style.css';
import { PROFILE, MODES } from './config.js';
import { buildWorld } from './scene.js';
import { SHOTS, applyShot as applyShotDefinition } from './shots.js';
import { createPost } from './post.js';
import { installCaptureApi } from './captureApi.js';

const canvas=document.querySelector('#scene');
const renderer=new THREE.WebGLRenderer({canvas,antialias:true,powerPreference:'high-performance'});
renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));
renderer.shadowMap.enabled=true;
renderer.shadowMap.type=THREE.PCFShadowMap;
renderer.outputColorSpace=THREE.SRGBColorSpace;
renderer.toneMapping=THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure=PROFILE.renderer.exposure;

const scene=new THREE.Scene();
scene.background=new THREE.Color(PROFILE.renderer.background);
scene.fog=new THREE.FogExp2(0x070707,.0048);
const camera=new THREE.PerspectiveCamera(38,innerWidth/innerHeight,.1,220);
const controls=new OrbitControls(camera,renderer.domElement);
controls.enableDamping=true; controls.dampingFactor=.07; controls.maxDistance=120;
const post=createPost(renderer,scene,camera,PROFILE);
const world=await buildWorld(scene,PROFILE);
let shotIndex=0, modeIndex=3;
const label=document.querySelector('#shotLabel');

function render(){ controls.update(); post.composer.render(); }
function applyShot(index){
  shotIndex=((index%SHOTS.length)+SHOTS.length)%SHOTS.length;
  const s=applyShotDefinition(camera,controls,shotIndex,(name)=>world.setActive(name));
  label.textContent=s.label; render(); return s;
}
function setMode(name){
  post.setMode(name); world.setLinesVisible(name==='line'||name==='final'); render();
}
function resize(){
  const w=innerWidth,h=innerHeight; renderer.setSize(w,h,false); camera.aspect=w/h; camera.updateProjectionMatrix(); post.resize(w,h); render();
}
addEventListener('resize',resize);
addEventListener('keydown',(e)=>{
  if(e.key>='1'&&e.key<='4') applyShot(Number(e.key)-1);
  if(e.key.toLowerCase()==='m'){ modeIndex=(modeIndex+1)%MODES.length; setMode(MODES[modeIndex]); }
  if(e.key.toLowerCase()==='h') document.querySelector('#hud').classList.toggle('hidden');
});
controls.addEventListener('change',render);
resize(); applyShot(0); setMode('final');
installCaptureApi({world,post,applyShot,camera,controls,render});
