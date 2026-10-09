#!/usr/bin/env python3
"""拆分生成的双帧跑姿；保留统一画布尺度，去除分隔/标签并派生透明外部。"""
from pathlib import Path
import re, uuid
from PIL import Image, ImageDraw, ImageFilter, ImageChops
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'UnityClient/Assets/Resources/Portraits/Chase'
OUT.mkdir(parents=True,exist_ok=True)
template=(ROOT/'UnityClient/Assets/Resources/Portraits/Neon/尼尔.png.meta').read_text()
for who,source in [('尼尔','角色库/尼尔/形象/姿势/追逐/跑步双帧.png'),('取信人','角色库/路人/形象/追逐/跑步双帧.png')]:
    sheet=Image.open(ROOT/source).convert('RGB')
    w,h=sheet.size
    assert w%2==0 and abs(w//2-h)<20, sheet.size
    side=w//2
    for n,label in enumerate(['甲','乙']):
        frame=Image.new("RGB",(side,side))
        frame.paste(sheet.crop((n*side,0,(n+1)*side,h)),(0,0))
        draw=ImageDraw.Draw(frame)
        draw.rectangle((0,0,3,side),fill='black');draw.rectangle((side-3,0,side,side),fill='black')
        draw.rectangle((0,0,55,45),fill='black') # 图外 A/B 标签，不涉及人物
        frame=frame.resize((1024,1024),Image.Resampling.LANCZOS)
        channels=frame.split()
        light=ImageChops.lighter(ImageChops.lighter(channels[0],channels[1]),channels[2])
        border=light.point(lambda p:255 if p>=45 else 0)
        border=border.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
        pad=Image.new('L',(1026,1026));pad.paste(border,(1,1))
        ImageDraw.floodfill(pad,(0,0),128)
        mask=pad.crop((1,1,1025,1025)).point(lambda p:0 if p==128 else 255)
        bbox=mask.getbbox();assert bbox and bbox[3]-bbox[1]>600
        image=frame.convert('RGBA');image.putalpha(mask)
        # 制作阶段对齐鞋底；不裁紧、不按角色或帧缩放身体。
        aligned=Image.new('RGBA',(1024,1024));aligned.paste(image,(0,1024-bbox[3]))
        asset=OUT/(who+'_跑步'+label+'.png');aligned.save(asset)
        meta=asset.with_suffix('.png.meta')
        if not meta.exists():
            txt=re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,template)
            txt=txt.replace('grayScaleToAlpha: 1','grayScaleToAlpha: 0').replace('alphaIsTransparency: 0','alphaIsTransparency: 1')
            meta.write_text('\n'.join(l.rstrip() for l in txt.splitlines())+'\n')
        print(asset.name,bbox)
meta=OUT.with_suffix('.meta')
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\n')
