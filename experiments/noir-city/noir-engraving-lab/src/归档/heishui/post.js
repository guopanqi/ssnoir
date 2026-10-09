/**
 * 黑水的后处理链。
 *
 *   1 反射     镜像相机渲染（main.js 负责）→ 湿地面/河面自己去取
 *   2 主场景   HDR 颜色 + 角色写进 alpha + DepthTexture
 *   3 体积雾   半分辨率光线步进：雾里跑真的灯（钠灯 + 城区底光 + 探照灯锥 + 月亮前向散射）
 *   4 边缘     深度/法线边缘 → 逆光线。**线的亮度由它背后的光决定**：
 *              黑块贴着黑，不画线；黑块贴着亮雾，才画出一道银边。
 *   5 光晕     亮部提取 → 高斯 + 变形镜头横向拉丝 + 红色 halation
 *   6 成片     曝光 → 冷暖分离 → 去饱和 → 受限色阶(带抖动) → 颗粒 → 暗角 → 色散 → sRGB
 */
import * as THREE from 'three';
import { AIR, LENS, MOON, SPOTS, GLOW_LIGHTS, INK } from './config.js';

const QUAD_VERT = /* glsl */`
varying vec2 vUv;
void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }
`;

class Pass {
  constructor(shader, name) {
    this.name = name;
    this.material = new THREE.ShaderMaterial(Object.assign({ vertexShader: QUAD_VERT }, shader));
    this.mesh = new THREE.Mesh(Pass.geo, this.material);
    this.mesh.frustumCulled = false;
    this.scene = new THREE.Scene();
    this.scene.add(this.mesh);
    this.camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
  }
}
Pass.geo = new THREE.PlaneGeometry(2, 2);

/* ================================================================ 体积雾 */

const FOG_FRAG = /* glsl */`
varying vec2 vUv;
uniform sampler2D tDepth;
uniform mat4 uInvProj;
uniform mat4 uCamWorld;
uniform vec2 uNearFar;
uniform vec2 uRes;
uniform float uTime;
uniform vec3 uSunTo;
uniform vec3 uSunColor;
uniform float uSunPower;
uniform float uDensity;
uniform float uHeight;
uniform float uHG;
uniform vec3 uAmbient;
uniform vec3 uHorizon;
uniform vec3 uZenith;
uniform vec3 uFogFar;

#define NP 24
#define NG 6
#define NS 4
uniform vec4 uPPos[NP]; uniform vec3 uPCol[NP]; uniform float uPRad[NP]; uniform int uPCount;
uniform vec4 uGPos[NG]; uniform vec3 uGCol[NG]; uniform float uGRad[NG]; uniform int uGCount;
uniform vec4 uSPos[NS]; uniform vec4 uSDir[NS]; uniform vec4 uSPar[NS]; uniform vec3 uSCol[NS]; uniform int uSCount;

float h21(vec2 p){ vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vnoise(vec2 p){
  vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h21(i), h21(i + vec2(1,0)), f.x), mix(h21(i + vec2(0,1)), h21(i + vec2(1,1)), f.x), f.y);
}
float hg(float c, float g){ float g2 = g * g; return (1.0 - g2) / (4.0 * 3.14159265 * pow(1.0 + g2 - 2.0 * g * c, 1.5)); }

/** 从深度反算世界坐标。depth == 1 表示天空，返回远点。 */
vec3 worldAt(vec2 uv, float d){
  vec4 ndc = vec4(uv * 2.0 - 1.0, d * 2.0 - 1.0, 1.0);
  vec4 vp = uInvProj * ndc;
  vp /= vp.w;
  return (uCamWorld * vp).xyz;
}

/** 天空：只有两个色 + 月亮 + 星。地平线附近是城市辉光抬起来的一层暖灰。 */
vec3 sky(vec3 dir){
  float up = clamp(dir.y, 0.0, 1.0);
  vec3 c = mix(uHorizon, uZenith, pow(up, 0.42));
  float m = max(dot(dir, uSunTo), 0.0);
  c += uSunColor * (pow(m, 260.0) * 0.85 + pow(m, 34.0) * 0.10) * (uSunPower * 1.1);
  c += uSunColor * smoothstep(0.99960, 0.99985, m) * 2.2;
  if (dir.y > 0.02){
    vec2 g = floor(dir.xz / max(dir.y, 0.02) * 130.0);
    float s = h21(g);
    c += vec3(0.55, 0.62, 0.78) * step(0.9975, s) * (0.5 + 0.5 * h21(g + uTime)) * 0.30;
  }
  return c;
}

float densityAt(vec3 p){
  float h = exp(-max(p.y, 0.0) / uHeight);
  float n = 0.72 + 0.56 * vnoise(p.xz * 0.0022 + uTime * 0.006);
  return uDensity * h * n;
}

void main(){
  vec2 uv = vUv;
  float d = texture2D(tDepth, uv).x;
  vec3 far = worldAt(uv, 1.0);
  vec3 near = worldAt(uv, d);
  bool isSky = d > 0.99999;

  vec3 P0 = uCamWorld[3].xyz;
  vec3 dir = normalize(far - P0);
  float tEnd = isSky ? uFogFar.x : min(length(near - P0), uFogFar.x);

  int steps = 26;
  float dt = tEnd / float(steps);
  float jit = h21(gl_FragCoord.xy * 0.7331);
  vec3 acc = vec3(0.0);
  float T = 1.0;

  for (int i = 0; i < 32; i++){
    if (i >= steps) break;
    float t = (float(i) + jit) * dt;
    vec3 sp = P0 + dir * t;
    float dens = densityAt(sp);
    if (dens > 1e-7){
      vec3 s = uAmbient * (0.35 + 0.65 * exp(-max(sp.y,0.0) / 120.0));
      // 月亮的前向散射：雾在月亮方向那条带上最亮
      s += uSunColor * uSunPower * hg(dot(dir, uSunTo), uHG) * 0.10;
      for (int k = 0; k < NP; k++){
        if (k >= uPCount) break;
        float dd = length(uPPos[k].xyz - sp);
        float att = 1.0 / (1.0 + pow(dd / uPRad[k], 2.0));
        s += uPCol[k] * att * hg(dot(dir, normalize(sp - uPPos[k].xyz)), 0.35) * 0.34;
      }
      for (int k = 0; k < NG; k++){
        if (k >= uGCount) break;
        float dd = length(uGPos[k].xyz - sp);
        float att = 1.0 / (1.0 + pow(dd / uGRad[k], 2.0));
        s += uGCol[k] * att * 0.55;
      }
      for (int k = 0; k < NS; k++){
        if (k >= uSCount) break;
        vec3 v = sp - uSPos[k].xyz;
        float dd = length(v);
        vec3 sd = normalize(v);
        float cone = smoothstep(uSPar[k].y, uSPar[k].x, dot(sd, uSDir[k].xyz));
        float att = uSPar[k].z / (1.0 + pow(dd / 160.0, 2.0));
        s += uSCol[k] * cone * att * hg(dot(dir, sd), 0.55) * 0.65;
      }
      float a = dens * dt;
      acc += T * a * s;
      T *= exp(-a);
    }
    if (T < 0.004) break;
  }

  if (isSky) acc += sky(dir) * T;
  gl_FragColor = vec4(acc, T);
}
`;

/* ================================================================ 边缘 */

const EDGE_FRAG = /* glsl */`
varying vec2 vUv;
uniform sampler2D tScene;   // rgb = hdr, a = role/255
uniform sampler2D tFog;
uniform sampler2D tDepth;
uniform mat4 uInvProj;
uniform mat4 uCamWorld;
uniform vec2 uNearFar;
uniform vec2 uRes;
uniform float uWidthPx;
uniform float uLineGain;
uniform float uCreaseGain;
uniform vec3 uInkCold;
uniform vec3 uInkWarm;
uniform vec3 uMoonTo;

float linearDepth(vec2 uv){
  float d = texture2D(tDepth, uv).x;
  float n = uNearFar.x, f = uNearFar.y;
  float vz = (n * f) / ((f - n) * d - f);   // 视空间 z（负）
  return -vz;
}
vec3 worldAt(vec2 uv){
  float d = texture2D(tDepth, uv).x;
  vec4 ndc = vec4(uv * 2.0 - 1.0, d * 2.0 - 1.0, 1.0);
  vec4 vp = uInvProj * ndc; vp /= vp.w;
  return (uCamWorld * vp).xyz;
}
float luma(vec3 c){ return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

void main(){
  vec2 uv = vUv;
  vec2 px = uWidthPx / uRes;

  vec4 s = texture2D(tScene, uv);
  vec4 fg = texture2D(tFog, uv);
  vec3 base = s.rgb * fg.a + fg.rgb;

  float dc = linearDepth(uv);
  vec3 pc = worldAt(uv);
  vec3 nc = normalize(cross(dFdx(pc), dFdy(pc)));
  float role = s.a * 255.0;

  // 4 邻域（十字 + 斜角用两档）
  vec2 offs[8];
  offs[0] = vec2( px.x, 0.0); offs[1] = vec2(-px.x, 0.0);
  offs[2] = vec2(0.0,  px.y); offs[3] = vec2(0.0, -px.y);
  offs[4] = vec2( px.x,  px.y); offs[5] = vec2(-px.x,  px.y);
  offs[6] = vec2( px.x, -px.y); offs[7] = vec2(-px.x, -px.y);

  float depthEdge = 0.0;
  float normEdge = 0.0;
  float bgLuma = luma(base);
  vec3 bgCol = base;
  float hero = role > 0.5 ? 1.0 : 0.0;

  for (int i = 0; i < 8; i++){
    vec2 o = offs[i];
    float dn = linearDepth(uv + o);
    // 相对深度差：远处楼群之间的缝也一样锐利
    depthEdge = max(depthEdge, abs(dn - dc) / max(min(dn, dc), 1.0));
    vec3 pn = worldAt(uv + o);
    vec3 nn = normalize(cross(dFdx(pn), dFdy(pn)));
    normEdge = max(normEdge, 1.0 - clamp(dot(nc, nn), -1.0, 1.0));
    vec4 sn = texture2D(tScene, uv + o);
    vec4 fn = texture2D(tFog, uv + o);
    vec3 cn = sn.rgb * fn.a + fn.rgb;
    float ln = luma(cn);
    if (ln > bgLuma){ bgLuma = ln; bgCol = cn; }
    if (sn.a * 255.0 > 0.5) hero = 1.0;
  }

  // 逆光边：只有深度断裂才算轮廓
  float silhouette = smoothstep(0.030, 0.085, depthEdge);
  // 折面线：只给主角（地点外壳 / 桥 / 驳岸 / 广场）
  float crease = smoothstep(0.50, 0.90, normEdge) * hero * uCreaseGain;

  float mask = clamp(silhouette * uLineGain + crease, 0.0, 1.6);
  if (mask < 0.002){ gl_FragColor = vec4(base, 1.0); return; }

  // **线的亮度由它背后的光决定**，两件事相乘：
  //   1 背景越亮，线越亮 —— 黑块贴着亮雾的那一侧才烧出银边
  //   2 **只有朝向冷键的那一半边才亮** —— 背光侧的边几乎消失。
  //     少了第 2 条，俯视机位下每栋楼都会闭合描边，整座城退回线框图。
  float facing = smoothstep(-0.15, 0.65, dot(nc, uMoonTo));
  float glow = (0.06 + 1.35 * smoothstep(0.010, 0.30, bgLuma)) * mix(0.16, 1.0, facing);
  vec3 warm = uInkWarm * 1.0;
  float warmth = clamp((bgCol.r - bgCol.b) * 3.2, 0.0, 1.0);
  vec3 lineCol = mix(uInkCold, warm, warmth);

  vec3 outc = base + lineCol * mask * glow * 1.05;
  gl_FragColor = vec4(outc, 1.0);
}
`;

/* ================================================================ 亮部 / 模糊 */

const BRIGHT_FRAG = /* glsl */`
varying vec2 vUv;
uniform sampler2D tSrc;
uniform float uThreshold;
uniform float uKnee;
void main(){
  vec3 c = texture2D(tSrc, vUv).rgb;
  float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
  float w = smoothstep(uThreshold, uThreshold + uKnee, l);
  gl_FragColor = vec4(c * w, 1.0);
}
`;

const BLUR_FRAG = /* glsl */`
varying vec2 vUv;
uniform sampler2D tSrc;
uniform vec2 uDir;      // 像素步长方向
uniform float uRadius;
void main(){
  vec3 acc = vec3(0.0);
  float wsum = 0.0;
  for (int i = -8; i <= 8; i++){
    float t = float(i) / 8.0;
    float w = exp(-t * t * 3.2);
    acc += texture2D(tSrc, vUv + uDir * (t * uRadius)).rgb * w;
    wsum += w;
  }
  gl_FragColor = vec4(acc / wsum, 1.0);
}
`;

const STREAK_FRAG = /* glsl */`
varying vec2 vUv;
uniform sampler2D tSrc;
uniform vec2 uDir;
uniform float uRadius;
void main(){
  // 变形镜头：一条又长又细的横向拖尾，带一点色散
  vec3 acc = vec3(0.0);
  float wsum = 0.0;
  for (int i = -16; i <= 16; i++){
    float t = float(i) / 16.0;
    float w = exp(-abs(t) * 3.4);
    vec2 o = uDir * (t * uRadius);
    acc.r += texture2D(tSrc, vUv + o * 1.02).r * w;
    acc.g += texture2D(tSrc, vUv + o).g * w;
    acc.b += texture2D(tSrc, vUv + o * 0.98).b * w;
    wsum += w;
  }
  gl_FragColor = vec4(acc / wsum, 1.0);
}
`;

/* ================================================================ 成片 */

const GRADE_FRAG = /* glsl */`
varying vec2 vUv;
uniform sampler2D tSrc;
uniform sampler2D tBloom;
uniform sampler2D tStreak;
uniform sampler2D tHalation;
uniform vec2 uRes;
uniform float uTime;
uniform float uExposure;
uniform float uLevels;
uniform float uDither;
uniform float uDesat;
uniform float uSplitH;
uniform float uHalation;
uniform float uBloomAmt;
uniform float uStreakAmt;
uniform float uGrain;
uniform float uVignette;
uniform float uAberr;
uniform float uLetterbox;
uniform float uCrush;
uniform float uGain;
uniform float uWeave;
uniform float uGate;
uniform vec3 uShadowTint;
uniform vec3 uHighlightTint;

float ign(vec2 p){ return fract(52.9829189 * fract(0.06711056 * p.x + 0.00583715 * p.y)); }
float h21(vec2 p){ vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float hs(float n){ return fract(sin(n * 78.233) * 43758.5453) * 2.0 - 1.0; }
float luma(vec3 c){ return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

vec3 tonemap(vec3 x){
  // 比 ACES 更硬：暗部直接压死，亮部也不给太多肩部
  x = max(x * uExposure, vec3(0.0));
  return x / (1.0 + x * 0.82);
}

void main(){
  vec2 uv = vUv;

  // 片门抖动：画面在静止的片门下面走。没有片门暗边，抖动只会像"画面在晃"。
  float k = floor(uTime * 8.0);
  uv += vec2(hs(k), hs(k + 37.0)) * uWeave / uRes;
  vec2 c = uv - 0.5;
  float r2 = dot(c, c);

  // 色散只出现在画面边缘（径向调制），比均匀 RGB shift 更像真镜头
  float amt = uAberr * max(r2 * 4.0 - 0.35, 0.0);
  vec3 src;
  src.r = texture2D(tSrc, uv + c * amt).r;
  src.g = texture2D(tSrc, uv).g;
  src.b = texture2D(tSrc, uv - c * amt).b;

  vec3 bloom = texture2D(tBloom, uv).rgb;
  vec3 streak = texture2D(tStreak, uv).rgb;
  vec3 hal = texture2D(tHalation, uv).rgb;      // 独立通道：阈值更高、半径更大、强偏红

  vec3 col = tonemap(src);
  // 逐环染色：紧的芯是冷青，大的晕是暖钠。一条链就给出冷暖分层。
  float bmix = smoothstep(0.0, 0.22, luma(bloom));
  col += tonemap(bloom * uBloomAmt) * mix(vec3(0.72, 0.86, 1.12), vec3(1.10, 0.90, 0.70), bmix);
  col += tonemap(streak * uStreakAmt) * vec3(0.74, 0.86, 1.15);
  col += tonemap(hal * uHalation) * vec3(1.0, 0.30, 0.14);

  // 冷暖分离
  float l = clamp(luma(col), 0.0, 1.0);
  col *= mix(uShadowTint, vec3(1.0), smoothstep(0.0, 0.45, l));
  col = mix(col, col * uHighlightTint, smoothstep(0.45, 1.0, l) * uSplitH);

  // 去饱和：把中间调的颜色抽掉，只留黑与两点色
  float g = luma(col);
  col = mix(col, vec3(g), uDesat * smoothstep(0.02, 0.35, l) * (1.0 - smoothstep(0.6, 1.0, l)));

  // 暗角
  col *= 1.0 - uVignette * smoothstep(0.10, 0.72, r2);

  // 到这一行为止都在**线性**空间。密度控制与受限色阶改到**显示域**做：
  // 线性空间的等距档位在暗部几乎全是 0（12 档时 1/12 已经是 sRGB 0.32），
  // 那会把整幅夜景压成纯黑。显示域的档位才是感知均匀的。
  vec3 disp = pow(max(col, vec3(0.0)), vec3(1.0 / 2.2));

  // 求黑：乘法抬不起真黑，只有减法 + 钳位能把一段暗部钉死在 0
  disp = max(disp * uGain - uCrush, vec3(0.0));

  // 受限色阶：**量化前**抖动，否则暗部成片跳档。
  // 只量化亮度再按比例缩回 —— 逐通道 floor 会把暗处的色相撕成青块/红块。
  float dith = (ign(gl_FragCoord.xy + fract(uTime) * 91.7) - 0.5) * uDither;
  float lv = max(uLevels, 2.0);
  float peak = max(max(disp.r, disp.g), max(disp.b, 1e-4));
  disp *= clamp(floor(peak * lv + dith) / lv / peak, 0.0, 1.0);

  // 颗粒：乘性。加性颗粒会在纯黑上撒灰，那就不是黑水了。
  disp *= 1.0 + (h21(gl_FragCoord.xy * 1.13 + floor(uTime * 24.0) * 137.0) - 0.5) * uGrain;

  // 片门暗边（静止参照物）
  if (uGate > 0.001){
    vec2 edgePx = min(vUv, 1.0 - vUv) * uRes;
    float dEdge = min(edgePx.x, edgePx.y);
    disp *= mix(1.0 - uGate, 1.0, smoothstep(0.0, 14.0, dEdge));
  }

  // 遮幅
  if (uLetterbox > 0.001){
    float bar = (1.0 - 1.0 / uLetterbox) * 0.5;
    if (uv.y < bar || uv.y > 1.0 - bar) disp = vec3(0.0);
  }

  col = disp;
  gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
`;

/* ================================================================ 组装 */

export function createPost(renderer, shared) {
  const NP = 24, NG = 6, NS = 4;
  const rt = {};
  let W = 1, H = 1;

  const fog = new Pass({ fragmentShader: FOG_FRAG, uniforms: {
    tDepth: { value: null },
    uInvProj: { value: new THREE.Matrix4() },
    uCamWorld: { value: new THREE.Matrix4() },
    uNearFar: { value: new THREE.Vector2(2, 9000) },
    uRes: { value: new THREE.Vector2() },
    uTime: shared.uTime,
    uSunTo: shared.uMoonTo,
    uSunColor: shared.uMoonColor,
    uSunPower: shared.uMoonPower,
    uDensity: { value: AIR.baseDensity },
    uHeight: { value: AIR.heightScale },
    uHG: { value: AIR.hg },
    uAmbient: { value: new THREE.Vector3(...AIR.ambient) },
    uHorizon: { value: new THREE.Vector3(...INK.cold.steel).multiplyScalar(0.55) },
    uZenith: { value: new THREE.Vector3(...INK.cold.void).multiplyScalar(1.2) },
    uFogFar: { value: new THREE.Vector3(5200, 0, 0) },
    uPPos: { value: Array.from({ length: NP }, () => new THREE.Vector4()) },
    uPCol: { value: Array.from({ length: NP }, () => new THREE.Vector3()) },
    uPRad: { value: new Float32Array(NP) },
    uPCount: { value: 0 },
    uGPos: { value: Array.from({ length: NG }, () => new THREE.Vector4()) },
    uGCol: { value: Array.from({ length: NG }, () => new THREE.Vector3()) },
    uGRad: { value: new Float32Array(NG) },
    uGCount: { value: 0 },
    uSPos: { value: Array.from({ length: NS }, () => new THREE.Vector4()) },
    uSDir: { value: Array.from({ length: NS }, () => new THREE.Vector4()) },
    uSPar: { value: Array.from({ length: NS }, () => new THREE.Vector4()) },
    uSCol: { value: Array.from({ length: NS }, () => new THREE.Vector3()) },
    uSCount: { value: 0 },
  } }, 'fog');

  const edge = new Pass({ fragmentShader: EDGE_FRAG, uniforms: {
    tScene: { value: null }, tFog: { value: null }, tDepth: { value: null },
    uInvProj: { value: new THREE.Matrix4() },
    uCamWorld: { value: new THREE.Matrix4() },
    uNearFar: { value: new THREE.Vector2(2, 9000) },
    uRes: { value: new THREE.Vector2() },
    uWidthPx: { value: 1.0 },
    uLineGain: { value: 1.0 },
    uCreaseGain: { value: 0.55 },
    uInkCold: { value: new THREE.Vector3(0.72, 0.84, 1.00) },
    uInkWarm: { value: new THREE.Vector3(1.00, 0.78, 0.50) },
    uMoonTo: shared.uMoonTo,
  } }, 'edge');

  const bright = new Pass({ fragmentShader: BRIGHT_FRAG, uniforms: {
    tSrc: { value: null }, uThreshold: { value: 0.34 }, uKnee: { value: 0.42 },
  } }, 'bright');

  const blur = new Pass({ fragmentShader: BLUR_FRAG, uniforms: {
    tSrc: { value: null }, uDir: { value: new THREE.Vector2() }, uRadius: { value: 3.0 },
  } }, 'blur');

  const streak = new Pass({ fragmentShader: STREAK_FRAG, uniforms: {
    tSrc: { value: null }, uDir: { value: new THREE.Vector2() }, uRadius: { value: 0.05 },
  } }, 'streak');

  const halBright = new Pass({ fragmentShader: BRIGHT_FRAG, uniforms: {
    tSrc: { value: null }, uThreshold: { value: 0.85 }, uKnee: { value: 0.55 },
  } }, 'halBright');

  const grade = new Pass({ fragmentShader: GRADE_FRAG, uniforms: {
    tSrc: { value: null }, tBloom: { value: null }, tStreak: { value: null }, tHalation: { value: null },
    uRes: { value: new THREE.Vector2() }, uTime: shared.uTime,
    uExposure: { value: LENS.exposure },
    uCrush: { value: LENS.crush },
    uGain: { value: LENS.contrastGain },
    uWeave: { value: LENS.gateWeave },
    uGate: { value: LENS.gateShadow },
    uLevels: { value: LENS.levels },
    uDither: { value: LENS.dither },
    uDesat: { value: LENS.desaturate },
    uSplitH: { value: LENS.splitHighlight },
    uHalation: { value: LENS.halation },
    uBloomAmt: { value: LENS.bloom },
    uStreakAmt: { value: LENS.streak },
    uGrain: { value: LENS.grain },
    uVignette: { value: LENS.vignette },
    uAberr: { value: LENS.aberration },
    uLetterbox: { value: LENS.letterbox },
    uShadowTint: { value: new THREE.Vector3(0.72, 0.86, 1.06) },
    uHighlightTint: { value: new THREE.Vector3(1.07, 0.985, 0.90) },
  } }, 'grade');

  function makeRT(w, h, opts = {}) {
    const t = new THREE.WebGLRenderTarget(Math.max(1, w), Math.max(1, h), Object.assign({
      type: THREE.HalfFloatType, format: THREE.RGBAFormat,
      minFilter: THREE.LinearFilter, magFilter: THREE.LinearFilter,
      depthBuffer: false, stencilBuffer: false,
    }, opts));
    t.texture.colorSpace = THREE.NoColorSpace;
    return t;
  }

  function setSize(w, h, dpr) {
    W = Math.max(1, Math.round(w * dpr));
    H = Math.max(1, Math.round(h * dpr));
    for (const k of ['scene', 'fog', 'lit', 'b1', 'b2', 's1', 's2', 'h1', 'h2']) if (rt[k]) rt[k].dispose();
    const hw = Math.round(W * AIR.scale), hh = Math.round(H * AIR.scale);
    const qw = Math.max(1, Math.round(W / 4)), qh = Math.max(1, Math.round(H / 4));
    const sw = Math.max(1, Math.round(W / 8)), sh = Math.max(1, Math.round(H / 8));

    rt.scene = new THREE.WebGLRenderTarget(W, H, {
      type: THREE.HalfFloatType, format: THREE.RGBAFormat,
      minFilter: THREE.LinearFilter, magFilter: THREE.LinearFilter,
      depthBuffer: true, stencilBuffer: false,
    });
    rt.scene.depthTexture = new THREE.DepthTexture(W, H, THREE.UnsignedIntType);
    rt.scene.depthTexture.format = THREE.DepthFormat;
    rt.scene.texture.colorSpace = THREE.NoColorSpace;

    rt.fog = makeRT(hw, hh);
    rt.lit = makeRT(W, H);
    rt.b1 = makeRT(qw, qh);
    rt.b2 = makeRT(qw, qh);
    rt.s1 = makeRT(sw, sh);
    rt.s2 = makeRT(sw, sh);
    rt.h1 = makeRT(sw, sh);
    rt.h2 = makeRT(sw, sh);

    fog.material.uniforms.uRes.value.set(hw, hh);
    edge.material.uniforms.uRes.value.set(W, H);
    grade.material.uniforms.uRes.value.set(W, H);
  }

  function blit(pass, target) {
    renderer.setRenderTarget(target || null);
    renderer.render(pass.scene, pass.camera);
  }

  /** 把 config 的灯灌进体积雾（钠灯用 rh，城区底光单列）。 */
  function uploadFogLights(pointLights, glowLights, spots) {
    const u = fog.material.uniforms;
    const n = Math.min(pointLights.length, NP);
    for (let i = 0; i < n; i++) {
      const l = pointLights[i];
      u.uPPos.value[i].set(l.p[0], l.p[1], l.p[2], l.r);
      u.uPCol.value[i].set(l.c[0], l.c[1], l.c[2]).multiplyScalar(l.i);
      u.uPRad.value[i] = l.rh ?? l.r * 2.0;
    }
    u.uPCount.value = n;
    const m = Math.min(glowLights.length, NG);
    for (let i = 0; i < m; i++) {
      const l = glowLights[i];
      u.uGPos.value[i].set(l.p[0], l.p[1], l.p[2], l.r);
      u.uGCol.value[i].set(l.c[0], l.c[1], l.c[2]).multiplyScalar(l.i);
      u.uGRad.value[i] = l.r;
    }
    u.uGCount.value = m;
    const sn = Math.min(spots.length, NS);
    for (let i = 0; i < sn; i++) {
      const s = spots[i];
      u.uSPos.value[i].set(s.pos.x, s.pos.y, s.pos.z, 1);
      u.uSDir.value[i].set(s.dir.x, s.dir.y, s.dir.z, 0);
      u.uSPar.value[i].set(Math.cos(s.angle * 0.55), Math.cos(s.angle * 1.25), s.intensity, 0);
      u.uSCol.value[i].set(s.color.r, s.color.g, s.color.b);
    }
    u.uSCount.value = sn;
  }

  /**
   * 主链。scene 已渲染进 rt.scene。
   * opts: { fogColor, horizon, zenith, density, lineGain, creaseGain, excludeBloomBlack }
   */
  function resolve(camera, opts = {}) {
    const u = fog.material.uniforms;
    camera.updateMatrixWorld();
    u.uInvProj.value.copy(camera.projectionMatrixInverse);
    u.uCamWorld.value.copy(camera.matrixWorld);
    u.uNearFar.value.set(camera.near, camera.far);
    if (opts.density !== undefined) u.uDensity.value = opts.density;
    if (opts.horizon) u.uHorizon.value.set(...opts.horizon);
    if (opts.zenith) u.uZenith.value.set(...opts.zenith);
    u.tDepth.value = rt.scene.depthTexture;
    blit(fog, rt.fog);

    const e = edge.material.uniforms;
    e.tScene.value = rt.scene.texture;
    e.tFog.value = rt.fog.texture;
    e.tDepth.value = rt.scene.depthTexture;
    e.uInvProj.value.copy(camera.projectionMatrixInverse);
    e.uCamWorld.value.copy(camera.matrixWorld);
    e.uNearFar.value.set(camera.near, camera.far);
    if (opts.lineGain !== undefined) e.uLineGain.value = opts.lineGain;
    if (opts.creaseGain !== undefined) e.uCreaseGain.value = opts.creaseGain;
    blit(edge, rt.lit);

    // 亮部 → 高斯 + 横向拉丝
    bright.material.uniforms.tSrc.value = rt.lit.texture;
    bright.material.uniforms.uThreshold.value = opts.bloomThreshold ?? 0.34;
    blit(bright, rt.b1);

    let src = rt.b1, dst = rt.b2;
    for (let i = 0; i < 2; i++) {
      const r = 2.4 + i * 3.0;
      blur.material.uniforms.tSrc.value = src.texture;
      blur.material.uniforms.uDir.value.set(1 / rt.b1.width, 0);
      blur.material.uniforms.uRadius.value = r / rt.b1.width;
      blit(blur, dst);
      blur.material.uniforms.tSrc.value = dst.texture;
      blur.material.uniforms.uDir.value.set(0, 1 / rt.b1.height);
      blit(blur, src === rt.b1 ? rt.b2 : rt.b1);
      const t = src; src = dst; dst = t;
    }

    streak.material.uniforms.tSrc.value = rt.b1.texture;
    streak.material.uniforms.uDir.value.set(1 / rt.s1.width, 0);
    streak.material.uniforms.uRadius.value = (opts.streakRadius ?? 0.05) * (LENS.streakLength / 34);
    blit(streak, rt.s2);
    streak.material.uniforms.tSrc.value = rt.s2.texture;
    streak.material.uniforms.uDir.value.set(0, 1 / rt.s1.height);
    streak.material.uniforms.uRadius.value = 0.006;
    blit(streak, rt.s1);

    // halation：独立通道。阈值更高（只吃最强的高光）、半径更大、回加时强偏红。
    halBright.material.uniforms.tSrc.value = rt.lit.texture;
    blit(halBright, rt.h1);
    for (let i = 0; i < 3; i++) {
      const r = 0.010 + i * 0.014;
      blur.material.uniforms.tSrc.value = rt.h1.texture;
      blur.material.uniforms.uDir.value.set(1 / rt.h1.width, 0);
      blur.material.uniforms.uRadius.value = r;
      blit(blur, rt.h2);
      blur.material.uniforms.tSrc.value = rt.h2.texture;
      blur.material.uniforms.uDir.value.set(0, 1 / rt.h1.height);
      blur.material.uniforms.uRadius.value = r;
      blit(blur, rt.h1);
    }

    const g = grade.material.uniforms;
    g.tSrc.value = rt.lit.texture;
    g.tBloom.value = rt.b1.texture;
    g.tStreak.value = rt.s1.texture;
    g.tHalation.value = rt.h1.texture;
    if (opts.exposure !== undefined) g.uExposure.value = opts.exposure;
    if (opts.grade) Object.entries(opts.grade).forEach(([k, v]) => { if (g[k]) g[k].value = v; });
    blit(grade, null);
  }

  return { rt, setSize, resolve, uploadFogLights, fog, edge, grade, get size() { return [W, H]; } };
}

export { SPOTS, GLOW_LIGHTS };
