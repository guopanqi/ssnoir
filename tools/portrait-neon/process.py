#!/usr/bin/env python3
"""霓虹立绘的离线加工：从一张 Portraits/Neon/<名>.png 派生出舞台需要的三样东西。

  <名>_lines.png   去掉点缀色的线稿（点缀管子处挖空），运行时染成任意颜色
  <名>_accent.png  点缀色的蒙版（点缀管子处为白），运行时单独上色——人物的标志色可以随剧情变
  <名>.neon.json   管子的骨架（uv 坐标）+ 点缀色默认值；电流沿骨架游走、按位置爆管都靠它

骨架是一根根"真的管子"：先顶帽滤波减掉溢光（相邻的管子不再粘成一团），Zhang-Suen 细化，
再把骨架当图来整理——去毛刺、合并交叉处碎片、在分叉点按切线连续性把边配对——走成几十根长而
连续的笔画。每根笔画就是一根管子，有头有尾；paths 按空间邻近排成巡回，运行时一路走下去即可。
只依赖 Pillow，一张图几秒钟。

用法：
  python3 tools/portrait-neon/process.py                # 处理 Portraits/Neon 下所有基础图与姿势图
  python3 tools/portrait-neon/process.py 夜莺 尼尔_逼近  # 只处理这几张
派生文件已存在且比源图新时跳过；--force 全部重做。
"""
import json
import math
import os
import sys
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
NEON_DIR = os.path.join(ROOT, "UnityClient", "Assets", "Resources", "Portraits", "Neon")
SKELETON_SIZE = 1024
LINE_THRESHOLD = 110        # 亮度高于它算管子
ACCENT_SATURATION = 0.28    # (max-min)/max 高于它算点缀色
ACCENT_MIN_BRIGHTNESS = 90  # 点缀蒙版只收这么亮以上的像素，排除白线周围的淡色溢光
TOPHAT_SIZE = 9             # 开运算核，略大于管子宽度：减掉后只剩管子本体
TOPHAT_THRESHOLD = 40
MIN_SPUR = 5                # 比这短的悬空毛刺是细化产物，删
JUNCTION_MERGE = 8          # 两个分叉点之间比这短的碎边：合成一个点
MAX_TURN_DEG = 55           # 分叉处两条边夹角小于此（转弯不超过 55°）才算同一根管子
MIN_STROKE = 8              # 比这短的管子不要
SIMPLIFY_EPS = 1.5          # Douglas-Peucker 抽稀容差（像素）


def derived_names(name):
    return [f"{name}_lines.png", f"{name}_accent.png", f"{name}.neon.json"]


def is_source(filename):
    if not filename.endswith(".png"):
        return False
    stem = filename[:-4]
    return not (stem.endswith("_lines") or stem.endswith("_accent") or stem.endswith("_silhouette"))


def split_layers(img):
    """返回 (lines, accent, accent_rgb)。lines 把点缀像素提到与白线同样的亮度——它是给骨架用的全图；
    存盘的线稿由 lines_without() 再把点缀像素挖掉，不然运行时白线垫在蓝管底下，蓝会被冲成灰白。"""
    rgb = img.convert("RGB")
    w, h = rgb.size
    src = rgb.load()
    lines = Image.new("L", (w, h), 0)
    accent = Image.new("L", (w, h), 0)
    lp, ap = lines.load(), accent.load()
    acc_sum = [0, 0, 0]
    acc_n = 0
    for y in range(h):
        for x in range(w):
            r, g, b = src[x, y]
            mx = max(r, g, b)
            if mx == 0:
                continue
            mn = min(r, g, b)
            sat = (mx - mn) / mx
            if sat > ACCENT_SATURATION and mx > ACCENT_MIN_BRIGHTNESS:
                # 白线周围也有一圈淡蓝的溢光，亮度很低；蒙版只收管子本体，不收溢光。
                lp[x, y] = mx
                ap[x, y] = mx
                if mx > LINE_THRESHOLD:
                    acc_sum[0] += r; acc_sum[1] += g; acc_sum[2] += b; acc_n += 1
            else:
                lp[x, y] = int(0.299 * r + 0.587 * g + 0.114 * b)
    accent_rgb = tuple(int(c / acc_n) for c in acc_sum) if acc_n else None
    return lines, accent, accent_rgb


def lines_without(lines, accent):
    """存盘用的线稿：点缀管子的位置挖成黑，运行时那里只由 _accent 层上色。"""
    from PIL import ImageChops
    return ImageChops.subtract(lines, accent)


def binarize(lines):
    """顶帽：减去开运算，把溢光去掉只留管子本体；相邻的管子（格栅、手指、五官）不再粘成一团。"""
    from PIL import ImageChops, ImageFilter
    n = SKELETON_SIZE
    opened = lines.filter(ImageFilter.MinFilter(TOPHAT_SIZE)).filter(ImageFilter.MaxFilter(TOPHAT_SIZE))
    top = ImageChops.subtract(lines, opened).point(lambda v: 255 if v > TOPHAT_THRESHOLD else 0)
    top = top.filter(ImageFilter.MedianFilter(3)).resize((n, n), Image.BILINEAR)
    p = top.load()
    return [[1 if p[x, y] > 127 else 0 for x in range(n)] for y in range(n)]


def thin(grid):
    """Zhang-Suen 细化，原地。"""
    n = len(grid)
    def neighbours(x, y):
        return [grid[y - 1][x], grid[y - 1][x + 1], grid[y][x + 1], grid[y + 1][x + 1],
                grid[y + 1][x], grid[y + 1][x - 1], grid[y][x - 1], grid[y - 1][x - 1]]
    changed = True
    while changed:
        changed = False
        for step in (0, 1):
            remove = []
            for y in range(1, n - 1):
                row = grid[y]
                for x in range(1, n - 1):
                    if row[x] != 1:
                        continue
                    p2, p3, p4, p5, p6, p7, p8, p9 = neighbours(x, y)
                    b = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9
                    if b < 2 or b > 6:
                        continue
                    seq = [p2, p3, p4, p5, p6, p7, p8, p9, p2]
                    a = sum(1 for i in range(8) if seq[i] == 0 and seq[i + 1] == 1)
                    if a != 1:
                        continue
                    if step == 0:
                        if p2 * p4 * p6 != 0 or p4 * p6 * p8 != 0:
                            continue
                    else:
                        if p2 * p4 * p8 != 0 or p2 * p6 * p8 != 0:
                            continue
                    remove.append((x, y))
            for x, y in remove:
                grid[y][x] = 0
            if remove:
                changed = True
    return grid


OFFSETS = [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]


def build_graph(grid):
    """骨架像素 → 边：每条边是两个节点（端点或分叉点）之间的像素链；纯环自成一条。"""
    n = len(grid)
    pixels = {(x, y) for y in range(n) for x in range(n) if grid[y][x]}

    def nbrs(p):
        return [(p[0] + dx, p[1] + dy) for dx, dy in OFFSETS if (p[0] + dx, p[1] + dy) in pixels]

    degree = {p: len(nbrs(p)) for p in pixels}
    nodes = {p for p in pixels if degree[p] != 2}
    edges = []
    used = set()
    for s in nodes:
        for q in nbrs(s):
            if (s, q) in used:
                continue
            chain = [s, q]
            used.add((s, q)); used.add((q, s))
            prev, cur = s, q
            while cur not in nodes:
                nxt = [r for r in nbrs(cur) if r != prev]
                if not nxt:
                    break
                prev, cur = cur, nxt[0]
                chain.append(cur)
                used.add((prev, cur)); used.add((cur, prev))
            edges.append(chain)
    seen = {p for e in edges for p in e}
    for p in pixels:
        if p in seen or degree[p] != 2:
            continue
        chain = [p]
        prev, cur = None, p
        while True:
            nxt = [r for r in nbrs(cur) if r != prev and r not in seen]
            seen.add(cur)
            if not nxt:
                break
            prev, cur = cur, nxt[0]
            chain.append(cur)
        chain.append(p)
        edges.append(chain)
    return edges


def prune_spurs(edges):
    """一端悬空且很短的边是细化在粗线上长出的毛刺，删掉，反复到没有为止。"""
    while True:
        count = {}
        for e in edges:
            for p in (e[0], e[-1]):
                count[p] = count.get(p, 0) + 1
        keep = []
        for e in edges:
            dangling = count[e[0]] == 1 or count[e[-1]] == 1
            if dangling and len(e) < MIN_SPUR and e[0] != e[-1]:
                continue
            keep.append(e)
        if len(keep) == len(edges):
            return edges
        edges = keep


def merge_junctions(edges):
    """两个分叉点之间的极短边（交叉处细化出来的小三角）：把两端合并成一个节点。"""
    count = {}
    for e in edges:
        for p in (e[0], e[-1]):
            count[p] = count.get(p, 0) + 1
    alias = {}

    def find(p):
        while p in alias:
            p = alias[p]
        return p

    keep = []
    for e in edges:
        if len(e) <= JUNCTION_MERGE and count[e[0]] > 1 and count[e[-1]] > 1 and e[0] != e[-1]:
            a, b = find(e[0]), find(e[-1])
            if a != b:
                alias[b] = a
            continue
        keep.append(e)
    out = []
    for e in keep:
        e = list(e)
        e[0], e[-1] = find(e[0]), find(e[-1])
        out.append(e)
    return out


def tangent(edge, at_start, k=14):
    """边在某一端的方向（指向远离端点）。"""
    pts = edge[:k] if at_start else edge[::-1][:k]
    dx, dy = pts[-1][0] - pts[0][0], pts[-1][1] - pts[0][1]
    length = math.hypot(dx, dy) or 1
    return dx / length, dy / length


def merge_strokes(edges):
    """在每个分叉点把"直着穿过去"的两条边配成一对，再沿配对走出笔画：一根管子有头有尾。"""
    ends = {}
    for i, e in enumerate(edges):
        ends.setdefault(e[0], []).append((i, True))
        ends.setdefault(e[-1], []).append((i, False))
    link = {}
    cos_limit = math.cos(math.radians(180 - MAX_TURN_DEG))
    for incident in ends.values():
        if len(incident) < 2:
            continue
        cands = []
        for a in range(len(incident)):
            for b in range(a + 1, len(incident)):
                ta = tangent(edges[incident[a][0]], incident[a][1])
                tb = tangent(edges[incident[b][0]], incident[b][1])
                c = ta[0] * tb[0] + ta[1] * tb[1]     # 两边都指离节点，直穿则 c≈-1
                if c <= cos_limit:
                    cands.append((c, incident[a], incident[b]))
        cands.sort()
        taken = set()
        for _, a, b in cands:
            if a in taken or b in taken or a[0] == b[0]:
                continue
            link[a] = b; link[b] = a
            taken.add(a); taken.add(b)

    visited = set()
    strokes = []
    for i in range(len(edges)):
        if i in visited:
            continue
        cur = (i, True)                       # 先回溯到这根管子的头
        guard = 0
        while cur in link and guard < len(edges):
            j, at_start = link[cur]
            guard += 1
            if j == i:
                break
            cur = (j, not at_start)
        pts = []
        j, at_start = cur
        walked = set()
        while j not in walked:
            walked.add(j); visited.add(j)
            seg = edges[j] if at_start else edges[j][::-1]
            pts.extend(seg if not pts else seg[1:])
            other = (j, not at_start)
            if other not in link:
                break
            j, at_start = link[other]
        strokes.append(pts)
    return strokes


def simplify(pts, eps):
    """Douglas-Peucker。"""
    if len(pts) < 3:
        return pts
    a, b = pts[0], pts[-1]
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dy)
    best, bi = 0, 0
    for i in range(1, len(pts) - 1):
        p = pts[i]
        d = abs((p[0] - a[0]) * dy - (p[1] - a[1]) * dx) / length if length else math.dist(p, a)
        if d > best:
            best, bi = d, i
    if best > eps:
        return simplify(pts[:bi + 1], eps)[:-1] + simplify(pts[bi:], eps)
    return [a, b]


def pixel_length(pts):
    return sum(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))


def trace_strokes(grid):
    edges = merge_junctions(build_graph(grid))
    edges = prune_spurs(edges)
    strokes = [s for s in merge_strokes(edges) if pixel_length(s) >= MIN_STROKE]
    return [simplify(s, SIMPLIFY_EPS) for s in strokes]


def order_as_tour(paths):
    """把互不相连的折线串成一条尽量连续的巡回：从最长的一条起，每次接上离当前末端最近的一条
    （允许倒着接）。电流沿着它走，就像一根管子从头绕到尾，而不是在几十根断管之间乱跳。"""
    remaining = list(paths)
    remaining.sort(key=len, reverse=True)
    tour = [remaining.pop(0)]
    while remaining:
        end = tour[-1][-1]
        best_i, best_rev, best_d = 0, False, float("inf")
        for i, p in enumerate(remaining):
            d0 = math.dist(end, p[0])
            d1 = math.dist(end, p[-1])
            if d0 < best_d:
                best_i, best_rev, best_d = i, False, d0
            if d1 < best_d:
                best_i, best_rev, best_d = i, True, d1
        nxt = remaining.pop(best_i)
        tour.append(nxt[::-1] if best_rev else nxt)
    return tour


def to_uv(paths):
    """像素 → uv（u 从左，v 从下，和 GUI.DrawTextureWithTexCoords 一致）。"""
    n = SKELETON_SIZE
    return [[[round(x / n, 4), round(1 - y / n, 4)] for x, y in path] for path in paths]


def path_length(uv):
    return sum(math.dist(uv[i], uv[i + 1]) for i in range(len(uv) - 1))


def process(name, force=False):
    src_path = os.path.join(NEON_DIR, f"{name}.png")
    outputs = [os.path.join(NEON_DIR, f) for f in derived_names(name)]
    if not force and all(os.path.exists(o) and os.path.getmtime(o) >= os.path.getmtime(src_path) for o in outputs):
        print(f"  {name}: 已是最新")
        return
    img = Image.open(src_path)
    lines, accent, accent_rgb = split_layers(img)
    lines_without(lines, accent).save(outputs[0])
    accent.save(outputs[1])
    grid = thin(binarize(lines))
    paths = to_uv(order_as_tour(trace_strokes(grid)))
    data = {
        "source": f"{name}.png",
        "accent": "#{:02X}{:02X}{:02X}".format(*accent_rgb) if accent_rgb else None,
        "skeleton_size": SKELETON_SIZE,
        "paths": paths,
    }
    with open(outputs[2], "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, separators=(",", ":"))
    total = sum(path_length(p) for p in paths)
    print(f"  {name}: 管子 {len(paths)} 根，总长 {total:.2f}（uv），点缀色 {data['accent']}")


def main(argv):
    force = "--force" in argv
    names = [a for a in argv if not a.startswith("--")]
    if not names:
        names = sorted(f[:-4] for f in os.listdir(NEON_DIR) if is_source(f))
    print(f"处理 {len(names)} 张：")
    for name in names:
        process(name, force)


if __name__ == "__main__":
    main(sys.argv[1:])
