// 灯岸 LANTERN — by Spark · 装配（只装配，不承载视觉实现）
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { PROFILE } from './profile.js';
import { SHOTS, applyShot } from './shots.js';
import { loadCity } from './city.js';
import { makeSurfaceMaterial, makeWindowMaterial, makeSky, makeCone, makeLampPoints, makeAccentBeacons } from './materials.js';
import { createPost } from './post.js';

const MODES = [
  { id: 'shape', label: '体块', hint: '只有黑体块与月亮：无灯池、无窗、无银边、无成片' },
  { id: 'lamps', label: '灯河', hint: '加暖池、窗、光柱、银边：无 Bloom、无成片' },
  { id: 'final', label: '成片', hint: '全部：Bloom + 受限色阶 + 颗粒 + 暗角' },
];

const captureMode = new URLSearchParams(location.search).has('capture');
if (captureMode) document.body.dataset.capture = '1';

const canvas = document.getElementById('view');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, powerPreference: 'high-performance' });
renderer.setPixelRatio(1);
renderer.toneMapping = THREE.NoToneMapping;
renderer.outputColorSpace = THREE.LinearSRGBColorSpace;
renderer.setClearColor(0x000000, 1);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(30, 16 / 9, PROFILE.city.near, PROFILE.city.far);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.08;
controls.maxDistance = 3000;

// 全局开关（引用共享，材质里直接读）
const shared = {
  uTime: { value: 0 },
  uRimOn: { value: 1 },
  uLampOn: { value: 1 },
  uConeOn: { value: 1 },
};

const state = { shot: 0, mode: 'final', time: 8.0, paused: false, fog: true, bloom: true, grade: true };
let post = null;
let city = null;
let surfaceMats = [];
let cones = [];
let coneDefs = [];
let fogDensity = PROFILE.fog.density;

function applyModeToggles() {
  if (state.mode === 'shape') {
    shared.uRimOn.value = 0;
    shared.uLampOn.value = 0;
    shared.uConeOn.value = 0;
    post.setBloomGrade(false, false);
  } else if (state.mode === 'lamps') {
    shared.uRimOn.value = state.rim ?? 1;
    shared.uLampOn.value = 1;
    shared.uConeOn.value = state.cone ?? 1;
    post.setBloomGrade(false, false);
  } else {
    shared.uRimOn.value = state.rim ?? 1;
    shared.uLampOn.value = 1;
    shared.uConeOn.value = state.cone ?? 1;
    post.setBloomGrade(state.bloom, state.grade);
  }
  for (const m of surfaceMats) m.uniforms.uFogDensity.value = state.fog ? fogDensity : 0;
}

async function boot() {
  city = await loadCity(PROFILE.city.dataBase);

  // 填充建筑 / 屋顶 / 树：片区黑体块（完整镜面强度，碎面主体）
  const fillMat = makeSurfaceMaterial(PROFILE, shared, { district: true, spec: 1.0, pool: 0.15 });
  surfaceMats.push(fillMat);
  for (const inst of city.fillMeshes) {
    inst.material = fillMat;
    scene.add(inst);
  }

  // 基底结构：主角级
  if (city.structG) {
    const m = makeSurfaceMaterial(PROFILE, shared, { flat: PROFILE.flatTint.struct, spec: 0.9, pool: 0.2 });
    surfaceMats.push(m);
    const mesh = new THREE.Mesh(city.structG, m);
    mesh.frustumCulled = false;
    scene.add(mesh);
  }
  // 地点外壳：主角级最亮
  if (city.shellG) {
    const m = makeSurfaceMaterial(PROFILE, shared, { flat: PROFILE.flatTint.shell, spec: 0.9, pool: 0.2 });
    surfaceMats.push(m);
    const mesh = new THREE.Mesh(city.shellG, m);
    mesh.frustumCulled = false;
    scene.add(mesh);
  }
  // 地面：郊野最黑，城市地面带湿拉丝
  for (const part of city.ground) {
    const isLand = part.name === '郊野';
    const m = makeSurfaceMaterial(PROFILE, shared, {
      flat: isLand ? PROFILE.flatTint.land : PROFILE.flatTint.ground,
      wet: isLand ? 0 : PROFILE.wet.ground,
      spec: isLand ? 0 : 0.06, // 郊野零镜面；城市地面只留一线呼吸，月亮 dignity 不在地上
      pool: isLand ? 0 : 1.0, // 灯池只给城市地面
    });
    surfaceMats.push(m);
    const mesh = new THREE.Mesh(part.geometry, m);
    mesh.frustumCulled = false;
    scene.add(mesh);
  }
  // 河面：本体最黑，拉丝最强
  if (city.waterG) {
    const m = makeSurfaceMaterial(PROFILE, shared, { flat: PROFILE.flatTint.water, wet: PROFILE.wet.water, spec: 0.5, pool: 0.8 });
    surfaceMats.push(m);
    const mesh = new THREE.Mesh(city.waterG, m);
    mesh.frustumCulled = false;
    scene.add(mesh);
  }
  // 窗光
  if (city.winG) {
    const mesh = new THREE.Mesh(city.winG, makeWindowMaterial(PROFILE, shared));
    mesh.frustumCulled = false;
    scene.add(mesh);
  }

  scene.add(makeSky(PROFILE));
  scene.add(makeLampPoints(PROFILE, shared));
  scene.add(makeAccentBeacons(PROFILE, shared));

  // 探照灯锥
  coneDefs = PROFILE.spots.map((s) => ({ def: s, base: new THREE.Vector3(...s.p), aim: new THREE.Vector3(...s.aim) }));
  for (const c of coneDefs) {
    const cone = makeCone(PROFILE, shared, c.def);
    scene.add(cone);
    cones.push(cone);
  }

  post = createPost(renderer, scene, camera, PROFILE);
  resize();
  window.addEventListener('resize', resize);

  buildUI();
  applyShot(camera, controls, 0);
  applyModeToggles();
  document.getElementById('boot').classList.add('done');
  window.__lantern.ready = true;
}

function updateCones(t) {
  coneDefs.forEach((c, i) => {
    const mesh = cones[i];
    const d = c.def;
    const a = Math.sin(t * d.sweep * Math.PI * 2) * 0.30;
    const ax = c.aim.x - d.p[0], az = c.aim.z - d.p[2];
    const ca = Math.cos(a), sa = Math.sin(a);
    const tx = d.p[0] + ax * ca - az * sa;
    const tz = d.p[2] + ax * sa + az * ca;
    const target = new THREE.Vector3(tx, c.aim.y, tz);
    const dir = target.clone().sub(c.base).normalize();
    // 锥体 -Y 轴对准 dir
    mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0, -1, 0), dir);
  });
}

function resize() {
  const w = window.innerWidth, h = window.innerHeight;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  post?.setSize(w, h);
}

function renderFrame() {
  updateCones(state.time);
  post.composer.render();
}

/* ---------------- UI ---------------- */
function buildUI() {
  const shotsEl = document.getElementById('shots');
  const modesEl = document.getElementById('modes');
  SHOTS.forEach((s, i) => {
    const b = document.createElement('button');
    b.textContent = s.id + ' ' + s.name;
    b.title = s.note;
    b.onclick = () => { state.shot = i; applyShot(camera, controls, i); markUI(); };
    b.dataset.shot = String(i);
    shotsEl.appendChild(b);
  });
  MODES.forEach((m) => {
    const b = document.createElement('button');
    b.textContent = m.label;
    b.title = m.hint;
    b.onclick = () => { state.mode = m.id; applyModeToggles(); markUI(); };
    b.dataset.mode = m.id;
    modesEl.appendChild(b);
  });
  const bind = (id, key, fn) => {
    const el = document.getElementById(id);
    el.checked = state[key] ?? true;
    if (key === 'rim') state.rim = 1;
    if (key === 'cone') state.cone = 1;
    el.onchange = () => {
      if (key === 'rim') { state.rim = el.checked ? 1 : 0; }
      else if (key === 'cone') { state.cone = el.checked ? 1 : 0; }
      else state[key] = el.checked;
      applyModeToggles();
      if (key === 'fog') for (const m of surfaceMats) m.uniforms.uFogDensity.value = state.fog ? fogDensity : 0;
    };
  };
  state.rim = 1; state.cone = 1;
  bind('tRim', 'rim');
  bind('tCone', 'cone');
  bind('tFog', 'fog');
  bind('tBloom', 'bloom');
  bind('tGrade', 'grade');
  window.addEventListener('keydown', (e) => {
    const n = parseInt(e.key, 10);
    if (n >= 1 && n <= SHOTS.length) { state.shot = n - 1; applyShot(camera, controls, n - 1); markUI(); }
    if (e.key === ' ') { state.paused = !state.paused; e.preventDefault(); }
    if (e.key === 'h' || e.key === 'H') document.querySelectorAll('.hud,.bar').forEach((el) => { el.style.display = el.style.display === 'none' ? '' : 'none'; });
  });
  markUI();
}

function markUI() {
  document.querySelectorAll('#shots button').forEach((b) => b.classList.toggle('on', Number(b.dataset.shot) === state.shot));
  document.querySelectorAll('#modes button').forEach((b) => b.classList.toggle('on', b.dataset.mode === state.mode));
}

/* ---------------- 主循环与捕获 API ---------------- */
let last = performance.now();
function loop(now) {
  requestAnimationFrame(loop);
  const dt = Math.min((now - last) / 1000, 0.1);
  last = now;
  if (!state.paused) state.time += dt;
  shared.uTime.value = state.time;
  post.grade.uniforms.uTime.value = state.time;
  controls.update();
  renderFrame();
  const el = document.getElementById('perf');
  if (el && (loop.n = (loop.n ?? 0) + 1) % 20 === 0) {
    el.textContent = `${(dt * 1000).toFixed(1)} ms · ${post.composer._width ?? ''}×${post.composer._height ?? ''}`;
  }
}

window.__lantern = {
  ready: false,
  setShot(i) { state.shot = i; applyShot(camera, controls, i); markUI(); },
  setMode(m) { state.mode = m; applyModeToggles(); markUI(); },
  setTime(t) { state.time = t; shared.uTime.value = t; },
  render() { updateCones(state.time); post.composer.render(); },
  info() {
    return {
      shots: SHOTS.map((s) => ({ id: s.id, name: s.name, note: s.note })),
      modes: MODES.map((m) => ({ id: m.id, hint: m.hint })),
      buildings: city.stats.buildings,
      roofs: city.stats.roofs,
      windows: city.stats.windows,
      places: city.placeNames.length,
      lamps: PROFILE.lamps.length,
      accents: PROFILE.accents.length,
      spots: PROFILE.spots.length,
      lines: '前向 fresnel 银边（无屏幕空间描线 pass）',
    };
  },
};

boot().then(() => {
  if (captureMode) {
    // 截图模式：不跑自动循环，由 capture API 显式驱动
    window.__lantern.setTime(8.0);
  } else {
    requestAnimationFrame(loop);
  }
}).catch((err) => {
  document.getElementById('boot').textContent = '失败：' + err.message;
  console.error(err);
});
