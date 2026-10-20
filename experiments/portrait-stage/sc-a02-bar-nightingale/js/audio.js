/* 合成声音：房间底噪、玻璃、麦克风噪声、钢琴/贝斯/刷鼓、掌声。
   没有角色配音；演唱只给器乐与表演。 */
(function () {
  let ctx = null;
  let master = null;
  let music = null;
  let ambience = null;
  let muted = false;
  let started = false;
  let noiseBuf = null;
  let musicNodes = [];

  function init() {
    if (ctx) return ctx;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return null;
    ctx = new AC();
    master = ctx.createGain();
    master.gain.value = muted ? 0 : .9;
    master.connect(ctx.destination);

    noiseBuf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
    const data = noiseBuf.getChannelData(0);
    let last = 0;
    for (let i = 0; i < data.length; i++) {
      const white = Math.random() * 2 - 1;
      last = last * .96 + white * .04;
      data[i] = last * 3.2 + white * .18;
    }

    music = ctx.createGain();
    music.gain.value = .62;
    music.connect(master);

    ambience = ctx.createGain();
    ambience.gain.value = 0;
    ambience.connect(master);

    started = true;
    return ctx;
  }

  function noise(dur = .2, gain = .05, type = 'bandpass', freq = 1200, q = .8, when = 0) {
    if (!ctx) return;
    const src = ctx.createBufferSource();
    src.buffer = noiseBuf;
    const f = ctx.createBiquadFilter();
    f.type = type;
    f.frequency.value = freq;
    f.Q.value = q;
    const g = ctx.createGain();
    const t = ctx.currentTime + when;
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(gain, t + .012);
    g.gain.exponentialRampToValueAtTime(.0001, t + dur);
    src.connect(f).connect(g).connect(master);
    src.start(t);
    src.stop(t + dur + .05);
  }

  function tone(freq, dur = .4, gain = .05, type = 'triangle', when = 0, dest = null) {
    if (!ctx) return;
    const o = ctx.createOscillator();
    const g = ctx.createGain();
    o.type = type;
    o.frequency.value = freq;
    const t = ctx.currentTime + when;
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(gain, t + .015);
    g.gain.exponentialRampToValueAtTime(.0001, t + dur);
    o.connect(g).connect(dest || master);
    o.start(t);
    o.stop(t + dur + .05);
    return { o, g };
  }

  function clink(when = 0, gain = .07) {
    tone(2380 + Math.random() * 160, .22, gain, 'sine', when);
    tone(3320 + Math.random() * 180, .14, gain * .55, 'sine', when + .012);
  }

  function wipe() {
    noise(.16, .022, 'bandpass', 2400 + Math.random() * 600, 1.4);
  }

  function micTest() {
    if (!ctx) return;
    // 插头/手掌碰麦克风的低频砰声
    tone(82, .28, .16, 'sine', 0);
    noise(.08, .08, 'lowpass', 420, .7);
    // 短促反馈
    const o = ctx.createOscillator();
    const g = ctx.createGain();
    o.type = 'sine';
    const t = ctx.currentTime + .05;
    o.frequency.setValueAtTime(640, t);
    o.frequency.exponentialRampToValueAtTime(1180, t + .22);
    o.frequency.exponentialRampToValueAtTime(880, t + .34);
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(.05, t + .03);
    g.gain.exponentialRampToValueAtTime(.0001, t + .4);
    o.connect(g).connect(master);
    o.start(t);
    o.stop(t + .45);
    musicNodes.push(o);
  }

  function spot() {
    tone(58, .45, .055, 'sine');
    noise(.18, .02, 'lowpass', 500);
  }

  function applause() {
    if (!ctx) return;
    const t0 = ctx.currentTime;
    const bed = ctx.createBufferSource();
    bed.buffer = noiseBuf;
    const f = ctx.createBiquadFilter();
    f.type = 'bandpass';
    f.frequency.value = 1500;
    f.Q.value = .55;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0, t0);
    g.gain.linearRampToValueAtTime(.11, t0 + .16);
    g.gain.linearRampToValueAtTime(.085, t0 + 1.1);
    g.gain.exponentialRampToValueAtTime(.0001, t0 + 2.7);
    bed.connect(f).connect(g).connect(master);
    bed.start(t0);
    bed.stop(t0 + 2.8);
    for (let i = 0; i < 46; i++) {
      const d = .04 + Math.random() * 2.1;
      const pan = Math.random() * 1.4 - .7;
      const src = ctx.createBufferSource();
      src.buffer = noiseBuf;
      const bp = ctx.createBiquadFilter();
      bp.type = 'bandpass';
      bp.frequency.value = 1200 + Math.random() * 1800;
      bp.Q.value = 1.2;
      const gg = ctx.createGain();
      const p = ctx.createStereoPanner ? ctx.createStereoPanner() : null;
      const t = t0 + d;
      gg.gain.setValueAtTime(0, t);
      gg.gain.linearRampToValueAtTime(.03 + Math.random() * .035, t + .004);
      gg.gain.exponentialRampToValueAtTime(.0001, t + .055 + Math.random() * .05);
      src.connect(bp).connect(gg);
      if (p) {
        p.pan.value = pan;
        gg.connect(p).connect(master);
      } else {
        gg.connect(master);
      }
      src.start(t);
      src.stop(t + .12);
    }
  }

  function startAmbience() {
    if (!ctx || !ambience) return;
    ambience.gain.cancelScheduledValues(ctx.currentTime);
    ambience.gain.setTargetAtTime(.05, ctx.currentTime, 1.2);
    const src = ctx.createBufferSource();
    src.buffer = noiseBuf;
    src.loop = true;
    const lp = ctx.createBiquadFilter();
    lp.type = 'lowpass';
    lp.frequency.value = 420;
    lp.Q.value = .4;
    src.connect(lp).connect(ambience);
    src.start();
    // 零星杯声，让空酒馆不显得死
    const tick = () => {
      if (!ctx) return;
      if (!muted) clink(0, .018 + Math.random() * .016);
      setTimeout(tick, 3200 + Math.random() * 6000);
    };
    setTimeout(tick, 1800);
  }

  const Dm7 = [146.83, 174.61, 220.00, 261.63];
  const G7 = [98.00, 123.47, 146.83, 196.00];
  const Cm7 = [130.81, 155.56, 196.00, 233.08];
  const A7 = [110.00, 138.59, 164.81, 196.00];

  function startMusic() {
    if (!ctx || !music) return;
    const beat = 60 / 92;
    const bar = beat * 4;
    const prog = [Dm7, G7, Cm7, A7];
    const bassRoots = [73.42, 49.00, 65.41, 55.00];
    const t0 = ctx.currentTime + .08;
    const cycles = 5;

    for (let c = 0; c < cycles; c++) {
      for (let b = 0; b < 4; b++) {
        const chord = prog[(c * 4 + b) % 4];
        const root = bassRoots[(c * 4 + b) % 4];
        const bt = c * 4 * bar + b * bar;
        // 钢琴：第一、三拍轻和弦
        for (const beatPos of [0, 2]) {
          for (let i = 0; i < chord.length; i++) {
            const n = chord[i] * (i === 0 ? 1 : 1);
            tone(n, .55, .026, 'triangle', bt + beatPos * beat + i * .006, music);
            tone(n * 2, .3, .009, 'sine', bt + beatPos * beat + i * .006, music);
          }
        }
        // 第二、四拍的弱切分
        for (let i = 0; i < chord.length; i += 2) {
          tone(chord[i] * 1.5, .3, .012, 'triangle', bt + 3.5 * beat + i * .01, music);
        }
        // 贝斯走位
        const walk = [root, root * 1.25, root * 1.5, root * 1.33];
        for (let i = 0; i < 4; i++) {
          tone(walk[i], .5, .05, 'sine', bt + i * beat, music);
          tone(walk[i] / 2, .4, .018, 'triangle', bt + i * beat, music);
        }
        // 刷鼓
        for (const beatPos of [1, 3]) {
          noise(.14, .012, 'highpass', 2600, .6, bt + beatPos * beat);
        }
        // 踩镲细碎
        for (let i = 0; i < 4; i++) {
          noise(.06, .0045, 'highpass', 5400, .8, bt + i * beat + .01);
        }
      }
    }
    // 收束：一个低音与镲
    tone(73.42, 1.6, .045, 'sine', cycles * 4 * bar, music);
    noise(1.2, .01, 'highpass', 4200, .5, cycles * 4 * bar);
    musicNodes.push(music);
  }

  function fadeMusic(to, seconds = 2) {
    if (!ctx || !music) return;
    music.gain.cancelScheduledValues(ctx.currentTime);
    music.gain.setValueAtTime(music.gain.value, ctx.currentTime);
    music.gain.linearRampToValueAtTime(to, ctx.currentTime + seconds);
  }

  function setMuted(v) {
    muted = v;
    if (!ctx || !master) return;
    master.gain.cancelScheduledValues(ctx.currentTime);
    master.gain.setValueAtTime(master.gain.value, ctx.currentTime);
    master.gain.linearRampToValueAtTime(v ? 0 : .9, ctx.currentTime + .12);
  }

  function suspend() { if (ctx && ctx.state === 'running') ctx.suspend(); }
  function resume() { if (ctx && ctx.state === 'suspended') ctx.resume(); }
  function stopAll() {
    if (!ctx) return;
    fadeMusic(0, .6);
    if (ambience) ambience.gain.setTargetAtTime(0, ctx.currentTime, .4);
    setTimeout(() => {
      try { ctx.close(); } catch (e) { /* 已关闭 */ }
      ctx = null; master = null; music = null; ambience = null; started = false;
    }, 900);
  }

  window.Audio = {
    init, startAmbience, startMusic, fadeMusic,
    micTest, spot, applause, clink, wipe,
    setMuted, suspend, resume, stopAll,
    isReady: () => !!ctx,
  };
})();
