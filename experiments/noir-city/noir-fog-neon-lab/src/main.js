import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import GUI from 'three/addons/libs/lil-gui.module.min.js';
import {
  mergeCity, buildSky, buildRiverMirror, buildLamps, buildNeon, buildTraffic,
  buildRain, buildMoonLights, setMoon, mulberry32,
} from './world.js';
import { buildComposer } from './post.js';
import { buildCallouts } from './callouts.js';
import { mountHUD } from './hud.js';

/* 全部可调参数（GUI 同步） */
const P = {
  // 环境
  moonIntensity: 5.5, moonColor: '#b9c8e8', moonAz: 215, moonEl: 38, ambient: 1.0, exposure: 1.15,
  // 大气
  fogDens: 0.0005, fogFall: 0.02, haze: 0.00012, mist: 0.3,
  fogLow: '#101726', fogHigh: '#0b101c', skyHaze: 0.06,
  // 光源
  lampGlow: 1.0, lampPool: 1.0, sodiumPoints: 1.0, windowGain: 1.0, lineGain: 1.0,
  // 霓虹 / 动态
  neonCount: 16, neonFlicker: true,
  cars: 46, carSpeed: 1.0,
  rain: false, rainAmount: 0.7, showCallouts: true,
  // 画面
  bloom: 0.42, bloomRadius: 0.0, bloomThreshold: 0.7,
  contrast: 1.12, saturation: 0.82, vignette: 0.55, grain: 0.05, aberr: 0.0016,
  autoOrbit: false,
};

/* ============ 舞台：恒定 16:9（游戏世界视角的画幅） ============ */
const stage = document.getElementById('stage');
const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
stage.appendChild(renderer.domElement);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(22.87, 16 / 9, 2, 12000); // 50mm 水平
camera.position.set(-1040, 960, 110);                                // = Camera_世界
camera.lookAt(-226.1, 82.1, 27.7);

const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(-226.1, 82.1, 27.7);
controls.enableDamping = true;
controls.dampingFactor = 0.06;
controls.maxPolarAngle = 1.62;
controls.minDistance = 25;
controls.maxDistance = 2800;
controls.autoRotateSpeed = 0.5;

const viewportSize = { w: 1920, h: 1080 };
let hudScale = 1;
function resize() {
  const W = window.innerWidth, H = window.innerHeight;
  const w = Math.min(W, H * 16 / 9), h = w * 9 / 16;
  const dpr = Math.min(window.devicePixelRatio || 1, 1.75);
  renderer.setPixelRatio(dpr);
  renderer.setSize(w, h, false);
  renderer.domElement.style.width = `${w}px`;
  renderer.domElement.style.height = `${h}px`;
  post?.setSize(Math.round(w * dpr), Math.round(h * dpr));
  viewportSize.w = Math.round(w);
  viewportSize.h = Math.round(h);
  const r = renderer.domElement.getBoundingClientRect();
  const hud = document.getElementById('hud');
  const scaleEl = document.getElementById('hud-scale');
  if (scaleEl) scaleEl.style.transform = `scale(${w / 1720})`;
  hud.style.left = `${r.left}px`;
  hud.style.top = `${r.top}px`;
  hud.style.width = `${r.width}px`;
  hud.style.height = `${r.height}px`;
  // HUD 按设计基准画布（1720×968）等比缩放：任何窗口下比例恒定
  hudScale = w / 1720;
}

/* ============ 装配 ============ */
let post = null, moon = null, sky = null, atmo = null, rain = null, traffic = null;
let lineMats = [], windowMats = [], glowMats = [], poolMats = [], pointLights = [], signs = [];
let neonGroup = null;

const BASE = '/fog-neon/'; // 数据烘焙在 public/fog-neon/（import.meta.url 会被 HMR 加 ?t= 污染）
const [gltf, lamps, roads, anchors] = await Promise.all([
  new GLTFLoader().loadAsync(`${BASE}city.glb`),
  fetch(`${BASE}lamps.json`).then((r) => r.json()),
  fetch(`${BASE}roads.json`).then((r) => r.json()),
  fetch(`${BASE}anchors.json`).then((r) => r.json()).catch(() => []),
]);

const city = gltf.scene;
const { group: merged, candidates } = mergeCity(city);
scene.add(merged);
merged.traverse((o) => {
  if (!o.isMesh) return;
  const n = o.name;
  if (n.startsWith('M_描线') || n === 'M_White_Emission_Lines' || n === 'M_世界_水面短线') lineMats.push(o.material);
  if (n.startsWith('M_窗光')) windowMats.push(o.material);
});

// 兜底大地 + 天空 + 河面黑镜
const ground = new THREE.Mesh(
  new THREE.PlaneGeometry(9000, 9000),
  new THREE.MeshStandardMaterial({ color: '#04060a', roughness: 1 }),
);
ground.rotation.x = -Math.PI / 2;
ground.position.y = -0.45;
ground.receiveShadow = true;
scene.add(ground);

sky = buildSky(new THREE.Vector3(0, 1, 0));
scene.add(sky);
buildRiverMirror(merged);

const moonRig = buildMoonLights(P);
moon = moonRig.moon;
scene.add(moon, moon.target, moonRig.hemi);

const lampRig = buildLamps(lamps);
scene.add(lampRig.group);
glowMats = lampRig.glowMats; poolMats = lampRig.poolMats; pointLights = lampRig.pointLights;

rain = buildRain();
scene.add(rain.lines);

traffic = buildTraffic(roads, P.cars);
scene.add(traffic.group);

function rebuildNeon() {
  if (neonGroup) {
    neonGroup.traverse((o) => { o.geometry?.dispose?.(); o.material?.map?.dispose?.(); o.material?.dispose?.(); });
    scene.remove(neonGroup);
  }
  const rig = buildNeon(candidates, P.neonCount);
  neonGroup = rig.group;
  signs = rig.signs;
  scene.add(neonGroup);
}
rebuildNeon();

post = buildComposer(renderer, scene, camera);
resize();
window.addEventListener('resize', resize);
setMoon(moon, sky, post.atmo, (P.moonAz * Math.PI) / 180, (P.moonEl * Math.PI) / 180);
moon.color.set(P.moonColor);

/* ============ 地标标注（callout）：角括号 + 制图引线 + 案卷标签 ============ */
const callouts = buildCallouts(anchors);
callouts.bindControls(controls);
scene.add(callouts.group);

/* ============ HUD（案卷语言）：两层镜头 ============ */
const STYLE_NAMES = { case: '案卷', disco: '铅华', press: '号外', film: '胶片' };
let styleIdx = 0;
const applyStyle = (t) => {
  hud.setTheme(t);
  callouts.setTheme(t);
  hud.toast(`界面 · ${STYLE_NAMES[t]}`);
};
let hud = null;
const applyNextStyle = () => {
  const order = ['case', 'disco', 'press', 'film'];
  styleIdx = (styleIdx + 1) % order.length;
  applyStyle(order[styleIdx]);
};
hud = mountHUD(document.getElementById('hud'), {
  canvasRect: () => renderer.domElement.getBoundingClientRect(),
  mode: () => callouts.mode,
  rigs: () => callouts.rigs,
  previewDrop: (rig, on) => callouts.previewDrop(rig, on),
  hudScale: () => hudScale,
  onBack: () => callouts.exit(camera, controls),
  onToggleStyle: applyNextStyle,
});
callouts.onArrive = (kind) => hud.setView(kind === 'focus' ? 'focused' : 'world');
resize(); // mountHUD 重建了 HUD 内部，重新应用画布矩形与缩放

/* ============ 指针交互 ============ */
const ndc = new THREE.Vector2();
function eventNdc(e) {
  const r = renderer.domElement.getBoundingClientRect();
  ndc.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
}
let downAt = null;
renderer.domElement.addEventListener('pointermove', (e) => {
  eventNdc(e);
  const { cursor } = callouts.onPointer(camera, ndc, 'move');
  renderer.domElement.style.cursor = cursor;
});
renderer.domElement.addEventListener('pointerdown', (e) => { downAt = [e.clientX, e.clientY, performance.now()]; });
renderer.domElement.addEventListener('pointerup', (e) => {
  if (!downAt) return;
  const [x, y, t] = downAt; downAt = null;
  if (Math.hypot(e.clientX - x, e.clientY - y) > 6 || performance.now() - t > 600) return;
  eventNdc(e);
  const res = callouts.onPointer(camera, ndc, 'click');
  if (res.focus) callouts.focus(res.focus, camera, controls);
});

/* ============ 机位预设（研究用，H 呼出 GUI） ============ */
function flyTo(pos, target) {
  camera.position.set(...pos);
  controls.target.set(...target);
  controls.update();
}
const SHOTS = {
  世界机位: () => flyTo([-1040, 960, 110], [-226, 82, 28]),
  北岸: () => flyTo([-180, 22, -560], [-190, 26, -80]),
  长街: () => flyTo([-596.2, 2.0, -390.4], [-567.1, 3.0, -363.3]),
  屋顶: () => flyTo([110, 170, 380], [-190, 24, 10]),
};

/* ============ GUI ============ */
const gui = new GUI({ title: '雾与霓虹' });
const sync = () => {
  moon.intensity = P.moonIntensity;
  moon.color.set(P.moonColor);
  moonRig.hemi.intensity = P.ambient;
  renderer.toneMappingExposure = P.exposure;
  post.atmo.uniforms.uDens.value = P.fogDens;
  post.atmo.uniforms.uFall.value = P.fogFall;
  post.atmo.uniforms.uHaze.value = P.haze;
  post.atmo.uniforms.uMist.value = P.mist;
  post.atmo.uniforms.uFogLow.value.set(P.fogLow);
  post.atmo.uniforms.uFogHigh.value.set(P.fogHigh);
  post.atmo.uniforms.uSkyHaze.value = P.skyHaze;
  post.bloom.strength = P.bloom;
  post.bloom.radius = P.bloomRadius;
  post.bloom.threshold = P.bloomThreshold;
  post.grade.uniforms.uContrast.value = P.contrast;
  post.grade.uniforms.uSaturation.value = P.saturation;
  post.grade.uniforms.uVignette.value = P.vignette;
  post.grade.uniforms.uGrain.value = P.grain;
  post.grade.uniforms.uAberr.value = P.aberr;
  for (const m of lineMats) {
    if (!m.userData.base) m.userData.base = m.color.clone();
    m.color.copy(m.userData.base).multiplyScalar(P.lineGain);
  }
  for (const m of windowMats) {
    if (!m.userData.base) m.userData.base = m.color.clone();
    m.color.copy(m.userData.base).multiplyScalar(P.windowGain);
  }
  for (const m of glowMats) {
    if (!m.userData.base) m.userData.base = m.opacity;
    m.opacity = m.userData.base * P.lampGlow;
  }
  for (const m of poolMats) {
    if (!m.userData.base) m.userData.base = m.opacity;
    m.opacity = m.userData.base * P.lampPool;
  }
  for (const L of pointLights) L.intensity = L.userData.base * P.sodiumPoints;
  rain.mat.uniforms.uIntensity.value = P.rainAmount;
  rain.lines.visible = P.rain;
};
gui.add(P, 'moonIntensity', 0, 6, 0.05).name('月光强度').onChange(sync);
gui.addColor(P, 'moonColor').name('月色').onChange(sync);
gui.add(P, 'moonAz', 0, 360, 1).name('月方位').onChange(() => setMoon(moon, sky, post.atmo, (P.moonAz * Math.PI) / 180, (P.moonEl * Math.PI) / 180));
gui.add(P, 'moonEl', 5, 80, 1).name('月仰角').onChange(() => setMoon(moon, sky, post.atmo, (P.moonAz * Math.PI) / 180, (P.moonEl * Math.PI) / 180));
gui.add(P, 'ambient', 0, 1.5, 0.01).name('天光').onChange(sync);
gui.add(P, 'exposure', 0.3, 2.5, 0.01).name('曝光').onChange(sync);

const fFog = gui.addFolder('大气');
fFog.add(P, 'fogDens', 0, 0.004, 0.00005).name('地面雾密度').onChange(sync);
fFog.add(P, 'fogFall', 0.004, 0.06, 0.001).name('雾高度衰减').onChange(sync);
fFog.add(P, 'haze', 0, 0.0012, 0.00001).name('远景霾').onChange(sync);
fFog.add(P, 'mist', 0, 1.5, 0.01).name('流动雾').onChange(sync);
fFog.add(P, 'skyHaze', 0, 0.5, 0.01).name('天际霾').onChange(sync);
fFog.addColor(P, 'fogLow').name('雾色·低').onChange(sync);
fFog.addColor(P, 'fogHigh').name('雾色·高').onChange(sync);

const fLight = gui.addFolder('光源');
fLight.add(P, 'lampGlow', 0, 2.5, 0.01).name('路灯辉光').onChange(sync);
fLight.add(P, 'lampPool', 0, 2.5, 0.01).name('地面光池').onChange(sync);
fLight.add(P, 'sodiumPoints', 0, 2.5, 0.01).name('钠灯点光').onChange(sync);
fLight.add(P, 'windowGain', 0, 2.5, 0.01).name('窗光增益').onChange(sync);
fLight.add(P, 'lineGain', 0, 2.5, 0.01).name('描线亮度').onChange(sync);

const fNeon = gui.addFolder('霓虹与动态');
fNeon.add(P, 'neonCount', 0, 40, 1).name('灯牌数量').onFinishChange(rebuildNeon);
fNeon.add(P, 'neonFlicker').name('灯牌闪烁');
fNeon.add(P, 'cars', 0, 120, 1).name('车流数量').onFinishChange(() => {
  scene.remove(traffic.group);
  traffic = buildTraffic(roads, P.cars);
  scene.add(traffic.group);
});
fNeon.add(P, 'carSpeed', 0, 3, 0.05).name('车速倍率');
fNeon.add(P, 'rain').name('雨').onChange(sync);
fNeon.add(P, 'rainAmount', 0, 2, 0.01).name('雨量').onChange(sync);

const fImg = gui.addFolder('画面');
fImg.add(P, 'bloom', 0, 1.5, 0.01).name('辉光').onChange(sync);
fImg.add(P, 'bloomRadius', 0, 1, 0.01).name('辉光半径').onChange(sync);
fImg.add(P, 'bloomThreshold', 0.3, 1.2, 0.01).name('辉光阈值').onChange(sync);
fImg.add(P, 'contrast', 0.8, 1.6, 0.01).name('对比').onChange(sync);
fImg.add(P, 'saturation', 0.3, 1.3, 0.01).name('饱和').onChange(sync);
fImg.add(P, 'vignette', 0, 1, 0.01).name('暗角').onChange(sync);
fImg.add(P, 'grain', 0, 0.15, 0.002).name('颗粒').onChange(sync);
fImg.add(P, 'aberr', 0, 0.006, 0.0002).name('色差').onChange(sync);

const fUi = gui.addFolder('标注');
fUi.add(P, 'showCallouts').name('显示标注').onChange((v) => { callouts.group.visible = v; });

const fCam = gui.addFolder('机位');
for (const [name, fn] of Object.entries(SHOTS)) fCam.add({ fn }, 'fn').name(name);
fCam.add(P, 'autoOrbit').name('自动环游');

sync();
gui.hide();
window.addEventListener('keydown', (e) => { if (e.key === 'h' || e.key === 'H') (gui._hidden ? gui.show() : gui.hide()); });

/* ============ 循环 ============ */
const clock = new THREE.Clock();
const rnd = mulberry32(9);
renderer.setAnimationLoop(() => {
  const dt = Math.min(clock.getDelta(), 0.05);
  const t = clock.elapsedTime;
  controls.autoRotate = P.autoOrbit;
  controls.update();
  traffic.tick(dt, P.carSpeed);
  rain.mat.uniforms.uTime.value = t;
  rain.mat.uniforms.uCam.value.copy(camera.position);
  for (const s of signs) {
    const stutter = P.neonFlicker && rnd() < 0.012 ? 0.25 : 1;
    const breathe = 0.9 + 0.1 * Math.sin(t * 6 + s.phase);
    s.mat.color.copy(s.base).multiplyScalar(breathe * stutter);
  }
  callouts.tick(camera, dt, viewportSize);
  renderer.toneMappingExposure = P.exposure * (1 - 0.16 * callouts.focusFx);
  post.beforeRender(camera);
  post.renderScene(camera);
  post.atmo.uniforms.uTime.value = t;
  post.grade.uniforms.uTime.value = t;
  post.composer.render();
});

document.getElementById('veil').classList.add('gone');

// 调试句柄（实验用）
window.__dbg = {
  scene, camera, post, P, controls, moon, rain, gui, callouts,
  capture() {
    // 完整同步走一遍管线，不依赖动画循环的节流
    callouts.finishPending(camera);
    camera.lookAt(controls.target); // 相机被外部直设位置后朝向可能未同步
    moon.intensity = P.moonIntensity;
    callouts.tick(camera, 0.016, viewportSize);
    post.renderScene(camera);
    post.beforeRender(camera);
    post.atmo.uniforms.uTime.value = performance.now() / 1000;
    post.grade.uniforms.uTime.value = performance.now() / 1000;
    post.composer.render();
    return renderer.domElement.toDataURL('image/png');
  },
};
