import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

const NoirCompositeShader = {
  uniforms: {
    tDiffuse: { value: null },
    uResolution: { value: new THREE.Vector2(1,1) },
    uGrain: { value: 0.018 },
    uVignette: { value: 0.32 },
    uPosterize: { value: 7.0 }
  },
  vertexShader: `
    varying vec2 vUv;
    void main() {
      vUv = uv;
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0);
    }
  `,
  fragmentShader: `
    uniform sampler2D tDiffuse;
    uniform vec2 uResolution;
    uniform float uGrain;
    uniform float uVignette;
    uniform float uPosterize;
    varying vec2 vUv;

    float hash(vec2 p) {
      p = fract(p * vec2(123.34, 456.21));
      p += dot(p, p + 45.32);
      return fract(p.x * p.y);
    }

    void main() {
      vec3 c = texture2D(tDiffuse, vUv).rgb;

      float mx = max(max(c.r,c.g),c.b);
      float mn = min(min(c.r,c.g),c.b);
      float sat = mx - mn;
      bool gold = c.r > c.b * 1.35 && c.r > c.g * 1.05 && sat > 0.08;

      float l = dot(c, vec3(0.2126,0.7152,0.0722));
      l = floor(l * uPosterize + 0.5) / uPosterize;
      vec3 mono = vec3(l * 1.015, l, l * 0.965);
      c = gold ? c : mix(c, mono, 0.82);

      // Sparse engraved hatching lives only in middle/dark value groups.
      // It is screen-space on purpose: the geometry stays clean while the
      // final frame inherits a consistent illustrated medium.
      float d1 = abs(fract((gl_FragCoord.x + gl_FragCoord.y * 0.92) / 11.0) - 0.5);
      float d2 = abs(fract((gl_FragCoord.x - gl_FragCoord.y * 0.68) / 16.0) - 0.5);
      float hatch1 = 1.0 - smoothstep(0.035, 0.095, d1);
      float hatch2 = 1.0 - smoothstep(0.025, 0.075, d2);
      float midInk = smoothstep(0.12, 0.23, l) * (1.0 - smoothstep(0.39, 0.54, l));
      float deepInk = smoothstep(0.045, 0.11, l) * (1.0 - smoothstep(0.22, 0.32, l));
      float inkMask = gold ? 0.0 : 1.0;
      c -= vec3((hatch1 * midInk * 0.034 + hatch2 * deepInk * 0.024) * inkMask);

      vec2 p = vUv * 2.0 - 1.0;
      float vig = smoothstep(1.35, 0.30, dot(p,p));
      c *= mix(1.0 - uVignette, 1.0, vig);

      float paper = hash(gl_FragCoord.xy * 0.41) - 0.5;
      float scratch = step(0.996, hash(vec2(gl_FragCoord.y * 0.12, floor(gl_FragCoord.x / 3.0))));
      c += paper * uGrain;
      c += scratch * 0.025;

      gl_FragColor = vec4(c,1.0);
    }
  `
};

export function createComposer(renderer, scene, camera) {
  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene, camera));
  const composite = new ShaderPass(NoirCompositeShader);
  composer.addPass(composite);
  composer.addPass(new OutputPass());

  function resize(w,h,dpr) {
    renderer.setPixelRatio(dpr);
    renderer.setSize(w,h,false);
    composer.setSize(w,h);
    composite.uniforms.uResolution.value.set(w*dpr,h*dpr);
  }

  return {composer, resize, composite};
}
