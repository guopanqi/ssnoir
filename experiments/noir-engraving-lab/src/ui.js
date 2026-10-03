export function bindResearchUI({ post, world, atmosphere, lighting, onShot }) {
  const ui = {
    panel: document.querySelector('#panel'),
    shotLabel: document.querySelector('#shotLabel'),
    stylize: document.querySelector('#stylize'),
    outlines: document.querySelector('#outlines'),
    atmosphere: document.querySelector('#atmosphere'),
    lightCones: document.querySelector('#lightCones'),
    levels: document.querySelector('#levels'),
    levelsValue: document.querySelector('#levelsValue'),
    dither: document.querySelector('#dither'),
    ditherValue: document.querySelector('#ditherValue'),
    fog: document.querySelector('#fog'),
    fogValue: document.querySelector('#fogValue'),
    bloom: document.querySelector('#bloom'),
    bloomValue: document.querySelector('#bloomValue'),
    fps: document.querySelector('#fps'),
  };

  ui.stylize.addEventListener('change', () => {
    post.noir.uniforms.uEnabled.value = ui.stylize.checked ? 1 : 0;
  });

  ui.outlines.addEventListener('change', () => {
    world.groups.outlines.visible = ui.outlines.checked;
  });

  ui.atmosphere.addEventListener('change', () => {
    atmosphere.setEnabled(ui.atmosphere.checked);
  });

  ui.lightCones.addEventListener('change', () => {
    lighting.setConesVisible(ui.lightCones.checked);
  });

  ui.levels.addEventListener('input', () => {
    post.noir.uniforms.uLevels.value = +ui.levels.value;
    ui.levelsValue.value = ui.levels.value;
  });

  ui.dither.addEventListener('input', () => {
    post.noir.uniforms.uDither.value = +ui.dither.value;
    ui.ditherValue.value = (+ui.dither.value).toFixed(2);
  });

  ui.fog.addEventListener('input', () => {
    atmosphere.group.parent.fog.density = +ui.fog.value;
    ui.fogValue.value = (+ui.fog.value).toFixed(3);
  });

  ui.bloom.addEventListener('input', () => {
    post.bloom.strength = +ui.bloom.value;
    ui.bloomValue.value = (+ui.bloom.value).toFixed(2);
  });

  function selectShot(index) {
    const shot = onShot(index);
    ui.shotLabel.textContent = shot.name;
  }

  window.addEventListener('keydown', (event) => {
    if (event.key >= '1' && event.key <= '4') {
      selectShot(+event.key - 1);
    }

    if (event.code === 'Space') {
      event.preventDefault();
      atmosphere.setEnabled(!atmosphere.enabled);
      ui.atmosphere.checked = atmosphere.enabled;
    }

    if (event.key.toLowerCase() === 'h') {
      ui.panel.classList.toggle('is-hidden');
    }
  });

  return {
    fps: ui.fps,
    selectShot,
  };
}
