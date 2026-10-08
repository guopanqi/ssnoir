import * as THREE from 'three';
import { createComposer } from './style/post.js';
import { buildWorld } from './world/buildWorld.js';
import { buildTerminalCorner } from './world/buildTerminalCorner.js';
import { buildAssetAudition } from './world/buildAssetAudition.js';

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

const worlds={
  cafe:buildWorld(scene),
  terminal:buildTerminalCorner(scene),
  audition:buildAssetAudition(scene)
};
worlds.terminal.setVisible(false);
worlds.audition.setVisible(false);

const {composer,resize}=createComposer(renderer,scene,camera);

const shots={
  cafe:{
    wide:{pos:[-10.3,4.35,22.2],target:[.3,2.45,-9.6],fov:35},
    street:{pos:[-6.0,3.7,17.2],target:[.4,2.20,-8.3],fov:35},
    alley:{pos:[9.0,4.2,16.0],target:[2.5,2.55,-9.5],fov:34},
    detail:{pos:[7.0,3.0,12.0],target:[2.8,1.8,5.8],fov:30}
  },
  terminal:{
    wide:{pos:[-9.0,4.15,21.0],target:[0,2.35,-9.0],fov:35},
    street:{pos:[-5.3,3.55,16.0],target:[.4,2.15,-7.3],fov:35},
    detail:{pos:[7.4,3.0,12.2],target:[3.1,1.8,5.8],fov:30}
  }
};

let worldName='cafe',shotName='wide',mode='final';

function setWorld(name='cafe'){
  const next=worlds[name]?name:'cafe';
  for(const [id,world] of Object.entries(worlds)) world.setVisible(id===next);
  worldName=next;
  worlds[worldName].setMode(mode);
}

function applyCamera(spec){
  camera.position.set(...spec.pos);
  camera.fov=spec.fov;
  camera.updateProjectionMatrix();
  camera.lookAt(...spec.target);
}

function applyShot(name=shotName,nextWorld=worldName){
  if(nextWorld!==worldName) setWorld(nextWorld);
  if(worldName==='audition'){
    applyCamera(worlds.audition.cameraSpec());
    shotName='asset';
    return;
  }
  const table=shots[worldName];
  const shot=table[name]??table.wide;
  shotName=table[name]?name:'wide';
  applyCamera(shot);
}

function setMode(next='final'){
  mode=next;
  worlds[worldName].setMode(mode);
}
function render(){composer.render();}
function resizeCanvas(){
  const width=window.innerWidth,height=window.innerHeight,dpr=Math.min(window.devicePixelRatio||1,1.5);
  camera.aspect=width/height;camera.updateProjectionMatrix();
  resize(width,height,dpr);
  for(const world of Object.values(worlds)) world.resize(width*dpr,height*dpr);
  render();
}

window.__NOIR_LAB__={
  ready:false,
  async prepareCapture({
    scene:sceneName='cafe',
    shot='wide',
    evaluation='final',
    assetId=null
  }={}){
    setWorld(sceneName);
    if(sceneName==='audition'){
      if(!assetId) throw new Error('audition capture requires assetId');
      worlds.audition.setAsset(assetId);
    }
    setMode(evaluation);
    applyShot(shot,sceneName);

    renderer.setPixelRatio(1);renderer.setSize(1440,900,false);
    camera.aspect=1440/900;camera.updateProjectionMatrix();
    composer.setSize(1440,900);
    for(const world of Object.values(worlds)) world.resize(1440,900);
    render();await new Promise(requestAnimationFrame);render();
  },
  setScene:setWorld,setShot:applyShot,setMode,
  setAuditionAsset(id){
    setWorld('audition');
    worlds.audition.setAsset(id);
    applyShot('asset','audition');
  },
  info(){return {
    version:'0.7.1',
    phase:'AI vector ingress + automatic asset audition',
    renderer:'shared 3D skeleton + FacadeGrammar + VectorAssetCatalog + SVG Puppet/Prop + source-driven ReflectionField',
    scene:worldName,shot:shotName,evaluation:mode,
    worlds:Object.keys(worlds),
    audition:worlds.audition.info()
  };}
};

setWorld('cafe');applyShot('wide','cafe');resizeCanvas();
window.addEventListener('resize',resizeCanvas);
window.__NOIR_LAB__.ready=true;
(function loop(){render();requestAnimationFrame(loop);})();
