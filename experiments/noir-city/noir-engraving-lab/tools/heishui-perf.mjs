/**
 * 黑水 · 帧时实测
 *
 *   node tools/heishui-perf.mjs                      # 七个机位 × 四个阶段
 *   node tools/heishui-perf.mjs --mode final         # 只测成片
 *
 * 读的是页面里 `window.__heishui.perf()` —— 由 requestAnimationFrame 的间隔测得，
 * 包含镜像反射、主场景、体积雾、描线、光晕与成片的全部开销。
 * 这是"这套东西在浏览器里跑不跑得动"的证据，不是估算。
 */
import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import { findChromium, startVite, waitForServer, waitReady } from './heishui-harness.mjs';
import path from 'node:path';
import { parseArgs } from 'node:util';

const ROOT = process.cwd();
const { values } = parseArgs({ options: {
  mode: { type: 'string' }, out: { type: 'string' }, port: { type: 'string', default: '4187' },
  query: { type: 'string', default: '' },
} });
const PORT = Number(values.port);
const URL = `http://127.0.0.1:${PORT}/heishui.html?capture=1&${values.query}`;

const SHOT_IDS = ['01-world', '02-across', '03-river', '04-port', '05-rain', '06-silhouette', '07-plan'];
const MODES = values.mode ? [values.mode] : ['shape', 'haze', 'line', 'final'];


const server = startVite(ROOT, PORT);
let browser;
try {
  await waitForServer(URL);

  browser = await chromium.launch({ headless: true, executablePath: await findChromium(), args: ['--use-angle=metal'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 900 }, deviceScaleFactor: 1 });
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.stack ?? String(e)));
  page.on('console', (m) => { if (m.type() === 'error' && !/favicon/.test(m.location()?.url ?? '')) errors.push(m.text()); });
  await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 120000 });
  await waitReady(page, errors);

  const gpu = await page.evaluate(() => {
    const c = document.createElement('canvas');
    const gl = c.getContext('webgl2');
    const d = gl.getExtension('WEBGL_debug_renderer_info');
    return { renderer: d ? gl.getParameter(d.UNMASKED_RENDERER_WEBGL) : 'n/a', version: gl.getParameter(gl.VERSION) };
  });

  await page.evaluate(() => window.__heishui.setPaused(true));
  const rows = [];
  for (const mode of MODES) {
    await page.evaluate((m) => window.__heishui.setMode(m), mode);
    for (let i = 0; i < SHOT_IDS.length; i++) {
      await page.evaluate((k) => window.__heishui.setShot(k), i);
      await new Promise((r) => setTimeout(r, 700));
      const p = await page.evaluate(() => {
        const s = window.__heishui.bench(24).slice().sort((a, b) => a - b);
        const med = s[Math.floor(s.length / 2)];
        return { min: s[0], med, max: s[s.length - 1], samples: s };
      });
      rows.push({ mode, shot: SHOT_IDS[i], ...p });
      console.log(`${mode.padEnd(6)} ${SHOT_IDS[i].padEnd(15)} min ${String(p.min).padStart(7)} ms   median ${String(p.med).padStart(7)} ms   max ${String(p.max).padStart(7)} ms`);
    }
  }
  if (errors.length) throw new Error(errors.join('\n'));

  const lines = [
    '# 黑水 · 帧时实测', '',
    `时间 ${new Date().toISOString()}`,
    `GPU  ${gpu.renderer}`,
    `GL   ${gpu.version}`,
    '画布 1600×900（体积雾 0.5×、拉丝与 halation 0.125×）', '',
    '| 阶段 | 机位 | min ms | 中位 ms | max ms | 中位 fps |', '|---|---|---:|---:|---:|---:|',
  ];
  for (const r of rows) lines.push(`| ${r.mode} | ${r.shot} | ${r.min} | ${r.med} | ${r.max} | ${(1000 / r.med).toFixed(1)} |`);
  lines.push('', '> 每格同步连渲 24 帧，每帧后 1×1 `readPixels` 强制 GPU 落地并逐帧记时。');
  lines.push('> 包含镜像反射（0.5×）、主场景、体积雾（0.5×, 26 步）、描线、光晕（0.125×）');
  lines.push('> 与成片的**全部**开销。');
  lines.push('>');
  lines.push('> **无头 Chrome 的数值方差很大**（同一配置的 max 常常是 min 的 3–5 倍），');
  lines.push('> 因为无头合成路径要每帧回读画布。这里给 min 与中位数，**不要**把某个单值当真；');
  lines.push('> 有真实窗口时应该以窗口里 HUD 的 rAF 读数为准。');

  const outDir = path.resolve(ROOT, values.out ?? 'captures');
  await mkdir(outDir, { recursive: true });
  await writeFile(path.join(outDir, 'perf.md'), lines.join('\n') + '\n');
  await writeFile(path.join(outDir, 'perf.json'), JSON.stringify({ generatedAt: new Date().toISOString(), gpu, viewport: [1600, 900], rows }, null, 2) + '\n');
  console.log(`\n→ ${path.relative(ROOT, path.join(outDir, 'perf.md'))}`);
} finally {
  await browser?.close();
  server.kill('SIGTERM');
}
