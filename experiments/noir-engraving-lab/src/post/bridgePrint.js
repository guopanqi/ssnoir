import * as T from 'three';
import {ShaderPass} from 'three/addons/postprocessing/ShaderPass.js';

// 桥头与住所共用同一明暗映射，避免两个实验的风格参数漂移。
export function createBridgePrint(){
return new ShaderPass({
  uniforms:{tDiffuse:{value:null},uMode:{value:1},uResolution:{value:new T.Vector2()}},
  vertexShader:`varying vec2 vUv;void main(){vUv=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,
  fragmentShader:`
    uniform sampler2D tDiffuse;uniform float uMode;uniform vec2 uResolution;varying vec2 vUv;
    void main(){
      vec3 rgb=texture2D(tDiffuse,vUv).rgb;
      if(uMode<.5){gl_FragColor=vec4(rgb,1.);return;}
      float l=dot(rgb,vec3(.2126,.7152,.0722));
      float tone=smoothstep(.08,.76,l);
      vec3 black=vec3(.065,.082,.105),mid=vec3(.28,.34,.40),silver=vec3(.82,.87,.89);
      // 暗部收成剪影，亮部保留有面积的银白；线和实体亮面共同表达。
      vec3 col=mix(black,mid,smoothstep(.06,.38,tone));
      col=mix(col,silver,smoothstep(.62,.92,tone));
      // 刻线只落在中间调，不向纯黑和银白亮面铺噪声。
      if(uMode>1.5){
        vec2 px=vUv*uResolution;
        float hatch=1.-smoothstep(.65,1.3,abs(mod(px.x+px.y*.55,6.)-3.));
        float mask=smoothstep(.06,.12,tone)*(1.-smoothstep(.29,.40,tone));
        col=mix(col,black,hatch*mask*.35);
      }
      col+=vec3(.024,.031,.04)*smoothstep(.45,1.,vUv.y)*(1.-tone);
      gl_FragColor=vec4(col,1.);
    }`,
});}
