"""逆光 BACKLIGHT · 城市数据烘焙（一次性；运行时只读冻结在 public/backlight/ 的快照）

只读 `city-box/build/city/city_build.blend`（构建产物），压成 Three.js 侧直接可吃的
`public/backlight/city.json` + `city.bin`。**不修改任何正式产物。**

导出原则（和黑水的快照不同，本实验自己烘一份）
------------------------------------------------
1. **拆顶点 + 平面法线**。本实验的明暗是"硬终止线的卡通分带"，面与面之间必须断开；
   合并网格按三角面拆成非索引顶点，法线按面算，存 int8 归一化（每顶点 4 字节）。
   描线仍然一根不导——轮廓由逆光的明暗关系本身给出。
2. **停在城外的片场布景剔除**。三坐标 z < -800 的地点是搬出去的内景/过场布景
   （"首演之夜""租屋""修理棚"那一批），在城市视角里只是地平线上的一团杂色，直接丢。
3. **主角塔占位**。市中心 98 m 的填充方块从实例表里剔掉，位子留给本实验自建的
   Art Deco 主角塔（见 src/backlight/hero.js）。
4. 连续形体（地面/河/驳岸/桥/广场/地点外壳/干道）合成少量大网格；重复形体
   （填充建筑/屋顶件/树）导实例表，运行时用 InstancedMesh。
5. 坐标：Blender 右手 Z-up (x, y, z) -> Three.js 右手 Y-up (x, z, -y)。

用法
----
    cd experiments/noir-city/noir-engraving-lab
    npm run export:backlight
"""

import json
import os
import struct

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.normpath(os.path.join(HERE, "..", "public", "backlight"))

# 地点外壳的表面角色表（= world_style.json 的 surfaces 键），编号写进顶点属性。
SURFACE_ROLES = [
    "主体", "地面", "抛光地面", "象牙饰面", "亮金装饰", "暗金饰面",
    "金色细饰", "暖白发光表面", "冷白发光表面", "水面短线",
]
SURFACE_ID = {name: i for i, name in enumerate(SURFACE_ROLES)}

WINDOW_TIERS = ["white", "grey", "blue"]
BUILDING_PROTOS = ["box", "roofbox", "pitch", "setback"]
ROOF_PROTOS = ["box", "stack", "ridge"]

DROP_PLACE_BELOW = -800.0          # three z：城外布景
HERO_SITE = (-110.4, 52.8, 98.0)   # blender x, y, h：主角塔场址
HERO_SITE_TOL = 2.0


def to_three(v):
    """Blender (x, y, z) -> Three.js (x, z, -y)。真旋转（det=+1），绕序不变。"""
    return (v[0], v[2], -v[1])


def as_normal(n):
    """三分量单位法线 -> int8x4（按无符号字节写盘，读侧用 Int8Array 解）；w 留 127。"""
    def b(v):
        return int(round(max(-1.0, min(1.0, v)) * 127)) & 0xFF
    return (b(n[0]), b(n[1]), b(n[2]), 127)


class BinWriter:
    """按 4 字节对齐追加 typed array，记录每段的字节偏移。"""

    def __init__(self):
        self.chunks = []
        self.size = 0

    def add_f32(self, values):
        return self._push(struct.pack("<%df" % len(values), *values))

    def add_u32(self, values):
        return self._push(struct.pack("<%dI" % len(values), *values))

    def add_u16(self, values):
        return self._push(struct.pack("<%dH" % len(values), *values))

    def add_u8(self, values):
        return self._push(struct.pack("<%dB" % len(values), *values))

    def _push(self, raw):
        off = self.size
        raw += b"\0" * ((-len(raw)) % 4)
        self.chunks.append(raw)
        self.size += len(raw)
        return off

    def flush(self, path):
        with open(path, "wb") as f:
            for c in self.chunks:
                f.write(c)
        return self.size


class FlatBuilder:
    """累积非索引三角形：顶点位置 + 面法线 + 每顶点 role。"""

    def __init__(self, name, role):
        self.name = name
        self.role = role
        self.pos = []
        self.nrm = []
        self.srf = []

    def add_mesh(self, obj, place_id=None):
        me = obj.data
        mw = obj.matrix_world
        m3 = mw.to_3x3()
        role = SURFACE_ID.get(obj.get("surface", "主体"), 0)
        wv = [mw @ v.co for v in me.vertices]
        nverts = len(self.pos)
        for poly in me.polygons:
            vs = list(poly.vertices)
            n = to_three((m3 @ poly.normal).normalized())
            na = as_normal(n)
            for k in range(1, len(vs) - 1):
                for i in (vs[0], vs[k], vs[k + 1]):
                    self.pos.extend(to_three(wv[i]))
                    self.nrm.extend(na)
                    self.srf.append(role)
        return len(self.pos) // 3 - nverts, place_id

    def add_raw(self, tris):
        """tris: [(p0, p1, p2)]，法线按三角形叉积。"""
        for a, b, c in tris:
            n = Vector(b) - Vector(a)
            n = n.cross(Vector(c) - Vector(a))
            na = as_normal(to_three(n.normalized()) if n.length > 1e-9 else (0, 1, 0))
            for p in (a, b, c):
                self.pos.extend(to_three(p))
                self.nrm.extend(na)
                self.srf.append(0)

    @property
    def count(self):
        return len(self.pos) // 3

    def empty(self):
        return self.count == 0

    def emit(self, binw, primitives, extra=None):
        if self.empty():
            return
        prim = dict(
            name=self.name, role=self.role, count=self.count,
            position=binw.add_f32(self.pos),
            normal=binw.add_u8(self.nrm),
            roleOffset=binw.add_u8(self.srf),
        )
        if extra:
            prim.update(extra)
        primitives.append(prim)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    scene = bpy.context.scene
    primitives = []
    binw = BinWriter()
    stats = {}
    dropped_places = []

    # ---- 1. 基底：郊野 / 城市地面 / 街区 / 河 / 驳岸 / 桥 / 广场 ----------------
    base_root = bpy.data.objects.get("世界")
    assert base_root is not None, "找不到基底 Prefab 根 Empty「世界」"
    base_objs = [base_root] + list(base_root.children_recursive)

    land = FlatBuilder("郊野", "land")
    for n in ("郊野地面_西", "郊野地面_东"):
        ob = bpy.data.objects.get(n)
        if ob:
            land.add_mesh(ob)
    land.emit(binw, primitives)

    structb = FlatBuilder("基底结构", "shell")
    for ob in base_objs:
        if ob.type != "MESH":
            continue
        if ob.name.startswith("郊野地面") or ob.name == "河面":
            continue
        structb.add_mesh(ob)
    structb.emit(binw, primitives)

    water = FlatBuilder("河面", "water")
    w = bpy.data.objects.get("河面")
    if w:
        water.add_mesh(w)
    water.emit(binw, primitives)

    urban = FlatBuilder("城市地面", "urban")
    c = bpy.data.collections.get("地面")
    for ob in (c.objects if c else []):
        urban.add_mesh(ob)
    urban.emit(binw, primitives)

    pad = FlatBuilder("街区pad", "pad")
    c = bpy.data.collections.get("街区")
    for ob in (c.objects if c else []):
        pad.add_mesh(ob)
    pad.emit(binw, primitives)

    # 干道：曲线 -> 带状网格（世界视角要的是"雾里发亮的一条"，不是完整路面）
    roads = FlatBuilder("干道", "road")
    for ob in base_objs:
        if ob.type != "CURVE" or not ob.name.startswith("干道"):
            continue
        hw = float(ob["width"]) * 0.5 if "width" in ob else 4.25
        pts = [(ob.matrix_world @ p.co.to_3d()).xy for p in ob.data.splines[0].points]
        for k in range(len(pts) - 1):
            a, b = Vector(pts[k]), Vector(pts[k + 1])
            d = b - a
            if d.length < 1e-6:
                continue
            n = Vector((-d.y, d.x)).normalized() * hw
            roads.add_raw([
                ((a.x + n.x, a.y + n.y, 0.66), (a.x - n.x, a.y - n.y, 0.66), (b.x + n.x, b.y + n.y, 0.66)),
                ((a.x - n.x, a.y - n.y, 0.66), (b.x - n.x, b.y - n.y, 0.66), (b.x + n.x, b.y + n.y, 0.66)),
            ])
    roads.emit(binw, primitives)

    # ---- 2. 地点外壳（剔除 interior，剔除城外布景） -----------------------------
    shell = FlatBuilder("地点外壳", "shell")
    place_names = []
    place_bounds = []
    place_of_vert = []
    n_kept = n_dropped = 0
    for ob in scene.objects:
        if ob.parent is not None or ob.type != "EMPTY" or ob.name == "世界":
            continue
        subs = [o for o in [ob] + list(ob.children_recursive) if o.type == "MESH"]
        if not subs:
            continue
        # 地点包围盒（world，three 坐标）需要先量
        lo = [1e18] * 3
        hi = [-1e18] * 3
        for m in subs:
            if m.type != "MESH":
                continue
            mw = m.matrix_world
            for v in m.data.vertices:
                p = to_three(mw @ v.co)
                for i in range(3):
                    lo[i] = min(lo[i], p[i])
                    hi[i] = max(hi[i], p[i])
        center_z = (lo[2] + hi[2]) * 0.5
        if center_z < DROP_PLACE_BELOW:
            dropped_places.append(ob.name)
            continue
        pid = len(place_names)
        place_names.append(ob.name)
        place_bounds.append(dict(name=ob.name, min=[round(v, 2) for v in lo], max=[round(v, 2) for v in hi]))
        for m in subs:
            if m.get("layer") == "interior" or m.name.startswith(("描线_", "内部_")):
                n_dropped += 1
                continue
            before = shell.count
            shell.add_mesh(m)
            place_of_vert += [pid] * (shell.count - before)
            n_kept += 1
    stats["shell_meshes"] = dict(kept=n_kept, dropped=n_dropped)
    stats["dropped_places"] = dropped_places
    shell.emit(binw, primitives, extra=dict(placeOffset=binw.add_u16(place_of_vert)))

    # ---- 3. 填充建筑（主角塔场址上的那栋剔掉） ---------------------------------
    c = bpy.data.collections.get("填充建筑")
    buildings = []
    n_hero_removed = 0
    for ob in (c.objects if c else []):
        proto = ob.data.name.split(".")[0].replace("PROTO_", "")
        if proto not in BUILDING_PROTOS:
            continue
        p = ob.matrix_world.translation
        if (abs(p.x - HERO_SITE[0]) < HERO_SITE_TOL
                and abs(p.y - HERO_SITE[1]) < HERO_SITE_TOL
                and abs(ob.scale.z - HERO_SITE[2]) < 1.0):
            n_hero_removed += 1
            continue
        name = ob.data.materials[0].name if ob.data.materials else ""
        district = name.replace("M_建筑_", "") if name.startswith("M_建筑_") else "oldtown"
        buildings.append(dict(
            x=round(p.x, 2), y=round(p.y, 2), base=round(p.z, 2),
            a=round(ob.rotation_euler.z, 4),
            w=round(ob.scale.x, 2), d=round(ob.scale.y, 2), h=round(ob.scale.z, 2),
            p=BUILDING_PROTOS.index(proto), z=district,
        ))
    stats["buildings"] = len(buildings)
    stats["hero_site_cleared"] = n_hero_removed

    # ---- 4. 屋顶件 / 树 ---------------------------------------------------------
    c = bpy.data.collections.get("屋顶")
    roofs = []
    for ob in (c.objects if c else []):
        proto = ob.data.name.split(".")[0].replace("PROTO_roof_", "")
        if proto not in ROOF_PROTOS:
            continue
        p = ob.matrix_world.translation
        roofs.append(dict(x=round(p.x, 2), y=round(p.y, 2), base=round(p.z, 2),
                          a=round(ob.rotation_euler.z, 4),
                          w=round(ob.scale.x, 2), d=round(ob.scale.y, 2), h=round(ob.scale.z, 2),
                          p=ROOF_PROTOS.index(proto)))
    stats["roofs"] = len(roofs)

    c = bpy.data.collections.get("绿化")
    trees = []
    for ob in (c.objects if c else []):
        p = ob.matrix_world.translation
        trees.append(dict(x=round(p.x, 2), y=round(p.y, 2), base=round(p.z, 2),
                          a=round(ob.rotation_euler.z, 4),
                          w=round(ob.scale.x, 2), d=round(ob.scale.y, 2), h=round(ob.scale.z, 2)))
    stats["trees"] = len(trees)

    # ---- 5. 窗光：合并面片，顶点带 tier ----------------------------------------
    wins = FlatBuilder("窗光", "window")
    tier_of_vert = []
    for key in WINDOW_TIERS:
        ob = bpy.data.objects.get("窗光_" + key)
        if not ob:
            continue
        mw = ob.matrix_world
        m3 = mw.to_3x3()
        me = ob.data
        wv = [mw @ v.co for v in me.vertices]
        tier = WINDOW_TIERS.index(key)
        for poly in me.polygons:
            vs = list(poly.vertices)
            n = to_three((m3 @ poly.normal).normalized())
            na = as_normal(n)
            for k in range(1, len(vs) - 1):
                for i in (vs[0], vs[k], vs[k + 1]):
                    wins.pos.extend(to_three(wv[i]))
                    wins.nrm.extend(na)
                    wins.srf.append(tier)
    if not wins.empty():
        primitives.append(dict(
            name=wins.name, role=wins.role, count=wins.count,
            position=binw.add_f32(wins.pos),
            normal=binw.add_u8(wins.nrm),
            tierOffset=binw.add_u8(wins.srf),
        ))
    stats["windows"] = wins.count // 6  # 每个窗四边形 = 2 三角 = 6 顶点

    # ---- 6. 落盘 ----------------------------------------------------------------
    size = binw.flush(os.path.join(OUT_DIR, "city.bin"))
    xs = [b["x"] for b in buildings] or [0]
    ys = [b["y"] for b in buildings] or [0]
    manifest = dict(
        version=2,
        source="city-box/build/city/city_build.blend",
        axis="blender(x,y,z) -> three(x,z,-y)",
        binBytes=size,
        surfaces=SURFACE_ROLES,
        buildingProtos=BUILDING_PROTOS,
        roofProtos=ROOF_PROTOS,
        windowTiers=WINDOW_TIERS,
        districtOrder=sorted({b["z"] for b in buildings}),
        placeNames=place_names,
        placeBounds=place_bounds,
        primitives=primitives,
        buildings=buildings,
        roofs=roofs,
        trees=trees,
        bounds=dict(x=[min(xs), max(xs)], y=[min(ys), max(ys)]),
        stats=stats,
    )
    with open(os.path.join(OUT_DIR, "city.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, separators=(",", ":"))

    print("EXPORT| bin=%.1fMB json=%.1fMB" % (size / 1e6, os.path.getsize(os.path.join(OUT_DIR, "city.json")) / 1e6))
    for p in primitives:
        print("EXPORT| prim %-8s role=%-7s verts=%-8d tris=%d" % (p["name"], p["role"], p["count"], p["count"] // 3))
    print("EXPORT| " + json.dumps(stats, ensure_ascii=False))


if __name__ == "__main__":
    main()
