"""真实时间逐帧截图(不加速)，用于检查节奏/动作。用法: python3 tools/realtime_capture.py 步长秒 总秒数 [html] [章节]   第4个参数=章节按钮序号，默认4(追逐)；输出 rt0.png..."""
import sys
from playwright.sync_api import sync_playwright
from PIL import Image
f='file://'+__import__('os').path.abspath(sys.argv[3] if len(sys.argv)>3 else 'dist/portrait-theatre-story-claude-sonnet-5-5.html')+''
step=float(sys.argv[1]);total=float(sys.argv[2])
with sync_playwright() as p:
    b=p.chromium.launch();pg=b.new_page(viewport={'width':1280,'height':800});errs=[]
    pg.on('pageerror',lambda e:errs.append(str(e)[:300]))
    pg.goto(f);pg.wait_for_timeout(300);pg.click(f'#chips button:nth-child({int(sys.argv[4]) if len(sys.argv)>4 else 4})')
    n=int(total/step);ims=[]
    for i in range(n):
        pg.wait_for_timeout(int(step*1000))
        pg.screenshot(path='_x.png',clip={'x':50,'y':0,'width':1180,'height':664});ims.append(Image.open('_x.png').resize((590,332)))
    print('errors',errs or 'none',len(ims))
    for k in range(0,len(ims),8):
        part=ims[k:k+8];sh=Image.new('RGB',(590*2,332*((len(part)+1)//2)))
        for i,im in enumerate(part):sh.paste(im,((i%2)*590,(i//2)*332))
        sh.save(f'rt{k//8}.png')
    b.close()
