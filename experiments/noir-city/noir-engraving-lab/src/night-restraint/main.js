import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
import {mergeCity,buildSky,buildRiverMirror,buildLamps,buildNeon,buildMoonLights,setMoon,mulberry32} from './scene/referenceCity.js';
import {buildComposer} from './post/referencePost.js';
import {profile as P} from './config/visualProfile.js';
import {shots} from './shots.js';
import {buildMovement} from './systems/movement.js';
import {mountTuning} from './systems/tuning.js';
import './systems/tuning.css';
import {buildCallouts} from './systems/landmarks.js';
import {mountHUD} from './ui/hud.js';

async function main(){
 const scene=new THREE.Scene();
 const shot=shots[new URLSearchParams(location.search).get('view')||'world'];
 if(!shot)throw new Error('未知机位');
 const camera=new THREE.PerspectiveCamera(22.87,16/9,2,12000);
 camera.position.fromArray(shot.position);
 const renderer=new THREE.WebGLRenderer({antialias:true,powerPreference:'high-performance'});
 renderer.toneMapping=THREE.ACESFilmicToneMapping;
 renderer.toneMappingExposure=P.exposure;
 renderer.shadowMap.enabled=true;
 renderer.shadowMap.type=THREE.PCFSoftShadowMap;
 document.body.append(renderer.domElement);
 const controls=new OrbitControls(camera,renderer.domElement);
 controls.target.fromArray(shot.target);controls.enableDamping=true;
 controls.dampingFactor=.06;controls.maxPolarAngle=1.62;
 controls.minDistance=25;controls.maxDistance=2800;controls.update();
 const base='/night-restraint/';
 const read=async name=>{const r=await fetch(base+name);if(!r.ok)throw new Error(`资源加载失败: ${name}`);return r.json();};
 const [gltf,lamps,roads,riverRoute,anchors]=await Promise.all([new GLTFLoader().loadAsync(base+'city.glb'),read('lamps.json'),read('roads.json'),read('river-route.json'),read('anchors.json')]);
 const {group:city,candidates}=mergeCity(gltf.scene);scene.add(city);
 const ground=new THREE.Mesh(new THREE.PlaneGeometry(9000,9000),new THREE.MeshStandardMaterial({color:'#04060a',roughness:1}));
 ground.rotation.x=-Math.PI/2;ground.position.y=-4;ground.receiveShadow=true;scene.add(ground);
 const sky=buildSky(new THREE.Vector3(0,1,0));scene.add(sky);buildRiverMirror(city);
 const moonRig=buildMoonLights(P);scene.add(moonRig.moon,moonRig.moon.target,moonRig.hemi);
 const lampRig=buildLamps(lamps);scene.add(lampRig.group);
 for(const m of lampRig.glowMats){m.userData.baseOpacity=m.opacity;m.color.set('#e6c796');}
 for(const m of lampRig.poolMats)m.userData.baseOpacity=m.opacity;
 for(const light of lampRig.pointLights)light.intensity=light.userData.base*P.sodiumPoints;
 const movement=buildMovement(scene,roads,riverRoute,P);
 const neon=buildNeon(candidates,P.neonCount);scene.add(neon.group);
 const post=buildComposer(renderer,scene,camera);
 setMoon(moonRig.moon,sky,post.atmo,P.moonAz*Math.PI/180,P.moonEl*Math.PI/180);
 function apply(){
  for(const m of lampRig.glowMats)m.opacity=m.userData.baseOpacity*P.lampGlow;
  for(const m of lampRig.poolMats)m.opacity=m.userData.baseOpacity*P.lampPool;
  for(const light of lampRig.pointLights)light.intensity=light.userData.base*P.sodiumPoints;
  city.traverse(mesh=>{if(mesh.isMesh&&mesh.name.startsWith('M_窗光')){const m=mesh.material;if(!m.userData.baseColor)m.userData.baseColor=m.color.clone();m.color.copy(m.userData.baseColor).multiplyScalar(P.windowGain);}});
  renderer.toneMappingExposure=P.exposure;
 Object.entries({uDens:P.fogDens,uFall:P.fogFall,uHaze:P.haze,uMist:P.mist,uSkyHaze:P.skyHaze}).forEach(([key,value])=>post.atmo.uniforms[key].value=value);
 post.atmo.uniforms.uFogLow.value.set(P.fogLow);post.atmo.uniforms.uFogHigh.value.set(P.fogHigh);
 post.bloom.strength=P.bloom;post.bloom.radius=P.bloomRadius;post.bloom.threshold=P.bloomThreshold;
 Object.entries({uContrast:P.contrast,uSaturation:P.saturation,uVignette:P.vignette,uGrain:P.grain,uAberr:P.aberr}).forEach(([key,value])=>post.grade.uniforms[key].value=value);
 }
 apply();mountTuning(P,apply);
 const landmarks=buildCallouts(anchors);landmarks.bindControls(controls);scene.add(landmarks.group);
 const focus=rig=>{if(landmarks.mode!=='world')return;landmarks.focus(rig,camera,controls);controls.enabled=false;hud.setView('focusing',rig.name);};
 const hud=mountHUD({landmarks:landmarks.rigs,onFocus:focus,onBack:()=>{if(landmarks.mode!=='focused')return;landmarks.exit(camera,controls);controls.enabled=false;hud.setView('exiting',landmarks.selected.name);}});
 landmarks.onArrive=kind=>{controls.enabled=true;hud.setView(kind==='exit'?'world':'focused',landmarks.selected?.name);};
 const ndc=new THREE.Vector2(),raycaster=new THREE.Raycaster(),hit=new THREE.Vector3();
 const byName=new Map(anchors.map(a=>[a.name,a]));
 const bounds=landmarks.rigs.map(rig=>{const a=byName.get(rig.name);return {rig,box:new THREE.Box3(new THREE.Vector3(a.cx-a.hx,0,-a.cy-a.hy),new THREE.Vector3(a.cx+a.hx,a.top,-a.cy+a.hy))};});
 function pointer(e){const r=renderer.domElement.getBoundingClientRect();ndc.set((e.clientX-r.left)/r.width*2-1,-(e.clientY-r.top)/r.height*2+1);}
 function pickBuilding(){raycaster.setFromCamera(ndc,camera);let chosen=null,best=Infinity;for(const {rig,box}of bounds){if(raycaster.ray.intersectBox(box,hit)){const d=hit.distanceTo(camera.position);if(d<best){chosen=rig;best=d;}}}return chosen;}
 let down=null;
 renderer.domElement.addEventListener('pointermove',e=>{if(landmarks.mode!=='world'){renderer.domElement.style.cursor='default';return;}pointer(e);const feedback=landmarks.onPointer(camera,ndc,'move');renderer.domElement.style.cursor=feedback.cursor==='pointer'||pickBuilding()?'pointer':'default';});
 renderer.domElement.addEventListener('pointerdown',e=>{down=[e.clientX,e.clientY];});
 renderer.domElement.addEventListener('pointerup',e=>{if(!down)return;const moved=Math.hypot(e.clientX-down[0],e.clientY-down[1]);down=null;if(moved>6||landmarks.mode!=='world')return;pointer(e);const rig=landmarks.onPointer(camera,ndc,'click').focus||pickBuilding();if(rig)focus(rig);});
 renderer.domElement.addEventListener('pointercancel',()=>{down=null;});
 const viewport={w:1280,h:720};
 function resize(){
  const w=Math.min(innerWidth,innerHeight*16/9),h=w*9/16,dpr=Math.min(devicePixelRatio||1,1.75);
  renderer.setPixelRatio(dpr);renderer.setSize(w,h,false);renderer.domElement.style.width=`${w}px`;renderer.domElement.style.height=`${h}px`;
  post.setSize(Math.round(w*dpr),Math.round(h*dpr));
  viewport.w=w;viewport.h=h;hud.resize(renderer.domElement.getBoundingClientRect());
 }
 resize();addEventListener('resize',resize);
 const clock=new THREE.Clock(),rnd=mulberry32(9);
 renderer.setAnimationLoop(()=>{
  const dt=Math.min(clock.getDelta(),.05),time=clock.elapsedTime;
  controls.update();movement.tick(dt);landmarks.tick(camera,dt,viewport);hud.tick(camera);
  renderer.toneMappingExposure=P.exposure*(1-.16*landmarks.focusFx);
  for(const sign of neon.signs){const flicker=P.neonFlicker&&rnd()<.012?.25:1;sign.mat.color.copy(sign.base).multiplyScalar((.9+.1*Math.sin(time*6+sign.phase))*flicker);}
  post.beforeRender(camera);post.renderScene(camera);
  post.atmo.uniforms.uTime.value=time;post.grade.uniforms.uTime.value=time;post.composer.render();
 });
 renderer.domElement.dataset.ready='true';
}
main().catch(error=>{console.error(error);const pre=document.createElement('pre');pre.id='failure';pre.textContent=error.message;document.body.append(pre);});
