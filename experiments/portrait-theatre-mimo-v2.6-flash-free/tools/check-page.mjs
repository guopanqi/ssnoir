// 浏览器实测：打开页面、开演、逐拍比对字幕与基准数据、暂停/继续/重播/落幕、
// 跳拍重建、声音开关、窄窗口无横向溢出，并导出 art/preview-*.png。
// 用法：node tools/check-page.mjs
import path from 'node:path';
import fs from 'node:fs';
import { chromium } from 'playwright-core';

const ROOT = path.resolve(import.meta.dirname, '..');
const URL = process.env.PT_URL || `file://${path.join(ROOT, 'index.html')}`;
const problems = [];
const fail = (m) => problems.push(m);

// 每幕截图的拍点：[片段, 拍, 文件名, 等待毫秒]
const SHOTS = [
  [0, 1, 's1-rain-buzz', 600],
  [1, 3, 's2-pack', 900],
  [2, 4, 's3-accuse', 900],
  [3, 11, 's4-tears', 1400],
  [4, 2, 's5-shoulder', 1600],
  [5, 10, 's6-slam', 5200],
];

const browser = await chromium.launch({ channel: 'chrome', headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(`pageerror: ${e.message}`));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(`console: ${m.text()}`); });

  await page.goto(URL);

  // 1) 载入完成，出现开演按钮
  await page.waitForFunction(() => {
    const b = document.querySelector('#curtainCard button');
    return b && !b.disabled;
  }, null, { timeout: 25000 });
  await page.screenshot({ path: path.join(ROOT, 'art/preview-title.png') });

  // 2) 开演：幕布打开，片段一第一拍出现
  await page.click('#curtainCard button');
  await page.waitForFunction(() => document.querySelector('#curtain').classList.contains('open'));
  await page.waitForTimeout(1200);
  const firstSub = await page.evaluate(() => document.querySelector('#stage .subtitle .dtext')?.textContent || '');
  if (!firstSub.includes('夜莺来到公寓楼下')) fail(`片段一第一拍不是舞台指示，得到：${firstSub}`);

  const scenes = await page.evaluate(() => structuredClone(PT.SCENES));
  if (scenes.length !== 6) fail(`片段数量 ${scenes.length} ≠ 6`);

  await page.evaluate(() => ptTheatre.player.hold());

  // 3) 逐片段逐拍比对字幕（同时覆盖跳拍重建）
  for (const [si, scene] of scenes.entries()) {
    await page.evaluate((i) => ptTheatre.player.openScene(i, { autoplay: false }), si);
    for (let k = 0; k < scene.beats.length; k++) {
      const got = await page.evaluate((beat) => {
        ptTheatre.player.goto(beat);
        const el = document.querySelector('#stage .subtitle');
        if (!el || !el.classList.contains('show')) return null;
        if (el.classList.contains('dir')) return { dir: el.querySelector('.dtext').textContent };
        return {
          who: el.querySelector('.who').textContent,
          via: el.querySelector('.via')?.textContent || null,
          text: el.querySelector('.txt').textContent,
        };
      }, k);
      const b = scene.beats[k];
      if (!got) { fail(`${scene.title} 第 ${k + 1} 拍没有字幕`); continue; }
      if (b.say) {
        if (got.who !== b.say) fail(`${scene.title} 第 ${k + 1} 拍说话人 ${got.who} ≠ ${b.say}`);
        if (got.text !== b.text) fail(`${scene.title} 第 ${k + 1} 拍对白不符\n  页面: ${got.text}\n  数据: ${b.text}`);
        if ((b.via || null) !== got.via) fail(`${scene.title} 第 ${k + 1} 拍附加标签 ${got.via} ≠ ${b.via || null}`);
      } else if (got.dir !== b.dir) {
        fail(`${scene.title} 第 ${k + 1} 拍舞台指示不符\n  页面: ${got.dir}\n  数据: ${b.dir}`);
      }
    }
  }

  // 4) 舞台状态：雨、姿势、站位、道具确实生效
  const s1 = await page.evaluate(() => {
    ptTheatre.player.openScene(0, { autoplay: false });
    ptTheatre.player.goto(1);
    return {
      pose: ptTheatre.stage.state.actors['夜莺'].pose,
      rain: ptTheatre.stage.state.fx.rain,
      intercom: ptTheatre.stage.state.props.intercom.lit,
      el: document.querySelector('#stage .prop-intercom')?.className,
      rainVisible: document.querySelector('#stage .r1')?.classList.contains('gone'),
    };
  });
  if (s1.pose !== 'n-ring') fail(`片段一第 2 拍姿势 ${s1.pose} ≠ n-ring`);
  if (!s1.rain || s1.rainVisible) fail('片段一的雨没有打开');
  if (!s1.intercom || !/lit/.test(s1.el || '')) fail('电铃面板没有点亮');

  // 5) 暂停 / 继续 / 重播
  const pauseState = await page.evaluate(() => {
    const p = ptTheatre.player;
    p.goto(0); p.hold();
    return p.snapshot().playing;
  });
  if (pauseState) fail('暂停后仍标记为播放中');
  const resumeState = await page.evaluate(() => { ptTheatre.player.resume(); return ptTheatre.player.snapshot().playing; });
  if (!resumeState) fail('继续后没有恢复播放');
  const replayBeat = await page.evaluate(() => { ptTheatre.player.replay(); return ptTheatre.player.snapshot().beatIndex; });
  if (replayBeat !== 0) fail(`重播后落在第 ${replayBeat} 拍`);

  // 6) 落幕：最后一拍推进后显示“片段结束”
  const endText = await page.evaluate(() => {
    const p = ptTheatre.player;
    p.goto(p.scene.beats.length - 1);
    p.next();
    return null;
  });
  await page.waitForFunction(() => !document.querySelector('#curtain').classList.contains('open'));
  const curtainText = await page.textContent('#curtainCard');
  if (!curtainText.includes('片段结束')) fail(`落幕卡片内容不对: ${curtainText.trim()}`);
  await page.click('#curtainCard button');   // 重看这一段
  await page.waitForFunction(() => document.querySelector('#curtain').classList.contains('open'));

  // 7) 声音开关
  const mute1 = await page.evaluate(async () => { document.querySelector('[data-act="mute"]').click(); return document.querySelector('[data-act="mute"]').textContent; });
  const mute2 = await page.evaluate(async () => { document.querySelector('[data-act="mute"]').click(); return document.querySelector('[data-act="mute"]').textContent; });
  if (mute1 !== '声音：关' || mute2 !== '声音：开') fail(`声音开关状态异常: ${mute1} / ${mute2}`);

  // 8) 截图（每幕一帧）
  for (const [si, beat, name, wait] of SHOTS) {
    await page.evaluate(([s, b]) => {
      const p = ptTheatre.player;
      p.openScene(s, { autoplay: false });
      p.goto(b);
    }, [si, beat]);
    await page.waitForTimeout(wait);
    await page.screenshot({ path: path.join(ROOT, `art/preview-${name}.png`) });
  }

  // 9) 窄窗口无横向溢出，舞台等比缩放
  await page.setViewportSize({ width: 760, height: 900 });
  await page.waitForTimeout(300);
  const fitInfo = await page.evaluate(() => ({
    scrollW: document.documentElement.scrollWidth,
    innerW: window.innerWidth,
    stageH: document.querySelector('#stageWrap').getBoundingClientRect().height,
    stageW: document.querySelector('#stageWrap').getBoundingClientRect().width,
  }));
  if (fitInfo.scrollW > fitInfo.innerW + 1) fail(`窄窗口横向溢出: ${fitInfo.scrollW} > ${fitInfo.innerW}`);
  if (Math.abs(fitInfo.stageH - fitInfo.stageW * 9 / 16) > 2) fail(`舞台比例不对: ${fitInfo.stageW}x${fitInfo.stageH}`);
  await page.screenshot({ path: path.join(ROOT, 'art/preview-compact.png'), fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1000 });

  if (errors.length) for (const e of errors) fail(`页面错误 ${e}`);
} finally {
  await browser.close();
}

if (problems.length) {
  console.error(`✗ ${problems.length} 个问题：\n`);
  for (const p of problems) console.error('  - ' + p);
  process.exit(1);
}
console.log('✓ 开演、六幕逐拍字幕、舞台状态、暂停/继续/重播、落幕与重看、声音开关、窄窗口缩放全部通过');
console.log('✓ 无 console / page error；预览图已写入 art/preview-*.png');
