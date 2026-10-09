// 逆光 BACKLIGHT · 装配
// 这里只做接线：载入 → 建材质 → 摆对象 → 阶段开关 → 机位 → 渲染循环。
// 视觉实现全部在 config / glsl / materials / lights / hero / post / stages 里。
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { CITY, SURFACE, HERO, FOG, SEED } from './config.js';
import { loadCity } from './city.js';
import { buildHero } from './hero.js';
import { buildLights } from './lights.js';
import { shared, toonSurface, windowMaterial, waterMaterial, skyMaterial } from './materials.js';
import { createPost } from './post.js';
import { applyStage, STAGE_ORDER, STAGE_LABEL } from './stages.js';
import { SHOTS } from './shots.js';

const canvas = document.getElementById('view');
const boot = document.getElementById('boot');
const perfEl = document.getElementById('perf');
const freeEl = document.getElementById('free');

const state = {
  stage: 'final',
  shot: 0,
  time: 0,
  paused: false,
  free: false,
  flags: null,
};

const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFShadowMap;
renderer.shadowMap.autoUpdate = false;      // 静态光：只在装配完成后烘一次
renderer.toneMapping = THREE.NoToneMapping;
renderer.setClearColor(0x000000, 1);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(30, 1, CITY.near, CITY.far);
const controls = new OrbitControls(camera, canvas);
controls.enableDamping = true;
controls.dampingFactor = 0.08;
// 自由环视只用于检查，但必须有明确约束：镜头不许贴进塔身，相机不许钻到地面以下。
// 地面用世界坐标约束表达——固定机位最低 y = 14，完全不受影响；约束走明确的交互状态，
// 不篡改任何输入事件。
controls.minDistance = 90;
const CAMERA_FLOOR = 3;

function updateCamera() {
  controls.update();
  if (camera.position.y < CAMERA_FLOOR) camera.position.y = CAMERA_FLOOR;
}

let rig = null;
let post = null;
let hero = null;
let lights = null;
let city = null;
const glowPointMats = [];

function setCameraFromShot(i) {
  const s = SHOTS[i];
  camera.fov = s.fov;
  camera.position.set(...s.pos);
  controls.target.set(...s.target);
  camera.updateProjectionMatrix();
  controls.update();
  post.setExposure(s.exposure);
  state.free = false;
  freeEl.textContent = '';
}

function setStage(name) {
  state.flags = applyStage(name, rig);
  state.stage = name;
  for (const b of document.querySelectorAll('[data-stage]')) b.classList.toggle('on', b.dataset.stage === name);
}

function renderFrame() {
  shared.uTime.value = state.time;
  lights.update(state.time);

  // 信标呼吸（阶段没开灯时保持熄灭）
  const beacon = hero.beaconMat;
  beacon.uniforms.uIntensity.value = state.flags.lights
    ? HERO.beacon.intensity * (0.62 + 0.38 * Math.sin(state.time * Math.PI * 2 * HERO.beacon.pulse))
    : 0;

  // 辉光点的像素尺寸 = 世界尺寸 × 投影 × 半屏高
  const h = renderer.getDrawingBufferSize(new THREE.Vector2()).y;
  const scale = camera.projectionMatrix.elements[5] * h * 0.5;
  for (const m of glowPointMats) m.uniforms.uSizeScale.value = scale;

  sky.position.copy(camera.position);
  post.composer.render();
}

let raf = 0;
let last = performance.now();
function loop(now) {
  raf = requestAnimationFrame(loop);
  const dt = Math.min(0.1, (now - last) / 1000);
  last = now;
  if (!state.paused) state.time += dt;
  updateCamera();
  renderFrame();
  const info = renderer.info.render;
  perfEl.textContent = `${(1 / Math.max(dt, 1e-4)).toFixed(0)} fps · ${info.calls} draws · ${(info.triangles / 1000).toFixed(0)}k tris`;
}

/* ------------------------------------------------------------------ 装配 */

const sky = new THREE.Mesh(new THREE.SphereGeometry(6000, 48, 24), skyMaterial());
sky.frustumCulled = false;
sky.name = '天穹';

function addMesh(geometry, material, { cast = false, receive = true, name } = {}) {
  const m = new THREE.Mesh(geometry, material);
  if (name) m.name = name;
  m.castShadow = cast;
  m.receiveShadow = receive;
  scene.add(m);
  return m;
}

async function assemble() {
  city = await loadCity(CITY.dataBase);

  // --- 表面材质：地面按烘焙的表面角色分档，外壳带暖白/冷白发光表面的自发光
  const landMat = toonSurface({ color: SURFACE.land, useRole: true, name: '郊野' });
  const urbanMat = toonSurface({ color: SURFACE.urban, useRole: true, name: '城市地面' });
  const padMat = toonSurface({ color: SURFACE.pad, useRole: true, name: '街区pad' });
  const roadMat = toonSurface({ color: SURFACE.road, useRole: true, emissive: SURFACE.roadEmissive, name: '干道' });
  const shellMat = toonSurface({ color: SURFACE.shell, useRole: true, name: '地点外壳' });
  // 填充楼/屋顶件/树的反照率走 instanceColor（片区色 × 每栋抖动），材质本体是白
  const fillMat = toonSurface({ color: [1, 1, 1], name: '填充建筑' });
  const winMat = windowMaterial();
  const waterMatInst = waterMaterial();

  const groundMatOf = { 郊野: landMat, 城市地面: urbanMat, 街区pad: padMat, 干道: roadMat };
  for (const g of city.ground) addMesh(g.geometry, groundMatOf[g.name], { name: g.name });
  if (city.structG) addMesh(city.structG, shellMat, { cast: true, name: '基底结构' });
  if (city.shellG) addMesh(city.shellG, shellMat, { cast: true, name: '地点外壳' });
  for (const inst of city.fillMeshes) {
    inst.material = fillMat;
    inst.castShadow = true;
    inst.receiveShadow = true;
    scene.add(inst);
  }
  if (city.waterG) addMesh(city.waterG, waterMatInst, { receive: false, name: '河面' });
  if (city.winG) addMesh(city.winG, winMat, { receive: false, name: '窗光' });

  // --- 主角塔
  hero = buildHero();
  hero.bodyMesh.material = toonSurface({ color: HERO.albedo, name: '塔身' });
  hero.stripMesh.material = toonSurface({
    color: [0.018, 0.017, 0.016],
    emissive: HERO.stripEmissive.map((v) => v * HERO.stripGain),
    name: '塔窗带',
  });
  hero.crownMesh.material = toonSurface({
    color: [0.020, 0.019, 0.018],
    emissive: HERO.crownGlow.map((v) => v * HERO.crownGain),
    name: '塔冠',
  });
  scene.add(hero.group);
  glowPointMats.push(hero.beaconMat);

  // --- 灯光与可见光
  lights = buildLights(city);
  scene.add(lights.group);
  glowPointMats.push(...lights.glow.filter((m) => m.uniforms.uSizeScale));

  scene.add(sky);

  // --- 后处理
  post = createPost(renderer, scene, camera);

  rig = {
    shared,
    fogBase: FOG.density,
    windows: winMat,
    glow: [...lights.glow, hero.beaconMat],
    spots: lights.uplights,
    emissive: [roadMat, shellMat, hero.stripMesh.material, hero.crownMesh.material],
    post,
  };

  // --- 静态阴影只烘一次
  renderer.shadowMap.needsUpdate = true;
}

/* -------------------------------------------------------------------- UI */

function buildUi() {
  const shotsEl = document.getElementById('shots');
  SHOTS.forEach((s, i) => {
    const b = document.createElement('button');
    b.textContent = `${i + 1} ${s.label}`;
    b.dataset.shot = String(i);
    b.onclick = () => selectShot(i);
    shotsEl.appendChild(b);
  });
  const modesEl = document.getElementById('modes');
  for (const stage of STAGE_ORDER) {
    const b = document.createElement('button');
    b.textContent = STAGE_LABEL[stage];
    b.dataset.stage = stage;
    b.onclick = () => setStage(stage);
    modesEl.appendChild(b);
  }
}

controls.addEventListener('start', () => {
  state.free = true;
  freeEl.textContent = '自由环视（按 1–5 回机位 · R 复位）';
});

function selectShot(i) {
  setCameraFromShot(i);
  state.shot = i;
  for (const b of document.querySelectorAll('[data-shot]')) b.classList.toggle('on', Number(b.dataset.shot) === i);
  document.getElementById('note').textContent = SHOTS[i].note;
}

function resize() {
  const w = window.innerWidth, h = window.innerHeight;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  if (post) post.setSize(w, h);
}

window.addEventListener('resize', resize);

window.addEventListener('keydown', (e) => {
  if (e.key >= '1' && e.key <= String(SHOTS.length)) selectShot(Number(e.key) - 1);
  else if (e.key === 'q' || e.key === 'Q') setStage(STAGE_ORDER[Math.max(0, STAGE_ORDER.indexOf(state.stage) - 1)]);
  else if (e.key === 'e' || e.key === 'E') setStage(STAGE_ORDER[Math.min(STAGE_ORDER.length - 1, STAGE_ORDER.indexOf(state.stage) + 1)]);
  else if (e.key === ' ') { state.paused = !state.paused; e.preventDefault(); }
  else if (e.key === 'h' || e.key === 'H') document.body.classList.toggle('hide-hud');
  else if (e.key === 'r' || e.key === 'R') selectShot(state.shot);
});

/* ------------------------------------------------------------ capture API */

window.__backlight = {
  ready: false,
  setStage,
  setShot: (i) => selectShot(i),
  setTime: (t) => { state.time = t; },
  render: () => { updateCamera(); renderFrame(); },
  info: () => ({
    stage: state.stage, shot: SHOTS[state.shot].id, time: state.time,
    stats: city?.stats, calls: renderer.info.render.calls, tris: renderer.info.render.triangles,
    seed: SEED,
  }),
};

/* ------------------------------------------------------------------ 启动 */

async function main() {
  if (new URLSearchParams(location.search).has('capture')) document.body.dataset.capture = '1';
  buildUi();
  resize();
  await assemble();
  selectShot(0);
  setStage('final');
  window.__backlight.ready = true;
  boot.classList.add('done');
  last = performance.now();
  raf = requestAnimationFrame(loop);
}

main().catch((err) => {
  console.error(err);
  boot.textContent = '启动失败：' + (err?.message ?? err);
});
