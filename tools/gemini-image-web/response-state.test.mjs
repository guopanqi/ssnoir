import assert from 'node:assert/strict';
import test from 'node:test';
import { classifyResponse } from './response-state.mjs';
test('区分可重试服务故障与生成失败', () => {
  assert.equal(classifyResponse('Sorry, something went wrong. Please try your request again.').kind, 'transient-error');
  assert.equal(classifyResponse("I can't create images right now.").kind, 'generation-error');
  assert.equal(classifyResponse("I'm having a hard time fulfilling your request. Can I help you with something else instead?").kind, 'generation-error');
  assert.equal(classifyResponse("I can't generate the image you requested right now due to interests of third-party content providers. Please edit your prompt and try again.").kind, 'generation-error');
  assert.equal(classifyResponse('正在为你生成图片').kind, 'pending');
});

test('等待结果只读取最新回复，不被提示词、旧错误或旧图片污染', async () => {
  const { chromium } = await import('playwright');
  const { waitForResult } = await import('./gemini-image-web.mjs');
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  try {
    const page = await browser.newPage();
    const svg = encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256"><rect width="256" height="256" fill="red"/></svg>');
    await page.setContent(`<user-query>Draw a sign saying Something went wrong</user-query><model-response>Sorry, something went wrong.<img alt="AI generated old" src="data:image/svg+xml,${svg}"></model-response><model-response id="current"><img alt="AI generated current" src="data:image/svg+xml,${svg}"></model-response>`);
    const image = await waitForResult(page, 3000, Date.now());
    assert.equal(await image.getAttribute('alt'), 'AI generated current');
  } finally { await browser.close(); }
});

test('当前临时错误通过 Redo 重试，返回该回复的新图', async () => {
  const { chromium } = await import('playwright');
  const { waitForResult } = await import('./gemini-image-web.mjs');
  const browser = await chromium.launch({channel:'chrome',headless:true});
  try {
    const page = await browser.newPage();
    await page.setContent('<model-response id="response">Sorry, something went wrong.<button aria-label="Redo">Retry</button></model-response>');
    await page.evaluate(() => {
      window.retryCount=0;
      document.querySelector('button').onclick=()=>{
        window.retryCount++;
        const svg=encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256"/>');
        document.querySelector('#response').innerHTML=`<img alt="AI generated retry" src="data:image/svg+xml,${svg}">`;
      };
    });
    const image = await waitForResult(page,16000,Date.now());
    assert.equal(await image.getAttribute('alt'),'AI generated retry');
    assert.equal(await page.evaluate(()=>window.retryCount),1);
  } finally { await browser.close(); }
});


test('拒绝即时返回完整原文；未知文字超时仍附带本次回复', async () => {
  const { chromium } = await import('playwright');
  const { waitForResult } = await import('./gemini-image-web.mjs');
  const browser = await chromium.launch({channel:'chrome',headless:true});
  try {
    const page = await browser.newPage();
    const refusal = "I can't generate the image you requested right now due to interests of third-party content providers. " + 'Detailed explanation. '.repeat(30) + 'Please edit your prompt and try again.';
    await page.setContent('<user-query>PRIVATE PROMPT</user-query><model-response>OLD ERROR</model-response><model-response id="current"></model-response>');
    await page.locator('#current').evaluate((el,text)=>el.textContent=text,refusal);
    await assert.rejects(waitForResult(page,3000,Date.now()), error => {
      assert.equal(error.response_kind,'generation-error');
      assert.equal(error.response_text,refusal);
      assert.ok(error.message.includes(refusal));
      assert.ok(!error.message.includes('PRIVATE PROMPT'));
      assert.ok(!error.message.includes('OLD ERROR'));
      return true;
    });
    const unknown='This request was declined for a newly worded reason.';
    await page.locator('#current').evaluate((el,text)=>el.textContent=text,unknown);
    await assert.rejects(waitForResult(page,100,Date.now()),error=>{
      assert.equal(error.response_kind,'timeout');
      assert.equal(error.response_text,unknown);
      assert.ok(error.message.includes(unknown));
      return true;
    });
  } finally {await browser.close();}
});
