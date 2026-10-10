#!/usr/bin/env python3
"""用法:
  python3 test_play.py FILE.html full            # 快进整部戏，报告JS错误
  python3 test_play.py FILE.html sheet N t1,t2,.. # 第N个片段(按钮序号,从1起)，在指定秒数截图并拼成 sheetN.png
需要: pip install playwright pillow && playwright install chromium
时间被 setTimeout 除以倍数加速(full=12, sheet=4)，截图秒数是加速后的时间。"""
import sys,os
from playwright.sync_api import sync_playwright
from PIL import Image
f='file://'+os.path.abspath(sys.argv[1]);mode=sys.argv[2]
def page(b,div):
    pg=b.new_page(viewport={'width':1280,'height':800});errs=[]
    pg.on('pageerror',lambda e:errs.append(str(e)[:300]))
    pg.add_init_script(f"const _st=window.setTimeout;window.setTimeout=(f,t,...a)=>_st(f,(t||0)/{div},...a);")
    pg.goto(f);pg.wait_for_timeout(300);return pg,errs
with sync_playwright() as p:
    b=p.chromium.launch()
    if mode=='full':
        pg,errs=page(b,12);pg.click('#all')
        for i in range(300):
            pg.wait_for_timeout(1500)
            if errs or (i>3 and pg.evaluate("document.querySelector('#stage').classList.contains('idl')")):break
        print('errors:',errs or 'none');print('end title:',pg.inner_text('#title')[:30])
    else:
        n=int(sys.argv[3]);ts=[float(x) for x in sys.argv[4].split(',')]
        pg,errs=page(b,4);pg.click(f'#chips button:nth-child({n})');t0=0;ims=[]
        for t in ts:
            pg.wait_for_timeout(int((t-t0)*1000));t0=t
            pg.screenshot(path='_x.png',clip={'x':50,'y':0,'width':1180,'height':664});ims.append(Image.open('_x.png').resize((472,266)))
        os.remove('_x.png');sh=Image.new('RGB',(944,266*((len(ims)+1)//2)))
        for i,im in enumerate(ims):sh.paste(im,((i%2)*472,(i//2)*266))
        sh.save(f'sheet{n}.png');print('errors:',errs or 'none','->',f'sheet{n}.png')
    b.close()
