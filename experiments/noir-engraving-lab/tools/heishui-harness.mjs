/**
 * 黑水 · 本地工具的共同部分
 *
 * 三个工具（capture / perf）都要：找到 Chromium、起 Vite、等页面 ready。
 * 这三件事以前各写了一份 —— 其中"等页面 ready"那份还踩过坑：
 *
 *   Vite 在首次遇到新的依赖（比如刚加的 OrbitControls）时会做依赖预打包，
 *   完成后**强制整页刷新**。如果只等一次 ready 就开始操作，刷新会把执行上下文
 *   销毁掉，工具报 "Execution context was destroyed"。
 *
 * 所以 waitReady 会等两次：先等到 ready，静置一会儿，再等一次。
 */
import { spawn } from 'node:child_process';
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';

/** 找到已缓存的 Chromium。Playwright 版本号对不上缓存时用它兜底。 */
export async function findChromium() {
  if (process.env.CHROME_PATH) return process.env.CHROME_PATH;
  const root = path.join(process.env.HOME, 'Library/Caches/ms-playwright');
  let dirs = [];
  try {
    dirs = (await readdir(root)).filter((d) => d.startsWith('chromium-') && !d.includes('headless'));
  } catch { return undefined; }
  for (const d of dirs.sort().reverse()) {
    const p = path.join(root, d, 'chrome-mac-arm64/Google Chrome for Testing.app/Contents/MacOS/Google Chrome for Testing');
    try { await readFile(p); return p; } catch { /* 换下一个 */ }
  }
  return undefined;
}

/** 起本实验室的 Vite 服务。调用方负责在 finally 里 kill。 */
export function startVite(root, port) {
  return spawn(
    process.execPath,
    [path.join(root, 'node_modules/vite/bin/vite.js'), '--host', '127.0.0.1', '--port', String(port), '--strictPort'],
    { cwd: root, stdio: ['ignore', 'pipe', 'pipe'] },
  );
}

export async function waitForServer(url, { attempts = 80, interval = 400 } = {}) {
  for (let i = 0; i < attempts; i++) {
    try { if ((await fetch(url, { signal: AbortSignal.timeout(1500) })).ok) return; } catch { /* 还在起 */ }
    await new Promise((r) => setTimeout(r, interval));
  }
  throw new Error('Vite 没起来：' + url);
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * 等页面 ready。会跨过 Vite 依赖预打包引发的一次整页刷新。
 * errors 由调用方收集（pageerror / console error / HTTP>=400），一旦非空立刻抛出。
 */
export async function waitReady(page, errors, { probe = 400, settle = 2000 } = {}) {
  for (let round = 0; round < 2; round++) {
    let ok = false;
    for (let i = 0; i < probe; i++) {
      if (errors.length) throw new Error(errors.join('\n'));
      if (await page.evaluate(() => window.__heishui?.ready === true).catch(() => false)) { ok = true; break; }
      await sleep(500);
    }
    if (!ok) throw new Error('页面没 ready');
    if (round === 0) await sleep(settle);   // 静置：让可能到来的整页刷新发生掉
  }
}
