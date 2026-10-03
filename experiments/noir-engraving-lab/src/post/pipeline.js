import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { createNoirShader } from './noirShader.js';

export function createPost(renderer, scene, camera, profile) {
  const p = profile.print;

  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene, camera));

  const bloom = new UnrealBloomPass(
    new THREE.Vector2(innerWidth, innerHeight),
    p.bloomStrength,
    p.bloomRadius,
    p.bloomThreshold,
  );
  composer.addPass(bloom);

  const noir = new ShaderPass(createNoirShader(profile));
  composer.addPass(noir);

  // Output transform 必须最后执行。NoirShader 自己负责
  // “线性渲染结果 ↔ 显示值”的往返，只让 OutputPass 做最终输出。
  composer.addPass(new OutputPass());

  function resize(width, height) {
    composer.setSize(width, height);
    noir.uniforms.uResolution.value.set(width, height);
  }

  return { composer, bloom, noir, resize };
}
