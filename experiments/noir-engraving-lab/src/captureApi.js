export function installCaptureApi({
  profile,
  camera,
  controls,
  world,
  atmosphere,
  lighting,
  post,
  applyShot,
  render,
}) {
  const baseline = {
    fogDensity: profile.atmosphere.fogDensity,
    bloomStrength: profile.print.bloomStrength,
  };

  const modes = {
    final() {
      post.noir.uniforms.uEnabled.value = 1;
      world.groups.outlines.visible = true;
      atmosphere.setEnabled(true);
      atmosphere.setFogDensity(baseline.fogDensity);
      // 现阶段的几何光锥只保留作交互实验，不进入正式视觉基准。
      lighting.setConesVisible(false);
      post.bloom.strength = baseline.bloomStrength;
    },

    shape() {
      post.noir.uniforms.uEnabled.value = 0;
      world.groups.outlines.visible = false;
      atmosphere.setEnabled(false);
      atmosphere.setFogDensity(0);
      lighting.setConesVisible(false);
      post.bloom.strength = 0;
    },

    line() {
      post.noir.uniforms.uEnabled.value = 0;
      world.groups.outlines.visible = true;
      atmosphere.setEnabled(false);
      atmosphere.setFogDensity(0);
      lighting.setConesVisible(false);
      post.bloom.strength = 0;
    },

    preprint() {
      post.noir.uniforms.uEnabled.value = 0;
      world.groups.outlines.visible = true;
      atmosphere.setEnabled(true);
      atmosphere.setFogDensity(baseline.fogDensity);
      lighting.setConesVisible(false);
      post.bloom.strength = baseline.bloomStrength;
    },
  };

  window.__noirLab = {
    ready: true,

    setShot(index) {
      const shot = applyShot(camera, controls, index);
      render();
      return shot;
    },

    setMode(name) {
      const apply = modes[name];
      if (!apply) throw new Error(`Unknown capture mode: ${name}`);
      apply();
      render();
      return name;
    },

    render,

    info() {
      return {
        camera: {
          position: camera.position.toArray(),
          target: controls.target.toArray(),
        },
        print: {
          levels: post.noir.uniforms.uLevels.value,
          dither: post.noir.uniforms.uDither.value,
          bloom: post.bloom.strength,
        },
        fogDensity: camera.parent?.fog?.density ?? null,
      };
    },
  };
}
