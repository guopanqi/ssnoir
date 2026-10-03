import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

const GenesisComposite = {
  uniforms:{
    tDiffuse:{value:null},
    uGrain:{value:.0055},
    uVignette:{value:.12}
  },
  vertexShader:`
    varying vec2 vUv;
    void main(){vUv=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0);}
  `,
  fragmentShader:`
    uniform sampler2D tDiffuse;
    uniform float uGrain;
    uniform float uVignette;
    varying vec2 vUv;
    float hash(vec2 p){p=fract(p*vec2(123.34,456.21));p+=dot(p,p+45.32);return fract(p.x*p.y);}
    float valueNoise(vec2 p){
      vec2 i=floor(p),f=fract(p);
      f=f*f*(3.0-2.0*f);
      float a=hash(i);
      float b=hash(i+vec2(1.0,0.0));
      float c=hash(i+vec2(0.0,1.0));
      float d=hash(i+vec2(1.0,1.0));
      return mix(mix(a,b,f.x),mix(c,d,f.x),f.y);
    }
    void main(){
      vec3 c=texture2D(tDiffuse,vUv).rgb;
      float mx=max(max(c.r,c.g),c.b),mn=min(min(c.r,c.g),c.b);
      bool gold=c.r>c.b*1.45 && c.g>c.b*1.12 && (mx-mn)>.08;
      float l=dot(c,vec3(.2126,.7152,.0722));
      vec3 mono=vec3(l*1.02,l,l*.97);
      c=gold?c:mix(c,mono,.92);
      vec2 p=vUv*2.0-1.0;
      float vig=smoothstep(1.45,.25,dot(p,p));
      c*=mix(1.0-uVignette,1.0,vig);
      float paperLarge=valueNoise(gl_FragCoord.xy/96.0)-.5;
      float paperFine=valueNoise(gl_FragCoord.xy/34.0)-.5;
      float darkMask=1.0-smoothstep(.12,.46,l);
      c+=vec3((paperLarge*.012+paperFine*.004)*darkMask);
      c+=(hash(gl_FragCoord.xy*.41)-.5)*uGrain;
      gl_FragColor=vec4(c,1.0);
    }
  `
};

export function createComposer(renderer,scene,camera){
  const composer=new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene,camera));
  const composite=new ShaderPass(GenesisComposite);
  composer.addPass(composite);
  composer.addPass(new OutputPass());
  function resize(w,h,dpr){
    renderer.setPixelRatio(dpr);renderer.setSize(w,h,false);composer.setSize(w,h);
  }
  return {composer,resize,composite};
}
