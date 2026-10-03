import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';

const FinishShader={
  uniforms:{
    tDiffuse:{value:null},
    uEnabled:{value:1},
    uGrain:{value:.032},
    uVignette:{value:.14},
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

      float n=hash(gl_FragCoord.xy)-.5;
      float l=dot(c,vec3(.2126,.7152,.0722));
      float mid=smoothstep(.06,.22,l)*(1.0-smoothstep(.62,.9,l));
      c += n*uGrain*(.45+.55*mid);

      vec2 p=vUv-.5;
      float vig=smoothstep(.82,.18,dot(p,p)*1.9);
      c*=mix(1.0-uVignette,1.0,vig);

      // Keep the blue-black base; do not crush intermediate values or posterize.
      c=pow(max(c,vec3(0.0)),vec3(.94));
      gl_FragColor=vec4(clamp(c,0.0,1.0),1.0);
    }
  `,
};

export function createPost(renderer,scene,camera,profile){
  const composer=new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene,camera));

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

  return {
    composer,bloom,finish,
    resize(w,h){composer.setSize(w,h);bloom.setSize(w,h);},
  };
}
