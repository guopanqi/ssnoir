// 灯岸 LANTERN — by Spark · 成片（印刷层）
//
// 顺序：曝光 → 冷暖分离 → 中调去饱和 → 显示域求黑 → 只量化亮度 → 乘性颗粒 → 暗角 → 径向色散。
// 三条硬规则：不在 log 空间量化；crush 用减法；颗粒必须被黑吃掉（乘性）。
import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

export const GradeShader = {
  uniforms: {
    tDiffuse: { value: null },
    uResolution: { value: new THREE.Vector2(1, 1) },
    uTime: { value: 0 },
    uExposure: { value: 1 },
    uGain: { value: 1.22 },
    uCrush: { value: 0.03 },
    uShadowTint: { value: new THREE.Color(0.9, 0.99, 1.14) },
    uHighTint: { value: new THREE.Color(1.08, 1.01, 0.9) },
    uDesat: { value: 0.55 },
    uLevels: { value: 12 },
    uDither: { value: 0.6 },
    uGrain: { value: 0.035 },
    uVignette: { value: 0.6 },
    uAberr: { value: 0.003 },
  },
  vertexShader: /* glsl */ `
    varying vec2 vUv;
    void main() {
      vUv = uv;
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    }
  `,
  fragmentShader: /* glsl */ `
    precision highp float;
    varying vec2 vUv;
    uniform sampler2D tDiffuse;
    uniform vec2 uResolution;
    uniform float uTime, uExposure, uGain, uCrush, uDesat, uLevels, uDither, uGrain, uVignette, uAberr;
    uniform vec3 uShadowTint, uHighTint;
    float ign(vec2 p) {
      return fract(52.9829189 * fract(0.06711056 * p.x + 0.00583715 * p.y));
    }
    float luma(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }
    void main() {
      vec2 px = (vUv - 0.5);
      float r2 = dot(px, px);
      // 径向色散：只有边缘有，真镜头的行为
      vec2 off = px * r2 * uAberr * 12.0;
      vec3 col;
      col.r = texture2D(tDiffuse, vUv + off).r;
      col.g = texture2D(tDiffuse, vUv).g;
      col.b = texture2D(tDiffuse, vUv - off).b;
      col *= uExposure;
      float y0 = luma(col);
      // 冷暖分离：暗部冷，亮部暖
      col *= mix(uShadowTint, vec3(1.0), smoothstep(0.0, 0.45, y0));
      col *= mix(vec3(1.0), uHighTint, smoothstep(0.45, 1.0, y0));
      // 中调去饱和：只留黑与两点色
      float y1 = luma(col);
      col = mix(vec3(y1), col, 1.0 - uDesat * 0.7);
      // 显示域求黑：一段暗部钉死在 0
      col = max(col * uGain - uCrush, 0.0);
      // 只量化亮度再按比例缩回：不撕色相
      float y2 = max(luma(col), 1e-4);
      float lv = uLevels;
      float j = (ign(vUv * uResolution + fract(uTime * 7.0) * 61.7) - 0.5) * uDither;
      float yq = (floor(y2 * lv + 0.5 + j) + 0.0) / lv;
      col *= clamp(yq / y2, 0.0, 4.0);
      // 乘性颗粒：黑位安全
      float g = ign(vUv * uResolution * 1.7 + fract(uTime * 13.0) * 131.3) - 0.5;
      col *= 1.0 + g * uGrain * 2.0;
      // 暗角
      col *= 1.0 - uVignette * smoothstep(0.12, 0.62, r2 * 2.0);
      gl_FragColor = vec4(col, 1.0);
    }
  `,
};

export function createPost(renderer, scene, camera, profile) {
  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene, camera));
  const bloom = new UnrealBloomPass(
    new THREE.Vector2(1600, 900),
    profile.post.bloom.strength,
    profile.post.bloom.radius,
    profile.post.bloom.threshold,
  );
  composer.addPass(bloom);
  const grade = new ShaderPass(GradeShader);
  const p = profile.post;
  grade.uniforms.uExposure.value = p.exposure;
  grade.uniforms.uGain.value = p.contrastGain;
  grade.uniforms.uCrush.value = p.crush;
  grade.uniforms.uShadowTint.value.set(...p.shadowTint);
  grade.uniforms.uHighTint.value.set(...p.highTint);
  grade.uniforms.uDesat.value = p.desat;
  grade.uniforms.uLevels.value = p.levels;
  grade.uniforms.uDither.value = p.dither;
  grade.uniforms.uGrain.value = p.grain;
  grade.uniforms.uVignette.value = p.vignette;
  grade.uniforms.uAberr.value = p.aberration;
  composer.addPass(grade);
  composer.addPass(new OutputPass());
  return {
    composer,
    bloom,
    grade,
    setSize(w, h) {
      composer.setSize(w, h);
      grade.uniforms.uResolution.value.set(w, h);
    },
    setBloomGrade(bloomOn, gradeOn) {
      bloom.strength = bloomOn ? p.bloom.strength : 0;
      grade.enabled = gradeOn;
    },
  };
}
