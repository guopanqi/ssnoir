/**
 * 逆光 BACKLIGHT · 共享 GLSL 片段。
 * 天空与空气透视共用同一份 `skyBase()`：远处几何精确收敛到"它背后那块天"，
 * 于是远楼永远比天暗一档，价值剪影不会被雾冲平。
 */

import { SKY, FOG } from './config.js';

const v3 = (a) => `vec3(${a[0].toFixed(5)}, ${a[1].toFixed(5)}, ${a[2].toFixed(5)})`;

export const UNIFORM_DEFAULTS = {
  uSkyZenith: v3(SKY.zenith),
  uSkyMid: v3(SKY.mid),
  uSkyHorizon: v3(SKY.horizon),
  uGlowDir: (() => {
    const [x, y, z] = SKY.glowDir;
    const l = Math.hypot(x, y, z);
    return v3([x / l, y / l, z / l]);
  })(),
};

/** 确定性 hash（用于窗光集群、尘粒）。 */
export const HASH_GLSL = /* glsl */ `
float bh11(float p) { p = fract(p * 0.1031); p *= p + 33.33; p *= p + p; return fract(p); }
vec3 bh33(vec3 p) {
  p = vec3(dot(p, vec3(127.1, 311.7, 74.7)),
           dot(p, vec3(269.5, 183.3, 246.1)),
           dot(p, vec3(113.5, 271.9, 124.6)));
  return fract(sin(p) * 43758.5453123);
}
`;

/** 天的基础结构：三段梯度 + 贴地亮带 + 只在地平线附近存在的热点扇区。 */
export const SKY_BASE_GLSL = /* glsl */ `
uniform vec3 uSkyZenith;
uniform vec3 uSkyMid;
uniform vec3 uSkyHorizon;
uniform vec3 uGlowDir;

vec3 skyBase(vec3 d) {
  float h = d.y;
  vec3 col = mix(uSkyMid, uSkyZenith, smoothstep(0.06, 0.72, h));
  col = mix(uSkyHorizon * 0.85, col, smoothstep(-0.02, 0.10, h));
  float ah = abs(h);
  float band = exp(-ah * ${SKY.coreFall.toFixed(3)}) * ${Number(SKY.bandCore).toFixed(3)}
             + exp(-ah * ${SKY.haloFall.toFixed(3)}) * ${Number(SKY.bandHalo).toFixed(3)}
             + exp(-ah * ${SKY.floorFall.toFixed(3)}) * ${Number(SKY.floor).toFixed(3)};
  float hot = pow(max(dot(normalize(d), uGlowDir), 0.0), ${SKY.glowPow.toFixed(3)})
            * exp(-ah * ${SKY.glowFall.toFixed(3)}) * ${Number(SKY.glowGain).toFixed(3)};
  col += uSkyHorizon * band * (1.0 + hot);
  if (h < 0.0) col = mix(col, col * vec3(0.9, 0.92, 1.0), clamp(-h * 6.0, 0.0, 0.6));
  return col;
}
`;

/** 空气透视：往上看推向“它背后那块天”，往下看推向地面的暗霾。 */
export const FOG_GLSL = /* glsl */ `
uniform float uFogDensity;
uniform float uFogYFall;
uniform float uFogHighMul;

${SKY_BASE_GLSL}

vec3 aerial(vec3 col, vec3 worldPos) {
  vec3 dv = worldPos - cameraPosition;
  float dist = length(dv);
  if (dist < 0.0001) return col;
  vec3 dir = dv / dist;
  float dens = uFogDensity * (1.0 + uFogYFall * max(0.0, worldPos.y));
  float t = 1.0 - exp(-dist * dens * mix(1.0, uFogHighMul, smoothstep(60.0, 300.0, worldPos.y)));
  // 俯视时如果也推向地平线亮带，整张图会变成一张暖色纸——那不是空气，是底片漏光。
  vec3 haze = uSkyHorizon * 0.10 + uSkyMid * 0.45;
  vec3 fc = mix(haze, skyBase(dir), smoothstep(-0.14, 0.05, dir.y));
  return mix(col, fc, clamp(t, 0.0, 1.0));
}
`;

/** 到on-grade：画面后端统一的一次天空求值（与雾同源）。 */
export const SKY_ONLY_GLSL = /* glsl */ `
${SKY_BASE_GLSL}
`;
