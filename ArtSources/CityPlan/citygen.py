"""从 town-plan.json 推导灰盒城市的几何。

纯 Python，不依赖 bpy 或 PIL —— 平面预览（render_plan.py）与 Blender 生成器
（build_graybox.py）都导入本模块，保证两边看到的是同一座城。

生成方式是**沿街面排布**，不是在网格上撒方块：规划只描述河、干道和街区规则，
次级路网按街区纹理自动加密，建筑再沿着所有街道的两侧贴着街沿长出来，
街区中心自然留出内院。这是真实城市肌理的来源，也是灰盒能不能读作"城"的关键。

同一份 plan + seed 永远得到同一座城。
"""

from __future__ import annotations

import json
import math
import os

PLAN_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "town-plan.json")


# ── 确定性伪随机 ────────────────────────────────────────────────
# 不用 random 模块：需要「同一条街的同一个位置永远长出同一栋楼」，与迭代顺序无关。

def _hash01(*keys) -> float:
    h = 2166136261
    for k in keys:
        h ^= (int(k * 1000) + 0x9E3779B9) & 0xFFFFFFFF
        h = (h * 16777619) & 0xFFFFFFFF
        h ^= h >> 13
    return ((h * 2654435761) & 0xFFFFFFFF) / 0xFFFFFFFF


def _lerp(a, b, t):
    return a + (b - a) * t


def _rng(lo, hi, *keys):
    return _lerp(lo, hi, _hash01(*keys))


# ── 几何 ───────────────────────────────────────────────────────

def _seg_closest(px, pz, ax, az, bx, bz):
    dx, dz = bx - ax, bz - az
    d2 = dx * dx + dz * dz
    t = 0.0 if d2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (pz - az) * dz) / d2))
    cx, cz = ax + dx * t, az + dz * t
    return t, math.hypot(px - cx, pz - cz)


def box_corners(cx, cz, sx, sz, rot_deg):
    a = math.radians(rot_deg)
    ca, sa = math.cos(a), math.sin(a)
    hx, hz = sx * 0.5, sz * 0.5
    return [(cx + dx * ca - dz * sa, cz + dx * sa + dz * ca)
            for dx, dz in ((-hx, -hz), (hx, -hz), (hx, hz), (-hx, hz))]


def _aabb(corners):
    xs = [c[0] for c in corners]
    zs = [c[1] for c in corners]
    return (min(xs), min(zs), max(xs), max(zs))


def _aabb_overlap(a, b, pad=0.0):
    return not (a[2] + pad < b[0] or b[2] + pad < a[0]
                or a[3] + pad < b[1] or b[3] + pad < a[1])


def river_depth(plan, px, pz):
    """到河中心线的距离减去该处半宽。<=0 表示在水里。"""
    cl = plan["river"]["centerline"]
    best = 1e9
    for i in range(len(cl) - 1):
        ax, az, aw = cl[i]
        bx, bz, bw = cl[i + 1]
        t, dist = _seg_closest(px, pz, ax, az, bx, bz)
        best = min(best, dist - _lerp(aw, bw, t))
    return best


def river_polygon(plan, samples=24):
    """河面轮廓（左右岸各一串点），供预览与 Unity 摆 water plane 参考。

    法线取中心线的中央差分而非单段方向——按段算会让每个弯道处的左右岸互相穿插。
    """
    cl = plan["river"]["centerline"]
    pts = []
    for i in range(len(cl) - 1):
        ax, az, aw = cl[i]
        bx, bz, bw = cl[i + 1]
        last = samples if i == len(cl) - 2 else samples - 1
        for s in range(last + 1):
            t = s / samples
            pts.append((_lerp(ax, bx, t), _lerp(az, bz, t), _lerp(aw, bw, t)))
    left, right = [], []
    for i, (x, z, w) in enumerate(pts):
        px, pz, _ = pts[max(0, i - 1)]
        qx, qz, _ = pts[min(len(pts) - 1, i + 1)]
        dx, dz = qx - px, qz - pz
        n = math.hypot(dx, dz) or 1.0
        ox, oz = -dz / n * w, dx / n * w
        left.append((x + ox, z + oz))
        right.append((x - ox, z - oz))
    return left + right[::-1]


# ── 路网索引 ───────────────────────────────────────────────────
# 加密后的路网有几百条线段，逐条求距离太慢；按格子分桶只查邻近的。

class RoadIndex:
    CELL = 8.0

    def __init__(self, roads):
        self.buckets = {}
        for r in roads:
            half = r["width"] * 0.5
            pts = r["points"]
            for i in range(len(pts) - 1):
                seg = (pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], half)
                x0 = min(seg[0], seg[2]) - half
                x1 = max(seg[0], seg[2]) + half
                z0 = min(seg[1], seg[3]) - half
                z1 = max(seg[1], seg[3]) + half
                for cx in range(int(x0 // self.CELL), int(x1 // self.CELL) + 1):
                    for cz in range(int(z0 // self.CELL), int(z1 // self.CELL) + 1):
                        self.buckets.setdefault((cx, cz), []).append(seg)

    def distance(self, px, pz):
        """到最近路面边缘的距离。<=0 表示压在路上。"""
        key = (int(px // self.CELL), int(pz // self.CELL))
        best = 1e9
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                for ax, az, bx, bz, half in self.buckets.get((key[0] + dx, key[1] + dz), ()):
                    _, dist = _seg_closest(px, pz, ax, az, bx, bz)
                    best = min(best, dist - half)
        return best


class BoxIndex:
    """已放置建筑的占位索引，用于相邻拒绝。"""
    CELL = 8.0

    def __init__(self):
        self.buckets = {}

    def _keys(self, ab, pad):
        for cx in range(int((ab[0] - pad) // self.CELL), int((ab[2] + pad) // self.CELL) + 1):
            for cz in range(int((ab[1] - pad) // self.CELL), int((ab[3] + pad) // self.CELL) + 1):
                yield (cx, cz)

    def hits(self, ab, pad=0.0):
        for k in self._keys(ab, pad):
            for other in self.buckets.get(k, ()):
                if _aabb_overlap(ab, other, pad):
                    return True
        return False

    def add(self, ab):
        for k in self._keys(ab, 0.0):
            self.buckets.setdefault(k, []).append(ab)


# ── 规划读取与派生 ─────────────────────────────────────────────

def load_plan(path=PLAN_PATH):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def district_at(plan, px, pz):
    for d in plan["districts"]:
        rx0, rz0, rx1, rz1 = d["rect"]
        if rx0 <= px <= rx1 and rz0 <= pz <= rz1:
            return d
    return None


def density_falloff(plan, px, pz):
    """离城心越远越稀。给整座城一个连续的密度梯度，而不是硬边界。"""
    f = plan["falloff"]
    cx, cz = plan["center"]
    d = math.hypot(px - cx, pz - cz)
    if d <= f["inner"]:
        return 1.0
    t = min(1.0, (d - f["inner"]) / max(1e-6, f["outer"] - f["inner"]))
    return _lerp(1.0, f["floor"], t * t)


def _on_land(plan, px, pz, margin):
    bx0, bx1 = plan["bounds"]["x"]
    bz0, bz1 = plan["bounds"]["z"]
    if not (bx0 <= px <= bx1 and bz0 <= pz <= bz1):
        return False
    return river_depth(plan, px, pz) >= margin


def minor_streets(plan):
    """按街区纹理自动加密的次级路网。

    只在规划里写干道；街区内部的小街由街区的 grain / block 尺寸推出来，
    这样改一个街区的肌理不必手写十几条巷子。
    """
    arterials = RoadIndex(plan["roads"] + [
        {"width": b["width"], "points": [b["from"], b["to"]]} for b in plan["bridges"]])
    quay = plan["river"].get("quay_margin", 1.0)
    out = []

    for di, d in enumerate(plan["districts"]):
        block = d.get("block")
        if not block:
            continue
        rx0, rz0, rx1, rz1 = d["rect"]
        mx, mz = (rx0 + rx1) * 0.5, (rz0 + rz1) * 0.5
        a = math.radians(d["grain"])
        ca, sa = math.cos(a), math.sin(a)
        width = d.get("minor_width", 1.4)
        span = math.hypot(rx1 - rx0, rz1 - rz0) * 0.5 + block

        def to_world(u, v):
            return (mx + u * ca - v * sa, mz + u * sa + v * ca)

        for axis in (0, 1):
            k = -int(span / block) - 1
            while k * block <= span:
                offset = k * block + _rng(-0.28, 0.28, di, axis, k) * block
                k += 1
                # 沿这条线走一遍，把落在陆地且不与干道重合的连续段切出来
                run, step = [], 1.0
                t = -span
                while t <= span:
                    u, v = (t, offset) if axis == 0 else (offset, t)
                    px, pz = to_world(u, v)
                    # 次级路网也跟着密度梯度退场：城外只剩干道和零星房子，
                    # 否则整张图会被均匀网格铺满，读起来是无边大城而不是小港城。
                    ok = (rx0 <= px <= rx1 and rz0 <= pz <= rz1
                          and _on_land(plan, px, pz, quay)
                          and density_falloff(plan, px, pz) > d.get("minor_cutoff", 0.35)
                          and arterials.distance(px, pz) > width * 0.5 + 1.0)
                    if ok:
                        run.append((px, pz))
                    else:
                        if len(run) * step >= block * 0.9:
                            out.append({"name": f"{d['name']}·巷", "width": width,
                                        "minor": True, "points": [run[0], run[-1]]})
                        run = []
                    t += step
                if len(run) * step >= block * 0.9:
                    out.append({"name": f"{d['name']}·巷", "width": width,
                                "minor": True, "points": [run[0], run[-1]]})
    return out


def all_roads(plan):
    return plan["roads"] + minor_streets(plan)


# ── 沿街建筑生成 ───────────────────────────────────────────────

def frontage_boxes(plan, roads=None, lots=None):
    """沿所有街道两侧排布建筑。街区中心自然留空，成为内院。"""
    if roads is None:
        roads = all_roads(plan)
    index = RoadIndex(roads)
    placed = BoxIndex()
    lot_aabbs = [l["aabb"] for l in (lots or [])]
    quay = plan["river"].get("quay_margin", 1.0)
    seed = plan["seed"]
    boxes = []

    for ri, r in enumerate(roads):
        pts = r["points"]
        half = r["width"] * 0.5
        minor = r.get("minor", False)

        # 把折线重采样成等距点，便于按弧长行走
        samples = []
        acc = 0.0
        for i in range(len(pts) - 1):
            ax, az = pts[i]
            bx, bz = pts[i + 1]
            seglen = math.hypot(bx - ax, bz - az)
            n = max(1, int(seglen / 0.5))
            for s in range(n):
                t = s / n
                samples.append((acc + seglen * t, _lerp(ax, bx, t), _lerp(az, bz, t)))
            acc += seglen
        if len(samples) < 4:
            continue
        total = samples[-1][0]

        def at(dist):
            lo, hi = 0, len(samples) - 1
            while lo < hi:
                mid = (lo + hi) // 2
                if samples[mid][0] < dist:
                    lo = mid + 1
                else:
                    hi = mid
            i = max(1, min(len(samples) - 2, lo))
            _, px, pz = samples[i]
            _, ax, az = samples[i - 1]
            _, bx, bz = samples[i + 1]
            dx, dz = bx - ax, bz - az
            n = math.hypot(dx, dz) or 1.0
            return px, pz, dx / n, dz / n

        for side in (1, -1):
            pos = _rng(0.0, 4.0, seed, ri, side)
            while pos < total - 1.0:
                px, pz, tx, tz = at(pos)
                d = district_at(plan, px, pz)
                if d is None:
                    pos += 4.0
                    continue

                # 尺寸跟着街区走：进深最多吃掉半个街区，两侧的楼才背靠背贴上而不是撞穿。
                block = d.get("block", 14.0)
                scale = d.get("grain_size", 1.0)
                w = _rng(0.20, 0.46, seed, ri, side, pos) * block * scale
                depth = _rng(0.22, 0.40, seed, ri, side, pos, 7) * block * scale
                if minor:
                    w *= 0.82
                    depth *= 0.78

                dens = d["density"] * density_falloff(plan, px, pz)
                if _hash01(seed, ri, side, pos, 3) > dens:
                    pos += w + _rng(1.5, 5.0, seed, ri, side, pos, 9)
                    continue

                setback = _rng(0.2, 1.0, seed, ri, side, pos, 5)
                nx, nz = -tz * side, tx * side
                off = half + setback + depth * 0.5
                cx, cz = px + nx * off, pz + nz * off
                rot = math.degrees(math.atan2(tz, tx))

                # 沿街走一步就往前挪一栋的宽度；偶尔留个缺口，避免整条街像一堵墙
                advance = w + (_rng(0.1, 2.6, seed, ri, side, pos, 13)
                               if _hash01(seed, ri, side, pos, 17) < 0.25 else 0.25)

                corners = box_corners(cx, cz, w, depth, rot)
                ab = _aabb(corners)
                if not all(_on_land(plan, x, z, quay) for x, z in corners):
                    pos += advance
                    continue
                if any(index.distance(x, z) < 0.25 for x, z in corners):
                    pos += advance
                    continue
                if placed.hits(ab, pad=0.3):
                    pos += advance
                    continue
                if any(_aabb_overlap(ab, la, 1.0) for la in lot_aabbs):
                    pos += advance
                    continue

                h0, h1 = d["height"]
                h = _lerp(h0, h1, _hash01(seed, ri, side, pos, 23) ** 1.4)
                h *= _lerp(0.72, 1.12, density_falloff(plan, px, pz))

                placed.add(ab)
                boxes.append({
                    "district": d["name"], "pos": (cx, cz), "size": (w, depth),
                    "rot": rot, "height": round(h, 2), "corners": corners, "aabb": ab,
                })
                pos += advance
    return boxes


def lot_boxes(plan):
    """hero 地块。骨架阶段不使用；等灰盒确认后才把真身吸附上去。"""
    out = []
    for lot in plan.get("lots", []):
        cx, cz = lot["pos"]
        sx, sz = lot["size"]
        corners = box_corners(cx, cz, sx, sz, lot["rot"])
        out.append({
            "id": lot["id"], "model": lot.get("model"), "reserved": lot.get("reserved", False),
            "pos": (cx, cz), "size": (sx, sz), "rot": lot["rot"],
            "height": lot["height"], "corners": corners, "aabb": _aabb(corners),
        })
    return out


def summarize(plan, roads, boxes):
    per = {}
    for b in boxes:
        per[b["district"]] = per.get(b["district"], 0) + 1
    return {
        "arterials": len(plan["roads"]),
        "minor_streets": len(roads) - len(plan["roads"]),
        "buildings": len(boxes),
        "per_district": per,
        "max_height": round(max(b["height"] for b in boxes), 1) if boxes else 0,
    }


if __name__ == "__main__":
    p = load_plan()
    rs = all_roads(p)
    bs = frontage_boxes(p, rs)
    print(json.dumps(summarize(p, rs, bs), ensure_ascii=False, indent=1))
