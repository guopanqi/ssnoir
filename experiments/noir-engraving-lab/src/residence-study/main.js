import * as T from 'three';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
import {EffectComposer} from 'three/addons/postprocessing/EffectComposer.js';
import {RenderPass} from 'three/addons/postprocessing/RenderPass.js';
import {OutputPass} from 'three/addons/postprocessing/OutputPass.js';
import {createBridgePrint} from '../post/bridgePrint.js';
import {buildExterior,buildRoom} from './scene.js';
import {lightResidence} from './lighting.js';
import {profile} from './profile.js';
import {shots} from './shots.js';
const params=new URLSearchParams(location.search),view=params.get('view')||'room',shot=shots[view];
if(!shot)throw new Error(`Unknown shot: ${view}`);
const renderer=new T.WebGLRenderer({canvas:document.querySelector('canvas'),antialias:true});renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));renderer.shadowMap.enabled=true;renderer.shadowMap.type=T.PCFSoftShadowMap;renderer.toneMapping=T.AgXToneMapping;renderer.toneMappingExposure=profile.exposure;
const scene=new T.Scene();scene.background=new T.Color(profile.background);
const world=shot.scene==='room'?buildRoom(scene):buildExterior(scene);lightResidence(scene,world);
const camera=new T.PerspectiveCamera(shot.fov,1,.1,160);camera.position.set(...shot.position);
const controls=new OrbitControls(camera,renderer.domElement);controls.target.set(...shot.target);controls.enableDamping=true;controls.minDistance=3;controls.maxDistance=80;controls.maxPolarAngle=Math.PI*.49;
const composer=new EffectComposer(renderer,new T.WebGLRenderTarget(innerWidth,innerHeight,{samples:4}));composer.addPass(new RenderPass(scene,camera));composer.addPass(new OutputPass());const print=createBridgePrint();composer.addPass(print);
function mode(name){if(!['shape','ink','hatch'].includes(name))throw new Error(`Unknown mode: ${name}`);print.uniforms.uMode.value={shape:0,ink:1,hatch:2}[name];scene.traverse(o=>{if(o.isLine)o.visible=name!=='shape';});document.querySelectorAll('button').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.mode===name)));}
function resize(){camera.aspect=innerWidth/innerHeight;camera.fov=T.MathUtils.radToDeg(2*Math.atan(Math.tan(T.MathUtils.degToRad(shot.fov/2))*Math.max(1,(16/9)/camera.aspect)));camera.updateProjectionMatrix();renderer.setSize(innerWidth,innerHeight);composer.setSize(innerWidth,innerHeight);print.uniforms.uResolution.value.set(innerWidth,innerHeight);}
document.querySelectorAll('a').forEach(a=>{if(a.dataset.view===view)a.setAttribute('aria-current','page');});document.querySelectorAll('button').forEach(b=>b.addEventListener('click',()=>mode(b.dataset.mode)));window.addEventListener('keydown',e=>{if(e.key.toLowerCase()==='h')document.querySelector('nav').hidden=!document.querySelector('nav').hidden;});if(params.has('capture'))document.body.dataset.capture='true';mode(params.get('mode')||'ink');resize();window.addEventListener('resize',resize);
function loop(){requestAnimationFrame(loop);controls.update();composer.render();}loop();
