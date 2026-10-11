#!/usr/bin/env python3
"""将两名跑者的透明冲刺定姿接入舞台，整张画布共用尺度，不裁紧人物。"""
from pathlib import Path
import re
import uuid
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'UnityClient/Assets/Resources/Portraits/Chase'
SOURCES = {
    '尼尔': '角色库/尼尔/形象/姿势/追逐/冲刺-openai.png',
    '取信人': '角色库/路人/形象/追逐/冲刺-openai.png',
}
template = (ROOT / 'UnityClient/Assets/Resources/Portraits/Neon/尼尔.png.meta').read_text()
OUT.mkdir(parents=True, exist_ok=True)
for who, source in SOURCES.items():
    image = Image.open(ROOT / source).convert('RGBA')
    assert image.width == image.height, image.size
    assert image.getchannel('A').getextrema()[0] == 0, '源图必须透明'
    # 两名跑者共用基础图的头部绘制尺度。固定缩整张画布，不按人物身高放大。
    image = image.resize((800, 800), Image.Resampling.LANCZOS)
    bbox = image.getchannel('A').point(lambda a: 255 if a >= 32 else 0).getbbox()
    assert bbox and bbox[3] - bbox[1] > 450, '人物缺失或尺度错误'
    canvas = Image.new('RGBA', (1024, 1024))
    # 腾空关键姿势：最低鞋底留在地面上方，不逐帧移动或晃动立绘。
    canvas.paste(image, (112, 992 - bbox[3]))
    asset = OUT / (who + '_冲刺.png')
    canvas.save(asset)
    meta = asset.with_suffix('.png.meta')
    if not meta.exists():
        text = re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, template)
        text = text.replace('grayScaleToAlpha: 1', 'grayScaleToAlpha: 0')
        text = text.replace('alphaUsage: 2', 'alphaUsage: 1')
        text = text.replace('alphaIsTransparency: 0', 'alphaIsTransparency: 1')
        meta.write_text('\n'.join(line.rstrip() for line in text.splitlines()) + '\n')
    print(asset.name, bbox)
