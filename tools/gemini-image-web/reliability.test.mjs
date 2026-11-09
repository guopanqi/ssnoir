import assert from 'node:assert/strict';
import { before, after, test } from 'node:test';
import { mkdtemp, readFile, writeFile, mkdir, stat } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createServer } from 'node:net';
import { chromium } from 'playwright';
import { parseArgs, validateOptions, assertImagesPage, waitForResult, generate, resume, connectDaemon } from './gemini-image-web.mjs';
import { downloadImage } from './image-download.mjs';
let browser;
before(async () => { browser = await chromium.launch({ channel: 'chrome', headless: true }); });
after(async () => { await browser?.close(); });
async function fixture(run) {
  const context = await browser.newContext({ acceptDownloads: true });
  try { const page = await context.newPage(); await run(page, context); }
  finally { await context.close(); }
}
async function png(page, width) {
  return page.evaluate(width => { const c = document.createElement('canvas'); c.width = c.height = width; c.getContext('2d').fillRect(0, 0, width, width); return c.toDataURL('image/png'); }, width);
}

test('拒绝未知参数与模型，保留默认 Flash', async () => {
  assert.throws(() => parseArgs(['generate', '--modle', 'Flash']), /未知参数/);
  assert.equal((await validateOptions({ command: 'doctor', reference: [] })).model, 'Flash');
  assert.equal((await validateOptions({ command: 'doctor', model: 'Argon', reference: [] })).model, 'Argon');
  await assert.rejects(validateOptions({ command: 'doctor', model: 'Flash-Lite', reference: [] }), /仅支持/);
});
test('登录失效在选择模型或工具前报告，避免按钮超时', () => fixture(async page => {
  await page.setContent('<a href="https://accounts.google.com">Sign in</a>');
  await assert.rejects(assertImagesPage(page), error => error.response_kind === 'auth-error');
}));
test('用户提示词中的额度词不会触发额度错误；真实额度通知立即停止', () => fixture(async page => {
  const image = await png(page, 256);
  await page.setContent(`<user-query>Draw a sign saying Limit reached.</user-query><model-response><img alt="AI generated" src="${image}"></model-response>`);
  assert.ok(await waitForResult(page, 1500, Date.now()));
  await page.setContent('<div role="alert">Limit reached. Resets tomorrow.</div><model-response><button aria-label="Redo">Redo</button></model-response>');
  await assert.rejects(waitForResult(page, 1500, Date.now()), error => error.response_kind === 'quota');
}));
test('完成的纯文字回复明确失败，并保留原文', () => fixture(async page => {
  await page.setContent('<model-response>Here is a written specification.<button aria-label="Copy">Copy</button></model-response>');
  await assert.rejects(waitForResult(page, 5000, Date.now()), error => error.response_kind === 'text-only' && error.response_text.includes('written specification'));
}));
test('延迟出现的 Redo 可重试；再次失败不会第三次生成', () => fixture(async page => {
  await page.setContent('<model-response>I encountered an error doing what you asked.</model-response>');
  await page.evaluate(() => {
    window.retries = 0;
    setTimeout(() => {
      const button = document.createElement('button'); button.setAttribute('aria-label', 'Redo'); button.textContent = 'Redo';
      button.onclick = () => window.retries++;
      document.querySelector('model-response').append(button);
    }, 100);
  });
  await assert.rejects(waitForResult(page, 16000, Date.now()), error => error.response_kind === 'transient-error');
  assert.equal(await page.evaluate(() => window.retries), 1);
}));
test('原图按钮优先，按实际文件尺寸与格式保存；坏文件报下载错误', () => fixture(async page => {
  const directory = await mkdtemp(join(tmpdir(), 'gemini-download-'));
  const preview = await png(page, 256), original = await png(page, 512);
  let corrupt = false;
  await page.route('https://fixture.invalid/original', route => route.fulfill({ status: 200, headers: { 'content-disposition': 'attachment; filename="original.png"' }, contentType: 'image/png', body: corrupt ? Buffer.from('not an image') : Buffer.from(original.split(',')[1], 'base64') }));
  await page.setContent(`<model-response><img alt="AI generated" src="${preview}"><button aria-label="Download full size image" onclick="const a=document.createElement('a');a.href='https://fixture.invalid/original';a.download='original.png';a.click()">Download</button></model-response>`);
  const image = page.locator('img');
  const result = await downloadImage(page, image, directory);
  assert.equal(result.width, 512); assert.equal(result.height, 512);
  assert.equal(result.original, true); assert.equal(result.extraction, 'download-button');
  assert.ok((await stat(result.path)).size > 0);
  corrupt = true;
  await assert.rejects(downloadImage(page, image, directory), error => error.response_kind === 'download-error');
}));
test('无原图入口时预览必须被明确标记', () => fixture(async page => {
  const image = await png(page, 256);
  await page.setContent(`<model-response><img src="${image}"></model-response>`);
  const directory = await mkdtemp(join(tmpdir(), 'gemini-preview-'));
  const result = await downloadImage(page, page.locator('img'), directory);
  assert.equal(result.original, false); assert.equal(result.extraction, 'preview-canvas');
}));
test('浏览器启动失败也写入失败 manifest', async () => {
  const root = await mkdtemp(join(tmpdir(), 'gemini-launch-'));
  const options = await validateOptions({ command: 'generate', prompt: 'test', session: 's', name: 'n', output_root: root, reference: [] });
  await assert.rejects(generate(options, { newPage: async () => { throw new Error('browser unavailable'); } }), /browser unavailable/);
  const manifest = JSON.parse(await readFile(join(root, 's/n/manifest.json')));
  assert.equal(manifest.status, 'interrupted'); assert.equal(manifest.phase, 'browser');
  assert.equal(manifest.response_kind, 'browser-error');
});
test('制作目录保存失败保留本地图片；resume 复用图片且不启动浏览器', async () => {
  const root = await mkdtemp(join(tmpdir(), 'gemini-resume-'));
  const imagePath = join(root, 'image.png'); await writeFile(imagePath, 'valid saved fixture');
  const { createHash } = await import('node:crypto');
  const sha256 = createHash('sha256').update('valid saved fixture').digest('hex');
  const blocker = join(root, 'blocker'); await writeFile(blocker, 'file blocks directory');
  const path = join(root, 'manifest.json');
  await writeFile(path, JSON.stringify({ status: 'interrupted', phase: 'saving', prompt: 'original prompt', name: 'n', requested_out: join(blocker, 'asset.png'), output_dir: root, download: { path: imagePath, sha256, width: 256, height: 256, mime_type: 'image/png', extraction: 'download-button' } }));
  const options = await validateOptions({ command: 'resume', manifest: path, reference: [] });
  await assert.rejects(resume(options), /EEXIST|ENOTDIR/);
  assert.equal(JSON.parse(await readFile(path)).status, 'interrupted');
  await resume({ ...options, out: join(root, 'fixed/asset.png') });
  assert.equal((await readFile(join(root, 'fixed/asset.png'))).toString(), 'valid saved fixture');
  assert.equal(JSON.parse(await readFile(path)).status, 'completed');
  // 再次恢复同一个完成任务可安全返回，不重复生成。
  await resume({ ...options, out: join(root, 'fixed/asset.png') });
});
test('后台进度按行即时传送，支持中文拆包；异常断连明确失败', async () => {
  const profile = await mkdtemp(join(tmpdir(), 'gemini-socket-'));
  let disconnect = false;
  const server = createServer(socket => {
    socket.once('data', () => {
      if (disconnect) return socket.destroy();
      const progress = Buffer.from(JSON.stringify({ type: 'progress', channel: 'stderr', line: '等待生成中' }) + '\n');
      for (let i = 0; i < progress.length; i++) socket.write(progress.subarray(i, i + 1));
      setTimeout(() => socket.end(JSON.stringify({ type: 'result', result: { ok: true } }) + '\n'), 30);
    });
  });
  await new Promise(resolve => server.listen(join(profile, '.gemini-image-web.sock'), resolve));
  try {
    const order = [];
    const result = await connectDaemon({ profile }, event => order.push(event.line));
    order.push(result.ok); assert.deepEqual(order, ['等待生成中', true]);
    disconnect = true;
    await assert.rejects(connectDaemon({ profile }), /提前断开/);
  } finally { await new Promise(resolve => server.close(resolve)); }
});

test('完整生成流程：下载成功后保存失败，manifest 可恢复且不重复提交', () => fixture(async (seed, context) => {
  const root = await mkdtemp(join(tmpdir(), 'gemini-flow-'));
  const image = await png(seed, 512);
  let submissions = 0;
  await context.exposeFunction('submitted', () => submissions++);
  const html = `<button aria-label="Deselect Images">Images</button><button aria-label="Open mode picker, currently Argon">Argon</button><div class="ql-editor" aria-label="Enter prompt" contenteditable="true"></div><button aria-label="Send message" onclick="submitted();document.querySelector('user-query').textContent=document.querySelector('.ql-editor').textContent;document.querySelector('model-response').style.display='block';history.pushState({},'', '/app/fixture-id')">Send</button><user-query></user-query><model-response style="display:none"><img alt="AI generated" src="${image}"><button aria-label="Download full size image" onclick="const a=document.createElement('a');a.href='${image}';a.download='original.png';a.click()">Download</button></model-response>`;
  await context.route('https://gemini.google.com/**', route => route.request().url().endsWith('/original.png') ? route.fulfill({ status: 200, headers: { 'content-disposition': 'attachment; filename="image.png"' }, contentType: 'image/png', body: Buffer.from(image.split(',')[1], 'base64') }) : route.fulfill({ contentType: 'text/html', body: html }));
  const blocker = join(root, 'blocker'); await writeFile(blocker, 'file');
  const options = await validateOptions({ command: 'generate', prompt: 'A blue cup', model: 'Argon', session: 's', name: 'n', output_root: root, reference: [], out: join(blocker, 'asset.png') });
  await assert.rejects(generate(options, context), /EEXIST|ENOTDIR/);
  const path = join(root, 's/n/manifest.json');
  const failed = JSON.parse(await readFile(path));
  assert.equal(failed.status, 'interrupted'); assert.equal(failed.phase, 'saving');
  assert.equal(failed.response_kind, 'save-error'); assert.equal(failed.retryable, true);
  assert.equal(failed.download.original, true); assert.equal(failed.download.width, 512);
  assert.ok(failed.conversation_url.endsWith('/app/fixture-id'));
  await resume(await validateOptions({ command: 'resume', manifest: path, out: join(root, 'fixed/asset.png'), reference: [] }), context);
  assert.equal(JSON.parse(await readFile(path)).status, 'completed');
  assert.equal(submissions, 1);
  assert.deepEqual(await readFile(join(root, 'fixed/asset.png')), Buffer.from(image.split(',')[1], 'base64'));
}));

test('没有会话 ID 且原页面丢失时，resume 立即报告无法恢复', async () => {
  const root = await mkdtemp(join(tmpdir(), 'gemini-lost-'));
  const path = join(root, 'manifest.json');
  await writeFile(path, JSON.stringify({ status: 'interrupted', prompt: 'a cup', output_dir: root, conversation_url: 'https://gemini.google.com/app' }));
  await assert.rejects(resume({ manifest: path }), /原浏览器页面已不存在/);
});

test('Flash-Lite 不得被误认成指定的 Flash', () => fixture(async page => {
  const { ensureImageModel } = await import('./gemini-image-web.mjs');
  await page.setContent(`<button id="picker" aria-label="Open mode picker, currently Flash-Lite" onclick="document.querySelector('#menu').hidden=false">Flash-Lite</button><div id="menu" hidden><span onclick="const p=document.querySelector('#picker');p.textContent='Flash';p.setAttribute('aria-label','Open mode picker, currently Flash');document.querySelector('#menu').hidden=true"> 3.8 Flash </span><span> 3.5 Flash-Lite </span></div>`);
  await ensureImageModel(page, 'Flash');
  assert.equal(await page.locator('#picker').innerText(), 'Flash');
}));
