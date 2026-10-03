import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';

const GenesisPrintShader = {
  uniforms: {
    tDiffuse: { value: null },
    uEnabled: { value: 1 },
    uInBlack: { value: 0.006 },
    uInWhite: { value: 0.52 },
    uGamma: { value: 0.88 },
    uGrain: { value: 0.062 },
    uHatch: { value: 0.075 },
    uVignette: { value: 0.16 },
  },
  vertexShader: `
    varying vec2 vUv;
    void main() {
      vUv = uv;
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    }
  `,
  fragmentShader: `
    uniform sampler2D tDiffuse;
    uniform float uEnabled;
    uniform float uInBlack;
    uniform float uInWhite;
    uniform float uGamma;
    uniform float uGrain;
    uniform float uHatch;
    uniform float uVignette;
    varying vec2 vUv;

    float hash(vec2 p) {
      p = fract(p * vec2(123.34, 456.21));
      p += dot(p, p + 45.32);
      return fract(p.x * p.y);
    }

    void main() {
      vec3 src = texture2D(tDiffuse, vUv).rgb;
      if (uEnabled < 0.5) {
        gl_FragColor = vec4(src, 1.0);
        return;
      }

      float l = dot(src, vec3(0.2126, 0.7152, 0.0722));
      l = clamp((l - uInBlack) / max(0.0001, uInWhite - uInBlack), 0.0, 1.0);
      l = pow(l, uGamma);

      vec3 ink = vec3(0.026, 0.038, 0.075);
      vec3 paper = vec3(0.95, 0.945, 0.89);
      vec3 col = mix(ink, paper, l);

      float n = hash(gl_FragCoord.xy);
      float grain = (n - 0.5) * uGrain;
      float midMask = smoothstep(0.10, 0.34, l) * (1.0 - smoothstep(0.72, 0.92, l));
      float h1 = sin((gl_FragCoord.x + gl_FragCoord.y * 0.72) * 0.34 + n * 2.0);
      float h2 = sin((gl_FragCoord.x * 0.31 - gl_FragCoord.y * 0.44) * 0.23);
      float hatch = (step(0.86, h1) + step(0.90, h2)) * uHatch * midMask;
      float scratchSeed = hash(vec2(floor(gl_FragCoord.x * 0.18), floor(gl_FragCoord.y * 0.012)));
      float scratch = step(0.985, scratchSeed) * 0.055 * midMask;
      float speck = (step(0.994, n) - step(n, 0.006)) * 0.085;

      col += grain + scratch + speck;
      col -= hatch;

      vec2 p = vUv - 0.5;
      float vig = smoothstep(0.82, 0.22, dot(p, p) * 1.8);
      col *= mix(1.0 - uVignette, 1.0, vig);

      gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
    }
  `,
};

export function createPost(renderer, scene, camera, profile) {
  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene, camera));

  const bloom = new UnrealBloomPass(
    new THREE.Vector2(1, 1),
    profile.print.bloomStrength,
    profile.print.bloomRadius,
    profile.print.bloomThreshold,
  );
  composer.addPass(bloom);

  const graphic = new ShaderPass(GenesisPrintShader);
  graphic.uniforms.uInBlack.value = profile.print.inBlack;
  graphic.uniforms.uInWhite.value = profile.print.inWhite;
  graphic.uniforms.uGamma.value = profile.print.gamma;
  graphic.uniforms.uGrain.value = profile.print.grain;
  graphic.uniforms.uHatch.value = profile.print.hatch;
  graphic.uniforms.uVignette.value = profile.print.vignette;
  composer.addPass(graphic);

  return {
    composer,
    bloom,
    graphic,
    resize(width, height) {
      composer.setSize(width, height);
      bloom.setSize(width, height);
    },
  };
}
