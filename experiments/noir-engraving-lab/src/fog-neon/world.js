import * as THREE from 'three';
import { Reflector } from 'three/addons/objects/Reflector.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

/* ============ 种子随机（每次刷新同一座城） ============ */
export function mulberry32(seed) {
  let a = seed >>> 0;
  return () => {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/* ============ 材质分派 ============
   原材质名 → 黑色电影材质。面全部压到近黑，光留给灯、窗和线。 */
const C = (hex, mul = 1) => new THREE.Color(hex).multiplyScalar(mul);

const FACADES = {
  M_建筑: '#1c2230', M_建筑_civic: '#222a3c', M_建筑_downtown: '#20273a',
  M_建筑_enclave: '#1e2536', M_建筑_newgrid: '#1d2332', M_建筑_newport: '#1c2230',
  M_建筑_oldport: '#1e2431', M_建筑_oldtown: '#202534', M_建筑_outskirt: '#181c27',
  M_建筑_village: '#1a1f2c', M_重要建筑: '#242c40', M_街区: '#181d2a',
};

const LINES = {
  M_描线_壳: C('#c9d0dc', 0.62),
  'M_描线_壳_FFFFFF': C('#e8e4d8', 0.85),
  'M_描线_壳_69B7FF': C('#7ba6d4', 0.55),
  'M_描线_壳_5A78FF': C('#5f74b0', 0.5),
  M_描线_填充: C('#3c4a68', 1),
  M_描线_设施: C('#465473', 1),
  M_描线_BDB9A7: C('#938d7c', 0.8),
  M_描线_CDA54D: C('#b08c40', 0.9),
  M_描线_D6A749: C('#c49e4c', 1.0),
  M_White_Emission_Lines: C('#fff3dc', 1.7),
};

const WINDOWS = {
  M_窗光_white: C('#ffd9a4', 1.25),
  M_窗光_grey: C('#c3ccd9', 0.95),
  M_窗光_blue: C('#a6c4ec', 0.95),
};

export function noirMaterial(name) {
  if (FACADES[name] !== undefined)
    return new THREE.MeshStandardMaterial({ color: FACADES[name], roughness: 0.9, metalness: 0 });
  if (name === 'M_干道')
    return new THREE.MeshStandardMaterial({ color: '#060809', roughness: 0.4, metalness: 0.05 });
  if (name === 'M_城市地面')
    return new THREE.MeshStandardMaterial({ color: '#07090d', roughness: 0.85 });
  if (name === 'M_郊野') return new THREE.MeshStandardMaterial({ color: '#080b0f', roughness: 1 });
  if (name === 'M_驳岸') return new THREE.MeshStandardMaterial({ color: '#0d1017', roughness: 0.8 });
  if (name === 'M_树') return new THREE.MeshStandardMaterial({ color: '#060908', roughness: 1 });
  if (name === 'M_世界_主体') return new THREE.MeshStandardMaterial({ color: '#10141c', roughness: 0.85 });
  if (name === 'M_世界_象牙饰面') return new THREE.MeshStandardMaterial({ color: '#1a1d23', roughness: 0.7 });
  if (name === 'M_世界_抛光地面') return new THREE.MeshStandardMaterial({ color: '#0d1017', roughness: 0.35, metalness: 0.1 });
  if (name === 'M_世界_暗金饰面') return new THREE.MeshStandardMaterial({ color: '#3a2f18', roughness: 0.5, metalness: 0.3 });
  if (name === 'M_世界_亮金装饰' || name === 'M_世界_金色细饰')
    return new THREE.MeshStandardMaterial({ color: '#2c2410', emissive: C('#d6a749', 0.35), roughness: 0.4, metalness: 0.5 });
  if (name === 'M_世界_冷白发光表面') return new THREE.MeshBasicMaterial({ color: C('#dfe8f5', 1.5) });
  if (name === 'M_世界_暖白发光表面') return new THREE.MeshBasicMaterial({ color: C('#ffe3b8', 1.4) });
  if (name === 'M_世界_水面短线') return new THREE.MeshBasicMaterial({ color: C('#2c4a52', 0.9) });
  if (LINES[name]) return new THREE.MeshBasicMaterial({ color: LINES[name].clone() });
  if (WINDOWS[name]) return new THREE.MeshBasicMaterial({ color: WINDOWS[name].clone() });
  return new THREE.MeshStandardMaterial({ color: '#161b26', roughness: 0.9 }); // 兜底：当建筑
}

/* ============ 合并城市网格（按材质） ============
   2.7k 个对象逐个画太浪费；烘焙世界变换后按材质合并，顺带收集霓虹候选楼。 */
export function mergeCity(root) {
  root.updateMatrixWorld(true);
  const byMat = new Map();
  const candidates = []; // 霓虹候选楼 { box, name }
  const districts = ['M_建筑_downtown', 'M_建筑_oldtown', 'M_建筑_newgrid', 'M_建筑_newport'];

  root.traverse((o) => {
    if (!o.isMesh || !o.material) return;
    const name = Array.isArray(o.material) ? o.material[0]?.name : o.material?.name;
    if (!name) return;
    const box = new THREE.Box3().setFromObject(o);
    const size = box.getSize(new THREE.Vector3());
    if (districts.includes(name) && size.y > 15 && Math.max(size.x, size.z) < 60 && Math.max(size.x, size.z) > 9)
      candidates.push({ box, name });
    let list = byMat.get(name);
    if (!list) byMat.set(name, (list = []));
    let g = o.geometry.clone().applyMatrix4(o.matrixWorld);
    if (g.index) g = g.toNonIndexed();
    for (const key of Object.keys(g.attributes))
      if (key !== 'position' && key !== 'normal') g.deleteAttribute(key);
    list.push(g);
  });

  const group = new THREE.Group();
  for (const [name, geos] of byMat) {
    let merged;
    try {
      merged = mergeGeometries(geos, false);
    } catch (e) {
      console.warn('merge failed for', name, e);
      merged = geos[0];
    }
    geos.forEach((g) => g !== merged && g.dispose());
    merged.computeBoundingSphere();
    const mat = noirMaterial(name);
    mat.userData.matName = name;
    const mesh = new THREE.Mesh(merged, mat);
    mesh.castShadow = mesh.receiveShadow = !(mat instanceof THREE.MeshBasicMaterial);
    mesh.name = name;
    group.add(mesh);
  }
  return { group, candidates };
}

/* ============ 辉光贴图 ============ */
function glowTexture() {
  const c = document.createElement('canvas');
  c.width = c.height = 256;
  const ctx = c.getContext('2d');
  const g = ctx.createRadialGradient(128, 128, 0, 128, 128, 128);
  g.addColorStop(0, 'rgba(255,225,180,1)');
  g.addColorStop(0.22, 'rgba(255,200,130,0.55)');
  g.addColorStop(0.55, 'rgba(255,170,90,0.16)');
  g.addColorStop(1, 'rgba(255,160,80,0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 256, 256);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

/* ============ 天空穹顶：地平线城市光污染 + 月轮 ============ */
export function buildSky(moonDir) {
  const geo = new THREE.SphereGeometry(5200, 32, 18);
  const mat = new THREE.ShaderMaterial({
    side: THREE.BackSide,
    depthWrite: false,
    uniforms: {
      uMoonDir: { value: moonDir.clone() },
      uZenith: { value: new THREE.Color('#020409') },
      uHorizon: { value: new THREE.Color('#26344a') },
      uGlow: { value: new THREE.Color('#4a3a1e') }, // 城市光污染（暖）
      uGlowDir: { value: new THREE.Vector3(0.25, 0, -0.95).normalize() },
    },
    vertexShader: /* glsl */ `
      varying vec3 vDir;
      void main() {
        vDir = normalize((modelMatrix * vec4(position, 0.0)).xyz);
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: /* glsl */ `
      varying vec3 vDir;
      uniform vec3 uMoonDir, uZenith, uHorizon, uGlow, uGlowDir;
      void main() {
        float h = clamp(vDir.y, -0.05, 1.0);
        vec3 col = mix(uHorizon, uZenith, pow(clamp(h * 1.6, 0.0, 1.0), 0.55));
        float cityGlow = pow(max(dot(normalize(vec3(vDir.x, 0.0, vDir.z)), uGlowDir), 0.0), 3.0);
        col += uGlow * cityGlow * exp(-max(h, 0.0) * 7.0);
        float m = max(dot(vDir, uMoonDir), 0.0);
        col += vec3(0.75, 0.82, 1.0) * (smoothstep(0.99945, 0.99980, m) * 6.0   // 月轮
              + pow(m, 700.0) * 0.7 + pow(m, 60.0) * 0.06);                     // 月晕
        gl_FragColor = vec4(col, 1.0);
      }`,
  });
  const sky = new THREE.Mesh(geo, mat);
  sky.frustumCulled = false;
  return sky;
}

/* ============ 河面：黑镜（平面反射，借鉴 three.js Reflector） ============ */
export function buildRiverMirror(root) {
  let mirror = null;
  root.traverse((o) => {
    if (o.isMesh && o.name.startsWith('河面') && !mirror) {
      const r = new Reflector(o.geometry, {
        clipBias: 0.004,
        textureWidth: 1024,
        textureHeight: 1024,
        color: 0x2a3440,
      });
      r.position.setFromMatrixPosition(o.matrixWorld);
      r.quaternion.setFromRotationMatrix(o.matrixWorld);
      r.name = '河面镜';
      o.parent.add(r);
      o.visible = false;
      mirror = r;
    }
  });
  return mirror;
}

/* ============ 钠灯：辉光 sprite + 地面光池 + 少量真实点光 ============ */
export function buildLamps(lampData, moonExclude) {
  const tex = glowTexture();
  const rnd = mulberry32(1104);
  const g = new THREE.Group();
  g.name = '钠灯';

  const glowMat = new THREE.SpriteMaterial({
    map: tex, color: '#ffa54e', blending: THREE.AdditiveBlending,
    depthWrite: false, transparent: true, opacity: 0.34, fog: false,
  });
  const poolMat = new THREE.MeshBasicMaterial({
    map: tex, color: '#ff9a44', blending: THREE.AdditiveBlending,
    depthWrite: false, transparent: true, opacity: 0.11, fog: false,
  });
  const poolGeo = new THREE.PlaneGeometry(1, 1);

  const pts = lampData.points; // Blender [x, y, z] → three [x, z, -y]
  const downtown = [];
  pts.forEach(([x, yB, z], i) => {
    const p = new THREE.Vector3(x, (z || 0) + 6.8, -yB);
    const dead = rnd() < 0.08; // 八分之一的灯坏了——黑电影的街道不完整
    const s = new THREE.Sprite(glowMat.clone());
    s.position.copy(p);
    const k = dead ? 0.12 : 0.65 + rnd() * 0.55;
    s.material.opacity = 0.34 * k;
    s.scale.setScalar(4.5 + rnd() * 2.5);
    g.add(s);
    if (!dead) {
      const pool = new THREE.Mesh(poolGeo, poolMat.clone());
      pool.rotation.x = -Math.PI / 2;
      pool.position.set(p.x, 0.14, p.z);
      pool.material.opacity = 0.11 * (0.7 + rnd() * 0.5);
      pool.scale.setScalar(13 + rnd() * 5);
      pool.renderOrder = 2;
      g.add(pool);
    }
    // 市中心一带才给真实点光，照亮立面
    if (!dead && x > -560 && x < 260 && yB > -420 && yB < 320) downtown.push({ i, x, yB, p });
  });

  // 点光：挑散开的 14 盏
  downtown.sort(() => rnd() - 0.5);
  const lights = [];
  for (const d of downtown.slice(0, 14)) {
    const L = new THREE.PointLight('#ffa04a', 6000, 200, 2);
    L.position.set(d.p.x, 9.5, d.p.z);
    L.userData.base = 6000;
    g.add(L);
    lights.push(L);
  }
  return { group: g, glowMats: g.children.filter((c) => c.isSprite).map((c) => c.material), poolMats: g.children.filter((c) => c.isMesh).map((c) => c.material), pointLights: lights };
}

/* ============ 霓虹灯牌 ============ */
const NEON_COLORS = ['#ff2d55', '#25c9c9', '#ffb02e', '#8af2cf', '#ff5c8a', '#e8e25a'];
export function buildNeon(candidates, count) {
  const rnd = mulberry32(2049);
  const g = new THREE.Group();
  g.name = '霓虹';
  const picks = [...candidates].sort(() => rnd() - 0.5).slice(0, count);
  const signs = [];
  picks.forEach(({ box }) => {
    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    const color = NEON_COLORS[Math.floor(rnd() * NEON_COLORS.length)];
    const h = 6 + rnd() * (Math.min(size.y * 0.5, 9));
    const w = 2.2 + rnd() * 1.8;
    // 贴最窄的立面
    const alongX = size.x < size.z; // 牌面朝 z 向 → 尺寸沿 x
    const geo = new THREE.BoxGeometry(alongX ? w : 0.5, h, alongX ? 0.5 : w);
    const mat = new THREE.MeshBasicMaterial({ color: C(color, 2.3), fog: false });
    const sign = new THREE.Mesh(geo, mat);
    const side = rnd() < 0.5 ? 0.5 : -0.5;
    if (alongX) {
      sign.position.set(center.x, box.min.y + size.y * (0.45 + rnd() * 0.25), center.z + side * (size.z / 2 + 0.45));
    } else {
      sign.position.set(center.x + side * (size.x / 2 + 0.45), box.min.y + size.y * (0.45 + rnd() * 0.25), center.z);
    }
    g.add(sign);
    // 光晕
    const halo = new THREE.Sprite(new THREE.SpriteMaterial({
      map: glowTexture(), color, blending: THREE.AdditiveBlending,
      depthWrite: false, transparent: true, opacity: 0.32, fog: false,
    }));
    halo.position.copy(sign.position);
    halo.scale.set(alongX ? w * 4 : h * 1.6, h * 2.4, 1);
    g.add(halo);
    signs.push({ mat, base: C(color, 2.3), phase: rnd() * 100 });
  });
  return { group: g, signs };
}

/* ============ 车流：干道曲线上的头灯与尾灯 ============ */
export function buildTraffic(roads, count) {
  const rnd = mulberry32(7311);
  const g = new THREE.Group();
  g.name = '车流';
  const headMat = new THREE.SpriteMaterial({ map: glowTexture(), color: C('#fff1cc', 1.6), blending: THREE.AdditiveBlending, depthWrite: false, transparent: true, opacity: 0.9, fog: false });
  const tailMat = new THREE.SpriteMaterial({ map: glowTexture(), color: C('#ff3528', 1.5), blending: THREE.AdditiveBlending, depthWrite: false, transparent: true, opacity: 0.75, fog: false });
  const cars = [];
  const paths = roads.filter((p) => p.points.length > 3);
  if (!paths.length) return { group: g, cars };
  for (let i = 0; i < count; i++) {
    const head = new THREE.Sprite(headMat.clone());
    const tail = new THREE.Sprite(tailMat.clone());
    head.scale.setScalar(2.6); tail.scale.setScalar(1.9);
    g.add(head, tail);
    const path = paths[Math.floor(rnd() * paths.length)];
    cars.push({
      path, t: rnd(), dir: rnd() < 0.5 ? 1 : -1, speed: (13 + rnd() * 14) / 1000,
      head, tail,
    });
  }
  const tmp = new THREE.Vector3();
  const segLen = (p) => Math.hypot(p[1][0] - p[0][0], p[1][1] - p[0][1]) || 1;
  cars.forEach((c) => (c.total = c.path.points.slice(0, -1).reduce((s, _, i) => s + segLen([c.path.points[i], c.path.points[i + 1]]), 0)));
  return {
    group: g, cars,
    tick(dt, speedK) {
      for (const c of cars) {
        c.t += c.dir * c.speed * speedK * dt;
        if (c.t > 1) { c.t = 1; c.dir = -1; }
        if (c.t < 0) { c.t = 0; c.dir = 1; }
        const pts = c.path.points;
        let target = c.t * c.total, i = 0;
        while (i < pts.length - 2 && target > segLen([pts[i], pts[i + 1]])) { target -= segLen([pts[i], pts[i + 1]]); i++; }
        const a = pts[i], b = pts[i + 1], L = segLen([a, b]);
        const f = Math.min(target / L, 1);
        tmp.set(a[0] + (b[0] - a[0]) * f, 1.1, -(a[1] + (b[1] - a[1]) * f));
        c.head.position.copy(tmp);
        const back = c.dir * 4.2 / L;
        const f2 = Math.max(0, Math.min(1, f - back));
        c.tail.position.set(a[0] + (b[0] - a[0]) * f2, 1.0, -(a[1] + (b[1] - a[1]) * f2));
      }
    },
  };
}

/* ============ 雨：跟随相机的斜雨丝（GPU 顶点动画） ============ */
export function buildRain() {
  const N = 2600, H = 300, R = 260;
  const pos = new Float32Array(N * 2 * 3);
  const seed = new Float32Array(N * 2);
  const end = new Float32Array(N * 2);
  for (let i = 0; i < N; i++) {
    const s = Math.random();
    for (let v = 0; v < 2; v++) {
      const k = (i * 2 + v) * 3;
      pos[k] = 0; pos[k + 1] = 0; pos[k + 2] = 0;
      seed[i * 2 + v] = s;
      end[i * 2 + v] = v;
    }
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  geo.setAttribute('aSeed', new THREE.BufferAttribute(seed, 1));
  geo.setAttribute('aEnd', new THREE.BufferAttribute(end, 1));
  const mat = new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    uniforms: { uTime: { value: 0 }, uCam: { value: new THREE.Vector3() }, uIntensity: { value: 0.7 } },
    vertexShader: /* glsl */ `
      attribute float aSeed, aEnd;
      uniform float uTime; uniform vec3 uCam;
      varying float vA;
      void main() {
        float sp = 80.0 * (0.6 + 0.8 * fract(aSeed * 7.31));
        float y = mod(aSeed * 997.0 - uTime * sp, ${H}.0);
        float x = uCam.x + (fract(aSeed * 13.7) - 0.5) * ${R * 2}.0;
        float z = uCam.z + (fract(aSeed * 17.3) - 0.5) * ${R * 2}.0;
        vec3 head = vec3(x + y * 0.22, y, z + y * 0.1);
        vec3 p = head + vec3(0.22, -1.0, 0.1) * (5.0 + 4.0 * fract(aSeed * 3.7)) * aEnd;
        vA = 0.05 + 0.11 * fract(aSeed * 5.3);
        gl_Position = projectionMatrix * viewMatrix * vec4(p, 1.0);
      }`,
    fragmentShader: /* glsl */ `
      uniform float uIntensity; varying float vA;
      void main() { gl_FragColor = vec4(vec3(0.62, 0.70, 0.82) * vA * uIntensity, 1.0); }`,
  });
  const lines = new THREE.LineSegments(geo, mat);
  lines.frustumCulled = false;
  lines.visible = false;
  return { lines, mat };
}

/* ============ 月光 + 环境光 ============ */
export function buildMoonLights(P) {
  const moon = new THREE.DirectionalLight(P.moonColor, P.moonIntensity);
  moon.castShadow = true;
  moon.shadow.mapSize.set(4096, 4096);
  const S = 1500;
  Object.assign(moon.shadow.camera, { left: -S, right: S, top: S, bottom: -S, near: 200, far: 5200 });
  moon.shadow.bias = -0.0002;
  moon.shadow.normalBias = 2.2;
  moon.shadow.radius = 3;
  const hemi = new THREE.HemisphereLight('#111a2a', '#020304', P.ambient);
  return { moon, hemi };
}

export function setMoon(moon, sky, atmo, az, el) {
  const dir = new THREE.Vector3(
    Math.cos(el) * Math.sin(az), Math.sin(el), Math.cos(el) * Math.cos(az),
  );
  moon.position.copy(dir).multiplyScalar(2600);
  moon.target.position.set(0, 0, 0);
  if (sky) sky.material.uniforms.uMoonDir.value.copy(dir);
  if (atmo) atmo.uniforms.uMoonDir.value.copy(dir);
  return dir;
}
