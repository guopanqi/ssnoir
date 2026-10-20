#!/usr/bin/env node
/**
 * Film-noir looping BGM generator — pure Node.js DSP, no deps.
 *
 * Seamless-loop architecture:
 *  - Musical events render into a buffer of [loopLen + tail]. Notes that decay
 *    past the loop end are wrap-added back onto the start, so resonance
 *    continues exactly as it would in an infinite loop.
 *  - Sustained/ambient layers (noise beds, drones) are generated with exactly
 *    loop-length buffers, integer-cycle LFOs and freq quantization, so they
 *    are seamless by construction.
 */
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const SR = 44100;
const TAU = Math.PI * 2;
const OUT_DIR = path.dirname(new URL(import.meta.url).pathname);
const AUDIO_DIR = path.join(OUT_DIR, "audio");
fs.mkdirSync(AUDIO_DIR, { recursive: true });

/* ---------------------------------------------------------------- utils */

function mulberry32(seed) {
  let a = seed >>> 0;
  return function () {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const midiF = (m) => 440 * Math.pow(2, (m - 69) / 12);
const clamp = (x, a, b) => (x < a ? a : x > b ? b : x);

/** In-place RBJ biquad on a Float32Array. kind: 'lp'|'hp'|'bp' */
function biquad(buf, kind, freq, q = 0.707) {
  const w0 = TAU * freq / SR;
  const cw = Math.cos(w0), sw = Math.sin(w0);
  const alpha = sw / (2 * q);
  let b0, b1, b2, a0, a1, a2;
  if (kind === "lp") {
    b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2;
    a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha;
  } else if (kind === "hp") {
    b0 = (1 + cw) / 2; b1 = -(1 + cw); b2 = (1 + cw) / 2;
    a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha;
  } else {
    b0 = alpha; b1 = 0; b2 = -alpha;
    a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha;
  }
  b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
  let x1 = 0, x2 = 0, y1 = 0, y2 = 0;
  for (let i = 0; i < buf.length; i++) {
    const x = buf[i];
    const y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
    x2 = x1; x1 = x; y2 = y1; y1 = y;
    buf[i] = y;
  }
}

/* ------------------------------------------------------------- renderer */

class Renderer {
  constructor(loopSecs, tailSecs, seed) {
    this.sr = SR;
    this.loopN = Math.round(loopSecs * SR);
    this.tailN = Math.round(tailSecs * SR);
    this.total = this.loopN + this.tailN;
    this.L = new Float32Array(this.total);
    this.R = new Float32Array(this.total);
    this.rng = mulberry32(seed);
  }
  panGains(pan = 0) {
    const p = (clamp(pan, -1, 1) + 1) * (Math.PI / 4);
    return [Math.cos(p), Math.sin(p)];
  }
}

/* ----------------------------------------------------- musical map used */

const beatSec = (bpm) => 60 / bpm;
const barSecOf = (bpm) => beatSec(bpm) * 4;

/* ----------------------------------------------------------- instruments */

/** Piano: decaying inharmonic partials + hammer noise + damper release. */
function piano(R, tSec, m, durSec = 3, vel = 1, { pan = 0, bright = 0 } = {}) {
  const f0 = midiF(m);
  const decay = clamp(5.2 * Math.sqrt(220 / f0), 0.6, 7.2);
  const start = Math.max(0, Math.round(tSec * SR));
  const len = Math.min(Math.round(decay * SR) + (SR >> 1), R.total - start);
  if (len <= 0) return;
  const relS = clamp(Math.round(durSec * SR), 0, len);
  const gEnd = relS + Math.round(0.7 * SR); // damper tail budget
  const [lg, rg] = R.panGains(pan);
  for (let h = 1; h <= 9; h++) {
    const f = f0 * h * (1 + 0.00035 * h * h);
    if (f > SR * 0.42) break;
    let amp = Math.pow(h, -1.5) * (1 + bright * Math.max(0, 1 - h * 0.12));
    if (vel < 0.45) amp *= h <= 3 ? 1 : Math.pow(0.55, h - 3);
    const dTau = decay / (1 + 0.33 * (h - 1));
    const atkS = Math.max(8, Math.round(SR * (0.002 + 0.003 * h)));
    const kA = Math.exp(-1 / atkS);
    const kD = Math.exp(-1 / (SR * dTau));
    const kR = Math.exp(-1 / (0.055 * SR));
    let eA = kA, eD = 1, eR = 1, ph = R.rng() * TAU;
    const w = TAU * f / SR;
    const lplen = Math.min(len, gEnd, Math.round(SR * 11.5 * dTau) + atkS + 8);
    for (let i = 0; i < lplen; i++) {
      const env = (1 - eA) * eD * (i > relS ? eR : 1);
      const s = Math.sin(ph) * env * amp * 0.3 * vel;
      ph += w;
      eA = Math.min(eA * kA, 1);
      eD *= kD;
      if (i > relS) eR *= kR;
      R.L[start + i] += s * lg;
      R.R[start + i] += s * rg;
    }
  }
  // hammer thump
  const nl = Math.round(0.012 * SR);
  let lp = 0;
  for (let i = 0; i < nl && start + i < R.total; i++) {
    const x = (R.rng() * 2 - 1) * (1 - i / nl);
    lp += 0.35 * (x - lp);
    const s = lp * 0.09 * vel;
    R.L[start + i] += s * lg; R.R[start + i] += s * rg;
  }
}

/** Upright bass: warm sine stack + gentle drive + pluck noise. */
function bass(R, tSec, m, durSec = 1, vel = 1, pan = 0) {
  const f = midiF(m);
  const start = Math.max(0, Math.round(tSec * SR));
  const end = Math.min(start + Math.round((durSec + 0.15) * SR), R.total);
  if (start >= R.total) return;
  const relS = Math.round(durSec * SR);
  const [lg, rg] = R.panGains(pan);
  const w = TAU * f / SR, w2 = w * 2, w3 = w * 3;
  const kA = Math.exp(-1 / (0.006 * SR)), kD = Math.exp(-1 / (1.3 * SR)),
    kR = Math.exp(-1 / (0.05 * SR));
  let eA = kA, eD = 1, eR = 1, p1 = R.rng() * TAU, p2 = p1, p3 = p1;
  for (let i = 0; start + i < end; i++) {
    const abs = start + i;
    const env = (1 - eA) * eD * (i > relS ? eR : 1);
    const s = Math.sin(p1) + 0.35 * Math.sin(p2) + 0.1 * Math.sin(p3);
    p1 += w; p2 += w2; p3 += w3;
    eA = Math.min(eA * kA, 1); eD *= kD;
    if (i > relS) eR *= kR;
    const out = Math.tanh(1.6 * s) * env * 0.5 * vel;
    R.L[abs] += out * lg; R.R[abs] += out * rg;
  }
  // pluck noise
  const nl = Math.round(0.01 * SR);
  let hp = 0, lpx = 0;
  for (let i = 0; i < nl && start + i < R.total; i++) {
    const x = R.rng() * 2 - 1;
    lpx += 0.5 * (x - lpx);
    hp = x - lpx;
    const s = hp * (1 - i / nl) * 0.05 * vel;
    R.L[start + i] += s * lg; R.R[start + i] += s * rg;
  }
}

/** Brush swish on snare: band-limited noise swell. */
function swish(R, tSec, vel = 1, lenSec = 0.3, pan = 0) {
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round(lenSec * SR), R.total - start);
  if (n <= 0) return;
  const [lg, rg] = R.panGains(pan * 0.6);
  const atk = Math.max(2, (n * 0.22) | 0);
  const kD = Math.exp(-1 / (lenSec * 0.4 * SR));
  let eD = 1, lp = 0;
  for (let i = 0; i < n; i++) {
    const x = R.rng() * 2 - 1;
    lp += 0.22 * (x - lp); // ≈2.3 kHz one-pole
    const env = Math.min(i / atk, 1) * (i > atk ? eD : 1);
    eD *= kD;
    const s = lp * env * 0.17 * vel;
    R.L[start + i] += s * lg; R.R[start + i] += s * rg;
  }
}

/** Closed hat / sock tick. */
function hatTick(R, tSec, vel = 1, pan = 0.3) {
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round(0.045 * SR), R.total - start);
  if (n <= 0) return;
  const buf = new Float32Array(n);
  for (let i = 0; i < n; i++) buf[i] = R.rng() * 2 - 1;
  biquad(buf, "bp", 7800, 1.1);
  const [lg, rg] = R.panGains(pan);
  for (let i = 0; i < n; i++) {
    const s = buf[i] * Math.exp(-i / (0.016 * SR)) * 0.16 * vel;
    R.L[start + i] += s * lg; R.R[start + i] += s * rg;
  }
}

/** Soft kit kick. */
function kick(R, tSec, vel = 1) {
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round(0.3 * SR), R.total - start);
  const kD = Math.exp(-1 / (0.11 * SR));
  let eD = 1;
  for (let i = 0; i < n; i++) {
    const f = 42 + 52 * Math.exp(-i / (0.05 * SR));
    const s = Math.sin(TAU * f / SR * i) * eD * eD;
    eD *= kD;
    const out = s * 0.34 * vel;
    R.L[start + i] += out; R.R[start + i] += out;
  }
}

/** Rim click / brush backbeat. */
function rim(R, tSec, vel = 1) {
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round(0.09 * SR), R.total - start);
  if (n <= 0) return;
  const buf = new Float32Array(n);
  for (let i = 0; i < n; i++) buf[i] = R.rng() * 2 - 1;
  biquad(buf, "bp", 1850, 1.4);
  for (let i = 0; i < n; i++) {
    const s = buf[i] * Math.exp(-i / (0.02 * SR)) * 0.14 * vel;
    R.L[start + i] += s; R.R[start + i] += s;
  }
}

/** Ride cymbal ping + wash. */
function ride(R, tSec, vel = 1, pan = 0.35) {
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round(1.1 * SR), R.total - start);
  const [lg, rg] = R.panGains(pan);
  const base = 500 + R.rng() * 90;
  const parts = [[1, 1], [1.48, 0.5], [2.34, 0.42], [3.01, 0.27], [4.2, 0.15]];
  const ks = parts.map(([, a]) => a);
  const kp = parts.map(() => R.rng() * TAU);
  const kw = parts.map(([r]) => TAU * base * r / SR);
  const kD = parts.map(() => Math.exp(-1 / (SR * 0.75)));
  for (let i = 0; i < n; i++) {
    let s = 0;
    for (let k = 0; k < parts.length; k++) {
      s += Math.sin(kp[k]) * ks[k];
      kp[k] += kw[k]; ks[k] *= kD[k];
    }
    const env = Math.min(i / (0.001 * SR), 1);
    const out = s * env * 0.075 * vel;
    R.L[start + i] += out * lg; R.R[start + i] += out * rg;
  }
  const wl = Math.round(0.08 * SR);
  let lp = 0;
  for (let i = 0; i < wl && start + i < R.total; i++) {
    const x = R.rng() * 2 - 1;
    lp += 0.45 * (x - lp);
    const s = lp * (1 - i / wl) * 0.05 * vel;
    R.L[start + i] += s * lg; R.R[start + i] += s * rg;
  }
}

/** Harmon-mute trumpet note. */
function trumpet(R, tSec, m, durSec = 1, vel = 1, pan = 0.25) {
  const f0 = midiF(m);
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round((durSec + 0.2) * SR), R.total - start);
  if (n <= 0) return;
  const relS = Math.round(durSec * SR);
  const seg = new Float32Array(n);
  const vibW = TAU * 5.4 / SR;
  let ph = R.rng() * TAU, vp = R.rng() * TAU, pcw = 0;
  for (let i = 0; i < n; i++) {
    const vibRamp = Math.min(1, Math.max(0, (i / SR - 0.25) / 0.3));
    const bend = 1 - 0.022 * Math.exp(-i / (0.045 * SR));
    const f = f0 * bend * (1 + 0.007 * Math.sin(vp) * vibRamp);
    const w = TAU * f / SR;
    ph += w; vp += vibW;
    pcw = ph;
    let s = 0;
    for (let h = 1; h <= 10; h++) s += Math.sin(pcw * h + h * 1.7) / Math.pow(h, 1.15);
    seg[i] = s;
  }
  biquad(seg, "lp", 2600, 0.9);
  const bp = Float32Array.from(seg);
  biquad(bp, "bp", 1350, 1.6);
  const [lg, rg] = R.panGains(pan);
  const kA = Math.exp(-1 / (0.03 * SR)), kR = Math.exp(-1 / (0.11 * SR));
  let eA = kA, eR = 1, pk = 0;
  for (let i = 0; i < n; i++) pk = Math.max(pk, Math.abs(seg[i] + bp[i] * 0.6));
  const norm = pk > 0 ? 0.5 * vel / pk : 0;
  for (let i = 0; i < n; i++) {
    const env = (1 - eA) * (i > relS ? eR : 1);
    eA = Math.min(eA * kA, 1);
    if (i > relS) eR *= kR;
    const out = (seg[i] + bp[i] * 0.6) * env * norm;
    R.L[start + i] += out * lg; R.R[start + i] += out * rg;
  }
}

/** Karplus–Strong pluck (guzheng-ish / guitar-ish). */
function pluck(R, tSec, m, durSec = 1, vel = 1, { tone = 0.5, pan = 0, decayScale = 1 } = {}) {
  const f = midiF(m);
  const start = Math.max(0, Math.round(tSec * SR));
  const decay = clamp(2.4 * Math.pow(240 / f, 0.45), 0.35, 3.2) * decayScale;
  const n = Math.min(Math.round(Math.min(decay, durSec + 0.3) * SR), R.total - start);
  if (n <= 0) return;
  const N = Math.max(2, Math.round(SR / f));
  const buf = new Float32Array(N);
  for (let i = 0; i < N; i++) buf[i] = (R.rng() * 2 - 1) * vel;
  const passes = tone < 0.35 ? 3 : tone > 0.6 ? 0 : 1; // darker excitation
  for (let p = 0; p < passes; p++) {
    let prev = buf[N - 1];
    for (let i = 0; i < N; i++) {
      const cur = buf[i];
      buf[i] = 0.5 * (cur + prev) * 1.35;
      prev = cur;
    }
  }
  const damp = Math.pow(1e-4, 1 / (decay * SR));
  const [lg, rg] = R.panGains(pan);
  const relS = Math.round(durSec * 0.9 * SR);
  let eR = 1;
  const kR = Math.exp(-1 / (0.08 * SR));
  for (let i = 0; i < n; i++) {
    const j = i % N;
    const y = buf[j];
    buf[j] = damp * 0.5 * (y + buf[(j + 1) % N]);
    const env = i > relS ? (eR *= kR) : 1;
    const out = y * 0.3 * vel * env;
    R.L[start + i] += out * lg; R.R[start + i] += out * rg;
  }
}

/** Breathy sax stab. */
function saxStab(R, tSec, m, durSec = 0.4, vel = 1, pan = -0.35) {
  const f0 = midiF(m);
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round((durSec + 0.15) * SR), R.total - start);
  if (n <= 0) return;
  const relS = Math.round(durSec * SR);
  const seg = new Float32Array(n);
  for (let i = 0; i < n; i++) {
    const bend = 1 + 0.03 * Math.exp(-i / (0.02 * SR));
    const w = TAU * f0 * bend / SR;
    let s = 0;
    for (let h = 1; h <= 12; h++) s += Math.sin(w * h * i + h * 2.1) / Math.pow(h, 1.1);
    seg[i] = s;
  }
  biquad(seg, "lp", 2900, 0.9);
  const br = Float32Array.from(seg);
  biquad(br, "bp", 2100, 1.0);
  const [lg, rg] = R.panGains(pan);
  const kA = Math.exp(-1 / (0.012 * SR)), kR = Math.exp(-1 / (0.07 * SR));
  let eA = kA, eR = 1, pk = 0;
  for (let i = 0; i < n; i++) pk = Math.max(pk, Math.abs(seg[i]) + Math.abs(br[i]) * 0.12);
  const norm = pk > 0 ? 0.26 * vel / pk : 0;
  for (let i = 0; i < n; i++) {
    const env = (1 - eA) * (i > relS ? eR : 1);
    eA = Math.min(eA * kA, 1);
    if (i > relS) eR *= kR;
    const out = (seg[i] + br[i] * 0.12) * env * norm;
    R.L[start + i] += out * lg; R.R[start + i] += out * rg;
  }
}

/** Soft bell / celesta note. */
function bell(R, tSec, m, vel = 1, pan = -0.2) {
  const f = midiF(m);
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round(1.6 * SR), R.total - start);
  if (n <= 0) return;
  const [lg, rg] = R.panGains(pan);
  const parts = [[1, 1], [3.98, 0.28], [9.2, 0.08]];
  const kp = parts.map(() => R.rng() * TAU);
  const kw = parts.map(([r]) => TAU * f * r / SR);
  const kd = [Math.exp(-1 / (SR * 0.55)), Math.exp(-1 / (SR * 0.3)), Math.exp(-1 / (SR * 0.15))];
  const ks = parts.map(([, a]) => a);
  for (let i = 0; i < n; i++) {
    let s = 0;
    for (let k = 0; k < 3; k++) { s += Math.sin(kp[k]) * ks[k]; kp[k] += kw[k]; ks[k] *= kd[k]; }
    const env = Math.min(i / (0.002 * SR), 1);
    const out = s * env * 0.14 * vel;
    R.L[start + i] += out * lg; R.R[start + i] += out * rg;
  }
}

/** Low tam-tam gong. */
function gong(R, tSec, vel = 1, pan = 0.15) {
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round(6 * SR), R.total - start);
  if (n <= 0) return;
  const [lg, rg] = R.panGains(pan);
  const base = 96;
  const parts = [[1, 0.9], [1.47, 0.6], [2.09, 0.5], [2.83, 0.3], [3.9, 0.22], [5.1, 0.1]];
  const kp = parts.map(() => R.rng() * TAU);
  const kw = parts.map(([r]) => TAU * base * r / SR);
  const kd = parts.map((_, k) => Math.exp(-1 / (SR * (3.4 - k * 0.35))));
  const ks = parts.map(([, a]) => a);
  for (let i = 0; i < n; i++) {
    let s = 0;
    for (let k = 0; k < parts.length; k++) { s += Math.sin(kp[k]) * ks[k]; kp[k] += kw[k]; ks[k] *= kd[k]; }
    const env = Math.min(i / (0.05 * SR), 1);
    const out = Math.tanh(0.9 * s) * env * 0.3 * vel;
    R.L[start + i] += out * lg; R.R[start + i] += out * rg;
  }
  let lp = 0;
  const th = Math.round(0.06 * SR);
  for (let i = 0; i < th; i++) {
    const x = (R.rng() * 2 - 1) * (1 - i / th);
    lp += 0.08 * (x - lp);
    R.L[start + i] += lp * 0.4 * vel; R.R[start + i] += lp * 0.4 * vel;
  }
}

/** Wooden fish / temple block tick. */
function woodTick(R, tSec, vel = 1, pan = 0) {
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round(0.12 * SR), R.total - start);
  if (n <= 0) return;
  const [lg, rg] = R.panGains(pan);
  const w = TAU * 760 / SR;
  let ph = R.rng() * TAU;
  for (let i = 0; i < n; i++) {
    const s = (Math.sin(ph) * 0.7 + (R.rng() * 2 - 1) * 0.3) * Math.exp(-i / (0.028 * SR));
    ph += w;
    const out = s * 0.22 * vel;
    R.L[start + i] += out * lg; R.R[start + i] += out * rg;
  }
}

/** Grandfather-clock tick, alternating tick/tock. */
function clockTick(R, tSec, isTock, vel = 1) {
  const start = Math.max(0, Math.round(tSec * SR));
  const n = Math.min(Math.round(0.1 * SR), R.total - start);
  if (n <= 0) return;
  const pan = isTock ? -0.3 : 0.3;
  const [lg, rg] = R.panGains(pan);
  const w = TAU * (isTock ? 1750 : 2350) / SR;
  let ph = R.rng() * TAU;
  for (let i = 0; i < n; i++) {
    const s = (Math.sin(ph) * 0.8 + (R.rng() * 2 - 1) * 0.35) * Math.exp(-i / (0.022 * SR));
    ph += w;
    const out = s * 0.1 * vel * (isTock ? 0.75 : 1);
    R.L[start + i] += out * lg; R.R[start + i] += out * rg;
  }
}

/** Low piano cluster (adjacent keys pressed, dark). */
function cluster(R, tSec, mA, mB, vel = 1) {
  piano(R, tSec, mA, 4.5, vel, { pan: -0.15, bright: -0.6 });
  piano(R, tSec + 0.012, mB, 4.5, vel * 0.92, { pan: 0.15, bright: -0.6 });
}

/** Heartbeat lub-dub. */
function heartbeat(R, tSec, vel = 1) {
  kick(R, tSec, 0.55 * vel);
  kick(R, tSec + 0.38, 0.34 * vel);
}

/* ------------------------------------------------------- ambient layers */

/**
 * Steady noise bed, exactly loop-length (seam-safe), integer-cycle swell.
 * lp: one-pole coefficient (0.2 ≈ 1.4kHz, 0.14 ≈ 1kHz) — washes into "rain".
 */
function bed(R, { gain = 0.03, lp = 0.16, swellAmp = 0.2, swellCycles = 1, seed = 77 } = {}) {
  const N = R.loopN;
  for (const [buf, salt] of [[R.L, 1], [R.R, 2]]) {
    const rng = mulberry32(seed * 7919 + salt);
    let lpv = 0;
    for (let i = 0; i < N; i++) {
      const x = rng() * 2 - 1;
      lpv += lp * (x - lpv);
      const t = i / N; // 0..1 per loop → sin(TAU*swellCycles*t) is seam-free
      buf[i] += lpv * gain * (1 + swellAmp * Math.sin(TAU * swellCycles * t));
    }
  }
}

/**
 * Sustained sine drone. Frequencies are quantized to integer cycles per loop;
 * vibrato & swell LFOs run integer cycles per loop → seamless by construction.
 */
function drone(R, freqs, gain = 0.55, { vibCents = 10, vibCycles = 2, swellAmp = 0.16, pan = 0 } = {}) {
  const loopSecs = R.loopN / SR;
  const [lg, rg] = R.panGains(pan);
  for (const f0 of freqs) {
    const cycles = Math.max(1, Math.round(f0 * loopSecs));
    const fq = cycles / loopSecs;
    const w = TAU * fq / SR;
    const vw = TAU * vibCycles / R.loopN;
    const sw = TAU / R.loopN;
    for (const [octMul, octGain, ph0] of [[1, 0.5, null], [2, 0.12, null]]) {
      let ph = R.rng() * TAU, vp = R.rng() * TAU;
      for (let i = 0; i < R.loopN; i++) {
        // vib depth as a ratio whose total arc per loop is integer cycles
        const vib = 1 + (vibCents / 1200) * Math.sin(vp);
        const env = gain * octGain * (1 + swellAmp * Math.sin(sw * i));
        const s = Math.sin(ph) * env;
        ph += w * octMul * vib;
        vp += vw;
        R.L[i] += s * lg;
        R.R[i] += s * rg;
      }
    }
  }
}

/** Vinyl surface: steady hiss + sparse crackle and pops (loop-length layer). */
function vinyl(R, { hiss = 0.01, crackle = 3.5 } = {}) {
  const N = R.loopN;
  for (const [buf, salt] of [[R.L, 5], [R.R, 9]]) {
    const rng = mulberry32(555 + salt);
    let lp = 0;
    for (let i = 0; i < N; i++) {
      const x = rng() * 2 - 1;
      lp += 0.5 * (x - lp);
      buf[i] += (x * 0.55 + lp * 0.45) * hiss;
    }
    const nPops = Math.round(crackle * (N / SR));
    for (let k = 0; k < nPops; k++) {
      const pos = Math.floor(rng() * (N - 0.05 * SR));
      const amp = 0.15 + rng() * rng() * 0.85;
      const dur = Math.round((0.001 + rng() * 0.006) * SR);
      let lp2 = 0;
      const len = rng() < 0.08 && amp > 0.7 ? dur * 6 : dur;
      const kLow = rng() < 0.08 && amp > 0.7 ? 0.06 : 0.4;
      const g = kLow < 0.1 ? 0.5 : 0.22;
      for (let i = 0; i < len; i++) {
        const x2 = (rng() * 2 - 1) * Math.exp(-i / (len * 0.5));
        lp2 += kLow * (x2 - lp2);
        buf[pos + i] += lp2 * g * amp;
      }
    }
  }
}

/* --------------------------------------------------------------- reverb */

/** Freeverb-style stereo reverb over full buffer. */
function reverb(R, { fb = 0.855, damp = 0.42, wet = 0.36, dryBalance = 0.85 } = {}) {
  const combTunings = [1557, 1617, 1491, 1422, 1277, 1116];
  const apTunings = [225, 556, 441, 341];
  const combL = combTunings.map((d) => ({ buf: new Float32Array(d), idx: 0, last: 0 }));
  const combR = combTunings.map((d) => ({ buf: new Float32Array(d + 23), idx: 0, last: 0 }));
  const apL = apTunings.map((d) => ({ buf: new Float32Array(d), idx: 0 }));
  const apR = apTunings.map((d) => ({ buf: new Float32Array(d), idx: 0 }));
  const damp2 = 1 - damp;
  const combStep = (c, x) => {
    const y = c.buf[c.idx];
    const filt = y * damp2 + c.last * damp;
    c.last = filt;
    c.buf[c.idx] = x + filt * fb;
    if (++c.idx >= c.buf.length) c.idx = 0;
    return y;
  };
  const apStep = (a, x) => {
    const bufout = a.buf[a.idx];
    a.buf[a.idx] = x + bufout * 0.5;
    if (++a.idx >= a.buf.length) a.idx = 0;
    return bufout - x;
  };
  const wetOutL = new Float32Array(R.total);
  const wetOutR = new Float32Array(R.total);
  for (let i = 0; i < R.total; i++) {
    const xl = R.L[i] + R.R[i] * 0.28;
    const xr = R.R[i] + R.L[i] * 0.28;
    let sl = 0, sr = 0;
    for (let k = 0; k < combL.length; k++) { sl += combStep(combL[k], xl); sr += combStep(combR[k], xr); }
    let yl = sl / combL.length, yr = sr / combR.length;
    for (let k = 0; k < apL.length; k++) { yl = apStep(apL[k], yl); yr = apStep(apR[k], yr); }
    wetOutL[i] = yl; wetOutR[i] = yr;
  }
  for (let i = 0; i < R.total; i++) {
    const dl = R.L[i] * dryBalance + wetOutL[i] * wet;
    const dr = R.R[i] * dryBalance + wetOutR[i] * wet;
    R.L[i] = dl; R.R[i] = dr;
  }
}

/* ----------------------------------------------------------- master chain */

function finish(R, name, { lpFreq = 9500, reps = 2, rmsTarget = 0.16, outDir = AUDIO_DIR } = {}) {
  fs.mkdirSync(outDir, { recursive: true });
  // wrap decaying tails onto the loop start
  for (let i = 0; i < R.tailN; i++) {
    R.L[i] += R.L[R.loopN + i];
    R.R[i] += R.R[R.loopN + i];
  }
  R.L = R.L.subarray(0, R.loopN);
  R.R = R.R.subarray(0, R.loopN);

  // "old tape" gentle top roll-off
  if (lpFreq > 0) { biquad(R.L, "lp", lpFreq, 0.6); biquad(R.R, "lp", lpFreq, 0.6); }

  // loudness-match: linear gain to a common RMS, then a soft limiter that is
  // linear below ~-15dBFS and only tames peaks (C chosen so out->0.90 at tanh)
  let peak = 0, sumSq = 0;
  for (let i = 0; i < R.loopN; i++) {
    peak = Math.max(peak, Math.abs(R.L[i]), Math.abs(R.R[i]));
    sumSq += R.L[i] * R.L[i] + R.R[i] * R.R[i];
  }
  if (!isFinite(peak) || peak === 0) throw new Error(`${name}: bad peak ${peak}`);
  const g = clamp(rmsTarget / Math.sqrt(sumSq / (2 * R.loopN)), 0.05, 3.2);
  const A = 0.92; // limiter asymptote — slope stays 1 in the linear region
  for (let i = 0; i < R.loopN; i++) {
    R.L[i] = A * Math.tanh(R.L[i] * g / A);
    R.R[i] = A * Math.tanh(R.R[i] * g / A);
  }
  // measure final stats
  peak = 0; sumSq = 0;
  for (let i = 0; i < R.loopN; i++) {
    peak = Math.max(peak, Math.abs(R.L[i]), Math.abs(R.R[i]));
    sumSq += R.L[i] * R.L[i] + R.R[i] * R.R[i];
  }
  const rms = Math.sqrt(sumSq / (2 * R.loopN));
  const gainPeak = 1;

  // write 16-bit stereo WAV, repeated `reps` times for a longer audition file
  const n = R.loopN;
  const pcm = new Int16Array(n * 2 * reps);
  for (let r = 0; r < reps; r++) {
    for (let i = 0; i < n; i++) {
      const o = (r * n + i) * 2;
      pcm[o] = Math.max(-32768, Math.min(32767, Math.round(R.L[i] * 32767)));
      pcm[o + 1] = Math.max(-32768, Math.min(32767, Math.round(R.R[i] * 32767)));
    }
  }
  const wavPath = path.join(outDir, `${name}.wav`);
  const dataSize = pcm.length * 2;
  const header = Buffer.alloc(44);
  header.write("RIFF", 0); header.writeUInt32LE(36 + dataSize, 4); header.write("WAVE", 8);
  header.write("fmt ", 12); header.writeUInt32LE(16, 16); header.writeUInt16LE(1, 20);
  header.writeUInt16LE(2, 22); header.writeUInt32LE(SR, 24); header.writeUInt32LE(SR * 4, 28);
  header.writeUInt16LE(4, 32); header.writeUInt16LE(16, 34);
  header.write("data", 36); header.writeUInt32LE(dataSize, 40);
  fs.writeFileSync(wavPath, Buffer.concat([header, Buffer.from(pcm.buffer)]));

  // compressed audition copy
  const m4aPath = path.join(outDir, `${name}.m4a`);
  try {
    execSync(`afconvert -f m4af -d aac -b 256000 "${wavPath}" "${m4aPath}"`, { stdio: "pipe" });
  } catch {
    execSync(`ffmpeg -y -i "${wavPath}" -codec:a aac -b:a 256k "${m4aPath}"`, { stdio: "pipe" });
  }
  console.log(
    `${name}.wav  loop=${(n / SR).toFixed(2)}s x${reps}  peak=${peak.toFixed(3)} ` +
    `rms=${(20 * Math.log10(rms)).toFixed(1)}dB  ` +
    `wav=${(fs.statSync(wavPath).size / 1e6).toFixed(1)}MB m4a=${(fs.statSync(m4aPath).size / 1e6).toFixed(1)}MB`
  );
}

/* ------------------------------------------------------------ sequencer */

function barTime(bar, barSec, beat = 0) {
  return bar * barSec + beat * (barSec / 4);
}

/* ====================== TRACK 1 — 雨夜长巷 (Rainy Alley) ================= */
function track1() {
  const BPM = 72, barSec = barSecOf(BPM);
  const R = new Renderer(8 * barSec, 5.0, 20261019);

  /* progression: Dm9 | Bbmaj9 | Gm9 | A7b13 | Dm9 | Bb13 | E7b9 | A7b9 */
  const chords = [
    { p: [[0, [50, 57, 60, 64], 0.55, 2], [2.5, [60, 64, 69], 0.3, 1.2]], b: [[0, 38, 1.7], [2, 45, 0.8], [3, 43, 1]] },
    { p: [[0, [50, 62, 65], 0.5, 2], [2.5, [62, 65, 69, 72], 0.3, 1.2]],  b: [[0, 46, 1.7], [2, 44, 0.8], [3, 41, 1]] },
    { p: [[0, [58, 62, 65, 69], 0.5, 2], [2.5, [65, 69, 72], 0.28, 1.2]], b: [[0, 43, 1.7], [2, 41, 0.8], [3, 44, 1]] },
    { p: [[0, [61, 65, 67], 0.5, 2], [2.5, [61, 64, 65], 0.3, 1.2]],      b: [[0, 45, 1.7], [2, 41, 0.8], [3, 40, 1]] },
    { p: [[0, [53, 57, 60, 64], 0.55, 2], [2.5, [69, 72, 74], 0.28, 1.2]], b: [[0, 38, 1.7], [2, 41, 0.8], [3, 43, 1]] },
    { p: [[0, [56, 62, 65], 0.5, 2], [2.5, [62, 65, 69], 0.3, 1.2]],      b: [[0, 46, 1.7], [2, 41, 0.8], [3, 36, 1]] },
    { p: [[0, [56, 59, 62, 65], 0.5, 2], [2.5, [59, 62, 65, 68], 0.3, 1.2]], b: [[0, 40, 1.7], [2, 38, 0.8], [3, 44, 1]] },
    { p: [[0, [61, 64, 67, 70], 0.55, 2.2], [3.5, [73], 0.2, 0.5]],       b: [[0, 45, 1.7], [2, 49, 0.8], [3, 47, 1]] },
  ];
  chords.forEach((c, bar) => {
    for (const [beat, notes, vel, dur] of c.p)
      for (const m of notes)
        piano(R, barTime(bar, barSec, beat), m, dur * beatSec(BPM), vel, { pan: -0.1, bright: 0.35 });
    for (const [beat, m, dBeats] of c.b)
      bass(R, barTime(bar, barSec, beat), m, dBeats * beatSec(BPM) * 0.98, 0.85);
    // half-time brush groove
    swish(R, barTime(bar, barSec, 0), 0.9, 0.24, 0);
    hatTick(R, barTime(bar, barSec, 1), 0.4);
    swish(R, barTime(bar, barSec, 2), 1.25, 0.3, 0.08);
    hatTick(R, barTime(bar, barSec, 3), 0.4, -0.3);
    if (bar % 2 === 0) kick(R, barTime(bar, barSec, 0), 0.5);
  });
  rim(R, barTime(3, barSec, 3.5), 0.5);

  // muted trumpet phrases (bar, beat, midi, durBeats)
  const phr = [
    [1, 0, 65, 1.25], [1, 1.25, 64, 0.75], [2, 0, 60, 2.5],
    [5, 0, 67, 1.5], [5, 1.5, 65, 1], [6, 0, 62, 1], [6, 1, 60, 1], [6, 2, 58, 0.5], [7, 1, 61, 2],
  ];
  for (const [bar, beat, m, d] of phr)
    trumpet(R, barTime(bar, barSec, beat), m, d * beatSec(BPM) * 0.92, 0.8);

  // ambience: distant rain + vinyl
  bed(R, { gain: 0.03, lp: 0.16, swellAmp: 0.22, swellCycles: 1, seed: 4 });
  vinyl(R, { hiss: 0.004, crackle: 1.5 });

  finish(R, "01-rainy-alley", { lpFreq: 9000, reps: 2 });
}

/* ===================== TRACK 2 — 烟雾俱乐部 (Smoky Club) ================= */
function track2() {
  const BPM = 96, barSec = barSecOf(BPM), bs = beatSec(BPM);
  const R = new Renderer(12 * barSec, 5, 8703021);

  /* 12-bar minor blues in Fm */
  const VOX = {
    Fm7: [51, 56, 60], Bbm7: [49, 53, 56], D7b9: [54, 57, 60, 63],
    Gm7b5: [58, 61, 65], C7b9: [58, 61, 64],
  };
  const prog = ["Fm7", "Bbm7", "Fm7", "Fm7", "Bbm7", "Bbm7", "Fm7", "D7b9", "Gm7b5", "C7b9", "Fm7", "SPLIT"];
  prog.forEach((nm, bar) => {
    const vox = nm === "SPLIT" ? null : VOX[nm];
    if (vox) {
      for (const m of vox) piano(R, barTime(bar, barSec, 0), m, 1.4, 0.45, { pan: -0.1, bright: 0.4 });
      piano(R, barTime(bar, barSec, 1.5), vox[1], 0.55, 0.32, { pan: -0.1, bright: 0.4 });
      piano(R, barTime(bar, barSec, 1.5), vox[2], 0.55, 0.32, { pan: -0.1, bright: 0.4 });
      if (nm === "Fm7") piano(R, barTime(bar, barSec, 3), 67, 0.4, 0.22, { pan: 0.2, bright: 0.5 });
      if (nm === "Bbm7") piano(R, barTime(bar, barSec, 3), 60, 0.4, 0.22, { pan: 0.2, bright: 0.5 });
    } else {
      for (const m of VOX.Gm7b5) piano(R, barTime(bar, barSec, 0), m, 0.9, 0.45, { pan: -0.1, bright: 0.4 });
      for (const m of VOX.C7b9) piano(R, barTime(bar, barSec, 2), m, 0.9, 0.45, { pan: -0.1, bright: 0.4 });
    }
    // blues-scale fill at ends of bars 4 / 8 / 12
    if (bar === 3 || bar === 7 || bar === 11) {
      const scale = [65, 68, 70, 72, 75];
      const startBeat = bar === 11 ? 2.5 : 3.0;
      const nb = bar === 11 ? 3 : 4;
      let prev = -1;
      for (let k = 0; k < nb; k++) {
        let idx;
        do { idx = Math.floor(R.rng() * scale.length); } while (idx === prev);
        prev = idx;
        piano(R, barTime(bar, barSec, startBeat + k * 0.25), scale[idx], 0.35, 0.24, { pan: 0.15, bright: 0.6 });
      }
    }
  });

  // walking bass
  const walk = [
    [41, 43, 44, 46], [46, 49, 48, 40], [41, 44, 48, 51], [41, 43, 46, 48],
    [46, 49, 53, 56], [46, 53, 51, 48], [41, 44, 46, 48], [38, 45, 51, 44],
    [43, 46, 48, 49], [48, 52, 46, 43], [41, 48, 44, 43], [43, 46, 36, 40],
  ];
  walk.forEach((notes, bar) => {
    notes.forEach((m, q) => bass(R, barTime(bar, barSec, q), m, barSec / 4 * 0.9, 0.9));
  });

  // swing ride + brush backbeats
  prog.forEach((_, bar) => {
    for (const beat of [0, 1, 2, 3]) ride(R, barTime(bar, barSec, beat), beat % 2 === 0 ? 0.7 : 0.45);
    ride(R, barTime(bar, barSec, 1.666), 0.4);
    ride(R, barTime(bar, barSec, 3.666), 0.4);
    swish(R, barTime(bar, barSec, 1), 1.0, 0.22, -0.1);
    swish(R, barTime(bar, barSec, 3), 1.1, 0.24, 0.12);
    kick(R, barTime(bar, barSec, 0), 0.55);
    if (bar % 4 === 2) kick(R, barTime(bar, barSec, 3.5), 0.4);
  });

  // occasional distant muted-trumpet murmur
  trumpet(R, barTime(4, barSec, 1.5), 72, 1.2, 0.5);
  trumpet(R, barTime(5, barSec, 0), 68, 2.0, 0.45);

  vinyl(R, { hiss: 0.005, crackle: 2 });
  finish(R, "02-smoky-club", { lpFreq: 10500, reps: 3 });
}

/* =================== TRACK 3 — 侦探办公室 (Cold Case) ==================== */
function track3() {
  const R = new Renderer(16, 5, 3311204);

  drone(R, [43.65, 65.41, 87.31], 0.55, { vibCents: 10, vibCycles: 2, swellAmp: 0.16 });
  bed(R, { gain: 0.028, lp: 0.14, swellAmp: 0.2, swellCycles: 1, seed: 9 }); // rain on the window

  // low minor-second clusters
  cluster(R, 2.0, 29, 30, 0.6);
  cluster(R, 9.5, 39, 40, 0.5);
  // eerie celesta motif
  bell(R, 5.0, 77, 0.5);
  bell(R, 5.35, 80, 0.38);
  bell(R, 13.1, 80, 0.32);
  bell(R, 13.5, 84, 0.26); // major-third answer — false comfort

  for (let k = 0; k < 16; k++) clockTick(R, k * 1.0, k % 2 === 1, 1.0);

  heartbeat(R, 3.5, 1.0);
  heartbeat(R, 11.8, 0.8);

  vinyl(R, { hiss: 0.003, crackle: 0.6 });
  finish(R, "03-cold-case", { lpFreq: 8200, reps: 5 });
}

/* ==================== TRACK 4 — 长衫侠影 (Shanghai Noir) ================= */
function track4() {
  const BPM = 84, barSec = barSecOf(BPM), bs = beatSec(BPM);
  const R = new Renderer(8 * barSec, 5, 19110527);

  /* | Am6 | Dm6 | Am6 | E7b9 | x2 — pentatonic melody floats over */
  const VOX = { Am6: [57, 60, 64, 66], Dm6: [53, 57, 62, 71], E7b9: [59, 62, 65, 68] };
  const prog = ["Am6", "Dm6", "Am6", "E7b9", "Am6", "Dm6", "Am6", "E7b9"];
  prog.forEach((nm, bar) => {
    const vox = VOX[nm];
    for (const m of vox) piano(R, barTime(bar, barSec, 0), m, 2.0, 0.4, { pan: -0.1, bright: 0.3 });
    if (bar % 2 === 1) {
      piano(R, barTime(bar, barSec, 2.5), vox[1], 0.9, 0.26, { pan: 0.1, bright: 0.4 });
      piano(R, barTime(bar, barSec, 2.5), vox[2], 0.9, 0.26, { pan: 0.1, bright: 0.4 });
    }
    swish(R, barTime(bar, barSec, 0), 0.75, 0.22, 0);
    hatTick(R, barTime(bar, barSec, 1), 0.32);
    swish(R, barTime(bar, barSec, 2), 1.0, 0.26, 0.1);
    woodTick(R, barTime(bar, barSec, 2), 0.55, 0.25);
    if (bar % 2 === 0) kick(R, barTime(bar, barSec, 0), 0.45);
  });
  gong(R, barTime(4, barSec, 0), 0.5);

  // guzheng-style pentatonic phrases (A min pent: 57 60 62 64 67 69 72 74 76)
  const gz = [
    [0, 0, 64, 0.75, 0.8], [0, 0.75, 67, 0.25, 0.55], [0, 1, 69, 1.5, 0.85], [0, 2.5, 72, 0.5, 0.6],
    [1, 0, 74, 1.0, 0.8], [1, 1.5, 69, 0.5, 0.55], [1, 2, 67, 0.5, 0.6], [1, 2.5, 69, 1.25, 0.8],
    [2, 0, 72, 0.5, 0.7], [2, 0.5, 69, 0.5, 0.55], [2, 1, 67, 0.75, 0.6], [2, 2, 69, 1.0, 0.7],
    [3, 0, 71, 0.75, 0.7], [3, 1, 69, 0.75, 0.6], [3, 2, 67, 0.5, 0.55], [3, 2.5, 64, 1.0, 0.7],
    [4, 0, 76, 0.75, 0.85], [4, 0.75, 72, 0.25, 0.5], [4, 1, 74, 1.5, 0.8], [4, 2.5, 76, 0.5, 0.6],
    [5, 0, 69, 1.0, 0.65], [5, 1, 71, 0.5, 0.55], [5, 2, 72, 1.0, 0.65], [5, 3, 74, 0.75, 0.6],
    [6, 0, 69, 1.0, 0.7], [6, 2, 64, 0.75, 0.6], [6, 2.75, 67, 0.75, 0.5],
    [7, 0, 74, 0.75, 0.7], [7, 1, 71, 0.5, 0.6], [7, 2, 69, 1.75, 0.8],
  ];
  for (const [bar, b, m, d, v] of gz) {
    pluck(R, barTime(bar, barSec, b), m, d * bs * 0.97, v, { tone: 0.4, pan: -0.05 });
    if (b === 0 && bar % 2 === 0) // guzheng grace/slide
      pluck(R, barTime(bar, barSec, b) - 0.07, m - 1, 0.07, v * 0.5, { tone: 0.4, pan: -0.05 });
  }

  // halftime walking bass
  const bassLine = [
    [[0, 33, 1.7], [2, 40, 0.8], [3, 36, 1]],
    [[0, 38, 1.7], [2, 41, 0.8], [3, 43, 1]],
    [[0, 45, 1.7], [2, 40, 0.8], [3, 36, 1]],
    [[0, 40, 1.7], [2, 44, 0.8], [3, 35, 1]],
    [[0, 33, 1.7], [2, 40, 0.8], [3, 36, 1]],
    [[0, 38, 1.7], [2, 41, 0.8], [3, 43, 1]],
    [[0, 45, 1.7], [2, 40, 0.8], [3, 42, 1]],
    [[0, 40, 1.7], [2, 38, 0.8], [3, 40, 1]],
  ];
  bassLine.forEach((notes, bar) => {
    for (const [b, m, d] of notes) bass(R, barTime(bar, barSec, b), m, d * bs * 0.98, 0.8);
  });

  vinyl(R, { hiss: 0.0035, crackle: 1 });
  finish(R, "04-shanghai-noir", { lpFreq: 8800, reps: 3 });
}

/* ==================== TRACK 5 — 缉凶行动 (The Manhunt) =================== */
function track5() {
  const BPM = 112, barSec = barSecOf(BPM), bs = beatSec(BPM);
  const R = new Renderer(8 * barSec, 5, 6661312);

  const stabVox = [55, 59, 62, 66]; // Em9
  const riffA = [52, null, 55, 52, 57, null, 55, 52];
  const riffB = [52, null, 57, null, 58, 57, 55, null]; // Bb3 color
  const walkDown = [[62, 60, 58, 57], [55, 57, 58, null]]; // bars 6-7 guitar
  for (let bar = 0; bar < 8; bar++) {
    const t0 = bar * barSec;
    // bass pluck on the E pedal
    const bHits = bar === 7 ? [0, 2, 4] : [0, 2, 4, 6];
    for (const q of bHits) {
      pluck(R, t0 + q * bs, bar === 7 && q === 4 ? 55 : 40, bs * 0.9, bar % 2 === 0 ? 0.9 : 0.75, { tone: 0.3, pan: -0.15 });
    }
    // guitar riff
    if (bar <= 3 || bar === 5) {
      const riff = bar === 5 ? riffB : riffA;
      riff.forEach((m, idx) => {
        if (m === null) return;
        pluck(R, t0 + idx * bs, m, bs * 0.85, 0.7, { tone: 0.7, pan: 0.3 });
      });
    } else if (bar === 6 || bar === 7) {
      const notes = walkDown[bar - 6];
      notes.forEach((m, idx) => {
        if (m === null) return;
        pluck(R, t0 + idx * bs, m, bs * 0.85, 0.75, { tone: 0.7, pan: 0.3 });
      });
    }
    // piano stabs
    if (bar % 4 === 0 || bar === 6) {
      for (const m of stabVox) piano(R, barTime(bar, barSec, 0), m, 0.9, 0.42, { pan: -0.1, bright: 0.45 });
      for (const m of stabVox) piano(R, barTime(bar, barSec, 2.5), m, 0.5, 0.3, { pan: -0.1, bright: 0.45 });
    }
    // drums: driving straight-8ths
    kick(R, barTime(bar, barSec, 0), 0.7);
    kick(R, barTime(bar, barSec, 2.5), 0.55);
    kick(R, barTime(bar, barSec, 3.75), 0.45);
    rim(R, barTime(bar, barSec, 1), 0.8);
    rim(R, barTime(bar, barSec, 3), 0.85);
    for (let e = 0; e < 8; e++)
      hatTick(R, barTime(bar, barSec, 0) + e * bs, e % 2 === 0 ? 0.5 : 0.35, (e % 2 ? 1 : -1) * 0.3);
  }
  // sax punches
  saxStab(R, barTime(3, barSec, 2), 62, 0.5, 0.9);
  saxStab(R, barTime(3, barSec, 3), 59, 0.7, 0.8);
  saxStab(R, barTime(7, barSec, 2), 62, 0.5, 0.9);
  saxStab(R, barTime(7, barSec, 2.5), 64, 0.9, 1.0);

  vinyl(R, { hiss: 0.004, crackle: 1.5 });
  finish(R, "05-the-manhunt", { lpFreq: 11000, reps: 4 });
}

/* ------------------------------------------------------------------ run */

import { pathToFileURL } from "node:url";

const arg = process.argv[2];
const all = { 1: track1, 2: track2, 3: track3, 4: track4, 5: track5 };
const isMain = process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href;
if (isMain) {
  if (arg && all[arg]) all[arg]();
  else for (const fn of Object.values(all)) fn();
  console.log("done.");
}

/* reusable engine — variant scripts import from this module */
export {
  SR, TAU, clamp, midiF, mulberry32, biquad, Renderer,
  beatSec, barSecOf, barTime,
  piano, bass, swish, hatTick, kick, rim, ride, trumpet,
  pluck, saxStab, bell, gong, woodTick, clockTick, cluster, heartbeat,
  bed, drone, vinyl, finish,
};
