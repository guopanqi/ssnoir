import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const GraphicPrintShader = {
  uniforms: {
    tDiffuse: { value: null },
    uEnabled: { value: 1 },
    uLevels: { value: 4 },
    uInBlack: { value: 0.01 },
    uInWhite: { value: 0.82 },
    uGamma: { value: 0.88 },
    uGrain: { value: 0.02 },
    uVignette: { value: 0.23 },
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
    uniform float uLevels;
    uniform float uInBlack;
    uniform float uInWhite;
    uniform float uGamma;
    uniform float uGrain;
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
      l = floor(l * (uLevels - 1.0) + 0.5) / (uLevels - 1.0);

      vec3 ink = vec3(0.012, 0.013, 0.014);
      vec3 paper = vec3(0.91, 0.875, 0.79);
      vec3 mono = mix(ink, paper, l);

      float goldSignal = smoothstep(0.08, 0.24, src.r - src.b) * smoothstep(0.18, 0.48, src.r);
      vec3 gold = vec3(0.83, 0.56, 0.18) * (0.72 + 0.5 * l);
      vec3 col = mix(mono, gold, goldSignal);

      float grain = (hash(gl_FragCoord.xy) - 0.5) * uGrain;
      col += grain;

      vec2 p = vUv - 0.5;
      float vig = smoothstep(0.82, 0.28, dot(p, p) * 1.9);
      col *= mix(1.0 - uVignette, 1.0, vig);

      gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
    }
  `,
};

export function createPost(renderer, scene, camera, profile) {
  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene, camera));
  const graphic = new ShaderPass(GraphicPrintShader);
  graphic.uniforms.uLevels.value = profile.print.levels;
  graphic.uniforms.uInBlack.value = profile.print.inBlack;
  graphic.uniforms.uInWhite.value = profile.print.inWhite;
  graphic.uniforms.uGamma.value = profile.print.gamma;
  graphic.uniforms.uGrain.value = profile.print.grain;
  graphic.uniforms.uVignette.value = profile.print.vignette;
  composer.addPass(graphic);

  return {
    composer,
    graphic,
    resize(width, height) {
      composer.setSize(width, height);
    },
  };
}
