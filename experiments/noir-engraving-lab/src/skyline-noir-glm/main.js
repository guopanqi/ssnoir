import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';

import { PROFILE } from './profile.js';
import { createRng } from './rng.js';
import { createSky } from './scene/sky.js';
import { getGlowTexture } from './scene/glowTexture.js';
import { createCity } from './scene/city.js';
import { createLandmarks } from './scene/landmarks.js';
import { createWindows } from './scene/windows.js';
import { addInkShell } from './scene/inkOutline.js';
import { createLighting } from './systems/lighting.js';
import { createAtmosphere } from './systems/atmosphere.js';
import { NoirBloomPass } from './post/bloom.js';
import { createGradePass } from './post/grade.js';
import { SHOTS, applyShot } from './shots.js';
import { installUI } from './ui.js';
import { installCaptureApi } from './captureApi.js';

// ── 装配 ────────────────────────────────────────────────
const captureMode = new URLSearchParams(location.search).has('capture');
if (captureMode) document.body.dataset.capture = '1';
const profile = PROFILE;

const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.toneMapping = THREE.NoToneMapping; // 海报语言：亮部硬切，不糊
document.getElementById('scene').appendChild(renderer.domElement);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(40, window.innerWidth / window.innerHeight, 1, 3000);

const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.08;
controls.maxPolarAngle = Math.PI * 0.52;
controls.minDistance = 20;
controls.maxDistance = 900;

const rng = createRng(profile.seed);

const sky = createSky(profile);
scene.add(sky.mesh);

const city = createCity(profile, rng);
scene.add(city.group);

const landmarks = createLandmarks(profile);
scene.add(landmarks.group);

// 窗光：普通楼与主角塔共用一套语法，一次 draw call
const windows = createWindows([...city.windowSpecs, ...landmarks.heroWindowSpecs], profile, rng);
scene.add(windows.mesh);

// 墨线壳：只给地标
const inkShells = [];
for (const { mesh, thickness } of landmarks.inkTargets) {
  const shell = addInkShell(mesh, profile, thickness);
  inkShells.push(shell);
  landmarks.group.add(shell);
}
// 霓虹光斑使用与街灯同一张光斑贴图
landmarks.anchors.marqueeGlow.material.map = getGlowTexture();

const lighting = createLighting(scene, profile, city.lampPoints);
const atmosphere = createAtmosphere(scene, profile, landmarks);

// ── 后期：Bloom（自写，标准 read/write 契约）→ 成片（含 sRGB 输出）─────
const composer = new EffectComposer(renderer);
composer.addPass(new RenderPass(scene, camera));
const bloom = new NoirBloomPass(
  new THREE.Vector2(window.innerWidth, window.innerHeight),
  profile.post.bloom,
);
composer.addPass(bloom);
const grade = createGradePass(profile);
composer.addPass(grade);

// ── 状态开关 ────────────────────────────────────────────
const state = {
  ink: true,
  beacon: true,
  lamps: true,
  smoke: true,
  bloom: true,
  fog: true,
  grade: true,
};

function applyState() {
  for (const s of inkShells) s.visible = state.ink;
  atmosphere.setBeaconVisible(state.beacon);
  lighting.setLampsVisible(state.lamps);
  atmosphere.setSmokeVisible(state.smoke);
  bloom.strength = state.bloom ? profile.post.bloom.strength : 0;
  atmosphere.setFogEnabled(state.fog);
  grade.enabled = state.grade;
  grade.uniforms.uGrain.value = state.grade ? profile.post.grain : 0;
  grade.uniforms.uVignette.value = state.grade ? profile.post.vignette : 0;
}

function setMode(name) {
  if (name === 'final') {
    Object.assign(state, { ink: true, beacon: true, lamps: true, smoke: true, bloom: true, fog: true, grade: true });
  } else if (name === 'shape') {
    Object.assign(state, { ink: false, beacon: false, lamps: false, smoke: false, bloom: false, fog: false, grade: false });
  } else {
    throw new Error(`Unknown mode: ${name}`);
  }
  applyState();
}

// ── 相机与循环 ──────────────────────────────────────────
applyShot(camera, controls, 0);
applyState();

let time = 0;
let last = performance.now();
let paused = false;
let fpsAccum = 0;
let fpsCount = 0;
const ui = captureMode ? null : installUI({
  applyShot: (i) => {
    const shot = applyShot(camera, controls, i);
    render();
    return shot;
  },
  toggles: {
    ink: (v) => { state.ink = v; applyState(); },
    beacon: (v) => { state.beacon = v; applyState(); },
    lamps: (v) => { state.lamps = v; applyState(); },
    smoke: (v) => { state.smoke = v; applyState(); },
    bloom: (v) => { state.bloom = v; applyState(); },
    fog: (v) => { state.fog = v; applyState(); },
    grade: (v) => { state.grade = v; applyState(); },
  },
});

window.addEventListener('skyline-toggle-pause', (e) => { paused = e.detail; });

function setTime(t) {
  time = t;
  atmosphere.update(time, camera);
  windows.applyFlicker(time);
}

function render() {
  composer.render();
}

function loop() {
  requestAnimationFrame(loop);
  const now = performance.now();
  const dt = Math.min((now - last) / 1000, 0.1);
  last = now;
  if (!paused) {
    time += dt;
    atmosphere.update(time, camera);
    windows.applyFlicker(time);
  }
  controls.update();
  render();
  if (ui) {
    fpsAccum += dt;
    fpsCount += 1;
    if (fpsAccum > 0.5) {
      ui.setFps(fpsCount / fpsAccum);
      fpsAccum = 0;
      fpsCount = 0;
    }
  }
}

function onResize() {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
  composer.setSize(window.innerWidth, window.innerHeight);
  grade.uniforms.uResolution.value.set(window.innerWidth, window.innerHeight);
}
window.addEventListener('resize', onResize);

installCaptureApi({
  applyShot: (i) => applyShot(camera, controls, i),
  camera,
  controls,
  render,
  setMode,
  setTime,
});

if (captureMode) {
  // 截图模式：不跑自动循环，渲染全部由 capture API 显式驱动
  setTime(8.0);
  window.__skylineDebug = { scene, renderer, composer, camera, city, windows, landmarks, lighting, atmosphere, bloom, grade };
} else {
  loop();
}
