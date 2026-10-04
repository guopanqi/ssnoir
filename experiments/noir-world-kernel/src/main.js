import * as THREE from 'three';
import { createComposer } from './style/post.js';
import { buildWorld } from './world/buildWorld.js';

const app=document.querySelector('#app');
const renderer=new THREE.WebGLRenderer({antialias:true,alpha:false,powerPreference:'high-performance'});
renderer.outputColorSpace=THREE.SRGBColorSpace;
renderer.toneMapping=THREE.NoToneMapping;
renderer.shadowMap.enabled=true;
renderer.shadowMap.type=THREE.PCFSoftShadowMap;
renderer.setClearColor(0x020309,1);
app.appendChild(renderer.domElement);

const scene=new THREE.Scene();
scene.background=new THREE.Color(0x020309);
scene.fog=new THREE.FogExp2(0x020309,.007);

const camera=new THREE.PerspectiveCamera(35,1,.1,160);
scene.add(new THREE.HemisphereLight(0x20232d,0x000000,.38));
const key=new THREE.DirectionalLight(0xe9e6dc,.62);
key.position.set(-10,18,12);scene.add(key);

const world=buildWorld(scene);
const {composer,resize}=createComposer(renderer,scene,camera);

const shots={
  wide:{pos:[-10.3,4.35,22.2],target:[.3,2.45,-9.6],fov:35},
  street:{pos:[-6.0,3.7,17.2],target:[.4,2.20,-8.3],fov:35},
  alley:{pos:[9.0,4.2,16.0],target:[2.5,2.55,-9.5],fov:34},
  detail:{pos:[7.0,3.0,12.0],target:[2.8,1.8,5.8],fov:30}
};

let shotName='wide',mode='final';

function applyShot(name=shotName){
  const shot=shots[name]??shots.wide;shotName=name;
  camera.position.set(...shot.pos);camera.fov=shot.fov;camera.updateProjectionMatrix();
  camera.lookAt(...shot.target);
}
function setMode(next='final'){mode=next;world.setMode(mode);}
function render(){composer.render();}
function resizeCanvas(){
  const width=window.innerWidth,height=window.innerHeight,dpr=Math.min(window.devicePixelRatio||1,1.5);
  camera.aspect=width/height;camera.updateProjectionMatrix();
  resize(width,height,dpr);world.resize(width*dpr,height*dpr);render();
}

window.__NOIR_LAB__={
  ready:false,
  async prepareCapture({shot='wide',evaluation='final'}={}){
    applyShot(shot);setMode(evaluation);
    renderer.setPixelRatio(1);renderer.setSize(1440,900,false);
    camera.aspect=1440/900;camera.updateProjectionMatrix();
    composer.setSize(1440,900);world.resize(1440,900);
    render();await new Promise(requestAnimationFrame);render();
  },
  setShot:applyShot,setMode,
  info(){return {
    version:'0.5.3',
    phase:'reference-driven Genesis Noir local street',
    renderer:'3D spatial skeleton + authored vector/SVG illustration layers + source-driven wet reflections',
    shot:shotName,evaluation:mode,proceduralPlusVectorAssets:true
  };}
};

applyShot();resizeCanvas();window.addEventListener('resize',resizeCanvas);window.__NOIR_LAB__.ready=true;
(function loop(){render();requestAnimationFrame(loop);})();
