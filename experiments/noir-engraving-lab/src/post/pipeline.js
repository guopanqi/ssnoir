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

  // 先完成 Three.js 的 tone mapping 与输出色彩变换。Noir Print 是一个
  // display-referred 效果，和 FXAA 一样应当读取已经转换到显示空间的图像。
  composer.addPass(new OutputPass());

  // Noir Print 是最终屏幕层：不再做第二次 tone mapping / gamma 转换。
  const noir = new ShaderPass(createNoirShader(profile));
  composer.addPass(noir);

  function resize(width, height) {
    composer.setSize(width, height);
    noir.uniforms.uResolution.value.set(width, height);
  }

  return { composer, bloom, noir, resize };
}
