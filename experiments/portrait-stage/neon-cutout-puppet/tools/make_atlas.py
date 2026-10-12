"""Generate compact visual sprite sheet from the manually reviewed character cutout concept.
Raw generation is independent of SSNoir production Neon; this experiment does not overwrite it.
Usage: python tools/make_atlas.py /path/to/noir_detective_puppet_rig_parts.png
"""
from PIL import Image
from pathlib import Path
import argparse
p=argparse.ArgumentParser();p.add_argument('source'); a=p.parse_args()
src=Image.open(a.source).convert('RGBA')
assert src.size==(1448,1086), f'Unexpected cutout reference dimensions: {src.size}'
root=Path(__file__).resolve().parents[1]/'assets'
root.mkdir(parents=True,exist_ok=True)
# Retain full-res source locally for close inspection, and a small immutable runtime sprite sheet.
src.save(root/'source-atlas.png',optimize=True)
small=src.resize((720,540),Image.Resampling.LANCZOS)
# Transparency is preserved in palette. This file is transportable through GitHub without storing the >1MB source.
small.quantize(colors=16,method=Image.Quantize.FASTOCTREE).save(root/'puppet-atlas.png',optimize=True,compress_level=9)
print('Runtime sheet:',root/'puppet-atlas.png',(root/'puppet-atlas.png').stat().st_size)
