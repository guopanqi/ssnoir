#!/usr/bin/env node
/**
 * 烟雾俱乐部（02）的变体：同一 F 小调 12 小节布鲁斯底盘，四种提速/密度/阴晴方向。
 * 用法: node variants.mjs        渲染全部变体
 *       node variants.mjs a      只渲染 A（last-call）
 *
 * 变体:
 *   a  last-call       72 BPM  打烊时分 —— 更慢更散，小号唱一段完整的头
 *   b  house-band     116 BPM  台上乐队 —— 大乐队式，管乐 riff 进出
 *   c  backroom-deal   88 BPM  后房交易 —— 更阴：壳位和声、镲收掉、低二度簇
 *   d  dance-floor    132 BPM  点燃舞池 —— 更快更亮，贝斯八度跳跃 + 管乐呐喊
 */
import path from "node:path";
import { Renderer, beatSec, piano, bass, swish, hatTick, kick, rim, ride, trumpet,
         saxStab, cluster, vinyl, finish } from "./generate-bgm.mjs";

const OUT = path.join(path.dirname(new URL(import.meta.url).pathname), "variants");

function barTime(bar, barSec, beat = 0) {
  return bar * barSec + beat * (barSec / 4);
}
const T = { Fm7: [51, 56, 60], Bbm7: [49, 53, 56], D7b9: [54, 57, 60, 63], Gm7b5: [58, 61, 65], C7b9: [58, 61, 64] };
const PROG = ["Fm7", "Bbm7", "Fm7", "Fm7", "Bbm7", "Bbm7", "Fm7", "D7b9", "Gm7b5", "C7b9", "Fm7", "SPLIT"];
// 每小节 4 个四分音符的 walking bass 基线
const WALK = [
  [41, 43, 44, 46], [46, 49, 48, 40], [41, 44, 48, 51], [41, 43, 46, 48],
  [46, 49, 53, 56], [46, 53, 51, 48], [41, 44, 46, 48], [38, 45, 51, 44],
  [43, 46, 48, 49], [48, 52, 46, 43], [41, 48, 44, 43], [43, 46, 36, 40],
];

/** 按配置渲染一版布鲁斯底盘；extra(R, barSec, bs) 在收尾前追加变奏事件 */
function bluesChassis({
  bpm, seed, comp, drums, bassStyle = { v: 0.9, passing: false }, horn = null, extra = null,
  name, lpFreq = 10000, reps = 3, vinylCfg = { hiss: 0.005, crackle: 2 },
}) {
  const barSec = 240 / bpm, bs = beatSec(bpm);
  const R = new Renderer(12 * barSec, 5, seed);
  PROG.forEach((nm, bar) => {
    const t = (b) => barTime(bar, barSec, b);
    if (nm === "SPLIT") {
      for (const m of T.Gm7b5) piano(R, t(0), m, 0.9, comp.soft, { pan: -0.1, bright: comp.bright });
      for (const m of T.C7b9) piano(R, t(2), m, 0.9, comp.soft, { pan: -0.1, bright: comp.bright });
    } else {
      const vox = T[nm];
      for (const [beat, vel, dur] of comp.hits(bar)) {
        // 重拍给完整的 shell voicing，弱拍只剩两个高音
        const chord = beat === 0 ? vox : vox.slice(1);
        piano(R, t(beat), chord[0], dur, vel, { pan: -0.1, bright: comp.bright });
        if (chord[1]) piano(R, t(beat), chord[1], dur, vel, { pan: -0.1, bright: comp.bright });
        if (chord[2]) piano(R, t(beat), chord[2], dur, vel, { pan: -0.1, bright: comp.bright });
      }
    }
    drums(R, bar, barSec, bs);
    // walking bass；bassStyle.passing 开时在 1.5 / 3.5 拍加半音经过音
    WALK[bar].forEach((m, q) => {
      let note = m;
      if (bassStyle.octaveSkip && bar % 2 === 1 && q === 2) note += 12;
      bass(R, t(q), note, bs * 0.9, bassStyle.v);
      if (bassStyle.passing && (q === 0 || q === 2) && q + 1 < 4) {
        const mid = Math.round((WALK[bar][q] + WALK[bar][q + 1]) / 2);
        if (mid !== WALK[bar][q] && mid !== WALK[bar][q + 1])
          bass(R, t(q + 0.5), mid, bs * 0.4, bassStyle.v * 0.85);
      }
    });
  });
  if (horn) horn.forEach(([bar, beat, midi, dBeats, vel]) =>
    saxStab(R, barTime(bar, barSec, beat), midi, dBeats * bs, vel));
  if (extra) extra(R, barSec, bs);
  vinyl(R, vinylCfg);
  finish(R, name, { lpFreq, reps, outDir: OUT });
}

/* ---------------- A · 打烊时分 Last Call（72 BPM，慢散，小号头） --------------- */
function lastCall() {
  const BPM = 72, barSec = 240 / BPM, bs = beatSec(BPM);
  bluesChassis({
    bpm: BPM, seed: 220311, name: "02a-last-call", lpFreq: 8200, reps: 2,
    comp: { bright: 0.25, soft: 0.4, hits: (bar) => [
      [0, 0.42, 1.8], ...(bar % 2 === 1 ? [[2.5, 0.26, 0.7]] : []),
    ] },
    drums: (R, bar, barSec) => {
      swish(R, barTime(bar, barSec, 0), 0.7, 0.26, 0);
      hatTick(R, barTime(bar, barSec, 1), 0.25);
      swish(R, barTime(bar, barSec, 2), 0.95, 0.3, 0.1);
      hatTick(R, barTime(bar, barSec, 3), 0.25, -0.3);
      if (bar % 2 === 0) kick(R, barTime(bar, barSec, 0), 0.4);
      if (bar % 4 === 3) rim(R, barTime(bar, barSec, 3.75), 0.4);
    },
    bassStyle: { v: 0.8, passing: false },
    vinylCfg: { hiss: 0.004, crackle: 1 },
    extra: (R, barSec) => {
      // 打烊时分的小号头（bar, beat, midi, durBeats）
      const head = [
        [0, 0, 63, 1.5], [0, 1.5, 65, 1.0], [0, 2.5, 68, 1.25],
        [1, 0, 72, 1.5], [1, 1.5, 70, 1.5],
        [2, 0, 68, 2.0], [2, 2, 65, 1.0],
        [4, 1, 65, 1.5], [4, 2.5, 68, 0.75],
        [5, 0, 70, 1.0], [5, 1, 68, 1.5], [5, 2.5, 65, 0.5],
        [6, 0, 63, 2.0],
        [7, 0, 62, 1.0], [7, 1.5, 66, 0.75], [7, 2.25, 64, 0.75],
        [8, 0, 65, 1.5],
        [9, 0, 64, 1.0], [9, 1, 61, 1.5],
        [10, 0, 65, 2.5],
      ];
      for (const [bar, beat, m, d] of head)
        trumpet(R, barTime(bar, barSec, beat), m, d * bs * 0.92, 0.7);
      // bar 12 的柔和下行填充
      for (let k = 0; k < 3; k++)
        piano(R, barTime(11, barSec, 3.0 + k * 0.25), [68, 65, 63][k], 0.3, 0.2, { pan: 0.15, bright: 0.5 });
    },
  });
}

/* ---------------- B · 台上乐队 House Band（116 BPM，大乐队riff） -------------- */
function houseBand() {
  const BPM = 116, barSec = 240 / BPM, bs = beatSec(BPM);
  const horn = [
    // 每 4 小节末管乐 riff 进出：进 Bb 段 → 进 D7 段 → C7 上呼回主题
    [3, 3.0, 70, 0.25, 0.9], [3, 3.25, 68, 0.25, 0.8], [3, 3.5, 67, 0.4, 0.7],
    [7, 2.5, 66, 0.25, 0.9], [7, 2.75, 64, 0.25, 0.8], [7, 3.0, 65, 0.5, 0.75],
    [11, 2.5, 64, 0.25, 0.9], [11, 2.75, 67, 0.25, 0.85], [11, 3.0, 64, 0.25, 0.8], [11, 3.25, 62, 0.75, 0.9],
  ];
  bluesChassis({
    bpm: BPM, seed: 116417, name: "02b-house-band", lpFreq: 11000, reps: 3,
    comp: { bright: 0.45, soft: 0.5, hits: (bar) => [
      [0, 0.5, 1.2], [1.5, 0.36, 0.5], ...(bar % 2 === 1 ? [[2.5, 0.3, 0.5]] : []),
    ] },
    drums: (R, bar, barSec) => {
      for (const beat of [0, 1, 2, 3]) ride(R, barTime(bar, barSec, beat), beat % 2 === 0 ? 0.85 : 0.55);
      ride(R, barTime(bar, barSec, 1.666), 0.5);
      ride(R, barTime(bar, barSec, 3.666), 0.5);
      swish(R, barTime(bar, barSec, 1), 1.15, 0.2, -0.12);
      swish(R, barTime(bar, barSec, 3), 1.3, 0.24, 0.14);
      rim(R, barTime(bar, barSec, 1), 0.55);
      kick(R, barTime(bar, barSec, 0), 0.6);
      if (bar % 2 === 1) kick(R, barTime(bar, barSec, 2.5), 0.45);
    },
    bassStyle: { v: 0.95, passing: true },
    horn,
    vinylCfg: { hiss: 0.0045, crackle: 1.6 },
  });
}

/* ---------------- C · 后房交易 Backroom Deal（88 BPM，阴度拉高） -------------- */
function backroomDeal() {
  const BPM = 88, barSec = 240 / BPM, bs = beatSec(BPM);
  bluesChassis({
    bpm: BPM, seed: 881203, name: "02c-backroom-deal", lpFreq: 7400, reps: 3,
    comp: { bright: 0.1, soft: 0.38, hits: (bar) => [
      [0, 0.4, 2.2], ...(bar % 2 === 0 ? [[2.5, 0.24, 0.8]] : []),
    ] },
    drums: (R, bar, barSec) => {
      swish(R, barTime(bar, barSec, 0), 0.55, 0.3, 0);
      if (bar % 2 === 1) swish(R, barTime(bar, barSec, 2), 0.8, 0.34, 0.1); // 镲只在奇数小节呼吸
      hatTick(R, barTime(bar, barSec, 1), 0.18);
      hatTick(R, barTime(bar, barSec, 3), 0.18, -0.3);
      kick(R, barTime(bar, barSec, 0), 0.45);
      if (bar % 4 === 3) rim(R, barTime(bar, barSec, 3.75), 0.5);
    },
    bassStyle: { v: 0.85, passing: false },
    horn: [
      // 吸烟室里的低音问句（小号低声部，几乎听茶话低）
      [4, 2, 60, 1.5, 0.42], [5, 0, 58, 2.0, 0.38],
      [6, 2, 58, 1.5, 0.42], [7, 0, 56, 1.5, 0.36], [7, 1.5, 54, 1.5, 0.36],
    ],
    vinylCfg: { hiss: 0.0035, crackle: 0.8 },
    extra: (R, barSec) => {
      // 阴点：低二度钢琴簇（bar 4 / 8 / 12 最后一拍）
      cluster(R, barTime(3, barSec, 3.5), 51, 52, 0.3);
      cluster(R, barTime(7, barSec, 3.5), 54, 55, 0.28);
      cluster(R, barTime(11, barSec, 3.5), 51, 52, 0.32);
    },
  });
}

/* ---------------- D · 点燃舞池 Dance Floor（132 BPM，亮快） ----------------- */
function danceFloor() {
  const BPM = 132, barSec = 240 / BPM, bs = beatSec(BPM);
  const horn = [
    // 与钢琴对话的呐喊，踩每 4 小节的最后一拍
    [3, 3.0, 70, 0.25, 1.0], [3, 3.5, 72, 0.4, 0.95],
    [7, 3.0, 73, 0.25, 1.0], [7, 3.5, 74, 0.4, 0.95],
    [11, 3.0, 70, 0.25, 1.0], [11, 3.5, 73, 0.4, 1.0],
  ];
  bluesChassis({
    bpm: BPM, seed: 132042, name: "02d-dance-floor", lpFreq: 11500, reps: 4,
    comp: { bright: 0.55, soft: 0.5, hits: (bar) => [
      [0, 0.5, 1.2], [1.5, 0.4, 0.5],
      ...(bar % 2 === 0 ? [[2.5, 0.34, 0.4], [3.5, 0.28, 0.4]] : [[3.0, 0.3, 0.5]]),
    ] },
    drums: (R, bar, barSec, bs) => {
      for (let e = 0; e < 8; e++)
        hatTick(R, barTime(bar, barSec, 0) + e * bs, e % 2 === 0 ? 0.42 : 0.3, (e % 2 ? 1 : -1) * 0.25);
      ride(R, barTime(bar, barSec, 0), 0.9);
      ride(R, barTime(bar, barSec, 2), 0.8);
      swish(R, barTime(bar, barSec, 1), 1.0, 0.18, -0.12);
      swish(R, barTime(bar, barSec, 3), 1.2, 0.2, 0.12);
      rim(R, barTime(bar, barSec, 1), 0.7);
      rim(R, barTime(bar, barSec, 3), 0.75);
      kick(R, barTime(bar, barSec, 0), 0.65);
      kick(R, barTime(bar, barSec, 2.5), 0.5);
    },
    bassStyle: { v: 1.0, passing: true, octaveSkip: true },
    horn,
    vinylCfg: { hiss: 0.004, crackle: 1.4 },
  });
}

/* ---------------------------------------------------------------- run */

const arg = process.argv[2];
const all = { a: lastCall, b: houseBand, c: backroomDeal, d: danceFloor };
if (arg && all[arg]) all[arg]();
else for (const fn of Object.values(all)) fn();
console.log("done.");
