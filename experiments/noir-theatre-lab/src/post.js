import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const vertexShader = /* glsl */`
  varying vec2 vUv;
  void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }
`;

const fragmentShader = /* glsl */`
  uniform sampler2D tDiffuse;
  uniform vec2 uResolution;
  uniform float uLineStrength;
  uniform float uLineThreshold;
  uniform float uLevels;
  uniform float uHatchStrength;
  uniform float uGrain;
  uniform float uVignette;
  uniform float uMode;
  varying vec2 vUv;

  float luma(vec3 c){ return dot(c, vec3(.2126,.7152,.0722)); }
  float hash21(vec2 p){ p=fract(p*vec2(123.34,456.21)); p+=dot(p,p+45.32); return fract(p.x*p.y); }

  void main(){
    vec2 px = 1.0 / uResolution;
    vec3 c = texture2D(tDiffuse, vUv).rgb;
    float y = luma(c);
    float gx = luma(texture2D(tDiffuse, vUv + vec2(px.x,0.)).rgb) - luma(texture2D(tDiffuse, vUv - vec2(px.x,0.)).rgb);
    float gy = luma(texture2D(tDiffuse, vUv + vec2(0.,px.y)).rgb) - luma(texture2D(tDiffuse, vUv - vec2(0.,px.y)).rgb);
    float edge = smoothstep(uLineThreshold, uLineThreshold * 2.7, length(vec2(gx,gy)));

    if (uMode < .5) { gl_FragColor = vec4(c,1.); return; }

    if (uMode > 1.5) {
      float lev = max(2.0, uLevels);
      y = floor(y * (lev - 1.0) + .5) / (lev - 1.0);
      c *= (0.82 + 0.18 * y);
      c = mix(vec3(y), c, 0.22);
    }

    if (uMode > .5) {
      float selective = edge * smoothstep(.025,.16,y) * (1.0 - smoothstep(.82,.98,y));
      c *= 1.0 - selective * uLineStrength;
    }

    if (uMode > 2.5) {
      vec2 p = gl_FragCoord.xy;
      float d1 = abs(fract((p.x + p.y) / 9.0) - .5);
      float d2 = abs(fract((p.x - p.y) / 13.0) - .5);
      float hatchA = 1.0 - smoothstep(.035,.12,d1);
      float hatchB = 1.0 - smoothstep(.035,.12,d2);
      float shadowA = 1.0 - smoothstep(.12,.38,y);
      float shadowB = 1.0 - smoothstep(.055,.20,y);
      float hatch = max(hatchA * shadowA, hatchB * shadowB);
      c *= 1.0 - hatch * uHatchStrength;
      float grain = (hash21(gl_FragCoord.xy) - .5) * uGrain;
      c += grain;
      float v = 1.0 - smoothstep(.28,.82,length(vUv-.5));
      c *= mix(1.0-uVignette,1.0,v);
    }

    gl_FragColor = vec4(max(c,0.0),1.0);
  }
`;

export function createPost(renderer, scene, camera, profile) {
  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene, camera));
  const pass = new ShaderPass({
    uniforms: {
      tDiffuse: { value: null },
      uResolution: { value: new THREE.Vector2(1,1) },
      uLineStrength: { value: profile.line.strength },
      uLineThreshold: { value: profile.line.threshold },
      uLevels: { value: profile.print.levels },
      uHatchStrength: { value: profile.print.hatchStrength },
      uGrain: { value: profile.print.grain },
      uVignette: { value: profile.print.vignette },
      uMode: { value: 3 },
    },
    vertexShader,
    fragmentShader,
  });
  composer.addPass(pass);

  return {
    composer,
    pass,
    resize(w,h){ composer.setSize(w,h); pass.uniforms.uResolution.value.set(w,h); },
    setMode(name){ pass.uniforms.uMode.value = ({shape:0, light:0, line:1, final:3})[name] ?? 3; },
  };
}
