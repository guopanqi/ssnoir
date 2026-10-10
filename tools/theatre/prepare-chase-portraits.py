#!/usr/bin/env python3
"""拆分内置生图生成的透明双帧跑姿；保留统一尺度与源图 alpha。"""
from pathlib import Path
import re, uuid
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'UnityClient/Assets/Resources/Portraits/Chase'
OUT.mkdir(parents=True,exist_ok=True)
template=(ROOT/'UnityClient/Assets/Resources/Portraits/Neon/尼尔.png.meta').read_text()
for who,source in [('尼尔','角色库/尼尔/形象/姿势/追逐/跑步双帧-openai.png'),('取信人','角色库/路人/形象/追逐/跑步双帧-openai.png')]:
    sheet=Image.open(ROOT/source).convert('RGBA')
    w,h=sheet.size
    assert w%2==0 and w//2==h, sheet.size
    assert sheet.getchannel('A').getextrema()[0]==0, '源图必须透明'
    side=w//2
    for n,label in enumerate(['甲','乙']):
        frame=sheet.crop((n*side,0,(n+1)*side,h))
        # 两张源图共用同一绘制比例；头部尺度与既有基础立绘相近。
        # 固定缩整张面板，不按人物包围盒或单帧身高撑满。
        frame=frame.resize((800,800),Image.Resampling.LANCZOS)
        canvas=Image.new('RGBA',(1024,1024));canvas.paste(frame,(112,0));frame=canvas
        bbox=frame.getchannel('A').point(lambda p:255 if p>=32 else 0).getbbox()
        assert bbox and bbox[3]-bbox[1]>600
        # 制作阶段对齐鞋底；不裁紧、不按角色或帧缩放身体。
        aligned=Image.new('RGBA',(1024,1024));aligned.paste(frame,(0,1024-bbox[3]))
        asset=OUT/(who+'_跑步'+label+'.png');aligned.save(asset)
        meta=asset.with_suffix('.png.meta')
        if not meta.exists():
            txt=re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,template)
            txt=txt.replace('grayScaleToAlpha: 1','grayScaleToAlpha: 0').replace('alphaIsTransparency: 0','alphaIsTransparency: 1')
            meta.write_text('\n'.join(l.rstrip() for l in txt.splitlines())+'\n')
        print(asset.name,bbox)
meta=OUT.with_suffix('.meta')
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\n')
