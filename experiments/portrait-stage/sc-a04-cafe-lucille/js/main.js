(function () {
  const start = document.getElementById('start');
  const startBtn = document.getElementById('start-btn');
  const startBtnDocks = document.getElementById('start-btn-docks');
  const replayBtn = document.getElementById('replay');
  const againBtn = document.getElementById('again-btn');
  const soundBtn = document.getElementById('sound');
  const posesBtn = document.getElementById('poses');
  const pauseBtn = document.getElementById('pause');
  const branchBtn = document.getElementById('branch');
  const stage = document.getElementById('stage');

  const params = new URLSearchParams(location.search);
  let workedDocks = params.get('docks') === '1' || params.get('docks') === 'true';
  let begun = false;
  let muted = false;

  Show.init();
  window.__workedDocks = workedDocks;
  window.__sc_a04 = {
    Show, Cast, Audio,
    workedDocks: () => workedDocks,
  };

  function syncBranch() {
    branchBtn.textContent = workedDocks ? '分支：做过码头' : '分支：未做码头';
    branchBtn.setAttribute('aria-pressed', String(workedDocks));
    window.__workedDocks = workedDocks;
  }
  syncBranch();

  function begin(docks) {
    if (begun) return;
    if (docks !== undefined) workedDocks = docks;
    syncBranch();
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
    await new Promise(r => setTimeout(r, 920));
    Audio.init();
    Show.run(window.perform);
  }

  startBtn.addEventListener('click', () => begin(startBtn.dataset.docks === '1'));
  startBtnDocks.addEventListener('click', () => begin(startBtnDocks.dataset.docks === '1'));

  replayBtn.addEventListener('click', replay);
  againBtn.addEventListener('click', replay);

  branchBtn.addEventListener('click', () => {
    workedDocks = !workedDocks;
    syncBranch();
    if (begun) replay();
  });

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
      if (e.target === startBtn || e.target === againBtn || e.target === startBtnDocks) return;
      if (Show.advance()) e.preventDefault();
    }
    if (e.code === 'KeyP' && begun) {
      Show.togglePause();
      e.preventDefault();
    }
  });
})();
