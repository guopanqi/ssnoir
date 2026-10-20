// seam sanity check: compare RMS of the last 0.5s and first 0.5s of each
// loop cycle, and count near-clipped samples.
import fs from "node:fs";
import path from "node:path";
const files = fs.readdirSync(new URL("./audio/", import.meta.url).pathname).filter((f) => f.endsWith(".wav"));
const variantsDir = new URL("./variants/", import.meta.url).pathname;
if (fs.existsSync(variantsDir)) {
  for (const f of fs.readdirSync(variantsDir)) if (f.endsWith(".wav")) files.push({ f, dir: variantsDir });
}
function loopSecsOf(file) {
  if (file.startsWith("01-")) return 8 * 240 / 72;
  if (file.startsWith("02-")) return 12 * 240 / 96;
  if (file.startsWith("02a-")) return 12 * 240 / 72;
  if (file.startsWith("02b-")) return 12 * 240 / 116;
  if (file.startsWith("02c-")) return 12 * 240 / 88;
  if (file.startsWith("02d-")) return 12 * 240 / 132;
  if (file.startsWith("03-")) return 16;
  if (file.startsWith("04-")) return 8 * 240 / 84;
  if (file.startsWith("05-")) return 8 * 240 / 112;
  throw new Error(`unknown track: ${file}`);
}
for (const entry of files) {
  const f = entry.f ?? entry;
  const dir = entry.dir ?? new URL("./audio/", import.meta.url).pathname;
  const b = fs.readFileSync(path.join(dir, f));
  const frames = (b.length - 44) / 4; // interleaved stereo
  const loopN = Math.round(loopSecsOf(f) * 44100);
  const reps = frames / loopN;
  const rms = (a, z) => {
    let s = 0, c = 0;
    for (let i = a; i < z; i++) {
      const o = 44 + i * 4;
      for (let k = 0; k < 2; k++) { const v = b.readInt16LE(o + k * 2) / 32768; s += v * v; }
      c += 2;
    }
    return Math.sqrt(s / c);
  };
  const clip = () => {
    let c = 0;
    for (let i = 0; i < frames; i++) {
      const o = 44 + i * 4;
      for (let k = 0; k < 2; k++) {
        const v = b.readInt16LE(o + k * 2);
        if (v >= 32760 || v <= -32760) c++;
      }
    }
    return c;
  };
  const perCycle = [];
  for (let r = 0; r < reps - 1; r++) {
    const end = (r + 1) * loopN;
    perCycle.push((rms(end - 22050, end) / rms(end, end + 22050)).toFixed(2));
  }
  console.log(
    `${f.padEnd(22)} boundary rmsRatio(last/first)=[${perCycle.join(", ")}]  clipped=${clip()}`
  );
}
