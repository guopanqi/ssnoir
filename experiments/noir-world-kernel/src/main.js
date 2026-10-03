import * as THREE from 'three';
import { PROFILE } from './style/profile.js';
import { createComposer } from './style/post.js';
import { buildWorld } from './world/buildWorld.js';

const app = document.querySelector('#app');

const renderer = new THREE.WebGLRenderer({
  antialias: true,
  alpha: false,
  powerPreference: 'high-performance'
});
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.15;
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.setClearColor(PROFILE.palette.void,1);
app.appendChild(renderer.domElement);

const scene = new THREE.Scene();
scene.background = new THREE.Color(PROFILE.palette.void);
scene.fog = new THREE.FogExp2(PROFILE.palette.void,0.018);

const camera = new THREE.PerspectiveCamera(PROFILE.camera.fov,1,PROFILE.camera.near,PROFILE.camera.far);

const hemi = new THREE.HemisphereLight(0xe8e3d6,0x020202,PROFILE.light.fill);
scene.add(hemi);

const key = new THREE.DirectionalLight(0xfff4d9,PROFILE.light.key);
key.position.set(-18,28,17);
key.castShadow = true;
key.shadow.mapSize.set(2048,2048);
key.shadow.camera.left=-38; key.shadow.camera.right=38;
key.shadow.camera.top=38; key.shadow.camera.bottom=-38;
key.shadow.camera.near=1; key.shadow.camera.far=90;
key.shadow.bias=-0.00025;
scene.add(key);

const rim = new THREE.DirectionalLight(0xd7dbe0,PROFILE.light.rim);
rim.position.set(22,12,-20);
scene.add(rim);

const world = buildWorld(scene,PROFILE.palette);
const {composer,resize} = createComposer(renderer,scene,camera);

const shots = {
  wide: {pos:[29,17,37], target:[-1,6,-6], fov:34},
  street: {pos:[17,8.5,23], target:[-1,4,-4], fov:38},
  alley: {pos:[-1,7.5,22], target:[-8,6,-13], fov:32},
  detail: {pos:[9,5.4,16], target:[-2,3.2,3.8], fov:28}
};

let shotName='wide';
let mode='final';

function applyShot(name=shotName) {
  const s=shots[name] ?? shots.wide;
  shotName=name;
  camera.position.set(...s.pos);
  camera.fov=s.fov;
  camera.updateProjectionMatrix();
  camera.lookAt(...s.target);
}

function setMode(next='final'){
  mode=next;
  world.setMode(mode,scene);
}

function render(){
  composer.render();
}

function resizeCanvas(){
  const w=window.innerWidth,h=window.innerHeight;
  camera.aspect=w/h;
  camera.updateProjectionMatrix();
  resize(w,h,Math.min(window.devicePixelRatio||1,1.5));
  render();
}

window.__NOIR_LAB__ = {
  ready:false,
  async prepareCapture({shot='wide',evaluation='final'}={}){
    applyShot(shot);
    setMode(evaluation);
    renderer.setPixelRatio(1);
    renderer.setSize(1440,900,false);
    camera.aspect=1440/900;
    camera.updateProjectionMatrix();
    composer.setSize(1440,900);
    render();
    await new Promise(requestAnimationFrame);
    render();
  },
  setShot:applyShot,
  setMode,
  info(){
    return {
      version:'0.1.0',
      phase:'A/B',
      renderer:'WebGLRenderer + toon value grouping + selective geometry lines + noir composite',
      shot:shotName,
      evaluation:mode,
      proceduralOnly:true
    };
  }
};

applyShot('wide');
resizeCanvas();
window.addEventListener('resize',resizeCanvas);
window.__NOIR_LAB__.ready=true;

function loop(){
  render();
  requestAnimationFrame(loop);
}
loop();
