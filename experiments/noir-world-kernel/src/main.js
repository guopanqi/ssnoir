import * as THREE from 'three';
import { createComposer } from './style/post.js';
import { buildWorld } from './world/buildWorld.js';

const app=document.querySelector('#app');
const renderer=new THREE.WebGLRenderer({antialias:true,alpha:false,powerPreference:'high-performance'});
renderer.outputColorSpace=THREE.SRGBColorSpace;
renderer.toneMapping=THREE.NoToneMapping;
renderer.shadowMap.enabled=true;
renderer.shadowMap.type=THREE.PCFSoftShadowMap;
renderer.setClearColor(0x030409,1);
app.appendChild(renderer.domElement);

const scene=new THREE.Scene();
scene.background=new THREE.Color(0x030409);
scene.fog=new THREE.FogExp2(0x030409,.008);

const camera=new THREE.PerspectiveCamera(35,1,.1,160);
scene.add(new THREE.HemisphereLight(0x222633,0x000000,.45));
const key=new THREE.DirectionalLight(0xe9e6dc,.75);
key.position.set(-10,18,12);key.castShadow=true;key.shadow.mapSize.set(1024,1024);scene.add(key);

const world=buildWorld(scene);
const {composer,resize}=createComposer(renderer,scene,camera);

const shots={
  wide:{pos:[15,4.8,25],target:[0,2.7,-10.5],fov:34},
  street:{pos:[9,4.0,18],target:[0,2.5,-8],fov:36},
  alley:{pos:[-9,4.2,16],target:[-4.5,2.7,-10],fov:34},
  detail:{pos:[4.7,3.1,11.5],target:[0,1.9,4.0],fov:30}
};
let shotName='wide',mode='final';

function applyShot(name=shotName){
  const s=shots[name]||shots.wide;shotName=name;
  camera.position.set(...s.pos);camera.fov=s.fov;camera.updateProjectionMatrix();camera.lookAt(...s.target);
}
function setMode(next='final'){mode=next;world.setMode(mode);}
function render(){composer.render();}
function resizeCanvas(){
  const w=window.innerWidth,h=window.innerHeight,dpr=Math.min(window.devicePixelRatio||1,1.5);
  camera.aspect=w/h;camera.updateProjectionMatrix();resize(w,h,dpr);world.resize(w*dpr,h*dpr);render();
}

window.__NOIR_LAB__={
  ready:false,
  async prepareCapture({shot='wide',evaluation='final'}={}){
    applyShot(shot);setMode(evaluation);
    renderer.setPixelRatio(1);renderer.setSize(1440,900,false);
    camera.aspect=1440/900;camera.updateProjectionMatrix();composer.setSize(1440,900);world.resize(1440,900);
    render();await new Promise(requestAnimationFrame);render();
  },
  setShot:applyShot,setMode,
  info(){return {
    version:'0.3.0',phase:'Genesis Noir spatial-language study',
    renderer:'black 3D depth skeleton + authored screen-space vector lines + luminous 2D planes',
    shot:shotName,evaluation:mode,proceduralOnly:true
  }}
};

applyShot();resizeCanvas();window.addEventListener('resize',resizeCanvas);window.__NOIR_LAB__.ready=true;
(function loop(){render();requestAnimationFrame(loop);})();
