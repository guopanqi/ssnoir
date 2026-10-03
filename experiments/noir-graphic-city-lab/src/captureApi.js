function setTaggedAccentVisible(world, visible) {
  world.groups.accents.visible = visible;
  for (const group of [world.groups.city, world.groups.characters]) {
    group.traverse((object) => {
      if (object.userData?.graphicAccent) object.visible = visible;
    });
  }
}

export function installCaptureApi({ profile, world, post, camera, controls, applyShot, render }) {
  const modes = {
    shape() {
      world.groups.lines.visible = false;
      setTaggedAccentVisible(world, false);
      post.graphic.uniforms.uEnabled.value = 0;
    },
    line() {
      world.groups.lines.visible = true;
      setTaggedAccentVisible(world, false);
      post.graphic.uniforms.uEnabled.value = 0;
    },
    accent() {
      world.groups.lines.visible = true;
      setTaggedAccentVisible(world, true);
      post.graphic.uniforms.uEnabled.value = 0;
    },
    final() {
      world.groups.lines.visible = true;
      setTaggedAccentVisible(world, true);
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
