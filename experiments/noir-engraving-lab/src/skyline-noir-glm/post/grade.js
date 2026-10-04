import * as THREE from 'three';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

// 成片（最终 pass，渲染到屏幕）：输入是线性值，自己做 sRGB 输出变换。
// 不量化、不描线——那是主实验室的版画语言，本实验刻意不走。
// 这里只做"胶片与镜头"的事：曝光、压黑、冷暖分离（乘性，黑位不动）、暗角、
// 显示域乘性颗粒（IGN 抖动 + 亮度遮罩，纯黑不被颗粒抬起）。
// 颗粒用 IGN：fract(52.9829189 * fract(0.06711056x + 0.00583715y))。

const GradeShader = {
  uniforms: {
    tDiffuse: { value: null },
    uExposure: { value: 1.0 },
    uCrush: { value: 0.004 },
    uShadowTint: { value: new THREE.Vector3(0.9, 0.99, 1.14) },
    uHighTint: { value: new THREE.Vector3(1.08, 1.01, 0.9) },
    uVignette: { value: 0.42 },
    uGrain: { value: 0.07 },
    uResolution: { value: new THREE.Vector2(1600, 900) },
    uFrame: { value: 0 },
  },
  vertexShader: /* glsl */ `
varying vec2 vUv;
void main() {
  vUv = uv;
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}
`,
  fragmentShader: /* glsl */ `
uniform sampler2D tDiffuse;
uniform float uExposure;
uniform float uCrush;
uniform vec3 uShadowTint;
uniform vec3 uHighTint;
uniform float uVignette;
uniform float uGrain;
uniform vec2 uResolution;
uniform float uFrame;
varying vec2 vUv;

float ign(vec2 p) {
  return fract(52.9829189 * fract(0.06711056 * p.x + 0.00583715 * p.y));
}

vec3 linearToSRGB(vec3 c) {
  return mix(c * 12.92, 1.055 * pow(max(c, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

void main() {
  vec3 c = texture2D(tDiffuse, vUv).rgb;

  // 线性域：曝光与压黑
  c *= uExposure;
  c = max(c - uCrush, 0.0);

  float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
  c *= mix(uShadowTint, uHighTint, smoothstep(0.05, 0.5, l));

  vec2 d2 = vUv - 0.5;
  d2.x *= uResolution.x / uResolution.y;
  c *= 1.0 - uVignette * smoothstep(0.42, 1.05, length(d2) * 1.45);

  // 显示域：sRGB 之后再上颗粒，暗部保持干净
  c = linearToSRGB(c);
  float g = ign(gl_FragCoord.xy + uFrame * 7.13);
  c *= 1.0 + (g - 0.5) * uGrain * smoothstep(0.0, 0.12, l);

  gl_FragColor = vec4(c, 1.0);
}
`,
};

export function createGradePass(profile) {
  const pass = new ShaderPass(GradeShader);
  const u = pass.uniforms;
  const p = profile.post;
  u.uExposure.value = p.exposure;
  u.uCrush.value = p.crush;
  u.uShadowTint.value.set(...p.shadowTint);
  u.uHighTint.value.set(...p.highTint);
  u.uVignette.value = p.vignette;
  u.uGrain.value = p.grain;
  return pass;
}
