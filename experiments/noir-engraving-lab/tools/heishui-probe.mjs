// 探针：打印若干采样点的 sRGB 值，用来判断"哪一层在发亮"
import sharp from 'sharp';
const file = process.argv[2];
const pts = JSON.parse(process.argv[3] || '[[0.5,0.15],[0.5,0.42],[0.5,0.62],[0.2,0.75],[0.8,0.9],[0.5,0.95]]');
const img = sharp(file);
const { width, height } = await img.metadata();
const { data, info } = await img.raw().toBuffer({ resolveWithObject: true });
const out = [];
for (const [fx, fy] of pts) {
  const x = Math.min(width - 1, Math.round(fx * width));
  const y = Math.min(height - 1, Math.round(fy * height));
  const i = (y * info.width + x) * info.channels;
  out.push({ at: [fx, fy], rgb: [data[i], data[i + 1], data[i + 2]].map((v) => +(v / 255).toFixed(3)) });
}
console.log(file.split('/').slice(-2).join('/'), JSON.stringify(out));
