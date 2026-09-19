#!/usr/bin/env python3
"""实验（未接入运行时）：用 process.py 整理出的"真的管子"骨架，让程序自己重新点亮一张立绘。

用来看骨架质量，也用来试霓虹的表现语言：等宽玻璃管 + 白芯 + 叠加光晕；交叉处上面的管子压住下面的；
长管两端有暗玻璃电极；每根管子亮度微差。运行时仍画原贴图，这里的渲染只是参考。

输出到 tools/portrait-neon/out/（gitignore）：<名>_tubes.png（源图 / 笔画分色 / 重渲）和 <名>_full.png。
用法： python3 tools/portrait-neon/tubes.py 夜莺 尼尔_逼近
"""
import sys, os, math, colorsys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import process as P
from PIL import Image, ImageDraw, ImageFilter

D = P.NEON_DIR
S = os.environ.get("OUT", os.path.join(os.path.dirname(os.path.abspath(__file__)), "out"))
os.makedirs(S, exist_ok=True)
N = P.SKELETON_SIZE


def chaikin(pts, it=3):
    for _ in range(it):
        if len(pts) < 3:
            return pts
        out = [pts[0]]
        for a, b in zip(pts, pts[1:]):
            out.append((0.75 * a[0] + 0.25 * b[0], 0.75 * a[1] + 0.25 * b[1]))
            out.append((0.25 * a[0] + 0.75 * b[0], 0.25 * a[1] + 0.75 * b[1]))
        out.append(pts[-1])
        pts = out
    return pts


stroke_len = P.pixel_length

# ---------- 4. 渲染 ----------
def render(strokes, colors, size=1024, scale=2, tube=4.0, occlude=True, electrodes=True):
    from PIL import ImageChops
    W=size*scale; k=W/N
    def sc(p): return (p[0]*k, p[1]*k)
    black=(0,0,0)
    img=Image.new("RGB",(W,W),black)
    # 光晕：三层，粗→细，模糊→清，加法叠
    def glowc(c):
        m=max(c) or 1; return tuple(min(255,int(v*255/m)) for v in c)
    for width,blur,alpha in ((tube*10,40,0.30),(tube*4,14,0.55),(tube*1.8,4,0.95)):
        layer=Image.new("RGB",(W,W),black); d=ImageDraw.Draw(layer)
        for s,c in zip(strokes,colors):
            d.line([sc(p) for p in s],fill=tuple(min(255,int(v*alpha)) for v in glowc(c)),width=int(width*scale),joint="curve")
        layer=layer.filter(ImageFilter.GaussianBlur(blur*scale))
        img=ImageChops.add(img,layer)
    d=ImageDraw.Draw(img)
    core_w=int(tube*scale)
    import random; rnd=random.Random(7)
    for s,c in zip(strokes,colors):
        c=tuple(int(v*rnd.uniform(0.82,1.0)) for v in c)
        pts=[sc(p) for p in s]
        if occlude:
            d.line(pts,fill=black,width=int(core_w*2.4),joint="curve")
        wall=tuple(min(255,int(v*0.6+255*0.4)) for v in glowc(c))
        d.line(pts,fill=wall,width=core_w,joint="curve")
        core=tuple(min(255,int(v*0.2+255*0.8)) for v in glowc(c))
        d.line(pts,fill=core,width=max(1,int(core_w*0.5)),joint="curve")
        if electrodes and s[0]!=s[-1] and stroke_len(s)>40*N/512:
            for end,nxt in ((pts[0],pts[1]),(pts[-1],pts[-2])):
                # 电极：管口一小截不亮的玻璃（暗色延伸段）＋ 封口的黑点
                dx,dy=end[0]-nxt[0],end[1]-nxt[1]; L=math.hypot(dx,dy) or 1
                stub=(end[0]+dx/L*core_w*2.2, end[1]+dy/L*core_w*2.2)
                dim=tuple(int(v*0.28) for v in c)
                d.line([end,stub],fill=dim,width=int(core_w*0.8))
                r=core_w*0.55
                d.ellipse([stub[0]-r,stub[1]-r,stub[0]+r,stub[1]+r],fill=(30,30,36))
    return img.resize((size,size),Image.LANCZOS)

def ImageChops_add(a,b):
    from PIL import ImageChops
    return ImageChops.add(a,b)

def run(name, tube=5.5, out=None):
    img=Image.open(f"{D}/{name}.png")
    lines,accent,accent_rgb=P.split_layers(img)
    strokes=P.trace_strokes(P.thin(P.binarize(lines)))
    strokes.sort(key=stroke_len,reverse=True)
    # 颜色：沿笔画采样点缀蒙版
    acc=accent.filter(ImageFilter.MaxFilter(9)).resize((N,N),Image.BILINEAR).load()
    colors=[]
    base=(235,240,255); accent_rgb=accent_rgb or (255,90,140)
    for s in strokes:
        hit=sum(1 for p in s if acc[int(p[0]),int(p[1])]>60)
        colors.append(accent_rgb if hit>len(s)*0.5 else base)
    smooth=[chaikin(s) for s in strokes]
    print(f"{name}: 管子 {len(strokes)} 根；最长 {[round(stroke_len(s)) for s in strokes[:8]]}")
    # 图1：笔画上色（每条一色）
    dbg=Image.new("RGB",(512,512),(0,0,0)); dd=ImageDraw.Draw(dbg)
    for i,s in enumerate(smooth):
        c=tuple(int(255*v) for v in colorsys.hsv_to_rgb((i*0.618)%1,0.85,1))
        dd.line([(x*512/N,y*512/N) for x,y in s],fill=c,width=2)
        for e in (s[0],s[-1]):
            e=(e[0]*512/N,e[1]*512/N); dd.ellipse([e[0]-3,e[1]-3,e[0]+3,e[1]+3],outline=(255,255,255))
    dbg=dbg.resize((512,512))
    neon=render(smooth,colors,tube=tube).resize((512,512),Image.LANCZOS)
    src=img.convert("RGB").resize((512,512))
    cmp=Image.new("RGB",(1536,512)); cmp.paste(src,(0,0)); cmp.paste(dbg,(512,0)); cmp.paste(neon,(1024,0))
    cmp.save(out or f"{S}/{name}_tubes.png")
    full=render(smooth,colors,tube=tube)
    big=Image.new("RGB",(2048,1024)); big.paste(img.convert("RGB"),(0,0)); big.paste(full,(1024,0)); big.save(f"{S}/{name}_full.png")
    return smooth,colors

if __name__=="__main__":
    for n in sys.argv[1:] or ["夜莺"]: run(n)
