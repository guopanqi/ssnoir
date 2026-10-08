import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { TexturePass } from 'three/addons/postprocessing/TexturePass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

/* 后处理链：场景→独立RT(带深度) → 纹理进链 → 辉光 → 大气(高度雾/远处霾/地面雾) → 胶片调色 → 输出
   场景深度放在独立的 rtScene 上（不跟 composer 的 ping-pong 目标共享）：大气 pass 采样它的
   深度纹理时它不是当前帧缓冲的附件，避免读写同一纹理的反馈回路。高度雾积分用
   IQ (https://iquilezles.org/articles/fog/) 的解析式。 */

const AtmoShader = {
  uniforms: {
    tDiffuse: { value: null },
    tDepth: { value: null },
    uProjInv: { value: new THREE.Matrix4() },
    uCamWorld: { value: new THREE.Matrix4() },
    uCamPos: { value: new THREE.Vector3() },
    uFogLow: { value: new THREE.Color('#101726') },
    uFogHigh: { value: new THREE.Color('#0b101c') },
    uDens: { value: 0.0011 },   // 地面雾基准密度
    uFall: { value: 0.02 },     // 随高度衰减
    uHaze: { value: 0.0003 },   // 远景霾（与高度无关的 aerial perspective）
    uMist: { value: 0.5 },      // 地面流动雾的量
    uSkyHaze: { value: 0.1 },
    uMoonDir: { value: new THREE.Vector3(0, 1, 0) },
    uMoonTint: { value: new THREE.Color('#5a6d92') },
    uTime: { value: 0 },
  },
  vertexShader: /* glsl */ `
    varying vec2 vUv;
    void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
  fragmentShader: /* glsl */ `
    varying vec2 vUv;
    uniform sampler2D tDiffuse, tDepth;
    uniform mat4 uProjInv, uCamWorld;
    uniform vec3 uCamPos, uFogLow, uFogHigh, uMoonDir, uMoonTint;
    uniform float uDens, uFall, uHaze, uMist, uSkyHaze, uTime;

    float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
    float vnoise(vec2 p) {
      vec2 i = floor(p), f = fract(p);
      f = f * f * (3.0 - 2.0 * f);
      return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x),
                 mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
    }

    void main() {
      vec4 src = texture2D(tDiffuse, vUv);
      float d = texture2D(tDepth, vUv).x;
      vec4 clip = vec4(vUv * 2.0 - 1.0, d * 2.0 - 1.0, 1.0);
      vec4 view = uProjInv * clip;
      view /= view.w;
      vec3 world = (uCamWorld * view).xyz;

      vec3 dv = world - uCamPos;
      float dist = length(dv);
      vec3 rd = dv / max(dist, 1e-4);

      float fog = 0.0;
      vec3 fogCol = uFogLow;
      bool isSky = d >= 0.99999;

      if (!isSky) {
        // 高度雾（IQ 解析积分）
        float ry = rd.y;
        float f1;
        if (abs(ry) < 1e-4) f1 = uDens * exp(-uCamPos.y * uFall) * dist;
        else f1 = (uDens / uFall) * exp(-uCamPos.y * uFall) * (1.0 - exp(-dist * ry * uFall)) / ry;
        f1 = clamp(f1, 0.0, 1.0);
        // 远景霾
        float f2 = 1.0 - exp(-dist * uHaze);
        // 贴地流动雾
        float n = vnoise(world.xz * 0.0026 + vec2(uTime * 0.018, uTime * 0.007));
        n = 0.65 * n + 0.35 * vnoise(world.xz * 0.009 - vec2(uTime * 0.03, 0.0));
        float m = smoothstep(0.52, 0.95, n) * uMist
                * exp(-max(world.y - 3.0, 0.0) * 0.11)
                * (1.0 - exp(-dist * 0.0011));
        fog = clamp(f1 * 0.9 + f2 * 0.75 + m, 0.0, 1.0);
        fogCol = mix(uFogLow, uFogHigh, clamp(world.y / 90.0, 0.0, 1.0));
        // 雾被月光轻微照亮（朝月方向更亮）
        float ms = pow(max(dot(rd, uMoonDir), 0.0), 6.0);
        fogCol += uMoonTint * ms * 0.28;
      } else {
        // 天空只吃薄霾，越贴地平线越浓
        fog = uSkyHaze * (0.45 + 0.55 * exp(-abs(rd.y) * 5.0));
        fogCol = uFogHigh;
      }
      gl_FragColor = vec4(mix(src.rgb, fogCol, fog), src.a);
    }`,
};

const GradeShader = {
  uniforms: {
    tDiffuse: { value: null },
    uTime: { value: 0 },
    uRes: { value: new THREE.Vector2(1920, 1080) },
    uContrast: { value: 1.12 },
    uSaturation: { value: 0.82 },
    uVignette: { value: 0.55 },
    uGrain: { value: 0.05 },
    uAberr: { value: 0.0016 },
  },
  vertexShader: /* glsl */ `
    varying vec2 vUv;
    void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
  fragmentShader: /* glsl */ `
    varying vec2 vUv;
    uniform sampler2D tDiffuse;
    uniform float uTime, uContrast, uSaturation, uVignette, uGrain, uAberr;
    uniform vec2 uRes;
    void main() {
      vec2 c = vUv - 0.5;
      float r2 = dot(c, c);
      vec2 off = c * uAberr * (0.5 + r2 * 2.0);
      vec3 col;
      col.r = texture2D(tDiffuse, vUv + off).r;
      col.g = texture2D(tDiffuse, vUv).g;
      col.b = texture2D(tDiffuse, vUv - off).b;
      col = clamp((col - 0.02) * uContrast + 0.02, 0.0, 8.0); // 温和 S 提压，支点贴近黑，不炸暗部
      float l = dot(col, vec3(0.2126, 0.7152, 0.0722));
      col = mix(vec3(l), col, uSaturation);                  // 收饱和
      col += vec3(0.0, 0.0008, 0.0022) * (1.0 - smoothstep(0.0, 0.25, l)); // 阴影偏冷
      col *= 1.0 - uVignette * smoothstep(0.12, 0.62, r2);   // 暗角
      float g = fract(sin(dot(vUv * uRes + mod(uTime, 10.0) * 61.7, vec2(12.9898, 78.233))) * 43758.5453);
      col += (g - 0.5) * uGrain * (1.35 - l);                // 颗粒（暗部更重）
      gl_FragColor = vec4(max(col, vec3(0.0)), 1.0);
    }`,
};

export function buildComposer(renderer, scene, camera) {
  const size = renderer.getDrawingBufferSize(new THREE.Vector2());
  const depth = new THREE.DepthTexture(size.x, size.y);
  depth.type = THREE.UnsignedIntType;
  const rtScene = new THREE.WebGLRenderTarget(size.x, size.y, {
    type: THREE.HalfFloatType, depthTexture: depth, depthBuffer: true, stencilBuffer: false,
  });
  const composer = new EffectComposer(
    renderer,
    new THREE.WebGLRenderTarget(size.x, size.y, { type: THREE.HalfFloatType, stencilBuffer: false }),
  );

  composer.addPass(new TexturePass(rtScene.texture));
  // radius=0：r180 的多级模糊链会把画面拖黑，只用最紧一级——恰好是"光源晕圈"
  const bloom = new UnrealBloomPass(size.clone(), 0.42, 0.0, 0.7);
  composer.addPass(bloom);
  const atmo = new ShaderPass(AtmoShader);
  atmo.uniforms.tDepth.value = depth;
  composer.addPass(atmo);
  const grade = new ShaderPass(GradeShader);
  composer.addPass(grade);
  composer.addPass(new OutputPass());

  return {
    composer, bloom, atmo, grade, rtScene,
    renderScene(cam) {
      renderer.setRenderTarget(rtScene);
      renderer.render(scene, cam);
      renderer.setRenderTarget(null);
    },
    setSize(w, h) {
      rtScene.setSize(w, h);
      composer.setSize(w, h);
      grade.uniforms.uRes.value.set(w, h);
    },
    beforeRender(cam) {
      atmo.uniforms.uProjInv.value.copy(cam.projectionMatrixInverse);
      atmo.uniforms.uCamWorld.value.copy(cam.matrixWorld);
      atmo.uniforms.uCamPos.value.copy(cam.position);
    },
  };
}
