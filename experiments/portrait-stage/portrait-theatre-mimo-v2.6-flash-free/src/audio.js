// Web Audio 合成音效：没有配音，全部由振荡器与噪声现场合成。
// AudioContext 必须在用户手势里创建（浏览器自动播放限制），由 main.js 在“开演”时调用 init()。
PT.audio = (() => {
  let ctx = null, master = null, rainNode = null, rainGain = null, muted = false;

  const now = () => ctx.currentTime;

  function noiseBuffer(seconds = 2) {
    const buf = ctx.createBuffer(1, ctx.sampleRate * seconds, ctx.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    return buf;
  }

  function env(node, t0, attack, decay, peak = 1) {
    const g = node.gain;
    g.setValueAtTime(0.0001, t0);
    g.exponentialRampToValueAtTime(peak, t0 + attack);
    g.exponentialRampToValueAtTime(0.0001, t0 + attack + decay);
  }

  function tone({ type = 'sine', freq = 440, freq2 = null, t = 0, dur = 0.3, gain = 0.2, filter = null }) {
    const o = ctx.createOscillator(), g = ctx.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, now() + t);
    if (freq2) o.frequency.exponentialRampToValueAtTime(freq2, now() + t + dur);
    let head = o;
    if (filter) {
      const f = ctx.createBiquadFilter();
      f.type = filter.type; f.frequency.value = filter.freq; f.Q.value = filter.q || 1;
      o.connect(f); head = f;
    }
    head.connect(g); g.connect(master);
    env(g, now() + t, 0.01, dur, gain);
    o.start(now() + t); o.stop(now() + t + dur + 0.06);
  }

  function noise({ t = 0, dur = 0.2, gain = 0.25, type = 'lowpass', freq = 1200, q = 1, freq2 = null }) {
    const src = ctx.createBufferSource();
    src.buffer = noiseBuffer(Math.max(0.3, dur + 0.2));
    const f = ctx.createBiquadFilter();
    f.type = type; f.frequency.setValueAtTime(freq, now() + t); f.Q.value = q;
    if (freq2) f.frequency.exponentialRampToValueAtTime(freq2, now() + t + dur);
    const g = ctx.createGain();
    src.connect(f); f.connect(g); g.connect(master);
    env(g, now() + t, 0.008, dur, gain);
    src.start(now() + t); src.stop(now() + t + dur + 0.1);
  }

  const SFX = {
    buzz() {           // 电铃按钮
      tone({ type: 'square', freq: 640, dur: 0.16, gain: 0.07 });
      tone({ type: 'square', freq: 640, t: 0.22, dur: 0.16, gain: 0.07 });
      noise({ dur: 0.05, gain: 0.05, freq: 3000, type: 'highpass' });
    },
    click() {          // 对讲机接通
      noise({ dur: 0.04, gain: 0.18, freq: 2400, type: 'bandpass', q: 3 });
      tone({ type: 'sine', freq: 180, dur: 0.07, gain: 0.06 });
    },
    unlock() {         // 电锁开
      noise({ dur: 0.03, gain: 0.2, freq: 5000, type: 'highpass' });
      noise({ t: 0.16, dur: 0.05, gain: 0.22, freq: 3500, type: 'highpass' });
      tone({ type: 'sine', freq: 120, freq2: 70, t: 0.2, dur: 0.22, gain: 0.22 });
    },
    creak() {          // 门轴
      noise({ dur: 0.55, gain: 0.1, type: 'bandpass', freq: 700, q: 8, freq2: 1500 });
      noise({ t: 0.5, dur: 0.3, gain: 0.07, type: 'bandpass', freq: 1400, q: 6, freq2: 900 });
    },
    step() {           // 脚步
      noise({ dur: 0.09, gain: 0.16, freq: 900, freq2: 300 });
      tone({ type: 'sine', freq: 90, freq2: 60, dur: 0.09, gain: 0.1 });
    },
    pat() {            // 拍肩两下
      noise({ dur: 0.08, gain: 0.16, freq: 700, freq2: 260 });
      tone({ type: 'sine', freq: 130, freq2: 80, dur: 0.1, gain: 0.14 });
      noise({ t: 0.24, dur: 0.08, gain: 0.14, freq: 640, freq2: 240 });
      tone({ type: 'sine', freq: 120, freq2: 70, t: 0.24, dur: 0.1, gain: 0.12 });
    },
    rustle() {         // 纸张 / 衣料
      noise({ dur: 0.3, gain: 0.1, type: 'bandpass', freq: 3400, q: 1.4, freq2: 5200 });
    },
    slam() {           // 大门砰
      tone({ type: 'sine', freq: 90, freq2: 38, dur: 0.5, gain: 0.5 });
      noise({ dur: 0.34, gain: 0.35, freq: 900, freq2: 160 });
      noise({ t: 0.02, dur: 0.6, gain: 0.1, type: 'bandpass', freq: 260, q: 4 });
    },
    ring() {           // 试线的电话铃
      for (let k = 0; k < 2; k++) {
        const t = k * 0.75;
        for (const f of [440, 480]) {
          const o = ctx.createOscillator(), g = ctx.createGain();
          o.type = 'sine'; o.frequency.value = f;
          const lfo = ctx.createOscillator(), lg = ctx.createGain();
          lfo.frequency.value = 16; lg.gain.value = 0.5;
          lfo.connect(lg); lg.connect(g.gain);
          g.gain.value = 0.0;
          g.gain.setValueAtTime(0.0001, now() + t);
          g.gain.exponentialRampToValueAtTime(0.05, now() + t + 0.03);
          g.gain.setValueAtTime(0.05, now() + t + 0.45);
          g.gain.exponentialRampToValueAtTime(0.0001, now() + t + 0.55);
          o.connect(g); g.connect(master);
          o.start(now() + t); o.stop(now() + t + 0.6);
          lfo.start(now() + t); lfo.stop(now() + t + 0.6);
        }
      }
    },
  };

  function startRain() {
    if (rainNode) return;
    const src = ctx.createBufferSource();
    src.buffer = noiseBuffer(3);
    src.loop = true;
    const hp = ctx.createBiquadFilter(); hp.type = 'highpass'; hp.frequency.value = 500;
    const lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 2600;
    rainGain = ctx.createGain(); rainGain.gain.value = 0.0001;
    src.connect(hp); hp.connect(lp); lp.connect(rainGain); rainGain.connect(master);
    src.start();
    rainNode = src;
    rainGain.gain.exponentialRampToValueAtTime(0.075, now() + 1.2);
  }

  function stopRain() {
    if (!rainNode) return;
    const node = rainNode, g = rainGain;
    rainNode = null; rainGain = null;
    g.gain.exponentialRampToValueAtTime(0.0001, now() + 0.8);
    setTimeout(() => { try { node.stop(); } catch (e) { /* 已停止 */ } }, 1000);
  }

  return {
    names: Object.keys(SFX),
    init() {
      if (ctx) { if (ctx.state === 'suspended') ctx.resume(); return; }
      const AC = window.AudioContext || window.webkitAudioContext;
      if (!AC) return;
      ctx = new AC();
      master = ctx.createGain();
      master.gain.value = muted ? 0 : 1;
      master.connect(ctx.destination);
    },
    play(name) {
      if (!ctx || muted) return;
      const fn = SFX[name];
      if (!fn) throw new Error(`未知音效: ${name}`);
      fn();
    },
    rain(on) { if (!ctx) return; on ? startRain() : stopRain(); },
    setMuted(m) {
      muted = m;
      if (master) master.gain.value = m ? 0 : 1;
    },
    get muted() { return muted; },
  };
})();
