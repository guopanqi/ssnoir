// 把角色库 / art/gen 中的黑底霓虹立绘处理成本实验的舞台素材：
// 背景转透明（保留辉光），轮廓内部填实黑，裁到内容外框并统一缩放。
// 用法：node tools/prepare-cast.mjs
import sharp from 'sharp';
import fs from 'node:fs/promises';
import path from 'node:path';

const ROOT = path.resolve(import.meta.dirname, '..');
const REPO = path.resolve(ROOT, '..', '..');
const OUT = path.join(ROOT, 'assets', 'cast');

// [角色, 姿势 id, 源文件（相对仓库根）]
const SOURCES = [
  ['夜莺', 'n-lean', '角色库/夜莺/形象/姿势/01-逼近.png'],
  ['夜莺', 'n-fist', '角色库/夜莺/形象/姿势/01-逼近-v2-握拳.png'],
  ['夜莺', 'n-back', '角色库/夜莺/形象/姿势/02-背身.png'],
  ['夜莺', 'n-bow', '角色库/夜莺/形象/姿势/03-低头.png'],
  ['夜莺', 'n-chin', '角色库/夜莺/形象/姿势/04-仰头.png'],
  ['夜莺', 'n-plead', '角色库/夜莺/形象/姿势/05-恳求.png'],
  ['夜莺', 'n-arms', '角色库/夜莺/形象/姿势/06-抱臂.png'],
  ['夜莺', 'n-cover', '角色库/夜莺/形象/姿势/07-掩面.png'],
  ['夜莺', 'n-glance', '角色库/夜莺/形象/姿势/08-回眸.png'],
  ['夜莺', 'n-sit', '角色库/夜莺/形象/姿势/09-坐.png'],
  ['夜莺', 'n-umbrella', 'art/gen/nightingale-umbrella.png'],
  ['夜莺', 'n-ring', 'art/gen/nightingale-ring.png'],
  ['夜莺', 'n-leave', 'art/gen/nightingale-leaving.png'],
  ['尼尔', 'e-accuse', '角色库/尼尔/形象/姿势/01-逼近.png'],
  ['尼尔', 'e-stand', '角色库/尼尔/形象/姿势/02-侧身退.png'],
  ['尼尔', 'e-light', '角色库/尼尔/形象/姿势/03-低头点烟.png'],
  ['尼尔', 'e-hatoff', '角色库/尼尔/形象/姿势/04-摘帽.png'],
  ['尼尔', 'e-arms', '角色库/尼尔/形象/姿势/05-抱臂.png'],
  ['尼尔', 'e-back', '角色库/尼尔/形象/姿势/06-背身.png'],
  ['尼尔', 'e-reach', '角色库/尼尔/形象/姿势/07-伸手.png'],
  ['尼尔', 'e-fist', '角色库/尼尔/形象/姿势/08-攥拳.png'],
  ['尼尔', 'e-listen', '角色库/尼尔/形象/姿势/09-靠墙.png'],
  ['经理', 'm-stand', '角色库/经理/形象/经理_neon.png'],
  ['经理', 'm-shoulder', 'art/gen/manager-shoulder.png'],
  ['经理', 'm-point', 'art/gen/manager-point.png'],
  ['工人', 'w-kneel', 'art/gen/worker-kneel.png'],
];

const LUMA_TUBE = 90;   // 低于它的像素视为“不是灯管”，用于判断图形内外
const LUMA_EDGE = 18;   // 低于它的像素视为纯背景，用于裁切外框
const OUT_HEIGHT = 720;

function floodOutside(lum, w, h) {
  const outside = new Uint8Array(w * h);
  const queue = new Int32Array(w * h);
  let head = 0, tail = 0;
  const push = (i) => { if (!outside[i] && lum[i] < LUMA_TUBE) { outside[i] = 1; queue[tail++] = i; } };
  for (let x = 0; x < w; x++) { push(x); push((h - 1) * w + x); }
  for (let y = 0; y < h; y++) { push(y * w); push(y * w + w - 1); }
  while (head < tail) {
    const i = queue[head++];
    const x = i % w, y = (i / w) | 0;
    if (x > 0) push(i - 1);
    if (x < w - 1) push(i + 1);
    if (y > 0) push(i - w);
    if (y < h - 1) push(i + w);
  }
  return outside;
}

async function prepare(role, id, srcPath) {
  const file = srcPath.startsWith('art/') ? path.join(ROOT, srcPath) : path.join(REPO, srcPath);
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width: w, height: h, channels } = info;
  if (channels !== 4) throw new Error(`不是四通道图像: ${srcPath}`);

  const lum = new Uint8Array(w * h);
  for (let i = 0; i < w * h; i++) {
    const o = i * 4;
    lum[i] = Math.max(data[o], data[o + 1], data[o + 2]);
  }
  const outside = floodOutside(lum, w, h);

  // 内部填实黑，外部保留辉光的软透明
  let x0 = w, x1 = -1, y0 = h, y1 = -1;
  for (let i = 0; i < w * h; i++) {
    const o = i * 4;
    if (outside[i]) {
      data[o + 3] = Math.min(255, Math.round(lum[i] * 2.2));
    } else {
      data[o + 3] = 255;
      if (lum[i] < 45) { data[o] = 0; data[o + 1] = 0; data[o + 2] = 0; }
    }
    if (lum[i] > LUMA_EDGE) {
      const x = i % w, y = (i / w) | 0;
      if (x < x0) x0 = x; if (x > x1) x1 = x;
      if (y < y0) y0 = y; if (y > y1) y1 = y;
    }
  }
  if (x1 <= x0 || y1 <= y0) throw new Error(`空素材: ${srcPath}`);
  const pad = 10;
  const left = Math.max(0, x0 - pad), top = Math.max(0, y0 - pad);
  const width = Math.min(w, x1 + pad + 1) - left;
  const height = Math.min(h, y1 + pad + 1) - top;

  const outPath = path.join(OUT, `${id}.png`);
  const png = await sharp(data, { raw: info })
    .extract({ left, top, width, height })
    .resize({ height: OUT_HEIGHT, withoutEnlargement: true })
    .png({ compressionLevel: 9 })
    .toBuffer();
  await fs.writeFile(outPath, png);
  const meta = await sharp(png).metadata();
  return { id, role, file: `assets/cast/${id}.png`, w: meta.width, h: meta.height, src: srcPath };
}

(async () => {
  await fs.mkdir(OUT, { recursive: true });
  const cast = {};
  for (const [role, id, src] of SOURCES) {
    cast[id] = await prepare(role, id, src);
    console.log(`${id.padEnd(14)} <- ${src}  ${cast[id].w}x${cast[id].h}`);
  }
  await fs.writeFile(path.join(OUT, 'manifest.json'), JSON.stringify({ cast }, null, 2) + '\n');
  // 同时生成供 <script> 直接使用的数据（file:// 打开时也能工作）
  await fs.writeFile(
    path.join(ROOT, 'src', 'cast.js'),
    `// 由 tools/prepare-cast.mjs 生成，勿手改。\nPT.cast = ${JSON.stringify(cast, null, 2)};\n`
  );
  console.log(`\n共 ${Object.keys(cast).length} 个素材 → assets/cast/`);
})().catch((e) => { console.error(e); process.exit(1); });
