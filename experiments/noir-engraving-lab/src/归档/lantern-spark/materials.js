// 灯岸 LANTERN — by Spark · 前向材质系统
//
//刻意不用的三样（与黑水/剪影夜城划清界限）：
//  1 无体积雾 raymarch —— 雾是前向解析高度雾，一次 exp 完事；
//  2 无镜像反射 RT —— 湿带是纵向解析拉丝（镜面瓣只在纵向收窄）；
//  3 无屏幕空间描线 pass —— 轮廓是逐像素 fresnel 银边 + 月亮朝向调制。
// 法线全部用面法线 cross(dFdx, dFdy)，合并网格与实例网格行为一致。
import * as THREE from 'three';

const NLAMPS = 8;
const NACC = 2;

function lampUniforms(profile) {
  const pos = [], col = [];
  for (let i = 0; i < NLAMPS; i++) {
    const L = profile.lamps[i];
    const c = profile.lampColors[L.c];
    pos.push(new THREE.Vector4(L.p[0], L.p[1], L.p[2], L.r));
    col.push(new THREE.Vector4(c[0], c[1], c[2], L.i));
  }
  const ap = [], ac = [];
  for (let i = 0; i < NACC; i++) {
    const A = profile.accents[i];
    ap.push(new THREE.Vector4(A.p[0], A.p[1], A.p[2], A.r));
    ac.push(new THREE.Vector4(A.c[0], A.c[1], A.c[2], A.i));
  }
  const moonDir = new THREE.Vector3(...profile.moon.toLight).normalize();
  return {
    uMoonDir: { value: moonDir },
    uMoonColor: { value: new THREE.Color(...profile.moon.color) },
    uMoonI: { value: profile.moon.intensity },
    uTerm: { value: new THREE.Vector2(...profile.moon.terminator) },
    uSpecPower: { value: profile.moon.specPower },
    uSpecGain: { value: profile.moon.specGain },
    uWarmPos: { value: pos },
    uWarmCol: { value: col },
    uAccPos: { value: ap },
    uAccCol: { value: ac },
    uHeightFall: { value: profile.heightFall },
    uDiffFloor: { value: profile.diffuseFloor },
    uRimColor: { value: new THREE.Color(...profile.rim.color) },
    uRimGain: { value: profile.rim.gain },
    uRimPower: { value: profile.rim.power },
    uRimBase: { value: profile.rim.base },
    uFogColor: { value: new THREE.Color(...profile.fog.color) },
    uFogDensity: { value: profile.fog.density },
    uFogHeight: { value: profile.fog.heightScale },
    uGlowPos: { value: new THREE.Vector3(...profile.fog.glowPos) },
    uGlowColor: { value: new THREE.Color(...profile.fog.glowColor) },
    uGlowR: { value: profile.fog.glowRadius },
    uGlowGain: { value: profile.fog.glowGain },
    uTime: { value: 0 },
  };
}

const SURF_VERT = /* glsl */ `
varying vec3 vW;
#ifdef USE_DISTRICT
attribute float aDistrict;
attribute float aRand;
varying float vDistrict;
varying float vRand;
#endif
void main() {
  vec3 p = position;
  #ifdef USE_INSTANCING
    p = (instanceMatrix * vec4(p, 1.0)).xyz;
  #endif
  vec4 w = modelMatrix * vec4(p, 1.0);
  vW = w.xyz;
  #ifdef USE_DISTRICT
    vDistrict = aDistrict;
    vRand = aRand;
  #endif
  gl_Position = projectionMatrix * viewMatrix * w;
}
`;

const SURF_FRAG = /* glsl */ `
precision highp float;
varying vec3 vW;
#ifdef USE_DISTRICT
varying float vDistrict;
varying float vRand;
uniform vec3 uTints[9];
#else
uniform vec3 uFlat;
#endif
uniform vec3 uMoonDir;
uniform vec3 uMoonColor;
uniform float uMoonI;
uniform vec2 uTerm;
uniform float uSpecPower;
uniform float uSpecGain;
uniform vec4 uWarmPos[${NLAMPS}];
uniform vec4 uWarmCol[${NLAMPS}];
uniform vec4 uAccPos[${NACC}];
uniform vec4 uAccCol[${NACC}];
uniform float uHeightFall;
uniform float uDiffFloor;
uniform vec3 uRimColor;
uniform float uRimGain;
uniform float uRimPower;
uniform float uRimBase;
uniform vec3 uFogColor;
uniform float uFogDensity;
uniform float uFogHeight;
uniform vec3 uGlowPos;
uniform vec3 uGlowColor;
uniform float uGlowR;
uniform float uGlowGain;
uniform float uWet;
uniform float uStreakNarrow;
uniform float uSpec;
uniform float uPool;
uniform float uRimOn;
uniform float uLampOn;

float hash12(vec2 p) {
  vec3 p3 = fract(vec3(p.xyx) * 0.1031);
  p3 += dot(p3, p3.yzx + 33.33);
  return fract((p3.x + p3.y) * p3.z);
}

void main() {
  // 面法线：合并网格与实例网格统一，不依赖光滑法线
  vec3 N = normalize(cross(dFdx(vW), dFdy(vW)));
  if (!gl_FrontFacing) N = -N;
  vec3 V = normalize(cameraPosition - vW);

  #ifdef USE_DISTRICT
    int di = int(vDistrict + 0.5);
    vec3 albedo = uTints[di];
    albedo *= 0.92 + 0.16 * vRand; // 同片区±8% 明度抖动，破实例重复感
  #else
    vec3 albedo = uFlat;
  #endif

  // —— 冷键：硬终止线漫反射 + 镜面碎面 ——
  float md = dot(N, uMoonDir);
  float lit = smoothstep(uTerm.x, uTerm.y, md);
  vec3 col = albedo * uMoonColor * (lit * uMoonI * 0.55 + uDiffFloor * 0.12);

  // 镜面：只有朝向正好把月亮反进眼睛的面才亮
  vec3 H = normalize(uMoonDir + V);
  float spec = pow(max(dot(N, H), 0.0), uSpecPower);
  // 湿带收窄水平瓣：光带只沿纵向拉长（纵向解析拉丝，无反射 RT）
  #ifdef WET_SURFACE
    vec3 Nw = normalize(vec3(N.x * uStreakNarrow, max(N.y, 0.35), N.z * uStreakNarrow));
    float wspec = pow(max(dot(Nw, H), 0.0), uSpecPower * 0.55);
    spec = mix(spec, wspec * 1.6, uWet);
  #endif
  col += uMoonColor * spec * uSpecGain * uSpec * albedo * 40.0;

  // —— 暖池：8 盏解析点光，各有动机；高度衰减保证灯不染高楼 ——
  if (uLampOn > 0.5) {
    for (int i = 0; i < ${NLAMPS}; i++) {
      vec3 Lp = uWarmPos[i].xyz - vW;
      float dist = length(Lp);
      vec3 L = Lp / max(dist, 0.001);
      float atten = exp(-dist / uWarmPos[i].w);
      atten *= exp(-max(vW.y - uWarmPos[i].y, 0.0) * uHeightFall);
      float diff = max(dot(N, L), 0.0);
      // 建模分量：按反照率，楼体保持近黑，只留朝向
      col += uWarmCol[i].rgb * (uWarmCol[i].a * atten * diff) * albedo * 14.0;
      // 灯池分量：只给贴地与水面（uPool），楼不吃
      col += uWarmCol[i].rgb * (uWarmCol[i].a * atten * uPool * 0.30);
    }
    // 叙事色：两点，各向同性小球，只染近处（远距离可见性交给发光信标点，不靠照亮街区）
    for (int i = 0; i < ${NACC}; i++) {
      vec3 Lp = uAccPos[i].xyz - vW;
      float dist = length(Lp);
      float atten = exp(-dist / uAccPos[i].w);
      col += uAccCol[i].rgb * (uAccCol[i].a * atten * 0.8);
    }
    #ifdef WET_SURFACE
      // 湿地灯带：灯在镜面方向的纵向拖尾（解析近似，不采样任何 RT）
      for (int i = 0; i < ${NLAMPS}; i++) {
        vec3 toLamp = uWarmPos[i].xyz - vW;
        vec3 Rm = reflect(-V, vec3(0.0, 1.0, 0.0));
        float band = pow(max(dot(normalize(Rm), normalize(toLamp)), 0.0), 18.0);
        float d2 = length(vW.xz - uWarmPos[i].xz);
        col += uWarmCol[i].rgb * band * exp(-d2 / (uWarmPos[i].w * 1.4)) * uWet * 0.55 * uWarmCol[i].a;
      }
    #endif
  }

  // —— 轮廓银边：fresnel，面向月亮的边更亮；只属于竖直面（地面/屋顶不吃） ——
  if (uRimOn > 0.5) {
    float fres = pow(1.0 - max(dot(N, V), 0.0), uRimPower);
    float vert = 1.0 - smoothstep(0.45, 0.80, abs(N.y));
    float moonFace = clamp(dot(N, uMoonDir) * 0.5 + 0.5, 0.0, 1.0);
    col += uRimColor * fres * vert * uRimGain * (uRimBase + moonFace);
  }

  // —— 空气：解析高度雾 + 市中心暖辉（只暖雾，不暖楼） ——
  float dist = length(cameraPosition - vW);
  float hf = 1.0 + 1.6 * exp(-max(vW.y, 0.0) / uFogHeight);
  float f = 1.0 - exp(-dist * uFogDensity * hf);
  vec3 fogC = uFogColor;
  float gd = length(vW - uGlowPos);
  fogC += uGlowColor * (exp(-gd / uGlowR) * uGlowGain);
  col = mix(col, fogC, clamp(f, 0.0, 1.0));

  gl_FragColor = vec4(col, 1.0);
}
`;

export function makeSurfaceMaterial(profile, shared, opts) {
  const defines = {};
  if (opts.district) defines.USE_DISTRICT = 1;
  if (opts.wet !== undefined) defines.WET_SURFACE = 1;
  const mat = new THREE.ShaderMaterial({
    defines,
    uniforms: Object.assign(lampUniforms(profile), {
      uTints: opts.district ? { value: profile.districtOrder.map((d) => new THREE.Color(...profile.districtTint[d])) } : undefined,
      uFlat: opts.flat ? { value: new THREE.Color(...opts.flat) } : undefined,
      uWet: { value: opts.wet ?? 0 },
      uSpec: { value: opts.spec ?? 1 },
      uPool: { value: opts.pool ?? 0.15 },
      uStreakNarrow: { value: profile.wet.streakNarrow },
      uRimOn: shared.uRimOn,
      uLampOn: shared.uLampOn,
    }),
    vertexShader: SURF_VERT,
    fragmentShader: SURF_FRAG,
  });
  // 清掉 undefined uniform（非片区材质没有 uTints）
  for (const k of Object.keys(mat.uniforms)) if (mat.uniforms[k] === undefined) delete mat.uniforms[k];
  // 共享灯光开关：uRimOn / uLampOn 与 shared 引用同一对象
  mat.uniforms.uRimOn = shared.uRimOn;
  mat.uniforms.uLampOn = shared.uLampOn;
  mat.uniforms.uTime = shared.uTime;
  return mat;
}

// —— 窗光：合并面片，tier 定色温，hash 定明灭，量化时间定闪烁 ——
export function makeWindowMaterial(profile, shared) {
  const tiers = profile.windows.tiers;
  return new THREE.ShaderMaterial({
    transparent: true,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    uniforms: {
      uTime: shared.uTime,
      uLampOn: shared.uLampOn,
      uC0: { value: new THREE.Color(...tiers[0].color) },
      uC1: { value: new THREE.Color(...tiers[1].color) },
      uC2: { value: new THREE.Color(...tiers[2].color) },
      uP0: { value: tiers[0].power },
      uP1: { value: tiers[1].power },
      uP2: { value: tiers[2].power },
      uLift: { value: profile.windows.lift },
      uFrac: { value: profile.windows.flickerFrac },
      uRate: { value: profile.windows.flickerRate },
      uMinOn: { value: profile.windows.minOn },
    },
    vertexShader: /* glsl */ `
      attribute float aTier;
      attribute float aRand;
      varying float vTier;
      varying float vRand;
      void main() {
        vTier = aTier;
        vRand = aRand;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      varying float vTier;
      varying float vRand;
      uniform float uTime, uLift, uFrac, uRate, uMinOn;
      uniform vec3 uC0, uC1, uC2;
      uniform float uP0, uP1, uP2;
      uniform float uLampOn;
      float h21(vec2 p) {
        vec3 p3 = fract(vec3(p.xyx) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return fract((p3.x + p3.y) * p3.z);
      }
      void main() {
        if (uLampOn < 0.5) discard;
        vec3 c = uC0; float pw = uP0;
        if (vTier > 0.5 && vTier < 1.5) { c = uC1; pw = uP1; }
        else if (vTier > 1.5) { c = uC2; pw = uP2; }
        float on = step(vRand, 0.62);
        // 脉动带：只有一部分窗参与，且时间量化，不量化就是噪点
        float band = step(0.62, vRand) * step(vRand, 0.62 + uFrac);
        float f = h21(vec2(vRand * 91.7, floor(uTime * uRate)));
        float flick = mix(1.0, mix(uMinOn, 1.0, step(0.5, f)), band);
        on = max(on, band * step(0.5, f));
        vec3 col = c * (pw * uLift) * mix(uMinOn, 1.0, on) * flick;
        gl_FragColor = vec4(col, 1.0);
      }
    `,
  });
}

// —— 天空：双色渐变 + 一线暖烟 + 硬月亮 + 稀疏星 ——
export function makeSky(profile) {
  const s = profile.sky;
  const moonDir = new THREE.Vector3(...profile.moon.toLight).normalize();
  const mat = new THREE.ShaderMaterial({
    side: THREE.BackSide,
    depthWrite: false,
    fog: false,
    uniforms: {
      uVoid: { value: new THREE.Color(...s.void) },
      uDeep: { value: new THREE.Color(...s.deep) },
      uHorizon: { value: new THREE.Color(...s.horizon) },
      uSmog: { value: new THREE.Color(...s.smog) },
      uMoonDir: { value: moonDir },
      uMoonDisc: { value: new THREE.Color(...s.moonDisc) },
      uMoonSize: { value: s.moonSize },
      uHalo: { value: s.haloGain },
      uStars: { value: s.stars },
    },
    vertexShader: /* glsl */ `
      varying vec3 vDir;
      void main() {
        vDir = position;
        vec4 mv = modelViewMatrix * vec4(position, 1.0);
        gl_Position = projectionMatrix * mv;
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      varying vec3 vDir;
      uniform vec3 uVoid, uDeep, uHorizon, uSmog, uMoonDir, uMoonDisc;
      uniform float uMoonSize, uHalo, uStars;
      float h21(vec2 p) {
        vec3 p3 = fract(vec3(p.xyx) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return fract((p3.x + p3.y) * p3.z);
      }
      void main() {
        vec3 d = normalize(vDir);
        float y = d.y;
        vec3 col = mix(uDeep, uVoid, smoothstep(0.02, 0.65, y));
        col = mix(uHorizon, col, smoothstep(-0.02, 0.16, y));
        // 贴地平线的一线暖烟：只在 y≈0.02 附近
        float band = exp(-pow((y - 0.025) * 22.0, 2.0));
        col += uSmog * band * 0.8;
        // 硬月亮 + 收紧的晕
        float m = dot(d, uMoonDir);
        float disc = smoothstep(uMoonSize, uMoonSize + 0.0004, m);
        float halo = pow(max(m, 0.0), 260.0) * uHalo;
        col += uMoonDisc * (disc * 2.2 + halo);
        // 稀疏星：只在高空，hash 门限
        vec2 sp = floor(d.xz / max(0.012 - d.y * 0.008, 0.004) * 3.0);
        float st = step(0.992, h21(sp)) * smoothstep(0.15, 0.5, y) * uStars;
        col += vec3(0.5, 0.6, 0.75) * st * 0.5;
        gl_FragColor = vec4(col, 1.0);
      }
    `,
  });
  const mesh = new THREE.Mesh(new THREE.SphereGeometry(4200, 32, 16), mat);
  mesh.frustumCulled = false;
  return mesh;
}

// —— 叙事色信标：两点红/绿的发光点，远距离可读的"眼睛落点"
// （不靠照亮街区——那会把整片区染红；只靠自发光 + Bloom）
export function makeAccentBeacons(profile, shared) {
  const n = profile.accents.length;
  const pos = new Float32Array(n * 3);
  const col = new Float32Array(n * 3);
  profile.accents.forEach((A, i) => {
    pos[i * 3] = A.p[0]; pos[i * 3 + 1] = A.p[1]; pos[i * 3 + 2] = A.p[2];
    col[i * 3] = A.c[0] * A.i * 1.6; col[i * 3 + 1] = A.c[1] * A.i * 1.6; col[i * 3 + 2] = A.c[2] * A.i * 1.6;
  });
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  g.setAttribute('aCol', new THREE.BufferAttribute(col, 3));
  const mat = new THREE.ShaderMaterial({
    transparent: true,
    depthWrite: false,
    depthTest: true,
    blending: THREE.AdditiveBlending,
    uniforms: { uOn: shared.uLampOn, uSize: { value: 16.0 } },
    vertexShader: /* glsl */ `
      attribute vec3 aCol;
      varying vec3 vC;
      uniform float uSize;
      void main() {
        vC = aCol;
        vec4 mv = modelViewMatrix * vec4(position, 1.0);
        gl_PointSize = uSize * 320.0 / max(-mv.z, 1.0);
        gl_Position = projectionMatrix * mv;
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      varying vec3 vC;
      uniform float uOn;
      void main() {
        if (uOn < 0.5) discard;
        vec2 q = gl_PointCoord - 0.5;
        float d = length(q) * 2.0;
        float core = exp(-d * d * 9.0);
        float halo = exp(-d * 2.2) * 0.35;
        gl_FragColor = vec4(vC * (core + halo), 1.0);
      }
    `,
  });
  const points = new THREE.Points(g, mat);
  points.frustumCulled = false;
  return points;
}

// —— 探照灯锥：加法几何体，纵向 + 边缘双衰减 ——
export function makeCone(profile, shared, def) {
  const len = 700;
  const geo = new THREE.CylinderGeometry(6, 90, len, 20, 1, true);
  geo.translate(0, -len / 2, 0); // 顶部在灯头
  const mat = new THREE.ShaderMaterial({
    transparent: true,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
    uniforms: {
      uColor: { value: new THREE.Color(...profile.cones.color) },
      uOpacity: { value: profile.cones.opacity * def.i * 0.5 },
      uOn: shared.uConeOn,
    },
    vertexShader: /* glsl */ `
      varying vec2 vUv;
      varying vec3 vN;
      varying vec3 vV;
      void main() {
        vUv = uv;
        vec4 w = modelMatrix * vec4(position, 1.0);
        vN = normalize(mat3(modelMatrix) * normal);
        vV = normalize(cameraPosition - w.xyz);
        gl_Position = projectionMatrix * viewMatrix * w;
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      varying vec2 vUv;
      varying vec3 vN;
      varying vec3 vV;
      uniform vec3 uColor;
      uniform float uOpacity;
      uniform float uOn;
      void main() {
        if (uOn < 0.5) discard;
        float axial = pow(vUv.y, 2.0); // 灯头最亮，向下衰减
        float edge = abs(dot(normalize(vN), normalize(vV))); // 正对强，掠射弱
        gl_FragColor = vec4(uColor, 1.0) * (axial * edge * uOpacity);
      }
    `,
  });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.position.set(...def.p);
  mesh.frustumCulled = false;
  return mesh;
}

// —— 灯头光斑：一组加法 Points，城市远景里的"灯河颗粒" ——
export function makeLampPoints(profile, shared) {
  const n = profile.lamps.length;
  const pos = new Float32Array(n * 3);
  const col = new Float32Array(n * 3);
  const pha = new Float32Array(n);
  profile.lamps.forEach((L, i) => {
    pos[i * 3] = L.p[0]; pos[i * 3 + 1] = L.p[1]; pos[i * 3 + 2] = L.p[2];
    const c = profile.lampColors[L.c];
    col[i * 3] = c[0] * L.i; col[i * 3 + 1] = c[1] * L.i; col[i * 3 + 2] = c[2] * L.i;
    pha[i] = (i * 0.6180339887) % 1;
  });
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  g.setAttribute('aCol', new THREE.BufferAttribute(col, 3));
  g.setAttribute('aPha', new THREE.BufferAttribute(pha, 1));
  const mat = new THREE.ShaderMaterial({
    transparent: true,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    uniforms: {
      uTime: shared.uTime,
      uOn: shared.uLampOn,
      uSize: { value: profile.lampPoints.size },
      uGain: { value: profile.lampPoints.gain },
      uTwinkle: { value: profile.lampPoints.twinkle },
    },
    vertexShader: /* glsl */ `
      attribute vec3 aCol;
      attribute float aPha;
      varying vec3 vC;
      varying float vTw;
      uniform float uTime, uSize, uTwinkle;
      float h21(vec2 p) {
        vec3 p3 = fract(vec3(p.xyx) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return fract((p3.x + p3.y) * p3.z);
      }
      void main() {
        vC = aCol;
        float tw = h21(vec2(aPha * 91.7, floor(uTime * 2.0)));
        vTw = 1.0 - uTwinkle * step(0.72, tw);
        vec4 mv = modelViewMatrix * vec4(position, 1.0);
        gl_PointSize = uSize * 320.0 / max(-mv.z, 1.0);
        gl_Position = projectionMatrix * mv;
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      varying vec3 vC;
      varying float vTw;
      uniform float uGain, uOn;
      void main() {
        if (uOn < 0.5) discard;
        vec2 q = gl_PointCoord - 0.5;
        float d = length(q) * 2.0;
        float a = exp(-d * d * 4.0);
        gl_FragColor = vec4(vC * vTw * uGain, 1.0) * a;
      }
    `,
  });
  const points = new THREE.Points(g, mat);
  points.frustumCulled = false;
  return points;
}
