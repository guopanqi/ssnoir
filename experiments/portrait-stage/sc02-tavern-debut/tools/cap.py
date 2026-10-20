#!/usr/bin/env python3
"""SC-A02 实验截帧验证：真实时间播放，抓取指定时刻画面 + 收集 console 错误。
用法：python3 tools/cap.py [out_dir]"""
import sys, os, json, asyncio
from playwright.async_api import async_playwright

HERE=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PAGE='file://'+os.path.join(HERE,'index.html')
OUT=sys.argv[1] if len(sys.argv)>1 else os.path.join(HERE,'shots')
# 时刻（秒）：幕启、问答中、目光/追光、登台、演唱、对话、近景、点头收杯、完
MARKS=[3,9,16,22,27,34,40,46,52,57,63,69,75]
os.makedirs(OUT,exist_ok=True)

async def main():
    errors=[]
    async with async_playwright() as p:
        b=await p.chromium.launch()
        pg=await b.new_page(viewport={'width':1600,'height':1010})
        pg.on('console',lambda m:errors.append(m.text) if m.type=='error' else None)
        pg.on('pageerror',lambda e:errors.append(str(e)))
        await pg.goto(PAGE);await pg.wait_for_timeout(800)
        await pg.click('#go');await pg.wait_for_timeout(600)
        started=pg.evaluate('performance.now()')
        for t in MARKS:
            dt=max(0,t*1000-(pg.evaluate('performance.now()-started')))
            await pg.wait_for_timeout(int(dt))
            await pg.screenshot(path=os.path.join(OUT,f'{t:02d}s.png'))
        await pg.wait_for_timeout(4000)
        paused=await pg.evaluate('paused')
        await b.close()
    print('paused_at_end',paused)
    print('ERRORS:',json.dumps(errors,ensure_ascii=False) if errors else 'none')

asyncio.run(main())
