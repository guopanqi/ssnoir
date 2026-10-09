"""黑水 · 城市数据烘焙（一次性；lab 的运行时只读冻结好的 public/heishui/）

只读 `city-box/build/city/city_build.blend`（构建产物），把它压成 Three.js 侧能直接吃的
`data/city.json` + `data/city.bin`。**不修改任何正式产物。**

导出原则
--------
1. 只导"世界视角能看到的东西"。`layer=interior` 的地点家具/道具/人形在世界视角整体隐藏，
   这里直接剔除——它们是地点预览的负担，不是城市剪影的一部分。
2. 描线一个都不导。正式城市把描线烘成 Mesh（`描线_*`，全城 100 万顶点），
   本实验的描线是屏幕空间的、按光照调制的，必须自己算。
3. 连续形体（地面/河/驳岸/桥/广场/地点外壳）合并成少量大 Mesh；重复形体
   （填充建筑、屋顶件、树）导成实例表，由 Three.js 侧用 InstancedMesh 画。
4. 坐标：Blender 右手 Z-up (x, y, z) -> Three.js 右手 Y-up (x, z, -y)。

用法
----
    cd experiments/noir-city/noir-engraving-lab            # 在 lab 根目录执行
    npm run export:heishui
"""

import json
import math
import os
import struct
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.normpath(os.path.join(HERE, "..", "public", "heishui"))

# 地点外壳用的表面角色表（= world_style.json 的 surfaces 键），编号写进顶点属性。
SURFACE_ROLES = [
    "主体", "地面", "抛光地面", "象牙饰面", "亮金装饰", "暗金饰面",
    "金色细饰", "暖白发光表面", "冷白发光表面", "水面短线",
]
SURFACE_ID = {name: i for i, name in enumerate(SURFACE_ROLES)}

# 窗光三档
WINDOW_TIERS = ["white", "grey", "blue"]

# 填充建筑原型：只有这四种。底 z=0，高 1，xy 落在 ±0.5。
BUILDING_PROTOS = ["box", "roofbox", "pitch", "setback"]
ROOF_PROTOS = ["box", "stack", "ridge"]

# 绿化里的"树"用 pitch 原型缩放而成，单独出来，给它自己的材质角色。
TREE_PROTO_INDEX = -1


def to_three(v):
    """Blender (x, y, z) -> Three.js (x, z, -y)"""
    return (v[0], v[2], -v[1])


def root_of(ob):
    while ob.parent is not None:
        ob = ob.parent
    return ob


class BinWriter:
    """按 4 字节对齐追加 typed array，记录每段的字节偏移。"""

    def __init__(self):
        self.chunks = []
        self.size = 0

    def add_f32(self, values):
        raw = struct.pack("<%df" % len(values), *values)
        return self._push(raw)

    def add_u32(self, values):
        raw = struct.pack("<%dI" % len(values), *values)
        return self._push(raw)

    def add_u16(self, values):
        raw = struct.pack("<%dH" % len(values), *values)
        return self._push(raw)

    def add_u8(self, values):
        raw = struct.pack("<%dB" % len(values), *values)
        return self._push(raw)

    def _push(self, raw):
        off = self.size
        pad = (-len(raw)) % 4
        raw += b"\0" * pad
        self.chunks.append(raw)
        self.size += len(raw)
        return off

    def flush(self, path):
        with open(path, "wb") as f:
            for c in self.chunks:
                f.write(c)
        return self.size


def mesh_tris(obj, extra_role=None):
    """对象 -> (世界坐标顶点列表, 三角索引, 每顶点 role)。已应用 matrix_world。"""
    me = obj.data
    mw = obj.matrix_world
    verts = [to_three(mw @ v.co) for v in me.vertices]
    role = SURFACE_ID.get(obj.get("surface", "主体"), 0)
    idx = []
    roles = [role] * len(verts)
    for p in me.polygons:
        vs = list(p.vertices)
        for k in range(1, len(vs) - 1):
            idx += [vs[0], vs[k], vs[k + 1]]
    return verts, idx, roles


class Builder:
    """累积若干 mesh 段，最后一次性写成一个 primitive。"""

    def __init__(self, name, role, dynamic=False):
        self.name = name
        self.role = role
        self.verts = []
        self.idx = []
        self.roles = []
        self.dynamic = dynamic

    def add(self, verts, idx, roles):
        base = len(self.verts)
        self.verts += verts
        self.roles += roles
        self.idx += [base + i for i in idx]

    def add_obj(self, obj):
        v, i, r = mesh_tris(obj)
        self.add(v, i, r)

    def empty(self):
        return not self.idx

    def emit(self, binw, primitives):
        if self.empty():
            return
        v = [c for p in self.verts for c in p]
        primitives.append(dict(
            name=self.name, role=self.role, count=len(self.verts),
            position=binw.add_f32(v),
            roleOffset=binw.add_u8(self.roles),
            index=binw.add_u32(self.idx), indexCount=len(self.idx),
        ))


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    scene = bpy.context.scene

    primitives = []
    binw = BinWriter()
    stats = {}

    # ---- 1. 基底：郊野、城市地面、街区、河、驳岸、道路、桥、广场 ----------------
    base_root = bpy.data.objects.get("世界")
    assert base_root is not None, "找不到基底 Prefab 根 Empty「世界」"
    base_objs = [base_root] + list(base_root.children_recursive)

    land = Builder("郊野", "land")
    for n in ("郊野地面_西", "郊野地面_东"):
        ob = bpy.data.objects.get(n)
        if ob:
            land.add_obj(ob)
    land.emit(binw, primitives)

    base_struct = Builder("基底结构", "shell")
    for ob in base_objs:
        if ob.type != "MESH":
            continue
        if ob.name.startswith("郊野地面") or ob.name == "河面":
            continue
        base_struct.add_obj(ob)
    base_struct.emit(binw, primitives)

    water = Builder("河面", "water")
    w = bpy.data.objects.get("河面")
    if w:
        water.add_obj(w)
    water.emit(binw, primitives)

    urban = Builder("城市地面", "urban")
    padv = Builder("街区pad", "pad")
    c = bpy.data.collections.get("地面")
    if c:
        for ob in c.objects:
            urban.add_obj(ob)
    c = bpy.data.collections.get("街区")
    if c:
        for ob in c.objects:
            padv.add_obj(ob)
    urban.emit(binw, primitives)
    padv.emit(binw, primitives)

    # 干道：曲线 -> 带状网格（世界视角要的是"路在雾里发亮的一条"，不是完整路面）
    roads = Builder("干道", "road")
    for ob in base_objs:
        if ob.type != 'CURVE' or not ob.name.startswith("干道"):
            continue
        hw = float(ob["width"]) * 0.5 if "width" in ob else 4.25
        pts = [(ob.matrix_world @ p.co.to_3d()).xy for p in ob.data.splines[0].points]
        v, i = [], []
        for k in range(len(pts) - 1):
            a, b = Vector(pts[k]), Vector(pts[k + 1])
            d = (b - a)
            if d.length < 1e-6:
                continue
            n = Vector((-d.y, d.x)).normalized() * hw
            o = len(v)
            v += [to_three((a.x + n.x, a.y + n.y, 0.66)), to_three((a.x - n.x, a.y - n.y, 0.66)),
                  to_three((b.x + n.x, b.y + n.y, 0.66)), to_three((b.x - n.x, b.y - n.y, 0.66))]
            i += [o, o + 1, o + 2, o + 2, o + 1, o + 3]
        roads.add(v, i, [0] * len(v))
    roads.emit(binw, primitives)

    # ---- 2. 地点外壳（剔除 interior） ----------------------------------------
    shell = Builder("地点外壳", "shell")
    place_names, place_of_vert = [], []
    n_kept = n_dropped = 0
    for ob in scene.objects:
        if ob.parent is not None or ob.type != 'EMPTY':
            continue
        if ob.name in ("世界",):
            continue
        subs = [o for o in [ob] + list(ob.children_recursive) if o.type == 'MESH']
        if not subs:
            continue
        place_names.append(ob.name)
        pid = len(place_names) - 1
        for m in subs:
            if m.get("layer") == "interior" or m.name.startswith(("描线_", "内部_")):
                n_dropped += 1
                continue
            before = len(shell.verts)
            shell.add_obj(m)
            n_kept += 1
            place_of_vert += [pid] * (len(shell.verts) - before)
    stats["shell_meshes"] = dict(kept=n_kept, dropped=n_dropped)
    if shell.idx:
        v = [c for p in shell.verts for c in p]
        primitives.append(dict(
            name=shell.name, role=shell.role, count=len(shell.verts),
            position=binw.add_f32(v),
            roleOffset=binw.add_u8(shell.roles),
            placeOffset=binw.add_u16(place_of_vert),
            index=binw.add_u32(shell.idx), indexCount=len(shell.idx),
        ))

    # ---- 3. 填充建筑：实例表 --------------------------------------------------
    c = bpy.data.collections.get("填充建筑")
    buildings = []
    for ob in (c.objects if c else []):
        proto = ob.data.name.split(".")[0].replace("PROTO_", "")
        if proto not in BUILDING_PROTOS:
            continue
        name = ""
        if ob.data.materials:
            name = ob.data.materials[0].name
        district = name.replace("M_建筑_", "") if name.startswith("M_建筑_") else "oldtown"
        p = ob.matrix_world.translation
        buildings.append(dict(
            x=round(p.x, 2), y=round(p.y, 2),
            a=round(ob.rotation_euler.z, 4),
            w=round(ob.scale.x, 2), d=round(ob.scale.y, 2), h=round(ob.scale.z, 2),
            p=BUILDING_PROTOS.index(proto), z=district,
        ))
    stats["buildings"] = len(buildings)

    # ---- 4. 屋顶件 ------------------------------------------------------------
    c = bpy.data.collections.get("屋顶")
    roofs = []
    for ob in (c.objects if c else []):
        proto = ob.data.name.split(".")[0].replace("PROTO_roof_", "")
        if proto not in ROOF_PROTOS:
            continue
        p = ob.matrix_world.translation
        roofs.append(dict(x=round(p.x, 2), y=round(p.y, 2), a=round(ob.rotation_euler.z, 4),
                          w=round(ob.scale.x, 2), d=round(ob.scale.y, 2), h=round(ob.scale.z, 2),
                          p=ROOF_PROTOS.index(proto)))
    stats["roofs"] = len(roofs)

    c = bpy.data.collections.get("绿化")
    trees = []
    for ob in (c.objects if c else []):
        p = ob.matrix_world.translation
        trees.append(dict(x=round(p.x, 2), y=round(p.y, 2), a=round(ob.rotation_euler.z, 4),
                          w=round(ob.scale.x, 2), d=round(ob.scale.y, 2), h=round(ob.scale.z, 2)))
    stats["trees"] = len(trees)

    # ---- 5. 窗光：合并成一个 mesh，顶点带 tier ---------------------------------
    wins = Builder("窗光", "window")
    tier_of_vert = []
    for key in WINDOW_TIERS:
        ob = bpy.data.objects.get("窗光_" + key)
        if not ob:
            continue
        v, i, _ = mesh_tris(ob)
        before = len(wins.verts)
        wins.add(v, i, [0] * len(v))
        tier_of_vert += [WINDOW_TIERS.index(key)] * (len(wins.verts) - before)
    if wins.idx:
        v = [c for p in wins.verts for c in p]
        primitives.append(dict(
            name=wins.name, role=wins.role, count=len(wins.verts),
            position=binw.add_f32(v),
            roleOffset=binw.add_u8([0] * len(wins.verts)),
            tierOffset=binw.add_u8(tier_of_vert),
            index=binw.add_u32(wins.idx), indexCount=len(wins.idx),
        ))
    stats["windows"] = len(wins.verts) // 4

    # ---- 6. 地点自带的灯（世界视角里就是几个暖点） -----------------------------
    lights = []
    for ob in scene.objects:
        if ob.type != 'LIGHT':
            continue
        prof = ob.get("visual_profile")
        if not prof:
            continue
        p = ob.matrix_world.translation
        lights.append(dict(x=round(p.x, 2), y=round(p.y, 2), z=round(p.z, 2),
                           e=round(float(ob.data.energy), 2), t=ob.data.type,
                           size=round(float(getattr(ob.data, "size", 0.0)), 2),
                           to=prof))
    stats["place_lights"] = len(lights)

    # ---- 7. 相机（世界视角的出厂机位，用来对齐构图） ---------------------------
    cams = []
    for ob in scene.objects:
        if ob.type != 'CAMERA':
            continue
        q = ob.matrix_world.to_quaternion()
        fwd = q @ Vector((0, 0, -1))
        up = q @ Vector((0, 1, 0))
        p = ob.matrix_world.translation
        cams.append(dict(
            name=ob.name, pos=[round(v, 2) for v in to_three(p)],
            forward=[round(v, 4) for v in to_three(fwd)],
            up=[round(v, 4) for v in to_three(up)],
            lens=round(float(ob.data.lens), 2),
            drag=ob.get("drag", "static"),
            pan=list(ob["pan_bounds"]) if ob.get("pan_bounds") else None,
        ))
    stats["cameras"] = len(cams)

    # ---- 8. 落盘 --------------------------------------------------------------
    size = binw.flush(os.path.join(OUT_DIR, "city.bin"))
    xs = [b["x"] for b in buildings] or [0]
    ys = [b["y"] for b in buildings] or [0]
    manifest = dict(
        version=1,
        source="city-box/build/city/city_build.blend",
        axis="blender(x,y,z) -> three(x,z,-y)",
        binBytes=size,
        surfaces=SURFACE_ROLES,
        buildingProtos=BUILDING_PROTOS,
        roofProtos=ROOF_PROTOS,
        windowTiers=WINDOW_TIERS,
        districtOrder=sorted({b["z"] for b in buildings}),
        placeNames=place_names,
        primitives=primitives,
        buildings=buildings,
        roofs=roofs,
        trees=trees,
        lights=lights,
        cameras=cams,
        bounds=dict(x=[min(xs), max(xs)], y=[min(ys), max(ys)]),
        stats=stats,
    )
    with open(os.path.join(OUT_DIR, "city.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, separators=(",", ":"))

    print("EXPORT| bin=%.1fMB json=%.1fMB" % (size / 1e6, os.path.getsize(os.path.join(OUT_DIR, "city.json")) / 1e6))
    for p in primitives:
        print("EXPORT| prim %-10s role=%-7s verts=%-7d tris=%d" % (p["name"], p["role"], p["count"], p["indexCount"] // 3))
    print("EXPORT| " + json.dumps(stats, ensure_ascii=False))


if __name__ == "__main__":
    main()
