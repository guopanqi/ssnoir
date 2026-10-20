(function () {
  const start = document.getElementById('start');
  const startBtn = document.getElementById('start-btn');
  const replayBtn = document.getElementById('replay');
  const againBtn = document.getElementById('again-btn');
  const soundBtn = document.getElementById('sound');
  const posesBtn = document.getElementById('poses');
  const pauseBtn = document.getElementById('pause');
  const stage = document.getElementById('stage');

  let begun = false;
  let muted = false;

  Show.init();
  window.__sc_a02 = { Show, Cast, Audio };

  function begin() {
    if (begun) return;
    begun = true;
    start.classList.add('gone');
    Audio.init();
    Show.run(window.perform);
  }

  async function replay() {
    if (!begun) {
      begin();
      return;
    }
    Audio.stopAll();
    Show.reset();
    pauseBtn.textContent = '暂停';
    await new Promise(r => setTimeout(r, 980));
    Audio.init();
    Show.run(window.perform);
  }

  startBtn.addEventListener('click', begin);

  replayBtn.addEventListener('click', replay);
  againBtn.addEventListener('click', replay);

  soundBtn.addEventListener('click', () => {
    muted = !muted;
    Audio.setMuted(muted);
    soundBtn.textContent = muted ? '声音 关' : '声音 开';
    soundBtn.classList.toggle('on', !muted);
    soundBtn.setAttribute('aria-pressed', String(!muted));
  });

  posesBtn.addEventListener('click', () => {
    const on = posesBtn.getAttribute('aria-pressed') !== 'true';
    Show.setHud(on);
  });

  pauseBtn.addEventListener('click', () => {
    if (!begun) return;
    Show.togglePause();
  });

  stage.addEventListener('click', () => {
    if (!begun) return;
    Show.advance();
  });

  addEventListener('keydown', e => {
    if (e.code === 'Space' || e.code === 'Enter') {
      if (e.target === startBtn || e.target === againBtn) return;
      if (Show.advance()) e.preventDefault();
    }
    if (e.code === 'KeyP' && begun) {
      Show.togglePause();
      e.preventDefault();
    }
  });
})();
