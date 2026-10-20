/* 合成声音：清晨咖啡馆的底噪、门铃、零钱、杯子、收音机里的钢琴。
   没有角色配音。 */
(function () {
  let ctx = null;
  let master = null;
  let music = null;
  let ambience = null;
  let muted = false;
  let noiseBuf = null;
  let radioNodes = [];

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
      last = last * .97 + white * .03;
      data[i] = last * 3.4 + white * .14;
    }

    music = ctx.createGain();
    music.gain.value = .34;
    music.connect(master);

    ambience = ctx.createGain();
    ambience.gain.value = 0;
    ambience.connect(master);

    return ctx;
  }

  function noise(dur = .2, gain = .05, type = 'bandpass', freq = 1200, q = .8, when = 0, dest = null) {
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
    src.connect(f).connect(g).connect(dest || master);
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

  function clink(when = 0, gain = .05) {
    tone(2260 + Math.random() * 180, .2, gain, 'sine', when);
    tone(3180 + Math.random() * 220, .13, gain * .55, 'sine', when + .012);
  }

  function coin(when = 0, gain = .035) {
    for (let i = 0; i < 3; i++) {
      tone(2100 + Math.random() * 900, .07 + Math.random() * .04, gain, 'square', when + i * .055);
    }
  }

  function doorBell() {
    tone(1540, .38, .035, 'sine', 0);
    tone(2070, .46, .026, 'sine', .12);
    noise(.05, .02, 'highpass', 5000, 1);
  }

  function register() {
    tone(1760, .5, .045, 'sine', 0);
    tone(2640, .38, .025, 'sine', .01);
    noise(.12, .02, 'highpass', 3400, 1, .02);
    noise(.18, .018, 'lowpass', 260, .7, .18);
  }

  function wipe() {
    noise(.18, .018, 'bandpass', 2200 + Math.random() * 700, 1.3);
  }

  function pour() {
    noise(.65, .022, 'bandpass', 1500, .7);
    noise(.4, .012, 'highpass', 3000, .8, .1);
    for (let i = 0; i < 5; i++) {
      tone(120 + Math.random() * 80, .08, .012, 'sine', .08 + i * .11);
    }
  }

  function startAmbience() {
    if (!ctx || !ambience) return;
    ambience.gain.cancelScheduledValues(ctx.currentTime);
    ambience.gain.setTargetAtTime(.07, ctx.currentTime, 1.2);
    const src = ctx.createBufferSource();
    src.buffer = noiseBuf;
    src.loop = true;
    const lp = ctx.createBiquadFilter();
    lp.type = 'lowpass';
    lp.frequency.value = 520;
    lp.Q.value = .35;
    src.connect(lp).connect(ambience);
    src.start();

    const tick = () => {
      if (!ctx) return;
      if (!muted) {
        const r = Math.random();
        if (r < .55) clink(0, .012 + Math.random() * .012);
        else if (r < .8) noise(.08, .008, 'highpass', 2400, 1);
        else coin(0, .012);
      }
      setTimeout(tick, 2600 + Math.random() * 5200);
    };
    setTimeout(tick, 2200);
  }

  /* 收音机里的慢拍钢琴：只给和弦，不抢对白 */
  function startRadio() {
    if (!ctx || !music) return;
    const beat = 60 / 76;
    const bar = beat * 4;
    const prog = [
      [130.81, 164.81, 196.00, 246.94],
      [110.00, 130.81, 164.81, 196.00],
      [87.31, 130.81, 164.81, 220.00],
      [98.00, 123.47, 146.83, 196.00],
    ];
    const t0 = ctx.currentTime + .2;
    const cycles = 10;
    for (let c = 0; c < cycles; c++) {
      for (let b = 0; b < 4; b++) {
        const chord = prog[(c * 4 + b) % 4];
        const bt = c * 4 * bar + b * bar;
        for (const pos of [0, 2]) {
          for (let i = 0; i < chord.length; i++) {
            tone(chord[i], .72, .012, 'triangle', bt + pos * beat + i * .008, music);
            tone(chord[i] * 2, .26, .0035, 'sine', bt + pos * beat + i * .008, music);
          }
        }
        if (b % 2 === 1) tone(chord[0] / 2, .9, .02, 'sine', bt + 3 * beat, music);
      }
    }
    radioNodes.push(music);
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
    fadeMusic(0, .5);
    if (ambience) ambience.gain.setTargetAtTime(0, ctx.currentTime, .35);
    setTimeout(() => {
      try { ctx.close(); } catch (e) { /* 已关闭 */ }
      ctx = null; master = null; music = null; ambience = null; radioNodes = [];
    }, 800);
  }

  window.Audio = {
    init, startAmbience, startRadio, fadeMusic,
    doorBell, register, coin, clink, wipe, pour,
    setMuted, suspend, resume, stopAll,
    isReady: () => !!ctx,
  };
})();
