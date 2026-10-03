import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { OutlinePass } from 'three/addons/postprocessing/OutlinePass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { FXAAShader } from 'three/addons/shaders/FXAAShader.js';

const FinishShader={
  uniforms:{
    tDiffuse:{value:null},
    uEnabled:{value:1},
    uGrain:{value:.026},
    uVignette:{value:.12},
  },
  vertexShader:`
    varying vec2 vUv;
    void main(){vUv=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0);}
  `,
  fragmentShader:`
    uniform sampler2D tDiffuse;
    uniform float uEnabled;
    uniform float uGrain;
    uniform float uVignette;
    varying vec2 vUv;

    float hash(vec2 p){
      p=fract(p*vec2(123.34,456.21));
      p+=dot(p,p+45.32);
      return fract(p.x*p.y);
    }

    void main(){
      vec3 c=texture2D(tDiffuse,vUv).rgb;
      if(uEnabled<.5){gl_FragColor=vec4(c,1.0);return;}

      float l=dot(c,vec3(.2126,.7152,.0722));
      float mid=smoothstep(.045,.18,l)*(1.0-smoothstep(.62,.88,l));
      float fine=(hash(gl_FragCoord.xy)-.5)*uGrain;
      float broad=(hash(floor(gl_FragCoord.xy/7.0))-.5)*.010;
      c += fine*(.35+.65*mid)+broad*mid;

      vec2 p=vUv-.5;
      float vig=smoothstep(.82,.18,dot(p,p)*1.9);
      c*=mix(1.0-uVignette,1.0,vig);

      c=pow(max(c,vec3(0.0)),vec3(.95));
      gl_FragColor=vec4(clamp(c,0.0,1.0),1.0);
    }
  `,
};

export function createPost(renderer,scene,camera,profile,outlinedObjects=[]){
  const composer=new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene,camera));

  const outline=new OutlinePass(
    new THREE.Vector2(1,1),
    scene,
    camera,
    outlinedObjects,
  );
  outline.edgeStrength=2.25;
  outline.edgeGlow=0;
  outline.edgeThickness=1.0;
  outline.pulsePeriod=0;
  outline.downSampleRatio=1;
  outline.visibleEdgeColor.set(profile.palette.line);
  outline.hiddenEdgeColor.set(profile.palette.background);
  composer.addPass(outline);

  const bloom=new UnrealBloomPass(
    new THREE.Vector2(1,1),
    profile.atmosphere.bloomStrength,
    profile.atmosphere.bloomRadius,
    profile.atmosphere.bloomThreshold,
  );
  composer.addPass(bloom);

  const finish=new ShaderPass(FinishShader);
  finish.uniforms.uGrain.value=profile.atmosphere.grain;
  finish.uniforms.uVignette.value=profile.atmosphere.vignette;
  composer.addPass(finish);

  const fxaa=new ShaderPass(FXAAShader);
  composer.addPass(fxaa);

  return {
    composer,outline,bloom,finish,fxaa,
    resize(w,h){
      composer.setSize(w,h);
      outline.setSize(w,h);
      bloom.setSize(w,h);
      fxaa.material.uniforms.resolution.value.set(1/w,1/h);
    },
  };
}
