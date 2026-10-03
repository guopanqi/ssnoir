export function installCaptureApi({ profile, world, post, camera, controls, applyShot, render }) {
  const modes = {
    shape() {
      world.groups.lines.visible = false;
      world.groups.accents.visible = false;
      post.graphic.uniforms.uEnabled.value = 0;
    },
    line() {
      world.groups.lines.visible = true;
      world.groups.accents.visible = false;
      post.graphic.uniforms.uEnabled.value = 0;
    },
    accent() {
      world.groups.lines.visible = true;
      world.groups.accents.visible = true;
      post.graphic.uniforms.uEnabled.value = 0;
    },
    final() {
      world.groups.lines.visible = true;
      world.groups.accents.visible = true;
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
        accentVisible: world.groups.accents.visible,
        printEnabled: post.graphic.uniforms.uEnabled.value > 0.5,
      };
    },
  };
}
