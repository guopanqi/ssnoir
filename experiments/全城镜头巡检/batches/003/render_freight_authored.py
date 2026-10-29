"""Render freight-company authored shot through the City preview renderer.

Run from repository root:
  blender -b city-box/build/city/city_build.blend --python-exit-code 1 \
    --python experiments/全城镜头巡检/batches/003/render_freight_authored.py
"""
import math
import sys
from pathlib import Path

import bpy

root = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(root / 'city-box/pipeline'))
import render as R

batch = Path(__file__).parent
scene = bpy.context.scene
R.setup(scene)
scene.render.resolution_x = 640
scene.render.resolution_y = 360
camera = bpy.data.objects['Camera_货运公司']
camera.data.sensor_fit = 'VERTICAL'
camera.data.sensor_height = 24
camera.data.lens = 12 / math.tan(math.radians(26.99146656 / 2))
R.set_focus(scene, R._top_root(camera))
output = batch / 'freight-authored-formal.png'
R.render(scene, camera, str(output))
print(f'AUTHORED_RENDER| {output}| vFov=26.99146656| lens={camera.data.lens:.6f}| size={scene.render.resolution_x}x{scene.render.resolution_y}')
