import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

const NoirShader = {
  uniforms: {
    tDiffuse: { value: null },
    uEnabled: { value: 1.0 },
    uLevels: { value: 6.0 },
    uDither: { value: 0.85 },
    uDitherScale: { value: 2.0 },
    uInBlack: { value: 0.018 },
    uInWhite: { value: 0.58 },
    uGamma: { value: 0.92 },
    uResolution: { value: new THREE.Vector2(1280, 720) },
    uTime: { value: 0 },
  },
  vertexShader: /* glsl */`
    varying vec2 vUv;
    void main() {
      vUv = uv;
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    }
  `,
  fragmentShader: /* glsl */`
    uniform sampler2D tDiffuse;
    uniform float uEnabled;
    uniform float uLevels;
    uniform float uDither;
    uniform float uDitherScale;
    uniform float uInBlack;
    uniform float uInWhite;
    uniform float uGamma;
    uniform vec2 uResolution;
    uniform float uTime;
    varying vec2 vUv;

    const float BAYER4[16] = float[16](
      0.0, 8.0, 2.0, 10.0,
      12.0, 4.0, 14.0, 6.0,
      3.0, 11.0, 1.0, 9.0,
      15.0, 7.0, 13.0, 5.0
    );

    float luma(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

    vec3 ramp(float x) {
      vec3 a = vec3(0.020, 0.028, 0.045);
      vec3 b = vec3(0.070, 0.105, 0.165);
      vec3 c = vec3(0.285, 0.360, 0.500);
      vec3 d = vec3(0.930, 0.955, 1.000);
      vec3 r = mix(a, b, clamp(x / 0.30, 0.0, 1.0));
      r = mix(r, c, clamp((x - 0.30) / 0.36, 0.0, 1.0));
      r = mix(r, d, clamp((x - 0.66) / 0.34, 0.0, 1.0));
      return r;
    }

    void main() {
      vec4 src = texture2D(tDiffuse, vUv);
      if (uEnabled < 0.5) { gl_FragColor = src; return; }

      float l = clamp((luma(src.rgb) - uInBlack) / max(0.001, uInWhite - uInBlack), 0.0, 1.0);
      l = pow(l, uGamma);

      float scale = max(1.0, uDitherScale * uResolution.y / 720.0);
      ivec2 cell = ivec2(floor(gl_FragCoord.xy / scale));
      int idx = (cell.y & 3) * 4 + (cell.x & 3);
      float bayer = (BAYER4[idx] + 0.5) / 16.0;
      bayer = mix(0.5, bayer, clamp(uDither, 0.0, 1.25));

      float steps = max(2.0, uLevels) - 1.0;
      float q = clamp(floor(l * steps + bayer) / steps, 0.0, 1.0);
      vec3 styled = ramp(q);

      float grain = fract(sin(dot(gl_FragCoord.xy + uTime * 17.0, vec2(12.9898, 78.233))) * 43758.5453) - 0.5;
      styled += grain * 0.012;

      gl_FragColor = vec4(styled, src.a);
    }
  `,
};

export function createPost(renderer, scene, camera) {
  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene, camera));

  const bloom = new UnrealBloomPass(new THREE.Vector2(innerWidth, innerHeight), 0.65, 0.72, 0.86);
  composer.addPass(bloom);

  // 先把 Three.js 的 HDR/线性画面做 tone mapping + output transform，
  // 再进入 NoirShader。这样版画层看到的是“屏幕上的画”，和 SSNoir Unity
  // 里 post-processing 之后再做 Stylize 的思路一致。
  composer.addPass(new OutputPass());

  const noir = new ShaderPass(NoirShader);
  composer.addPass(noir);

  function resize(w, h) {
    composer.setSize(w, h);
    noir.uniforms.uResolution.value.set(w, h);
  }

  return { composer, bloom, noir, resize };
}
