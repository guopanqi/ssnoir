import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import './style.css';
import { buildWorld } from './world.js';
import { createPost } from './post.js';

const canvas = document.querySelector('#scene');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(devicePixelRatio, 1.75));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.AgXToneMapping;
renderer.toneMappingExposure = 1.08;

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(42, innerWidth / innerHeight, 0.1, 220);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.06;
controls.maxPolarAngle = Math.PI * 0.48;
controls.minDistance = 12;
controls.maxDistance = 90;

const world = buildWorld(scene);
const post = createPost(renderer, scene, camera);

const shots = [
  { name: '01 · THEATER STREET', pos: [-31, 9.5, 31], target: [-2, 5.0, 2] },
  { name: '02 · WAREHOUSE FOG', pos: [54, 8.2, 18], target: [25, 4.6, -6] },
  { name: '03 · ALLEY MOUTH', pos: [-45, 5.4, -3], target: [-24, 3.3, -19] },
  { name: '04 · HIGH CITY', pos: [7, 34, 50], target: [0, 4.0, 0] },
];
let shotIndex = 0;
function applyShot(index) {
  shotIndex = index;
  const s = shots[index];
  camera.position.fromArray(s.pos);
  controls.target.fromArray(s.target);
  controls.update();
  document.querySelector('#shotLabel').textContent = s.name;
}
applyShot(0);

const ui = {
  panel: document.querySelector('#panel'),
  stylize: document.querySelector('#stylize'),
  outlines: document.querySelector('#outlines'),
  atmosphere: document.querySelector('#atmosphere'),
  lightCones: document.querySelector('#lightCones'),
  levels: document.querySelector('#levels'),
  levelsValue: document.querySelector('#levelsValue'),
  dither: document.querySelector('#dither'),
  ditherValue: document.querySelector('#ditherValue'),
  fog: document.querySelector('#fog'),
  fogValue: document.querySelector('#fogValue'),
  bloom: document.querySelector('#bloom'),
  bloomValue: document.querySelector('#bloomValue'),
  fps: document.querySelector('#fps'),
};

ui.stylize.addEventListener('change', () => { post.noir.uniforms.uEnabled.value = ui.stylize.checked ? 1 : 0; });
ui.outlines.addEventListener('change', () => { world.groups.outlines.visible = ui.outlines.checked; });
ui.atmosphere.addEventListener('change', () => world.setRain(ui.atmosphere.checked));
ui.lightCones.addEventListener('change', () => { world.groups.lightCones.visible = ui.lightCones.checked; });
ui.levels.addEventListener('input', () => { post.noir.uniforms.uLevels.value = +ui.levels.value; ui.levelsValue.value = ui.levels.value; });
ui.dither.addEventListener('input', () => { post.noir.uniforms.uDither.value = +ui.dither.value; ui.ditherValue.value = (+ui.dither.value).toFixed(2); });
ui.fog.addEventListener('input', () => { scene.fog.density = +ui.fog.value; ui.fogValue.value = (+ui.fog.value).toFixed(3); });
ui.bloom.addEventListener('input', () => { post.bloom.strength = +ui.bloom.value; ui.bloomValue.value = (+ui.bloom.value).toFixed(2); });

window.addEventListener('keydown', (e) => {
  if (e.key >= '1' && e.key <= '4') applyShot(+e.key - 1);
  if (e.code === 'Space') {
    e.preventDefault();
    world.setRain(!world.raining);
    ui.atmosphere.checked = world.raining;
  }
  if (e.key.toLowerCase() === 'h') ui.panel.classList.toggle('is-hidden');
});

function resize() {
  const w = innerWidth;
  const h = innerHeight;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  post.resize(w, h);
}
window.addEventListener('resize', resize);
resize();

const clock = new THREE.Clock();
let fpsFrames = 0;
let fpsTime = 0;

function animate() {
  requestAnimationFrame(animate);
  const dt = Math.min(clock.getDelta(), 0.05);
  world.update(dt);
  controls.update();
  post.noir.uniforms.uTime.value += dt;
  post.composer.render();

  fpsFrames++;
  fpsTime += dt;
  if (fpsTime >= 0.6) {
    ui.fps.textContent = `${Math.round(fpsFrames / fpsTime)} fps`;
    fpsFrames = 0;
    fpsTime = 0;
  }
}
animate();
