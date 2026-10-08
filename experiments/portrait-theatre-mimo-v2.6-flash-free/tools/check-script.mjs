// 静态校验：
//   1) src/script.js 的对白与舞台指示逐字等于 tools/baseline.txt，且顺序一致；
//   2) 姿势、人物、道具、音效、角色朝向数据都能在素材表里找到；
//   3) 六个片段、初始站位落在舞台内。
// 用法：node tools/check-script.mjs
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';

const ROOT = path.resolve(import.meta.dirname, '..');
const read = (p) => fs.readFileSync(path.join(ROOT, p), 'utf8');

function loadPT() {
  const PT = {};
  const ctx = vm.createContext({ PT, console, structuredClone });
  for (const f of ['src/script.js', 'src/stage.js', 'src/audio.js', 'src/cast.js']) {
    vm.runInContext(read(f), ctx, { filename: f });
  }
  return PT;
}

const errors = [];
const fail = (msg) => errors.push(msg);

// ---------- 基准剧本 ----------
const SCENE_RE = /^片段[一二三四五六]：/;
const baseline = [];
let cur = null;
for (const raw of read('tools/baseline.txt').split('\n')) {
  const line = raw.trim();
  if (SCENE_RE.test(line)) { cur = { title: line, lines: [] }; baseline.push(cur); continue; }
  if (!cur || line === '' || line === '片段结束。') continue;
  const say = line.match(/^(夜莺|尼尔|经理)：(.*)$/);
  if (say) { cur.lines.push({ kind: 'say', who: say[1], text: say[2] }); continue; }
  if (/^（.*）$/.test(line)) { cur.lines.push({ kind: 'dir', text: line }); }
}

// ---------- 舞台脚本 ----------
const PT = loadPT();
const scenes = PT.SCENES;

if (scenes.length !== baseline.length) fail(`片段数量: 脚本 ${scenes.length} vs 基准 ${baseline.length}`);

scenes.forEach((s, i) => {
  const b = baseline[i];
  if (!b) { fail(`片段 ${i} 缺少基准文本`); return; }
  if (s.title !== b.title) fail(`片段 ${i} 标题: “${s.title}” ≠ “${b.title}”`);

  const got = s.beats
    .filter((bt) => bt.say || bt.dir)
    .map((bt) => (bt.say ? { kind: 'say', who: bt.say, text: bt.text } : { kind: 'dir', text: bt.dir }));
  if (got.length !== b.lines.length) {
    fail(`${s.title}: 拍点 ${got.length} 条 ≠ 基准 ${b.lines.length} 条`);
  }
  const n = Math.min(got.length, b.lines.length);
  for (let k = 0; k < n; k++) {
    const g = got[k], e = b.lines[k];
    if (g.kind !== e.kind) fail(`${s.title} 第 ${k + 1} 条类型不同: ${g.kind} vs ${e.kind}`);
    else if (g.text !== e.text) fail(`${s.title} 第 ${k + 1} 条不符:\n  脚本: ${g.text}\n  基准: ${e.text}`);
    else if (g.kind === 'say' && g.who !== e.who) fail(`${s.title} 第 ${k + 1} 条说话人: ${g.who} ≠ ${e.who}`);
  }

  if (!s.colors || !s.colors.bg || !s.colors.floor) fail(`${s.title}: 缺少纯色舞台颜色`);
  if (!s.initial || !s.initial.actors) fail(`${s.title}: 缺少初始人物`);

  for (const [role, a] of Object.entries(s.initial.actors)) {
    if (!PT.ROLES.includes(role)) fail(`${s.title}: 未知人物 ${role}`);
    if (!(a.pose in PT.cast)) fail(`${s.title}: ${role} 的初始姿势 ${a.pose} 不存在`);
    if (a.x < 140 || a.x > 1140) fail(`${s.title}: ${role} 初始位置 ${a.x} 超出舞台`);
  }
  for (const id of Object.keys(s.initial.props || {})) {
    if (!(id in PT.PROPS)) fail(`${s.title}: 未知道具 ${id}`);
  }

  const checkCue = (cue, where) => {
    for (const [role, pose] of Object.entries(cue.poses || {})) {
      if (!PT.ROLES.includes(role)) fail(`${where}: 未知人物 ${role}`);
      if (!(pose in PT.cast)) fail(`${where}: 未知姿势 ${role}.${pose}`);
    }
    for (const [role, x] of Object.entries(cue.move || {})) {
      if (!PT.ROLES.includes(role)) fail(`${where}: 未知人物 ${role}`);
      if (x < 140 || x > 1140) fail(`${where}: ${role} 站位 ${x} 超出舞台`);
    }
    for (const id of Object.keys(cue.props || {})) if (!(id in PT.PROPS)) fail(`${where}: 未知道具 ${id}`);
    if (cue.sfx && !PT.audio.names.includes(cue.sfx)) fail(`${where}: 未知音效 ${cue.sfx}`);
    if (cue.light && cue.light.focus && !PT.ROLES.includes(cue.light.focus)) fail(`${where}: 未知聚焦人物 ${cue.light.focus}`);
    for (const step of cue.seq || []) checkCue(step, `${where} seq@${step.at}`);
  };

  s.beats.forEach((bt, k) => {
    const where = `${s.title} 第 ${k + 1} 拍`;
    if (bt.say && typeof bt.text !== 'string') fail(`${where}: 对白缺少 text`);
    if (!bt.say && !bt.dir) fail(`${where}: 既不是对白也不是舞台指示`);
    if (bt.say && !PT.ROLES.includes(bt.say)) fail(`${where}: 未知说话人 ${bt.say}`);
    checkCue(bt.cue || {}, where);
  });
});

if (errors.length) {
  console.error(`✗ ${errors.length} 个问题：\n`);
  for (const e of errors) console.error('  - ' + e);
  process.exit(1);
}
const says = scenes.reduce((n, s) => n + s.beats.filter((b) => b.say).length, 0);
const dirs = scenes.reduce((n, s) => n + s.beats.filter((b) => b.dir).length, 0);
console.log(`✓ ${scenes.length} 个片段、${says} 句对白、${dirs} 条舞台指示与基准剧本逐字一致`);
console.log(`✓ 姿势 ${Object.keys(PT.cast).length} 个、道具 ${Object.keys(PT.PROPS).length} 件、音效 ${PT.audio.names.length} 种全部可解析`);
