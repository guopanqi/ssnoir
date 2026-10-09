/**
 * 黑水的材质与光照。
 *
 * 全部是自写 ShaderMaterial。不用 MeshStandardMaterial 的原因很直接：
 * 黑色电影要的是一条**硬终止线**、一层**掠射银边**、一份**按面随机的颗粒**，
 * 这些在 PBR 里要么没有对应旋钮，要么会被能量守恒抹平。
 *
 * 光照模型（刻意的，不是简化）：
 *   lit = 环境(近黑)
 *       + 冷键 · [ 硬终止线 × 掠射银边 × 面颗粒 ]     <- 只出面，不出漫反射
 *       + 冷键 · albedo · 终止线                      <- 漫反射只是一点点
 *       + 暖池 · i/(1+(d/r)^2.4) · 朝向灯              <- 钠灯洗在墙上
 *       + 湿反射（一次真实镜像渲染，按粗糙度纵向拉丝）
 * 建筑 albedo 压到 0.011~0.05：**受光面之所以亮，是因为它有光泽，不是因为它白。**
 */
import * as THREE from 'three';
import { INK, MOON, SODIUM, ACCENTS, WINDOW_TIERS, WINDOW_LIFE, RAIN, makeRng } from './config.js';

export const MAX_POINT = 24;

/* ------------------------------------------------------------ 共用 uniform */

export function createShared() {
  const S = MAX_POINT;
  return {
    uTime: { value: 0 },
    uMoonTo: { value: new THREE.Vector3(...MOON.toLight).normalize() },
    uMoonColor: { value: new THREE.Vector3(...MOON.color) },
    uMoonPower: { value: MOON.intensity },
    uMoonTerm: { value: new THREE.Vector2(...MOON.terminator) },

    uCityGlow: { value: new THREE.Vector3(0.30, 0.20, 0.11) },
    uCityGlowPower: { value: 0.052 },

    uPtPos: { value: Array.from({ length: S }, () => new THREE.Vector4()) },
    uPtColor: { value: Array.from({ length: S }, () => new THREE.Vector3()) },
    uPtData: { value: Array.from({ length: S }, () => new THREE.Vector2()) },
    uPtCount: { value: 0 },

    uReflectMatrix: { value: new THREE.Matrix4() },
    tReflect: { value: null },
    uReflectPass: { value: 0 },
    uWet: { value: 1.0 },
    uRain: { value: 0.0 },

    uAmbSky: { value: new THREE.Vector3(...INK.cold.deep) },
    uAmbGround: { value: new THREE.Vector3(...INK.warm.deep) },
    uAmbPower: { value: 0.85 },
  };
}

/** 把 config 的灯表灌进 uniform（钠灯 + 叙事色）。一次调用全局生效。 */
export function uploadLights(shared) {
  const pts = [];
  for (const l of SODIUM) pts.push({ p: l.p, c: INK.warm[l.c] || l.c, i: l.i, r: l.r, rh: l.rh });
  for (const a of ACCENTS) pts.push({ p: a.p, c: a.c, i: a.i * 3.2, r: a.r, rh: a.r * 3.4 });
  const n = Math.min(pts.length, MAX_POINT);
  for (let i = 0; i < n; i++) {
    const q = pts[i];
    shared.uPtPos.value[i].set(q.p[0], q.p[1], q.p[2], q.r);
    shared.uPtColor.value[i].set(q.c[0], q.c[1], q.c[2]).multiplyScalar(q.i);
    shared.uPtData.value[i].set(q.i, q.rh);
  }
  shared.uPtCount.value = n;
  return pts;
}

/* ------------------------------------------------------------ GLSL 公共 */

const COMMON = /* glsl */`
uniform vec3 uMoonTo; uniform vec3 uMoonColor; uniform float uMoonPower;
uniform vec2 uMoonTerm;
uniform vec3 uCityGlow; uniform float uCityGlowPower;
uniform vec4 uPtPos[${MAX_POINT}];
uniform vec3 uPtColor[${MAX_POINT}];
uniform vec2 uPtData[${MAX_POINT}];
uniform int uPtCount;
uniform vec3 uAmbSky; uniform vec3 uAmbGround; uniform float uAmbPower;
uniform float uWet; uniform float uRain; uniform float uTime;
uniform mat4 uReflectMatrix; uniform sampler2D tReflect; uniform float uReflectPass;

float h11(float p){ p = fract(p * 0.1031); p *= p + 33.33; p *= p + p; return fract(p); }
float h21(vec2 p){ vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float h31(vec3 p){ return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453123); }
float ign(vec2 p){ return fract(52.9829189 * fract(0.06711056 * p.x + 0.00583715 * p.y)); }

float vnoise(vec2 p){
  vec2 i = floor(p), f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h21(i), h21(i + vec2(1,0)), f.x), mix(h21(i + vec2(0,1)), h21(i + vec2(1,1)), f.x), f.y);
}

/** 面的几何法线：位置导数。平直面精确，折面自动硬切——正是要的"碎面"。 */
vec3 facetNormal(vec3 P){
  vec3 n = normalize(cross(dFdx(P), dFdy(P)));
  return gl_FrontFacing ? n : -n;
}

/** 每个面一个稳定随机数：哈希"法线 + 平面距离"，再和实例随机数混合。 */
float faceRand(vec3 P, vec3 N, float instRand){
  float fid = h31(N * 37.13 + vec3(dot(N, P) * 0.113));
  return h21(vec2(fid * 811.0, instRand * 613.0));
}

/**
 * 冷键 = **镜面高光**，不是漫反射。
 * 这是黑水最重要的一行：黑体块之所以被看见，是因为"某个朝向正好把月亮反进眼睛"。
 * 于是城市被切成一块块碎银——朝向对的亮，朝向不对的彻底黑，中间不给过渡。
 */
vec3 moonSheen(vec3 N, vec3 V, float rough, float rnd, float gain){
  vec3 H = normalize(uMoonTo + V);
  float ndh = max(dot(N, H), 0.0);
  float shin = mix(22.0, 380.0, clamp(rough, 0.0, 1.0));
  float spec = pow(ndh, shin);
  float fres = pow(1.0 - clamp(abs(dot(N, V)), 0.0, 1.0), 2.5);
  return uMoonColor * (uMoonPower * spec * gain * (0.45 + 0.95 * rnd) * (0.30 + 1.15 * fres));
}

/**
 * 暖池：软化平方反比 + **高度衰减**。
 * 高度衰减这一项是黑水的关键结构：灯只洗到街面往上十几米，再往上就没有了。
 * 少了它，一盏钠灯会把六十米的塔整栋染成暖褐 —— 那正是"没有黑色电影"的样子。
 */
vec3 pointWash(vec3 P, vec3 N){
  vec3 acc = vec3(0.0);
  for (int i = 0; i < ${MAX_POINT}; i++){
    if (i >= uPtCount) break;
    vec3 d = uPtPos[i].xyz - P;
    float dist = length(d);
    float r = uPtPos[i].w;
    float att = 1.0 / (1.0 + pow(dist / r, 2.4));
    att *= exp(-max(P.y - uPtPos[i].y, 0.0) * 0.062);
    float nl = max(dot(N, d / max(dist, 1e-3)), 0.0);
    acc += uPtColor[i] * att * mix(0.30, 1.0, nl);
  }
  return acc;
}

/** 峡谷遮蔽：贴地暗、高处亮。整片城市因此有深度。 */
float canyon(vec3 P){ return mix(0.38, 1.0, smoothstep(0.0, 22.0, P.y)); }

/** 湿反射：投影到镜像相机，按粗糙度沿屏幕纵向拉丝（noir 的签名）。 */
vec3 wetReflect(vec3 P, vec3 N, vec3 V, float rough, float amount){
  if (uReflectPass > 0.5) return vec3(0.0);
  vec4 rp = uReflectMatrix * vec4(P, 1.0);
  if (rp.w <= 0.001) return vec3(0.0);
  vec2 uv = rp.xy / rp.w * 0.5 + 0.5;
  if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return vec3(0.0);
  float fres = pow(1.0 - clamp(dot(N, V), 0.0, 1.0), 4.0);
  // 雨滴涟漪：把反射 UV 推开，湿地面因此"活"起来
  vec2 gp = floor(P.xz * 0.5);
  float rh = h21(gp);
  vec2 c = gp + vec2(h21(gp + 7.1), h21(gp + 13.7));
  float phase = fract(uTime * 0.85 + rh);
  float d = length(P.xz - c);
  float ring = sin(d * 5.0 - phase * 20.0) * exp(-d * 1.2) * (1.0 - phase);
  vec2 wob = vec2(ring, ring * 0.6) * uRain * 0.014;
  float spread = rough * (0.006 + 0.030 / max(rp.w, 1.0));
  vec3 acc = vec3(0.0); float wsum = 0.0;
  for (int i = 0; i < 6; i++){
    float t = (float(i) - 2.5) * 0.4;
    float w = 1.0 - abs(t) * 0.55;
    acc += texture2D(tReflect, uv + wob + vec2(t * spread * 0.3, t * spread)).rgb * w;
    wsum += w;
  }
  return (acc / wsum) * amount * (0.16 + 0.95 * fres) * uWet;
}
`;

const VERT_HASH = /* glsl */`
float vh11(float p){ p = fract(p * 0.1031); p *= p + 33.33; p *= p + p; return fract(p); }
`;

/* ------------------------------------------------------------ 表面材质 */

/**
 * 通用受光实体。
 *   opts.ground  贴地/道路（湿反射强）
 *   opts.water   河面
 *   opts.instanced  实例化填充建筑（读 aDistrict / aRand / aSize）
 *   opts.hasRole    合并几何带 aRole（表面角色 id）
 */
export function makeSurfaceMaterial(shared, opts = {}) {
  const hasRole = !!opts.hasRole && !opts.instanced;
  const defines = {};
  if (hasRole) defines.HAS_ROLE = '1';
  if (opts.district) defines.DISTRICT = '1';

  const vert = /* glsl */`
    attribute float aRole;
    uniform float uRoleDefault;
    #ifdef USE_INSTANCING
    attribute float aDistrict; attribute float aRand; attribute vec3 aSize;
    #endif
    varying vec3 vWorld; varying float vInstRand; varying float vRoleOrDistrict; varying vec3 vSize;
    ${VERT_HASH}
    void main(){
      vec3 p = position;
      float rnd = 0.5; float role = uRoleDefault; vec3 sz = vec3(1.0);
      #ifdef USE_INSTANCING
        p = (instanceMatrix * vec4(p, 1.0)).xyz;
        rnd = aRand; sz = aSize; role = aDistrict;
      #endif
      #ifdef HAS_ROLE
        role = aRole;
      #endif
      vec4 wp = modelMatrix * vec4(p, 1.0);
      vWorld = wp.xyz; vInstRand = rnd; vRoleOrDistrict = role; vSize = sz;
      gl_Position = projectionMatrix * viewMatrix * wp;
    }
  `;

  const frag = /* glsl */`
    varying vec3 vWorld; varying float vInstRand; varying float vRoleOrDistrict; varying vec3 vSize;
    uniform vec3 uDistrictTint[9];
    uniform float uGroundWet;
    uniform float uEmissiveLift;
    uniform float uReflective;
    uniform float uShine;      // 表面粗糙度：越小越像镜子
    uniform float uSpecGain;
    ${COMMON}

    vec3 surfaceAlbedo(float role, float rnd){
      vec3 a;
      if (role > 6.5)      a = vec3(0.30, 0.29, 0.26);
      else if (role > 5.5) a = vec3(0.15, 0.098, 0.032);
      else if (role > 4.5) a = vec3(0.055, 0.045, 0.034);
      else if (role > 3.5) a = vec3(0.16, 0.11, 0.045);
      else if (role > 2.5) a = vec3(0.105, 0.098, 0.082);
      else if (role > 1.5) a = vec3(0.022, 0.026, 0.036);
      else if (role > 0.5) a = vec3(0.020, 0.026, 0.036);
      else                 a = vec3(0.011, 0.014, 0.021);
      return a * (0.60 + 0.88 * rnd);
    }

    void main(){
      vec3 P = vWorld;
      vec3 N = facetNormal(P);
      vec3 V = normalize(cameraPosition - P);
      float rnd = faceRand(P, N, vInstRand);
      float id = vRoleOrDistrict;
      float ao = canyon(P);

      #ifdef DISTRICT
        int di = int(clamp(id + 0.5, 0.0, 8.0));
        vec3 albedo = uDistrictTint[di] * (0.55 + 0.95 * rnd);
        float hFrac = clamp(P.y / max(vSize.y, 1.0), 0.0, 1.0);
        albedo *= mix(0.62, 1.0, smoothstep(0.0, 0.10, hFrac));
      #else
        vec3 albedo = surfaceAlbedo(id, rnd);
      #endif

      // 环境：近黑。城市不该被匀光"托起来"。
      vec3 col = albedo * mix(uAmbGround, uAmbSky, N.y * 0.5 + 0.5) * uAmbPower;

      // 城市自身的底光：贴地最亮（街上的灯在低处弥漫），往上迅速消失
      float gg = uCityGlowPower * exp(-max(P.y, 0.0) / 55.0) * (0.40 + 0.60 * max(N.y, 0.0));
      col += uCityGlow * gg * ao * 0.75;

      // 冷键：镜面高光 + 一点点漫反射。朝向不对的面彻底黑，不给中间调。
      col += moonSheen(N, V, uShine, rnd, uSpecGain) * mix(0.35, 1.0, ao);
      // 逆光银边：面向月亮、又被掠射看到的那一侧。它不给整体提亮，
      // 只把"黑块的边"从背景里划出来 —— 这是黑色电影的轮廓线。
      {
        float faceMoon = clamp(dot(N, uMoonTo), 0.0, 1.0);
        float rim = pow(1.0 - clamp(abs(dot(N, V)), 0.0, 1.0), 2.5);
        col += uMoonColor * (uMoonPower * 0.26 * faceMoon * rim * (0.35 + 0.9 * rnd)) * mix(0.3, 1.0, ao);
      }
      col += albedo * uMoonColor * uMoonPower * smoothstep(uMoonTerm.x, uMoonTerm.y, dot(N, uMoonTo)) * 0.28;

      // 暖池
      vec3 wash = pointWash(P, N);
      col += albedo * wash * 3.4;
      col += wash * 0.018 * (0.4 + 0.9 * rnd) * mix(0.30, 1.0, ao);

      // 表面角色自带的发光（暖白/冷白/金饰）：地点的"光岛"。
      // 只在"合并几何"这一支里成立 —— DISTRICT 材质的 id 是分区序号，不是表面角色。
      #ifndef DISTRICT
        if (id > 6.5){
          col += albedo * (id > 7.5 ? 2.1 : 1.35) * uEmissiveLift;
        } else if ((id > 3.5 && id < 4.5) || (id > 5.5 && id < 6.5)){
          col += albedo * 0.95 * uEmissiveLift;
        }
      #endif

      // 湿反射：**只有贴地与水面**。竖直面也取反射会让"楼反射楼"把整座城提亮。
      float wet = uGroundWet * uReflective;
      #ifndef DISTRICT
        if (id > 0.5 && id < 2.5) wet *= 0.5;
      #endif
      wet *= 0.32 + 0.68 * smoothstep(0.20, 0.85, vnoise(P.xz * 0.011));
      if (wet > 0.002){
        col += wetReflect(P, N, V, uShine, ${opts.water ? '1.15' : '0.80'}) * wet;
      }

      gl_FragColor = vec4(max(col, vec3(0.0)), id / 255.0);
    }
  `;

  const tints = opts.districtTints || [];
  const arr = [];
  for (let i = 0; i < 9; i++) arr.push(new THREE.Vector3(...(tints[i] || [0.02, 0.025, 0.035])));

  return new THREE.ShaderMaterial({
    defines,
    vertexShader: vert,
    fragmentShader: frag,
    side: THREE.DoubleSide,
    uniforms: Object.assign({}, shared, {
      uRoleDefault: { value: opts.role ?? 0 },
      uGroundWet: { value: opts.wet ?? 0.9 },
      uEmissiveLift: { value: opts.emissive ?? 1.0 },
      uReflective: { value: opts.reflective ? 1.0 : 0.0 },
      uShine: { value: opts.shine ?? 0.60 },
      uSpecGain: { value: opts.specGain ?? 1.1 },
      uDistrictTint: { value: arr },
    }),
  });
}

/* ------------------------------------------------------------ 窗光 */

export function makeWindowMaterial(shared) {
  return new THREE.ShaderMaterial({
    uniforms: {
      uTime: shared.uTime,
      uTierColor: { value: WINDOW_TIERS.map((t) => new THREE.Vector3(...t.color)) },
      uTierPower: { value: WINDOW_TIERS.map((t) => t.power) },
      uLife: { value: new THREE.Vector2(WINDOW_LIFE.flickerAmount, WINDOW_LIFE.flickerRate) },
      uMinOn: { value: WINDOW_LIFE.minOn },
      uLift: { value: 1.0 },
    },
    vertexShader: /* glsl */`
      attribute float aTier; attribute float aRand;
      varying float vTier; varying float vRand; varying vec3 vWorld;
      void main(){
        vTier = aTier; vRand = aRand;
        vWorld = (modelMatrix * vec4(position, 1.0)).xyz;
        gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
      }
    `,
    fragmentShader: /* glsl */`
      varying float vTier; varying float vRand; varying vec3 vWorld;
      uniform vec3 uTierColor[3]; uniform float uTierPower[3];
      uniform vec2 uLife; uniform float uMinOn; uniform float uTime; uniform float uLift;
      float h21b(vec2 p){ vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
      void main(){
        int t = int(vTier + 0.5);
        vec3 c = uTierColor[t];
        float p = uTierPower[t];
        // 少数窗会自己灭掉：城市是活的，但绝大多数时候它不动
        float gate = step(vRand, uLife.x);
        float slow = h21b(vec2(floor(vRand * 512.0), floor(uTime * uLife.y)));
        float on = mix(1.0, mix(uMinOn, 1.0, step(0.30, slow)), gate);
        gl_FragColor = vec4(c * p * on * uLift, 0.0);
      }
    `,
    side: THREE.DoubleSide,
  });
}

/* ------------------------------------------------------------ 雨 */

const RAIN_VERT = /* glsl */`
attribute vec3 aBase; attribute vec2 aCorner; attribute vec2 aInfo;
uniform vec3 uCam; uniform vec3 uBox; uniform vec3 uSlant;
uniform vec3 uSpeed; uniform vec3 uLen; uniform vec3 uWidth; uniform vec3 uLayerAmt;
uniform float uTime; uniform float uRainAmount;
varying float vFade; varying float vBright;
void main(){
  int L = int(aInfo.x + 0.5);
  float seed = aInfo.y;
  float along = aCorner.y;                  // -1 尾 / +1 头
  float acrossSide = aCorner.x;
  vec3 p = aBase;
  p += uSlant * (uTime * uSpeed[L] * (0.8 + 0.5 * seed));
  p = mod(p + uBox * 0.5, uBox) - uBox * 0.5;   // 在盒里循环，不漂走
  p += uCam;                                     // 盒跟着相机
  vec3 toCam = normalize(uCam - p);
  vec3 across = normalize(cross(uSlant, toCam));
  float len = uLen[L] * (0.55 + 0.9 * seed) * mix(0.35, 1.0, uRainAmount);
  float wid = uWidth[L] * mix(0.6, 1.4, uRainAmount);
  p += uSlant * (len * along);
  p += across * (wid * acrossSide);
  float dist = length(p.xz - uCam.xz);
  vFade = (1.0 - smoothstep(uBox.x * 0.20, uBox.x * 0.52, dist)) * uLayerAmt[L];
  vBright = 0.30 + 0.70 * seed;
  gl_Position = projectionMatrix * viewMatrix * vec4(p, 1.0);
}
`;

const RAIN_FRAG = /* glsl */`
varying float vFade; varying float vBright;
uniform vec3 uColor; uniform float uRainAmount;
void main(){
  gl_FragColor = vec4(uColor * vBright * vFade * uRainAmount, 1.0);
}
`;

export function makeRain(shared) {
  const rng = makeRng();          // 固定 seed：同一次实验的雨每次一样
  const n = RAIN.count;
  const base = new Float32Array(n * 4 * 3);
  const corner = new Float32Array(n * 4 * 2);
  const info = new Float32Array(n * 4 * 2);
  const idx = new Uint32Array(n * 6);
  const CORNERS = [[-1, -1], [1, -1], [1, 1], [-1, 1]];
  for (let i = 0; i < n; i++) {
    const seed = rng();
    const layer = i % 3;
    const px = (rng() - 0.5) * RAIN.box[0];
    const py = rng() * RAIN.box[1];
    const pz = (rng() - 0.5) * RAIN.box[2];
    for (let c = 0; c < 4; c++) {
      const k = i * 4 + c;
      base[k * 3] = px; base[k * 3 + 1] = py; base[k * 3 + 2] = pz;
      corner[k * 2] = CORNERS[c][0]; corner[k * 2 + 1] = CORNERS[c][1];
      info[k * 2] = layer; info[k * 2 + 1] = seed;
    }
    const b = i * 4;
    idx.set([b, b + 1, b + 2, b, b + 2, b + 3], i * 6);
  }

  const g = new THREE.BufferGeometry();
  // position 只是占位（WebGL 需要它存在），真正的基点在 aBase
  g.setAttribute('position', new THREE.BufferAttribute(base.slice(), 3));
  g.setAttribute('aBase', new THREE.BufferAttribute(base, 3));
  g.setAttribute('aCorner', new THREE.BufferAttribute(corner, 2));
  g.setAttribute('aInfo', new THREE.BufferAttribute(info, 2));
  g.setIndex(new THREE.BufferAttribute(idx, 1));
  g.boundingSphere = new THREE.Sphere(new THREE.Vector3(), 1e6);

  const material = new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, depthTest: true,
    blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
    uniforms: {
      uTime: shared.uTime,
      uCam: { value: new THREE.Vector3() },
      uRainAmount: { value: 0.0 },
      uBox: { value: new THREE.Vector3(...RAIN.box) },
      uSlant: { value: new THREE.Vector3(...RAIN.slant).normalize() },
      uSpeed: { value: new THREE.Vector3(...RAIN.speeds) },
      uLen: { value: new THREE.Vector3(...RAIN.lengths) },
      uWidth: { value: new THREE.Vector3(...RAIN.widths) },
      uLayerAmt: { value: new THREE.Vector3(...RAIN.layers) },
      uColor: { value: new THREE.Vector3(0.55, 0.70, 0.90) },
    },
    vertexShader: RAIN_VERT,
    fragmentShader: RAIN_FRAG,
  });
  const mesh = new THREE.Mesh(g, material);
  mesh.frustumCulled = false;
  mesh.renderOrder = 5;
  return { geometry: g, material, mesh };
}
