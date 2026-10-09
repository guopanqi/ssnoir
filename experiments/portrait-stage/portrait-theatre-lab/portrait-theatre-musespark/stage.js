/* Portrait Theatre musespark — 渲染 + 播放 + 合成音效。无依赖。 */
(function () {
"use strict";
var SVG = {
"夜莺": '<svg viewBox="0 0 120 260"><g class="head"><ellipse cx="60" cy="46" rx="26" ry="8" fill="#2a2f3a"/><circle cx="60" cy="52" r="17" fill="#e8d0b4"/><path d="M43 52 q-4 22 8 26 l-2-14z M77 52 q4 22-8 26 l2-14z" fill="#3a2c22"/><ellipse cx="60" cy="34" rx="20" ry="7" fill="#3d4454"/><rect x="46" y="22" width="28" height="14" rx="3" fill="#3d4454"/></g><path d="M38 78 Q60 70 82 78 L90 200 Q60 210 30 200 Z" fill="#232b3d" stroke="#0c0e14" stroke-width="2"/><rect x="38" y="140" width="44" height="7" fill="#c8a24a" opacity=".8"/><path d="M48 205 L44 250 M72 205 L76 250" stroke="#14161c" stroke-width="9"/><path d="M38 250 h14 M68 250 h14" stroke="#0a0a0c" stroke-width="5"/></svg>',
"尼尔": '<svg viewBox="0 0 120 260"><g class="head"><circle cx="60" cy="56" r="17" fill="#dcbf9e"/><rect x="38" y="30" width="44" height="12" rx="2" fill="#4a3f30"/><rect x="48" y="12" width="24" height="22" rx="2" fill="#4a3f30"/></g><path d="M32 82 Q60 72 88 82 L96 205 Q60 216 24 205 Z" fill="#3a3f4a" stroke="#0c0e14" stroke-width="2"/><path d="M48 84 L60 150 L72 84" fill="none" stroke="#1a1d24" stroke-width="4"/><path d="M48 208 L45 250 M72 208 L75 250" stroke="#14161c" stroke-width="10"/></svg>',
"经理": '<svg viewBox="0 0 140 260"><g class="head"><circle cx="70" cy="54" r="19" fill="#e0c39c"/><path d="M51 44 q19-16 38 0 l0-8 q-19-10-38 0z" fill="#5a5148"/></g><path d="M70 78 m-8 0 h16 l4 8 -12 8 -12-8z" fill="#8f1f1f"/><path d="M34 84 Q70 72 106 84 L112 205 Q70 220 28 205 Z" fill="#2e2a33" stroke="#0c0e14" stroke-width="2"/></svg>',
"工人": '<svg viewBox="0 0 120 260"><g class="head"><circle cx="60" cy="58" r="15" fill="#d8b894"/><rect x="44" y="38" width="32" height="10" rx="5" fill="#5a6a7a"/></g><rect x="40" y="84" width="40" height="90" fill="#4a5560" stroke="#0c0e14" stroke-width="2"/><rect x="86" y="150" width="26" height="20" fill="#6a4a2a" stroke="#0c0e14" stroke-width="2"/></svg>'
};
var $ = function (id) { return document.getElementById(id); };
var stage = $("stage"), actorsEl = $("actors"), rainEl = $("rain"),
  whoEl = $("who"), textEl = $("text"), dirEl = $("dir"),
  cntEl = $("cnt"), barEl = document.querySelector("#bar i"),
  noteEl = $("actnote"), floorEl = $("floor");
var actIdx = 0, beatIdx = 0, playing = true, typing = false;
var typeTimer = null, holdTimer = null, fullText = "", soundOn = false;
var AC = null, audioCtx = null, rainNodes = null;

for (var i = 0; i < 40; i++) {
  var d = document.createElement("div");
  d.className = "drop";
  d.style.left = (Math.random() * 100) + "%";
  d.style.animationDelay = (Math.random() * 1) + "s";
  rainEl.appendChild(d);
}

function ac() {
  if (!audioCtx) { try { audioCtx = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { return null; } }
  if (audioCtx && audioCtx.state === "suspended") { audioCtx.resume(); }
  return audioCtx;
}
function tone(freq, dur, type, gain) {
  if (!soundOn) return; var c = ac(); if (!c) return;
  var o = c.createOscillator(), g = c.createGain();
  o.type = type || "sine"; o.frequency.value = freq;
  g.gain.setValueAtTime(gain || 0.08, c.currentTime);
  g.gain.exponentialRampToValueAtTime(0.0001, c.currentTime + dur);
  o.connect(g); g.connect(c.destination); o.start(); o.stop(c.currentTime + dur);
}
function noise(dur, freq) {
  if (!soundOn) return; var c = ac(); if (!c) return;
  var b = c.createBuffer(1, c.sampleRate * dur, c.sampleRate), ch = b.getChannelData(0);
  for (var i = 0; i < ch.length; i++) ch[i] = (Math.random() * 2 - 1) * 0.25;
  var s = c.createBufferSource(); s.buffer = b;
  var f = c.createBiquadFilter(); f.type = "lowpass"; f.frequency.value = freq || 800;
  s.connect(f); f.connect(c.destination); s.start();
}
function playSound(name) {
  if (!soundOn || !name) return;
  if (name === "bell") { tone(880, 0.35); setTimeout(function () { tone(660, 0.5); }, 350); }
  else if (name === "unlock") { tone(300, 0.08, "square", 0.06); setTimeout(function () { tone(520, 0.12, "square", 0.06); }, 140); }
  else if (name === "slam") { noise(0.4, 300); tone(70, 0.5, "sine", 0.15); }
  else if (name === "pat") { noise(0.12, 900); }
  else if (name === "steps") { noise(0.2, 500); }
  else if (name === "drill") { tone(180, 0.4, "sawtooth", 0.03); }
  else if (name === "rain") { noise(1.2, 2500); }
}
function clearProps() {
  ["bell", "door", "pack", "things", "hatum", "phone", "bigdoor"].forEach(function (id) {
    $(id).classList.remove("show", "open", "focus", "shake", "slam");
  });
  $("doorlight").style.width = "0";
  rainEl.classList.remove("show");
  $("biglight").style.opacity = "0";
  stage.classList.remove("shake");
  var p = $("pack"); p.classList.remove("show", "focus", "shake");
}
function buildCast(act) {
  actorsEl.innerHTML = "";
  act.cast.forEach(function (name) {
    var el = document.createElement("div");
    el.className = "actor"; el.dataset.name = name;
    el.innerHTML = (SVG[name] || SVG["尼尔"]) + '<div class="nm">' + name + "</div>";
    el.style.left = "40%";
    actorsEl.appendChild(el);
  });
}
function actorEl(name) { return actorsEl.querySelector('[data-name="' + name + '"]'); }
function applyBeatFx(beat) {
  var act = window.ACTS[actIdx];
  var key = beat.fx || "";
  if (beat.sound === "rain" || act.id === 1 && beatIdx === 0) rainEl.classList.add("show");
  if (key === "doorlight" || beat.prop === "door-open") {
    $("door").classList.add("show", "open");
    $("doorlight").style.width = "30%";
  }
  if (beat.prop === "bell") $("bell").classList.add("show");
  if (beat.prop === "pack") { var p = $("pack"); p.classList.add("show"); }
  if (key === "pack-focus") { $("pack").classList.add("show", "focus"); }
  if (key === "pack-shake") { var q = $("pack"); q.classList.add("show"); q.classList.remove("shake"); void q.offsetWidth; q.classList.add("shake"); }
  if (beat.prop === "things") $("things").classList.add("show");
  if (beat.prop === "hat-umbrella") $("hatum").classList.add("show");
  if (beat.prop === "phone" || beat.prop === "phone-done") $("phone").classList.add("show");
  if (beat.prop === "bigdoor" || beat.prop === "bigdoor-slam") {
    var bd = $("bigdoor"); bd.classList.add("show");
    if (beat.prop === "bigdoor-slam") {
      bd.classList.add("slam"); stage.classList.remove("shake"); void stage.offsetWidth; stage.classList.add("shake");
    }
  }
  if (act.id === 6) $("biglight").style.opacity = "1";
  if (beat.moves) Object.keys(beat.moves).forEach(function (n) {
    var el = actorEl(n); if (el) el.style.left = beat.moves[n] + "%";
  });
  if (beat.poses) Object.keys(beat.poses).forEach(function (n) {
    var el = actorEl(n); if (!el) return;
    el.classList.remove("bow", "look-up", "look-down", "soft", "angry", "turn", "step-back", "stiff", "small", "leave", "sit", "kneel", "open-arms", "arm-around", "pat", "show-prop");
    var v = beat.poses[n];
    if (v === "reach" || v === "show") el.classList.add("show-prop");
    else if (v && v !== "stand") el.classList.add(v);
  });
  if (key === "shoulder") {
    var m = actorEl("经理"), n2 = actorEl("尼尔");
    if (m && n2) { m.style.left = n2.style.left; m.style.transform = "translateX(34px)"; }
  } else {
    var mm = actorEl("经理"); if (mm) mm.style.transform = "";
  }
  if (key === "tears") {
    var ng = actorEl("夜莺");
    if (ng) for (var i = 0; i < 2; i++) {
      (function (k) {
        var t = document.createElement("div"); t.className = "tear";
        t.style.left = (46 + k * 8) + "%"; t.style.top = "18%";
        ng.appendChild(t); setTimeout(function () { t.remove(); }, 1800);
      })(i);
    }
  }
  if (beat.sound) playSound(beat.sound);
}
function litSpeaker(beat) {
  var all = actorsEl.querySelectorAll(".actor");
  all.forEach(function (el) { el.classList.remove("lit", "dim", "voice"); });
  if (beat.who) {
    var base = beat.who.replace("（电铃）", "");
    all.forEach(function (el) {
      if (el.dataset.name === base) { el.classList.add("lit"); if (beat.voice) el.classList.add("voice"); }
      else el.classList.add("dim");
    });
  }
}
function beatDelay(beat) {
  if (beat.hold) return beat.hold;
  var len = ((beat.text || "") + (beat.dir || "")).length;
  if (beat.dir && !beat.text) return 1400 + Math.min(len * 40, 2500);
  return 1200 + Math.min(len * 90, 6000);
}
function render() {
  clearTimeout(typeTimer); clearTimeout(holdTimer);
  var act = window.ACTS[actIdx];
  stage.style.background = act.bg; floorEl.style.background = act.floor;
  noteEl.textContent = act.title + " · " + act.note;
  document.querySelectorAll("#controls [data-act]").forEach(function (b, i) {
    b.classList.toggle("on", i === actIdx);
  });
  var beat = act.beats[beatIdx];
  clearPropsKeep();
  applyBeatFx(beat);
  litSpeaker(beat);
  whoEl.textContent = beat.who || (beat.dir ? "【舞台指示】" : "");
  whoEl.classList.toggle("voice", !!beat.voice);
  dirEl.textContent = (!beat.text && beat.dir) ? "" : (beat.dir || "");
  fullText = beat.text || beat.dir || "";
  var target = beat.text ? textEl : textEl;
  if (beat.text) { textEl.textContent = ""; dirEl.textContent = beat.dir || dirEl.textContent; }
  else { textEl.textContent = ""; }
  if (beat.dir && !beat.text) dirEl.textContent = "";
  typeText(fullText, beat);
  cntEl.textContent = (beatIdx + 1) + "/" + act.beats.length;
  barEl.style.width = ((beatIdx + 1) / act.beats.length * 100) + "%";
}
/* 道具在同一幕内累积：只在切幕时清空 Stage 级元素 */
var lastAct = -1;
function clearPropsKeep() {
  if (lastAct !== actIdx) { clearProps(); lastAct = actIdx; }
  $("doorlight").style.width = $("door").classList.contains("open") ? "30%" : $("doorlight").style.width;
}
function typeText(s, beat) {
  clearTimeout(typeTimer);
  var i = 0; typing = true;
  textEl.textContent = "";
  if (beat.dir && !beat.text) { dirEl.textContent = ""; }
  (function step() {
    if (!typing) return;
    i += 2;
    var done = i >= s.length;
    var slice = s.slice(0, i);
    if (beat.text) textEl.textContent = slice;
    else dirEl.textContent = slice;
    if (done) { typing = false; scheduleNext(beat); }
    else typeTimer = setTimeout(step, 32);
  })();
}
function completeType() {
  var beat = window.ACTS[actIdx].beats[beatIdx];
  if (!typing) return false;
  clearTimeout(typeTimer); typing = false;
  if (beat.text) textEl.textContent = fullText; else dirEl.textContent = fullText;
  scheduleNext(beat);
  return true;
}
function scheduleNext(beat) {
  clearTimeout(holdTimer);
  if (!playing) return;
  holdTimer = setTimeout(function () { next(true); }, beatDelay(beat));
}
function next(auto) {
  var act = window.ACTS[actIdx];
  if (typing && !auto) { completeType(); return; }
  if (beatIdx < act.beats.length - 1) { beatIdx++; render(); }
  else { setPlaying(false); }
}
function prev() {
  if (typing) { completeType(); return; }
  if (beatIdx > 0) { beatIdx--; render(); }
}
function setPlaying(v) {
  playing = v;
  $("bPlay").textContent = playing ? "暂停" : "播放";
  clearTimeout(holdTimer);
  if (playing && !typing) {
    var beat = window.ACTS[actIdx].beats[beatIdx];
    scheduleNext(beat);
  }
}
function loadAct(i) {
  actIdx = i; beatIdx = 0; lastAct = -1;
  clearProps(); buildCast(window.ACTS[actIdx]);
  setPlaying(true); render();
}
document.querySelectorAll("#controls [data-act]").forEach(function (b) {
  b.addEventListener("click", function () { loadAct(+b.dataset.act); });
});
$("bPlay").addEventListener("click", function () { setPlaying(!playing); });
$("bReplay").addEventListener("click", function () { loadAct(actIdx); });
$("bNext").addEventListener("click", function () { setPlaying(true); next(false); });
$("bPrev").addEventListener("click", function () { prev(); });
$("bSound").addEventListener("click", function () {
  soundOn = !soundOn;
  $("bSound").textContent = soundOn ? "声音：开" : "声音：关";
  if (soundOn) ac();
});
$("caption").addEventListener("click", function () { setPlaying(true); next(false); });
stage.addEventListener("click", function () { setPlaying(true); next(false); });
document.addEventListener("keydown", function (e) {
  if (e.code === "Space" || e.code === "Enter") { e.preventDefault(); setPlaying(true); next(false); }
  else if (e.key === "p" || e.key === "P") setPlaying(!playing);
  else if (e.key === "r" || e.key === "R") loadAct(actIdx);
  else if (e.key === "ArrowRight") next(false);
  else if (e.key === "ArrowLeft") prev();
});
buildCast(window.ACTS[0]);
render();
})();
