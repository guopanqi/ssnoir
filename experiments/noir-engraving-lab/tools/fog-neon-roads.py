"""雾与霓虹 · 车流路径烘焙（读 prefabs/世界.blend 的干道曲线 → public/fog-neon/roads.json）

用法
----
    cd experiments/noir-engraving-lab            # 在 lab 根目录执行
    npm run export:fog-neon-roads
"""

import json
import os

import bpy

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'public', 'fog-neon')
STEP = 25.0  # 米

paths = []
for o in bpy.data.objects:
    if o.type != 'CURVE' or not o.name.startswith('干道'):
        continue
    mw = o.matrix_world
    for spline in o.data.splines:
        # 全是 POLY 样条（Blender 5.2 无 Spline.evaluate，poly 直接用控制点）
        raw = [mw @ p.co.xyz for p in spline.points]
        pts = []
        for i in range(len(raw) - 1):
            a, b = raw[i], raw[i + 1]
            d = (b - a)
            n = max(1, round(d.length / STEP))
            for k in range(n):
                pts.append(a + d * (k / n))
        pts.append(raw[-1])
        paths.append({'name': o.name, 'points': [[round(p.x, 1), round(p.y, 1)] for p in pts]})

os.makedirs(OUT_DIR, exist_ok=True)
with open(os.path.join(OUT_DIR, 'roads.json'), 'w') as f:
    json.dump(paths, f, ensure_ascii=False)
print(f'ROADS paths={len(paths)} pts={sum(len(p["points"]) for p in paths)}')
