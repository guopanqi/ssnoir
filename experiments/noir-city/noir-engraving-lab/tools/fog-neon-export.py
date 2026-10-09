"""雾与霓虹 · 城市数据烘焙（一次性；lab 的运行时只读冻结好的 public/fog-neon/）

只读 `city-box/build/city/city_build.blend`（构建产物），导出三份数据：
- city.glb      世界视角可见的城市网格（材质名原样保留，运行时按名字重新分派）
- lamps.json    干道网格边界边采样的沿街点位（路灯）
- anchors.json  引擎锚点 Anchor_* 及其所在实例的顶面矩形（标注 UI 的锚点）

导出过滤：剔除 `内部_*`（世界层不可见）与东北角舞台区（y>900 且 x>700）。
坐标：Blender Z-up → glTF Y-up 由导出器处理；JSON 里的 Blender 坐标由 JS 侧换算。

用法
----
    cd experiments/noir-city/noir-engraving-lab            # 在 lab 根目录执行
    npm run export:fog-neon                      # city.glb + lamps.json + anchors.json
    npm run export:fog-neon-roads                # roads.json（读 prefabs/世界.blend 的干道曲线）

注意：export_city / extract_lamps / extract_anchors 三段合并在同一个 blender 会话里执行，
由本文件按 __main__ 传入的模式分发。
"""

import json
import os

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'public', 'fog-neon')


def parent_chain_hidden(obj):
    o = obj
    while o is not None:
        if o.name.startswith('内部'):
            return True
        o = o.parent
    return False


def in_stage_corner(p):
    return p.y > 900 and p.x > 700


def export_glb():
    import bpy
    out = os.path.join(OUT_DIR, 'city.glb')
    for o in bpy.data.objects:
        keep = (
            o.type == 'MESH'
            and not parent_chain_hidden(o)
            and not in_stage_corner(o.matrix_world.translation)
            and not o.hide_render
        )
        o.select_set(keep)
    sel = len(bpy.context.selected_objects)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.context.selected_objects)
    print(f'EXPORT selected={sel} tris={tris}')
    bpy.ops.export_scene.gltf(
        filepath=out,
        export_format='GLB',
        use_selection=True,
        export_cameras=False,
        export_lights=False,
        export_apply=True,
    )
    print('EXPORT done ->', out)


def export_lamps(spacing=45.0):
    """干道网格边界边采样 → 路灯点位。"""
    import bpy
    out = os.path.join(OUT_DIR, 'lamps.json')

    def road_edges():
        segs = []
        for o in bpy.data.objects:
            if o.type != 'MESH':
                continue
            if not any(sl.material and sl.material.name == 'M_干道' for sl in o.material_slots):
                continue
            mw = o.matrix_world
            mesh = o.data
            edge_faces = {}
            for poly in mesh.polygons:
                for ek in poly.edge_keys:
                    edge_faces[ek] = edge_faces.get(ek, 0) + 1
            verts = mesh.vertices
            for ek, n in edge_faces.items():
                if n == 1:
                    segs.append((mw @ verts[ek[0]].co, mw @ verts[ek[1]].co))
        return segs

    segs = road_edges()
    pts = []
    for a, b in segs:
        d = (b - a)
        L = d.length
        if L < 1e-6:
            continue
        n = max(1, round(L / spacing))
        for i in range(n + 1):
            pts.append(a + d * (i / n))
    # 贪心抽稀
    out_pts = []
    for p in pts:
        if all((p - q).length_squared >= (spacing * 0.9) ** 2 for q in out_pts):
            out_pts.append(p)
    data = [[round(p.x, 1), round(p.y, 1), round(p.z, 2)] for p in out_pts]
    with open(out, 'w') as f:
        json.dump({'spacing': spacing, 'points': data}, f, ensure_ascii=False)
    print(f'LAMPS segments={len(segs)} points={len(data)}')


def export_anchors():
    """引擎锚点 Anchor_* + 所在实例的顶面矩形（标注 UI 用）。"""
    import bpy
    from mathutils import Vector
    out = os.path.join(OUT_DIR, 'anchors.json')
    bpy.context.view_layer.update()
    items = []
    for o in bpy.data.objects:
        if o.type != 'EMPTY' or not o.name.startswith('Anchor_'):
            continue
        p = o.matrix_world.translation
        if in_stage_corner(p):
            continue
        root = o
        while root.parent is not None:
            root = root.parent
        xs, ys, zs = [], [], []
        for m in root.children_recursive:
            if m.type != 'MESH':
                continue
            for corner in m.bound_box:
                w = m.matrix_world @ Vector(corner)
                xs.append(w.x); ys.append(w.y); zs.append(w.z)
        if not xs:
            bbox = {'top': round(p.z, 1), 'hx': 8.0, 'hy': 8.0, 'cx': round(p.x, 1), 'cy': round(p.y, 1)}
        else:
            bbox = {
                'top': round(max(zs), 1),
                'hx': round((max(xs) - min(xs)) / 2, 1),
                'hy': round((max(ys) - min(ys)) / 2, 1),
                'cx': round((max(xs) + min(xs)) / 2, 1),
                'cy': round((max(ys) + min(ys)) / 2, 1),
            }
        items.append({
            'name': o.name[len('Anchor_'):],
            'pos': [round(p.x, 1), round(p.y, 1), round(p.z, 2)],
            'root': root.name,
            **bbox,
        })
    with open(out, 'w') as f:
        json.dump(items, f, ensure_ascii=False, indent=1)
    print(f'ANCHORS {len(items)}')


if __name__ == '__main__':
    os.makedirs(OUT_DIR, exist_ok=True)
    export_glb()
    export_lamps()
    export_anchors()
