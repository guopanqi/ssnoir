// 播放器：拍点推进、自动阅读时长、暂停 / 重播 / 跳拍。
// 暂停时取消所有计时器（含 seq 舞台动作），恢复时按剩余时间继续。
PT.Player = class {
  constructor(stage, hooks = {}) {
    this.stage = stage;
    this.hooks = hooks;          // { onBeat, onEnd, onState }
    this.sceneIndex = -1;
    this.beatIndex = -1;
    this.playing = false;
    this.started = false;
    this.tasks = [];             // { timer, fn, start, ms, seq }
  }

  get scene() { return PT.SCENES[this.sceneIndex]; }

  schedule(ms, fn) {
    const task = { fn, ms, start: Date.now(), timer: null, dead: false };
    task.timer = setTimeout(() => {
      this.tasks = this.tasks.filter((t) => t !== task);
      if (!task.dead) fn();
    }, ms);
    this.tasks.push(task);
    return task;
  }

  cancelTasks() {
    for (const t of this.tasks) { clearTimeout(t.timer); t.dead = true; }
    this.tasks = [];
    this.pausedTasks = null;
  }

  pauseTasks() {
    const pending = [];
    for (const t of this.tasks) {
      clearTimeout(t.timer);
      const left = Math.max(120, t.ms - (Date.now() - t.start));
      pending.push({ ms: left, fn: t.fn });
      t.dead = true;
    }
    this.tasks = [];
    return pending;
  }

  // ---------- 场景 ----------
  openScene(index, { autoplay = true } = {}) {
    if (index < 0 || index >= PT.SCENES.length) throw new Error(`未知片段: ${index}`);
    this.cancelTasks();
    this.sceneIndex = index;
    this.stage.setScene(this.scene);
    this.beatIndex = -1;
    this.playing = false;
    this.started = false;
    this.stage.clearSubtitle();
    this.emit();
    if (autoplay) this.goto(0);
  }

  // 无动画地重建 0..to 的舞台状态，再演出第 to 拍
  goto(to) {
    const scene = this.scene;
    if (!scene) return;
    if (to < 0) to = 0;
    if (to >= scene.beats.length) { this.finish(); return; }
    this.cancelTasks();
    this.stage.setScene(scene);
    for (let i = 0; i < to; i++) this.applyBeat(i, { anim: false, live: false });
    this.stage.endRebuild();
    this.beatIndex = -1;
    this.playing = true;
    this.started = true;
    this.runBeat(to, { anim: true });
  }

  applyBeat(i, { anim = true, live = false } = {}) {
    const beat = this.scene.beats[i];
    const cue = Object.assign({}, beat.cue || {});
    const seq = cue.seq;
    delete cue.seq;
    this.stage.apply(cue, { anim });
    if (seq) {
      for (const step of seq) {
        if (live) {
          this.schedule(step.at, () => {
            this.stage.apply(step, { anim: true });
            if (step.sfx) PT.audio.play(step.sfx);
          });
        } else {
          this.stage.apply(step, { anim });
        }
      }
    }
    if (cue.sfx && live) PT.audio.play(cue.sfx);
  }

  runBeat(i, { anim = true } = {}) {
    const scene = this.scene;
    const beat = scene.beats[i];
    this.beatIndex = i;
    this.applyBeat(i, { anim, live: true });
    if (beat.say) this.stage.subtitle({ say: beat.say, text: beat.text, via: beat.via });
    else if (beat.dir) this.stage.subtitle({ dir: beat.dir });
    this.emit();
    const wait = this.duration(beat);
    this.schedule(wait, () => {
      if (i + 1 < scene.beats.length) this.runBeat(i + 1, { anim: true });
      else this.finish();
    });
  }

  duration(beat) {
    let base;
    if (beat.say) base = 1.0 + 0.165 * beat.text.length;
    else if (beat.dir) base = 2.2 + 0.05 * beat.dir.length;
    else base = 1.5;
    const seq = (beat.cue && beat.cue.seq) || [];
    const span = seq.length ? Math.max(...seq.map((s) => s.at)) + 700 : 0;
    return Math.min(9000, Math.max(base * 1000, span)) + (beat.hold || 0) * 1000;
  }

  finish() {
    this.cancelTasks();
    this.playing = false;
    this.stage.clearSubtitle();
    this.emit();
    if (this.hooks.onEnd) this.hooks.onEnd(this.sceneIndex);
  }

  // ---------- 控制 ----------
  play() {
    if (this.sceneIndex < 0) this.openScene(0);
    else if (!this.playing) this.resume();
  }

  resume() {
    if (this.playing) return;
    if (this.sceneIndex < 0) { this.openScene(0); return; }
    this.playing = true;
    const pending = this.pausedTasks || [];
    this.pausedTasks = null;
    if (pending.length) {
      for (const t of pending) this.schedule(t.ms, t.fn);
    } else if (this.beatIndex < 0 || !this.started) {
      this.goto(0);
      return;
    } else {
      this.runBeat(this.beatIndex, { anim: true });
    }
    this.emit();
  }

  toggle() {
    if (!this.started) { this.play(); return; }
    if (this.playing) this.hold();
    else this.resume();
  }

  hold() {
    if (!this.playing) return;
    this.pausedTasks = this.pauseTasks();
    this.playing = false;
    this.emit();
  }

  replay() {
    if (this.sceneIndex < 0) this.openScene(0);
    else this.goto(0);
  }

  next() {
    if (this.sceneIndex < 0) { this.openScene(0); return; }
    const at = this.beatIndex < 0 ? 0 : this.beatIndex + 1;
    if (at >= this.scene.beats.length) this.finish();
    else {
      this.cancelTasks();
      this.pausedTasks = null;
      this.playing = true;
      this.started = true;
      this.runBeat(at, { anim: true });
    }
  }

  prev() {
    if (this.sceneIndex < 0) return;
    this.goto(Math.max(0, (this.beatIndex < 0 ? 0 : this.beatIndex) - 1));
  }

  emit() {
    if (this.hooks.onState) this.hooks.onState(this.snapshot());
  }

  snapshot() {
    return {
      sceneIndex: this.sceneIndex,
      beatIndex: this.beatIndex,
      playing: this.playing,
      started: this.started,
      total: this.scene ? this.scene.beats.length : 0,
    };
  }
};
