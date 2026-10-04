/**
 * 黑水 · 主程序
 *
 * 每帧的顺序：
 *   1 镜像相机渲染 → rt.refl（湿地面/河面在里面看到城市）
 *   2 主场景 → rt.scene（HDR + 角色写进 alpha + 深度纹理）
 *   3 体积雾 → 边缘线 → 亮部/拉丝 → 成片
 */
import * as THREE from 'three';
import { CITY, INK, DISTRICT_TINT, MOON, SODIUM, ACCENTS, CITY_GLOW, GLOW_LIGHTS, SPOTS, AIR, LENS } from './config.js';
import { loadCity, ROLE } from './scene.js';
import { createShared, uploadLights, makeSurfaceMaterial, makeWindowMaterial, makeRain } from './noir.js';
import { createPost } from './post.js';
import { SHOTS } from './shots.js';
import { createControls } from './controls.js';

const MODES = [
  { id: 'shape', label: '体块', hint: '只有黑体块与光，无描线无成片' },
  { id: 'haze', label: '空气', hint: '加体积雾与天空' },
  { id: 'line', label: '描线', hint: '加逆光线与折面线' },
  { id: 'final', label: '成片', hint: '全部：光晕/拉丝/受限色阶/颗粒/暗角' },
];

const canvas = document.getElementById('view');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, alpha: false, powerPreference: 'high-performance' });
renderer.setPixelRatio(1);
renderer.outputColorSpace = THREE.LinearSRGBColorSpace;
renderer.autoClear = true;
renderer.setClearColor(0x000000, 1);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(30, 16 / 9, CITY.near, CITY.far);
const reflCam = new THREE.PerspectiveCamera(30, 16 / 9, CITY.near, CITY.far);

// 反射用的裁剪面：只保留镜面以上的世界（否则河床会挡住反射）
const clipAboveMirror = new THREE.Plane(new THREE.Vector3(0, 1, 0), -(CITY.mirrorY - 0.08));

const shared = createShared();
shared.uCityGlow.value.set(...CITY_GLOW.color);
shared.uCityGlowPower.value = CITY_GLOW.power;
const pointLights = uploadLights(shared);

let city = null;
let post = null;
let controls = null;
let rain = null;
let groups = {};
let state = { shot: 1, mode: 'final', time: 0, paused: false, benchmarking: false, free: false };

/* ---------------------------------------------------------------- 载入 */

async function boot() {
  city = await loadCity('./heishui/');
  scene.add(city.group);

  const tints = city.districts.map((d, i) => DISTRICT_TINT[d] || [0.02, 0.025, 0.035]);
  // 数据里 districtOrder 是排序过的，这里按同样顺序取色
  const tintByIndex = city.districts.map((d) => DISTRICT_TINT[d]);

  /* --- 贴地：郊野 / 城市地面 / 街区 / 干道 --- */
  const groundGroup = new THREE.Group();
  groundGroup.name = '地面';
  const groundMat = makeSurfaceMaterial(shared, {
    role: 0, ground: true, hasRole: true, wet: 1.25, emissive: 1.0, shine: 0.30, specGain: 2.4, reflective: true,
  });
  const landMat = makeSurfaceMaterial(shared, {
    role: 0, ground: true, hasRole: true, wet: 0.28, shine: 0.20, specGain: 1.0, reflective: true,
  });
  for (const part of city.ground) {
    const mat = part.name === '郊野' ? landMat : groundMat;
    const m = new THREE.Mesh(part.geometry, mat);
    m.frustumCulled = false;
    groundGroup.add(m);
  }
  scene.add(groundGroup);

  /* --- 基底结构（桥/驳岸/广场/水塔）：主角级 --- */
  const structMat = makeSurfaceMaterial(shared, { role: 1, hasRole: true, wet: 0.9, shine: 0.45, specGain: 2.8 });
  const struct = new THREE.Mesh(city.structG, structMat);
  struct.frustumCulled = false;
  scene.add(struct);

  /* --- 地点外壳：主角级 --- */
  const shellMat = makeSurfaceMaterial(shared, { role: 1, hasRole: true, wet: 0.85, emissive: 1.15, shine: 0.44, specGain: 3.0 });
  const shell = new THREE.Mesh(city.shellG, shellMat);
  shell.frustumCulled = false;
  scene.add(shell);

  /* --- 填充建筑 / 屋顶 / 树 --- */
  const fillMat = makeSurfaceMaterial(shared, {
    role: 2, instanced: true, district: true, districtTints: tintByIndex, wet: 0.7, shine: 0.46, specGain: 2.5,
  });
  for (const inst of city.fillMeshes) {
    inst.material = fillMat;
    scene.add(inst);
  }

  /* --- 窗光 --- */
  const winMat = makeWindowMaterial(shared);
  winMat.uniforms.uLift.value = 2.3;
  const winMesh = new THREE.Mesh(city.winG, winMat);
  winMesh.frustumCulled = false;
  scene.add(winMesh);

  /* --- 河面 --- */
  const waterGroup = new THREE.Group();
  const waterMat = makeSurfaceMaterial(shared, { role: 4, water: true, hasRole: true, wet: 1.5, shine: 0.22, specGain: 7.0, reflective: true });
  const waterMesh = new THREE.Mesh(city.waterG, waterMat);
  waterMesh.frustumCulled = false;
  waterGroup.add(waterMesh);
  scene.add(waterGroup);

  /* --- 雨 --- */
  rain = makeRain(shared);
  scene.add(rain.mesh);

  groups = { groundGroup, waterGroup, struct, shell, winMesh };

  /* --- 后处理 --- */
  post = createPost(renderer, shared);
  post.uploadFogLights(pointLights, GLOW_LIGHTS.map((g) => ({ p: g.p, c: INK.warm[g.c] || INK.cold.steel, i: g.i, r: g.r })), spotList());

  // 反射目标
  refTargets.refl = new THREE.WebGLRenderTarget(1, 1, {
    type: THREE.HalfFloatType, format: THREE.RGBAFormat,
    minFilter: THREE.LinearFilter, magFilter: THREE.LinearFilter, depthBuffer: true,
  });

  controls = createControls(camera, renderer.domElement);

  buildUI();
  resize();
  window.addEventListener('resize', resize);
  document.getElementById('boot').classList.add('done');

  applyShot(SHOTS[1]);
  window.__heishui.ready = true;
}

const refTargets = { refl: null };

/* ---------------------------------------------------------------- 探照灯 */

const spotState = SPOTS.map((s) => ({ def: s, pos: new THREE.Vector3(...s.p), dir: new THREE.Vector3() }));

function spotList() {
  return spotState.map((s) => ({
    pos: s.pos, dir: s.dir,
    angle: s.def.angle, intensity: s.def.i,
    color: new THREE.Color(...(INK.cold[s.def.c] || INK.warm[s.def.c] || INK.cold.bone)),
  }));
}

function updateSpots(t) {
  for (const s of spotState) {
    const d = s.def;
    const aim = new THREE.Vector3(...d.aim);
    // 绕 y 轴慢扫，让光柱在长曝光里"活着"
    const a = Math.sin(t * d.sweep * Math.PI * 2) * 0.30;
    const dx = aim.x - d.p[0], dz = aim.z - d.p[2];
    const ca = Math.cos(a), sa = Math.sin(a);
    aim.set(d.p[0] + dx * ca - dz * sa, aim.y, d.p[2] + dx * sa + dz * ca);
    s.dir.copy(aim).sub(s.pos).normalize();
  }
}

/* ---------------------------------------------------------------- 帧 */

function applyShot(s) {
  camera.fov = s.fov;
  camera.updateProjectionMatrix();
  controls.setShot(s);
  shared.uWet.value = s.wet;
  shared.uRain.value = s.rain;
  rain.material.uniforms.uRainAmount.value = s.rain;
  state.shotOpts = s;
  state.free = false;
  markUI();
}

function resize() {
  const w = window.innerWidth, h = window.innerHeight;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  post.setSize(w, h, 1);
  const rw = Math.max(1, Math.round(w * 0.5)), rh = Math.max(1, Math.round(h * 0.5));
  refTargets.refl.setSize(rw, rh);
}

/** 镜像相机：把相机关于镜面翻过去，用它渲一张反射图。 */
const _p = new THREE.Vector3(), _f = new THREE.Vector3(), _u = new THREE.Vector3(), _t = new THREE.Vector3();
function updateReflectionCamera(cam) {
  const h = CITY.mirrorY;
  _p.copy(cam.position); _p.y = 2 * h - _p.y;
  _f.set(0, 0, -1).applyQuaternion(cam.quaternion); _f.y *= -1;
  _u.set(0, 1, 0).applyQuaternion(cam.quaternion); _u.y *= -1;
  _t.copy(_p).add(_f);
  reflCam.position.copy(_p);
  reflCam.up.copy(_u);
  reflCam.lookAt(_t);
  reflCam.fov = cam.fov; reflCam.aspect = cam.aspect;
  reflCam.near = cam.near; reflCam.far = cam.far;
  reflCam.updateProjectionMatrix();
  reflCam.updateMatrixWorld();
  const m = new THREE.Matrix4().set(0.5, 0, 0, 0.5, 0, 0.5, 0, 0.5, 0, 0, 0.5, 0.5, 0, 0, 0, 1);
  m.multiply(reflCam.projectionMatrix);
  m.multiply(reflCam.matrixWorldInverse);
  shared.uReflectMatrix.value.copy(m);
}

function modeOpts(mode, shot) {
  const base = {
    density: AIR.baseDensity * shot.density,
    exposure: LENS.exposure * shot.exposure,
    lineGain: shot.line * 0.55,
    creaseGain: 0.85,
    horizon: INK.cold.deep.map((v) => v * 1.5),
    zenith: INK.cold.void.map((v) => v * 0.8),
  };
  if (mode === 'shape') {
    return Object.assign(base, {
      density: 0.0, lineGain: 0, creaseGain: 0, bloomThreshold: 9,
      grade: { uBloomAmt: 0, uStreakAmt: 0, uHalation: 0, uLevels: 64, uDither: 0, uDesat: 0, uGrain: 0, uVignette: 0, uAberr: 0, uSplitH: 0, uCrush: 0, uGain: 1, uWeave: 0, uGate: 0 },
    });
  }
  if (mode === 'haze') {
    return Object.assign(base, {
      lineGain: 0, creaseGain: 0, bloomThreshold: 9,
      grade: { uBloomAmt: 0, uStreakAmt: 0, uHalation: 0, uLevels: 64, uDither: 0, uDesat: 0, uGrain: 0, uVignette: 0, uAberr: 0, uSplitH: 0, uCrush: 0, uGain: 1, uWeave: 0, uGate: 0 },
    });
  }
  if (mode === 'line') {
    return Object.assign(base, {
      bloomThreshold: 9,
      grade: { uBloomAmt: 0, uStreakAmt: 0, uHalation: 0, uLevels: 64, uDither: 0, uDesat: 0, uGrain: 0, uVignette: 0, uAberr: 0, uSplitH: 0, uCrush: 0, uGain: 1, uWeave: 0, uGate: 0 },
    });
  }
  return base;
}

function renderFrame() {
  const shot = SHOTS[state.shot];
  const t = state.time;

  updateSpots(t);
  post.uploadFogLights(pointLights,
    GLOW_LIGHTS.map((g) => ({ p: g.p, c: INK.warm[g.c] || INK.cold.steel, i: g.i, r: g.r })),
    spotList());

  // --- 1 反射 ---
  updateReflectionCamera(camera);
  groups.groundGroup.visible = false;
  groups.waterGroup.visible = false;
  rain.mesh.visible = false;
  shared.uReflectPass.value = 1;
  renderer.clippingPlanes = [clipAboveMirror];
  renderer.setRenderTarget(refTargets.refl);
  renderer.clear();
  renderer.render(scene, reflCam);
  renderer.clippingPlanes = [];
  shared.uReflectPass.value = 0;
  groups.groundGroup.visible = true;
  groups.waterGroup.visible = true;
  rain.mesh.visible = true;
  shared.tReflect.value = refTargets.refl.texture;

  rain.material.uniforms.uCam.value.copy(camera.position);
  rain.material.uniforms.uTime.value = t;

  // --- 2 主场景 ---
  renderer.setRenderTarget(post.rt.scene);
  renderer.clear();
  renderer.render(scene, camera);

  // --- 3 后处理 ---
  post.resolve(camera, modeOpts(state.mode, shot));
}

let hudPerf = null;
const perf = { last: performance.now(), emaMs: 0, frames: 0 };

let last = performance.now();
function loop(now) {
  const dt = Math.min((now - last) / 1000, 0.1);
  last = now;
  // 帧时读数：这是"这套东西在浏览器里真的跑得动吗"的唯一证据。
  const ms = now - perf.last;
  perf.last = now;
  perf.emaMs = perf.emaMs === 0 ? ms : perf.emaMs * 0.9 + ms * 0.1;
  perf.frames++;
  if (hudPerf && perf.frames % 15 === 0) {
    hudPerf.textContent = `${perf.emaMs.toFixed(1)} ms · ${(1000 / perf.emaMs).toFixed(0)} fps · ${post.size[0]}×${post.size[1]}`;
  }
  if (!state.paused) state.time += dt;
  shared.uTime.value = state.time;
  controls.update();

  // "已偏离固定机位" = 自由观察。相机一动就切，回到机位就切回来。
  if (!state.benchmarking) {
    const free = controls.deviation(SHOTS[state.shot]) > 1.5;
    if (free !== state.free) { state.free = free; markUI(); }
    renderFrame();
  }
  requestAnimationFrame(loop);
}

/* ---------------------------------------------------------------- UI */

function buildUI() {
  hudPerf = document.getElementById('perf');
  const shotsEl = document.getElementById('shots');
  const modesEl = document.getElementById('modes');
  SHOTS.forEach((s, i) => {
    const b = document.createElement('button');
    b.textContent = s.id + ' ' + s.name;
    b.title = s.note;
    b.onclick = () => { state.shot = i; applyShot(s); markUI(); };
    b.dataset.shot = String(i);
    shotsEl.appendChild(b);
  });
  MODES.forEach((m) => {
    const b = document.createElement('button');
    b.textContent = m.label;
    b.title = m.hint;
    b.onclick = () => { state.mode = m.id; markUI(); };
    b.dataset.mode = m.id;
    modesEl.appendChild(b);
  });
  document.getElementById('reset')?.addEventListener('click', () => applyShot(SHOTS[state.shot]));
  window.addEventListener('keydown', (e) => {
    const n = parseInt(e.key, 10);
    if (n >= 1 && n <= SHOTS.length) { state.shot = n - 1; applyShot(SHOTS[n - 1]); markUI(); }
    if (e.key === ' ') { state.paused = !state.paused; e.preventDefault(); }
    if (e.key === 'h' || e.key === 'H') document.querySelectorAll('.hud,.bar').forEach((el) => { el.style.display = el.style.display === 'none' ? '' : 'none'; });
    if (e.key === 'r' || e.key === 'R') applyShot(SHOTS[state.shot]);
  });
  markUI();
}

function markUI() {
  document.querySelectorAll('#shots button').forEach((b) => b.classList.toggle('on', Number(b.dataset.shot) === state.shot));
  document.querySelectorAll('#modes button').forEach((b) => b.classList.toggle('on', b.dataset.mode === state.mode));
  const free = document.getElementById('free');
  if (free) {
    free.textContent = state.free ? '自由观察 · 偏离固定机位 · R 复位' : '';
    free.dataset.on = state.free ? '1' : '';
  }
}

/* ---------------------------------------------------------------- 捕获 API */

window.__heishui = {
  ready: false,
  _scene: scene,
  _camera: camera,
  setShot(i) { state.shot = i; applyShot(SHOTS[i]); markUI(); },
  setMode(m) { state.mode = m; markUI(); },
  setTime(t) { state.time = t; shared.uTime.value = t; },
  /** 冻结/恢复时钟。截图必须先冻结，否则等待期间 loop 还在推进 time，画面不可复现。 */
  setPaused(v) { state.paused = !!v; },
  isPaused() { return state.paused; },
  render() { renderFrame(); },
  /** rAF 帧时读数（受显示器 vsync 限制，只能说明"跑得动"）。 */
  perf() { return { ms: +perf.emaMs.toFixed(2), fps: +(1000 / perf.emaMs).toFixed(1), frames: perf.frames, canvas: post.size }; },
  /**
   * 逐帧渲染成本：同步连渲 n 帧，每帧后用 1×1 readPixels 强制管线落地，逐帧记时。
   * ANGLE 下 gl.finish() 基本是空操作，只有 readPixels 会真的等 GPU —— 这一点踩过。
   * 返回逐帧毫秒数组（调用方取中位数/最小值，均值在无头环境里没有意义）。
   */
  bench(n = 30) {
    const gl = renderer.getContext();
    const px = new Uint8Array(4);
    state.benchmarking = true;
    const one = () => {
      state.time += 1 / 60; shared.uTime.value = state.time;
      const t = performance.now();
      renderFrame();
      gl.readPixels(0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
      return performance.now() - t;
    };
    for (let i = 0; i < 4; i++) one();
    const samples = [];
    for (let i = 0; i < n; i++) samples.push(+one().toFixed(2));
    state.benchmarking = false;
    perf.emaMs = 0; perf.last = performance.now();
    return samples;
  },
  info() {
    return {
      shots: SHOTS.map((s) => ({ id: s.id, name: s.name, note: s.note })),
      modes: MODES.map((m) => ({ id: m.id, hint: m.hint })),
      buildings: city.json.stats.buildings,
      roofs: city.json.stats.roofs,
      windows: city.json.stats.windows,
      places: city.json.placeNames.length,
      pointLights: pointLights.length,
      glowLights: GLOW_LIGHTS.length,
      spots: SPOTS.length,
      fogSteps: AIR.steps,
      lines: '逆光边 + 主角折面线（无全城白线框）',
      freeLook: !!controls,
      cameraFloorY: controls?.floor ?? null,
    };
  },
};

boot().then(() => requestAnimationFrame(loop)).catch((err) => {
  document.getElementById('boot').textContent = '失败：' + err.message;
  console.error(err);
});
