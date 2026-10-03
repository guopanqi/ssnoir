function setFx(world, visible) {
  world.groups.accents.visible = visible;
  world.groups.haze.visible = visible;
}

export function installCaptureApi({ profile, world, post, camera, controls, applyShot, render }) {
  const bloomStrength = profile.print.bloomStrength;

  const modes = {
    shape() {
      world.groups.lines.visible = false;
      world.groups.windows.visible = false;
      setFx(world, false);
      post.bloom.strength = 0;
      post.graphic.uniforms.uEnabled.value = 0;
    },
    line() {
      world.groups.lines.visible = true;
      world.groups.windows.visible = true;
      setFx(world, false);
      post.bloom.strength = 0;
      post.graphic.uniforms.uEnabled.value = 0;
    },
    accent() {
      world.groups.lines.visible = true;
      world.groups.windows.visible = true;
      setFx(world, true);
      post.bloom.strength = bloomStrength;
      post.graphic.uniforms.uEnabled.value = 0;
    },
    final() {
      world.groups.lines.visible = true;
      world.groups.windows.visible = true;
      setFx(world, true);
      post.bloom.strength = bloomStrength;
      post.graphic.uniforms.uEnabled.value = 1;
    },
  };

  window.__graphicCityLab = {
    ready: true,
    setShot(index) {
      const shot = applyShot(camera, controls, index);
      render();
      return shot;
    },
    setMode(name) {
      const fn = modes[name];
      if (!fn) throw new Error(`Unknown mode: ${name}`);
      fn();
      render();
      return name;
    },
    render,
    info() {
      return {
        profile,
        camera: {
          position: camera.position.toArray(),
          target: controls.target.toArray(),
        },
        lineVisible: world.groups.lines.visible,
        windowsVisible: world.groups.windows.visible,
        fxVisible: world.groups.accents.visible,
        bloom: post.bloom.strength,
        printEnabled: post.graphic.uniforms.uEnabled.value > 0.5,
      };
    },
  };
}
