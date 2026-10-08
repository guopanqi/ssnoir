#!/usr/bin/env python3
"""派生 Neon 人物遮挡贴图，不修改原图。轮廓闭合后填充内部，保留外部凹口。
运行：python3 tools/portrait-neon/silhouette.py [人物或姿势名 ...]
遮罩与原图同画幅；_silhouette.png 可人工修订，运行时不推断轮廓。
"""
from pathlib import Path
import sys
import uuid
from PIL import Image, ImageChops, ImageFilter, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
DIRECTORY = ROOT / 'UnityClient/Assets/Resources/Portraits/Neon'

def generate(source):
    rgb = Image.open(source).convert('RGB')
    # 小尺度闭合断开的灯管，避免将整张人物矩形或凸包作为实体。
    channels = rgb.split()
    light = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2])
    size = rgb.size
    light = light.resize((512, 512), Image.Resampling.LANCZOS)
    border = light.point(lambda p: 255 if p >= 60 else 0)
    border = border.filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.MinFilter(7))
    # 源图中被画幅裁断的腿必须在画幅边缘封口，否则背景会从脚底灌入。
    bottom = [x for x in range(512) if border.getpixel((x, 511))]
    if bottom:
        ImageDraw.Draw(border).line((min(bottom), 511, max(bottom), 511), fill=255)
    padded = Image.new('L', (514, 514)); padded.paste(border, (1, 1))
    ImageDraw.floodfill(padded, (0, 0), 128, thresh=0)
    mask = padded.crop((1, 1, 513, 513)).point(lambda p: 0 if p == 128 else 255)
    mask = mask.resize(size, Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(.6))
    output = source.with_name(source.stem + '_silhouette.png')
    image = Image.new('RGBA', size, 'white'); image.putalpha(mask); image.save(output)
    meta = output.with_suffix('.png.meta')
    if not meta.exists():
        template = source.with_suffix('.png.meta').read_text()
        import re
        template = re.sub(r'guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, template, count=1)
        template = template.replace('grayScaleToAlpha: 1', 'grayScaleToAlpha: 0').replace('alphaIsTransparency: 0', 'alphaIsTransparency: 1').replace('alphaUsage: 2', 'alphaUsage: 1')
        meta.write_text("\n".join(line.rstrip() for line in template.splitlines()) + "\n")
    return output

if __name__ == '__main__':
    sources = [DIRECTORY / (name + '.png') for name in sys.argv[1:]] if len(sys.argv) > 1 else sorted(
        p for p in DIRECTORY.glob('*.png') if not p.stem.endswith(('_lines', '_accent', '_silhouette')))
    for source in sources: generate(source)
    print(f'已生成 {len(sources)} 张轮廓遮罩')
