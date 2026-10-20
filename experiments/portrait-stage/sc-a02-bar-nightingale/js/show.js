/* 播放层：时间、对白、镜头、灯光、字幕和姿势检查。
   所有等待都检查 runToken，重播时旧时间线自动失效。 */
(function () {
  const CANCEL = {};
  const NAME = {
    bm: ['酒保', '#c9a8e0'],
    nl: ['尼尔', '#f0cf8a'],
    ny: ['夜莺', '#8fd9d0'],
  };

  const VIEW = {
    wide: { x: 800, y: 450, s: 1 },
    bar: { x: 512, y: 506, s: 1.46 },
    stage: { x: 1128, y: 456, s: 1.17 },
    close: { x: 1178, y: 372, s: 2.35 },
    end: { x: 870, y: 462, s: 1.12 },
  };
  const CAMERA_LABEL = {
    wide: '全景', bar: '吧台', stage: '舞台', close: '侧面近景', end: '收场', custom: '自定义',
  };

  let stage, camera, sceneSvg, caption, speaker, lineEl, slate, endCard, hint;
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
    sceneSvg = document.getElementById('world');
    Cast.buildInto(document.getElementById('castBm'), 'bm');
    Cast.buildInto(document.getElementById('castNy'), 'ny');
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
    Audio.spot();
    await wait(1860);
    if (t !== runToken) throw CANCEL;
    camera.classList.add('built');
    await wait(280);
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

    const glass = document.getElementById('glass');
    if (glass) {
      glass.style.transition = 'none';
      glass.style.transform = 'translate(650px,552px) scale(1.18)';
      glass.style.opacity = 1;
      void glass.getBoundingClientRect();
      glass.style.transition = '';
    }

    document.getElementById('stageLight').classList.remove('on');
    document.getElementById('houseDim').classList.remove('on');
    document.getElementById('barGlow').classList.remove('dim');
    document.getElementById('audience-wrap').style.opacity = 0;
    const au = document.getElementById('audience');
    if (au) au.classList.remove('clap');

    for (const id of ['bm', 'nl', 'ny']) {
      const inst = Cast.get(id);
      inst.root.style.filter = 'none';
      inst.root.style.opacity = 0;
    }
    Cast.reset('bm', 470, 790, 1, 'bmWipeA');
    Cast.reset('nl', 715, 790, 1, 'nlLean');
    Cast.reset('ny', 1150, 702, 1, 'nySettle');
    Cast.show('bm', false, 0);
    Cast.show('nl', false, 0);
    Cast.show('ny', false, 0);
    updateHud();
  }

  function begin() {
    reset();
    stage.classList.add('playing');
    return runToken;
  }

  function showHands(on = true) {
    const au = document.getElementById('audience');
    if (au) au.classList.toggle('clap', on);
  }

  function audience(on, ms = 900) {
    const wrap = document.getElementById('audience-wrap');
    wrap.style.transition = `opacity ${ms}ms ease`;
    wrap.style.opacity = on ? 1 : 0;
  }

  function stageLight(on) {
    document.getElementById('stageLight').classList.toggle('on', on);
    document.getElementById('houseDim').classList.toggle('on', on);
    document.getElementById('barGlow').classList.toggle('dim', on);
    if (on) Audio.spot();
  }

  function silhouette(id, on) {
    const inst = Cast.get(id);
    inst.root.style.transition = 'filter 600ms ease, opacity 500ms ease';
    inst.root.style.filter = on ? 'brightness(.16) saturate(.35)' : 'none';
  }

  function opacity(id, v, ms = 500) {
    const inst = Cast.get(id);
    inst.root.style.transition = `opacity ${ms}ms ease`;
    inst.root.style.opacity = v;
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
    for (const id of ['bm', 'nl', 'ny']) {
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
    cameraTo, buildStage, stageLight, audience, showHands,
    silhouette, opacity, showSlate, say, wait, waitAdvance,
    advance, clearCaption, togglePause, setHud, updateHud,
    isPaused: () => paused,
    current: () => runToken,
    VIEW,
  };
})();
