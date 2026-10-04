// 面板与键盘。1-4 机位 · Space 暂停 · H 隐藏面板。

export function installUI({ applyShot, toggles }) {
  const shotLabel = document.getElementById('shotLabel');
  const panel = document.getElementById('panel');
  const fpsEl = document.getElementById('fps');

  const keymap = {
    Digit1: 0, Digit2: 1, Digit3: 2, Digit4: 3,
  };

  let paused = false;

  window.addEventListener('keydown', (e) => {
    if (e.code in keymap) {
      const shot = applyShot(keymap[e.code]);
      if (shotLabel) shotLabel.textContent = shot.name;
    } else if (e.code === 'KeyH') {
      if (panel) panel.hidden = !panel.hidden;
    } else if (e.code === 'Space') {
      paused = !paused;
      e.preventDefault();
      window.dispatchEvent(new CustomEvent('skyline-toggle-pause', { detail: paused }));
    }
  });

  for (const [id, apply] of Object.entries(toggles)) {
    const el = document.getElementById(id);
    if (!el) continue;
    el.addEventListener('change', () => apply(el.checked));
  }

  const shot = applyShot(0);
  if (shotLabel) shotLabel.textContent = shot.name;

  return {
    setFps(v) {
      if (fpsEl) fpsEl.textContent = `${v.toFixed(0)} fps`;
    },
    isPaused: () => paused,
  };
}
