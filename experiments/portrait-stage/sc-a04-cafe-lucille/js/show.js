/* 播放层：时间、对白、镜头、字幕和姿势检查。
   所有等待都检查 runToken，重播时旧时间线自动失效。 */
(function () {
  const CANCEL = {};
  const NAME = {
    lu: ['露西', '#e2a58d'],
    nl: ['尼尔', '#f0cf8a'],
  };

  const VIEW = {
    wide: { x: 800, y: 450, s: 1 },
    counter: { x: 965, y: 540, s: 1.55 },
    cup: { x: 955, y: 545, s: 2.0 },
    end: { x: 820, y: 470, s: 1.08 },
  };
  const CAMERA_LABEL = {
    wide: '全景', counter: '柜台', cup: '杯子近景', end: '收场', custom: '自定义',
  };

  let stage, camera, caption, speaker, lineEl, slate, endCard, hint;
  let runToken = 0;
  let waiting = false;
  let advanceFn = null;
  let paused = false;
  let activeView = 'wide';
  let hudOn = false;

  function init() {
    stage = document.getElementById('stage');
    camera = document.getElementById('camera');
    caption = document.getElementById('caption');
    speaker = document.getElementById('speaker');
    lineEl = document.getElementById('line');
    slate = document.getElementById('slate');
    endCard = document.getElementById('end-card');
    hint = document.getElementById('hint');

    camera.innerHTML = Art.build();
    Cast.buildInto(document.getElementById('castLu'), 'lu');
    Cast.buildInto(document.getElementById('castNl'), 'nl');
    Cast.onReport((id, st) => {
      if (!hudOn) return;
      const el = document.getElementById('hud-' + id);
      if (el) el.textContent = `${st.pose} · ${st.face}`;
    });

    cameraTo('wide', 0);
    reset();
  }

  function cameraTo(name, ms = 850) {
    const v = typeof name === 'string' ? VIEW[name] : name;
    if (!v) throw new Error('未知镜头：' + name);
    activeView = typeof name === 'string' ? name : 'custom';
    camera.style.transitionDuration = ms + 'ms';
    camera.style.transform = `translate(800px,450px) scale(${v.s}) translate(${-v.x}px,${-v.y}px)`;
    const cam = document.getElementById('hud-camera');
    if (cam) cam.textContent = CAMERA_LABEL[activeView] || activeView;
  }

  function restartLineDraw() {
    const inks = camera.querySelectorAll('.ink');
    inks.forEach(el => { el.style.animation = 'none'; });
    void camera.getBoundingClientRect();
    inks.forEach(el => { el.style.animation = ''; });
  }

  async function buildStage() {
    const t = runToken;
    camera.classList.remove('built');
    restartLineDraw();
    await wait(1760);
    if (t !== runToken) throw CANCEL;
    camera.classList.add('built');
    await wait(320);
  }

  function reset() {
    runToken++;
    waiting = false;
    advanceFn = null;
    paused = false;
    stage.classList.remove('waiting', 'playing', 'paused');
    caption.classList.remove('on');
    speaker.textContent = '';
    lineEl.textContent = '';
    slate.classList.remove('on');
    endCard.hidden = true;
    endCard.classList.remove('on');
    hint.textContent = '点击推进对白';
    cameraTo('wide', 0);
    camera.classList.remove('built');

    const fly = document.getElementById('cupFly');
    if (fly) {
      fly.style.transition = 'none';
      fly.style.opacity = 0;
      fly.style.transform = 'translate(920px,574px) scale(1)';
      void fly.getBoundingClientRect();
      fly.style.transition = '';
    }
    const cupB = document.getElementById('tray-cup-b');
    if (cupB) cupB.style.opacity = 1;

    Cast.reset('lu', 1040, 790, 1, 'luCountA');
    Cast.reset('nl', 520, 820, 1, 'nlTray');
    Cast.show('lu', false, 0);
    Cast.show('nl', false, 0);
    setCoal(false);
    updateHud();
  }

  function setCoal(on) {
    document.querySelectorAll('.coal').forEach(el => {
      el.style.transition = 'opacity 350ms ease';
      el.style.opacity = on ? 1 : 0;
    });
  }

  function begin() {
    reset();
    stage.classList.add('playing');
    return runToken;
  }

  function showSlate(on) {
    slate.classList.toggle('on', on);
  }

  function showCaption(who, text) {
    const n = NAME[who];
    if (!n) throw new Error('未知说话人：' + who);
    speaker.textContent = n[0];
    speaker.style.color = n[1];
    lineEl.textContent = text;
    caption.classList.add('on');
  }

  function clearCaption() {
    caption.classList.remove('on');
  }

  function advance() {
    if (!waiting || !advanceFn) return false;
    const fn = advanceFn;
    advanceFn = null;
    waiting = false;
    stage.classList.remove('waiting');
    fn();
    return true;
  }

  function waitAdvance(ms) {
    const t = runToken;
    return new Promise((resolve, reject) => {
      let done = false;
      let elapsed = 0;
      waiting = true;
      stage.classList.add('waiting');
      const finish = () => {
        if (done) return;
        done = true;
        waiting = false;
        advanceFn = null;
        stage.classList.remove('waiting');
        resolve();
      };
      advanceFn = finish;
      const tick = () => {
        if (t !== runToken) {
          if (!done) {
            done = true;
            waiting = false;
            stage.classList.remove('waiting');
            reject(CANCEL);
          }
          return;
        }
        if (done) return;
        if (!paused) elapsed += 40;
        if (elapsed >= ms) {
          finish();
          return;
        }
        setTimeout(tick, 40);
      };
      setTimeout(tick, 40);
    });
  }

  async function say(who, text) {
    const t = runToken;
    showCaption(who, text);
    const dur = Math.max(1600, Math.min(7200, 1050 + text.length * 78));
    await waitAdvance(dur);
    if (t !== runToken) throw CANCEL;
  }

  function wait(ms) {
    const t = runToken;
    return new Promise((resolve, reject) => {
      let elapsed = 0;
      if (ms <= 0) { resolve(); return; }
      const tick = () => {
        if (t !== runToken) { reject(CANCEL); return; }
        if (!paused) elapsed += 40;
        if (elapsed >= ms) { resolve(); return; }
        setTimeout(tick, 40);
      };
      setTimeout(tick, 40);
    });
  }

  function togglePause() {
    paused = !paused;
    stage.classList.toggle('paused', paused);
    if (paused) Audio.suspend();
    else Audio.resume();
    const btn = document.getElementById('pause');
    if (btn) btn.textContent = paused ? '继续' : '暂停';
  }

  function setHud(on) {
    hudOn = on;
    const p = document.getElementById('pose-hud');
    p.hidden = !on;
    document.getElementById('poses').setAttribute('aria-pressed', String(on));
    if (on) updateHud();
  }

  function updateHud() {
    for (const id of ['lu', 'nl']) {
      const inst = Cast.get(id);
      const el = document.getElementById('hud-' + id);
      if (el) el.textContent = `${inst.st.pose} · ${inst.st.face}`;
    }
    const cam = document.getElementById('hud-camera');
    if (cam) cam.textContent = CAMERA_LABEL[activeView] || activeView;
  }

  function end() {
    clearCaption();
    endCard.hidden = false;
    requestAnimationFrame(() => endCard.classList.add('on'));
    stage.classList.remove('playing');
  }

  async function run(fn) {
    begin();
    try {
      await fn();
      if (runToken) end();
    } catch (e) {
      if (e !== CANCEL) throw e;
    }
  }

  window.Show = {
    init, reset, begin, run, end,
    cameraTo, buildStage, showSlate, say, wait, waitAdvance,
    advance, clearCaption, togglePause, setHud, updateHud, setCoal,
    isPaused: () => paused,
    current: () => runToken,
    VIEW,
  };
})();
