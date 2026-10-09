import * as T from 'three';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
import {EffectComposer} from 'three/addons/postprocessing/EffectComposer.js';
import {RenderPass} from 'three/addons/postprocessing/RenderPass.js';
import {OutputPass} from 'three/addons/postprocessing/OutputPass.js';
import {createBridgePrint} from '../post/bridgePrint.js';
import {buildBridge} from './scene.js';

const renderer=new T.WebGLRenderer({canvas:document.querySelector('canvas'),antialias:true});
renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));
renderer.shadowMap.enabled=true;renderer.shadowMap.type=T.PCFSoftShadowMap;
renderer.toneMapping=T.AgXToneMapping;renderer.toneMappingExposure=1.25;
const scene=new T.Scene();scene.background=new T.Color(0x1a2433);
const camera=new T.PerspectiveCamera(40,innerWidth/innerHeight,.1,180);
// 延续已认可的斜向机位，局部实验适当降低俯角。
camera.position.set(-55,31,-36);
const controls=new OrbitControls(camera,renderer.domElement);controls.target.set(0,4,0);
controls.enableDamping=true;controls.minDistance=20;controls.maxDistance=110;controls.maxPolarAngle=Math.PI*.47;
buildBridge(scene);
const target=new T.WebGLRenderTarget(innerWidth,innerHeight,{samples:4});
const composer=new EffectComposer(renderer,target);composer.addPass(new RenderPass(scene,camera));composer.addPass(new OutputPass());
const print=createBridgePrint();composer.addPass(print);
let mode='ink';
function setMode(next){
  if(!['shape','ink','hatch'].includes(next))throw new Error(`Unknown mode ${next}`);
  mode=next;print.uniforms.uMode.value={shape:0,ink:1,hatch:2}[next];
  scene.traverse(o=>{if(o.isLine)o.visible=next!=='shape';});
  document.querySelectorAll('button').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.mode===next)));
  render();
}
function resize(){renderer.setSize(innerWidth,innerHeight);camera.aspect=innerWidth/innerHeight;camera.fov=T.MathUtils.radToDeg(2*Math.atan(Math.tan(T.MathUtils.degToRad(20))*Math.max(1,(16/9)/camera.aspect)));camera.updateProjectionMatrix();composer.setSize(innerWidth,innerHeight);print.uniforms.uResolution.value.set(innerWidth,innerHeight);}
function render(){controls.update();composer.render();}
resize();window.addEventListener('resize',resize);
document.querySelectorAll('button').forEach(b=>b.addEventListener('click',()=>setMode(b.dataset.mode)));
window.addEventListener('keydown',e=>{if(e.key.toLowerCase()==='h')document.querySelector('aside').hidden=!document.querySelector('aside').hidden;});
window.bridgeStudy={setMode,render,getState:()=>({mode,camera:camera.position.toArray(),target:controls.target.toArray(),drawCalls:renderer.info.render.calls})};
if(new URLSearchParams(location.search).has('capture'))document.body.dataset.capture='true';
function loop(){requestAnimationFrame(loop);render();}loop();
