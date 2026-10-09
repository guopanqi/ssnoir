// 逆光 BACKLIGHT · 后处理栈
//
//   RenderPass（MSAA HalfFloat）
//     → GodRays（自写：亮部遮罩 → 两趟径向模糊 → 叠加；GPU Gems 3 Ch.13 的 Mitchell 路数）
//     → UnrealBloomPass（借官方的，不重复造）
//     → Grade（ACES + split tone + 暗角 + 颗粒 + 色散，最后手动 sRGB）
//
// 光源在天边的热点上：中心取"沿固定世界方向 uGlowDir 走 1 km"那一点投影到屏幕，
// 相机背对热点时强度自动归 0（不做屏幕空间的假方向）。
import * as THREE from 'three';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/examples/jsm/postprocessing/ShaderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { Pass, FullScreenQuad } from 'three/examples/jsm/postprocessing/Pass.js';
import { POST, SKY } from './config.js';

const HALF = { type: THREE.HalfFloatType, magFilter: THREE.LinearFilter, minFilter: THREE.LinearFilter };

/* ------------------------------------------------------------------ GodRays */

const BRIGHT_FRAG = /* glsl */ `
varying vec2 vUv;
uniform sampler2D tDiffuse;
uniform float uThreshold;
void main() {
  vec3 c = texture2D( tDiffuse, vUv ).rgb;
  float l = max( max( c.r, c.g ), c.b );
  float b = max( l - uThreshold, 0.0 ) / max( l, 1e-4 );
  gl_FragColor = vec4( c * b, 1.0 );
}`;

const RADIAL_FRAG = /* glsl */ `
varying vec2 vUv;
uniform sampler2D tDiffuse;
uniform vec2 uCenter;
uniform float uDensity;
uniform float uDecay;
uniform float uWeight;
void main() {
  const int N = ${POST.rays.samples};
  vec2 delta = ( vUv - uCenter ) * ( uDensity / float( N ) );
  vec2 uv = vUv;
  float illum = 1.0;
  vec3 acc = vec3( 0.0 );
  for ( int i = 0; i < N; i ++ ) {
    uv -= delta;
    acc += texture2D( tDiffuse, uv ).rgb * illum;
    illum *= uDecay;
  }
  gl_FragColor = vec4( acc * uWeight, 1.0 );
}`;

const COMPOSITE_FRAG = /* glsl */ `
varying vec2 vUv;
uniform sampler2D tScene;
uniform sampler2D tRays;
uniform float uStrength;
void main() {
  vec3 s = texture2D( tScene, vUv ).rgb;
  vec3 r = texture2D( tRays, vUv ).rgb;
  gl_FragColor = vec4( s + r * uStrength, 1.0 );
}`;

class GodRaysPass extends Pass {
  constructor(camera) {
    super();
    this.camera = camera;
    this.needsSwap = true;
    const VERT = 'varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4( position.xy, 0.0, 1.0 ); }';
    const m = (frag, uniforms) => new THREE.ShaderMaterial({
      depthTest: false, depthWrite: false, vertexShader: VERT, fragmentShader: frag, uniforms,
    });
    this.matBright = m(BRIGHT_FRAG, {
      tDiffuse: { value: null }, uThreshold: { value: POST.rays.threshold },
    });
    // 18 个样本、decay 0.955 的权重和 ≈ 12.5，归一化成 1/12.5，否则径向模糊会整体过曝
    this.matRadial = m(RADIAL_FRAG, {
      tDiffuse: { value: null }, uCenter: { value: new THREE.Vector2(0.5, 0.5) },
      uDensity: { value: POST.rays.density }, uDecay: { value: POST.rays.decay },
      uWeight: { value: 1 / ((1 - Math.pow(POST.rays.decay, POST.rays.samples)) / (1 - POST.rays.decay)) },
    });
    this.matComposite = m(COMPOSITE_FRAG, {
      tScene: { value: null }, tRays: { value: null }, uStrength: { value: POST.rays.strength },
    });
    this.quad = new FullScreenQuad(this.matBright);

    this.maskRT = new THREE.WebGLRenderTarget(2, 2, HALF);
    this.rayA = new THREE.WebGLRenderTarget(2, 2, HALF);
    this.rayB = new THREE.WebGLRenderTarget(2, 2, HALF);

    this._ndc = new THREE.Vector3();
    this._world = new THREE.Vector3();
    this.glowDir = new THREE.Vector3(...SKY.glowDir).normalize();
    this.strength = POST.rays.strength;
  }

  setSize(w, h) {
    const s = POST.rays.scale;
    const W = Math.max(2, Math.round(w * s)), H = Math.max(2, Math.round(h * s));
    this.maskRT.setSize(W, H);
    this.rayA.setSize(W, H);
    this.rayB.setSize(W, H);
  }

  _blit(renderer, target, material, input) {
    if (input) material.uniforms.tDiffuse.value = input;
    this.quad.material = material;
    renderer.setRenderTarget(target);
    renderer.clear();
    this.quad.render(renderer);
  }

  render(renderer, writeBuffer, readBuffer) {
    // 光源中心：固定世界方向投影到屏幕
    const cam = this.camera;
    this._world.copy(cam.position).addScaledVector(this.glowDir, 1000);
    this._ndc.copy(this._world).project(cam);
    const behind = this._ndc.z > 1.0;
    this.matRadial.uniforms.uCenter.value.set(this._ndc.x * 0.5 + 0.5, this._ndc.y * 0.5 + 0.5);
    const w = behind ? 0 : this.strength;
    this.matComposite.uniforms.uStrength.value = w;
    if (w <= 0.0001) {
      renderer.setRenderTarget(this.renderToScreen ? null : writeBuffer);
      renderer.clear();
      this.quad.material = this.matComposite;
      this.matComposite.uniforms.tScene.value = readBuffer.texture;
      this.matComposite.uniforms.tRays.value = this.rayB.texture;
      this.quad.render(renderer);
      return;
    }

    this._blit(renderer, this.maskRT, this.matBright, readBuffer.texture);
    this._blit(renderer, this.rayA, this.matRadial, this.maskRT.texture);
    this.matRadial.uniforms.uDensity.value = POST.rays.density * 0.45;
    this._blit(renderer, this.rayB, this.matRadial, this.rayA.texture);
    this.matRadial.uniforms.uDensity.value = POST.rays.density;

    this.quad.material = this.matComposite;
    this.matComposite.uniforms.tScene.value = readBuffer.texture;
    this.matComposite.uniforms.tRays.value = this.rayB.texture;
    renderer.setRenderTarget(this.renderToScreen ? null : writeBuffer);
    renderer.clear();
    this.quad.render(renderer);
  }

  dispose() {
    this.maskRT.dispose(); this.rayA.dispose(); this.rayB.dispose();
    this.matBright.dispose(); this.matRadial.dispose(); this.matComposite.dispose();
    this.quad.dispose();
  }
}

/* -------------------------------------------------------------------- Grade */

const GRADE_FRAG = /* glsl */ `
varying vec2 vUv;
uniform sampler2D tDiffuse;
uniform float uExposure;
uniform vec3 uShadowTint;
uniform vec3 uHighTint;
uniform float uDesat;
uniform float uVignette;
uniform float uGrain;
uniform float uAberration;
uniform float uBlackPoint;
uniform float uMix;
uniform float uSeed;
uniform vec2 uRes;

float ign( vec2 p ) {
  return fract( 52.9829189 * fract( 0.06711056 * p.x + 0.00583715 * p.y ) );
}
vec3 aces( vec3 x ) {
  return clamp( ( x * ( 2.51 * x + 0.03 ) ) / ( x * ( 2.43 * x + 0.59 ) + 0.14 ), 0.0, 1.0 );
}
vec3 toSRGB( vec3 c ) {
  return mix( c * 12.92, 1.055 * pow( max( c, vec3( 0.0 ) ), vec3( 1.0 / 2.4 ) ) - 0.055, step( vec3( 0.0031308 ), c ) );
}

void main() {
  vec2 d = vUv - 0.5;
  float r2 = dot( d, d );
  // 轻微色散：只在画面外缘
  vec3 c;
  c.r = texture2D( tDiffuse, vUv + d * uAberration * r2 * 4.0 ).r;
  c.g = texture2D( tDiffuse, vUv ).g;
  c.b = texture2D( tDiffuse, vUv - d * uAberration * r2 * 4.0 ).b;

  c *= uExposure;
  c = aces( c );
  c = max( c - uBlackPoint, 0.0 ) / max( 1.0 - uBlackPoint, 1e-4 );

  float l = dot( c, vec3( 0.2126, 0.7152, 0.0722 ) );
  c = mix( c * uShadowTint, c * uHighTint, smoothstep( 0.0, 1.0, l ) );
  c = mix( vec3( dot( c, vec3( 0.2126, 0.7152, 0.0722 ) ) ), c, 1.0 - uDesat );
  c *= 1.0 - uVignette * smoothstep( 0.34, 1.06, length( d ) * 1.62 );
  c *= uMix;

  c = toSRGB( max( c, 0.0 ) );
  float g = ign( gl_FragCoord.xy + uSeed ) - 0.5;
  c += g * uGrain;
  gl_FragColor = vec4( c, 1.0 );
}`;

export function createPost(renderer, scene, camera) {
  const size = renderer.getSize(new THREE.Vector2());
  const target = new THREE.WebGLRenderTarget(size.x, size.y, { ...HALF, samples: 4 });
  const composer = new EffectComposer(renderer, target);
  const renderPass = new RenderPass(scene, camera);
  const rays = new GodRaysPass(camera);
  const bloom = new UnrealBloomPass(new THREE.Vector2(size.x, size.y), POST.bloom.strength, POST.bloom.radius, POST.bloom.threshold);
  const grade = new ShaderPass({
    uniforms: {
      tDiffuse: { value: null },
      uExposure: { value: POST.exposure },
      uShadowTint: { value: new THREE.Vector3(...POST.grade.shadowTint) },
      uHighTint: { value: new THREE.Vector3(...POST.grade.highTint) },
      uDesat: { value: POST.grade.desat },
      uVignette: { value: POST.grade.vignette },
      uGrain: { value: POST.grade.grain },
      uAberration: { value: POST.grade.aberration },
      uBlackPoint: { value: POST.grade.blackPoint },
      uMix: { value: 1.0 },
      uSeed: { value: 0.0 },
      uRes: { value: new THREE.Vector2(size.x, size.y) },
    },
    vertexShader: 'varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4( position.xy, 0.0, 1.0 ); }',
    fragmentShader: GRADE_FRAG,
  });

  composer.addPass(renderPass);
  composer.addPass(rays);
  composer.addPass(bloom);
  composer.addPass(grade);

  return {
    composer, renderPass, rays, bloom, grade,
    setSize(w, h) {
      composer.setSize(w, h);
      rays.setSize(w, h);
      grade.uniforms.uRes.value.set(w, h);
    },
    setExposure(v) { grade.uniforms.uExposure.value = v; },
    setSeed(v) { grade.uniforms.uSeed.value = v; },
  };
}
