"""把 town-plan.json 渲染成俯视平面图（PNG），用于在进 Blender 之前评估骨架。

    python3 render_plan.py out.png                 # 渲染规划
    python3 render_plan.py out.png --plan cur.json # 渲染任意同格式的规划（如现状快照）

配色沿用项目基调：深蓝底 + 白描边 + 琥珀强调，让平面图和游戏里看到的是同一座城。
"""

from __future__ import annotations

import argparse
import os
import sys

from PIL import Image, ImageDraw, ImageFont

import citygen

FONT = "/Users/usr/Documents/play/ssnoir/Content/assets/fonts/MiSans-Semibold.ttf"

BG = (7, 11, 24)
WATER = (13, 22, 44)
WATER_EDGE = (150, 175, 205)
ROAD = (108, 126, 158)
ROAD_MAIN = (150, 172, 205)
FABRIC_FILL = (10, 20, 42)
FABRIC_EDGE = (128, 150, 182)
LOT_FILL = (26, 24, 30)
LOT_EDGE = (208, 164, 92)
PLANNED_EDGE = (120, 104, 76)
LABEL = (232, 216, 186)
GRID = (22, 32, 56)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--plan", default=citygen.PLAN_PATH)
    ap.add_argument("--ppu", type=float, default=9.0, help="每 unit 多少像素")
    ap.add_argument("--title", default=None)
    ap.add_argument("--no-fabric", action="store_true")
    ap.add_argument("--lots", action="store_true", help="叠上 hero 地块（骨架期默认不画）")
    args = ap.parse_args()

    plan = citygen.load_plan(args.plan)
    bx0, bx1 = plan["bounds"]["x"]
    bz0, bz1 = plan["bounds"]["z"]
    ppu = args.ppu
    margin = 40
    W = int((bx1 - bx0) * ppu) + margin * 2
    H = int((bz1 - bz0) * ppu) + margin * 2

    def P(x, z):
        # Z 向北，图像 Y 向下 —— 翻转，让图上「上」就是北
        return (margin + (x - bx0) * ppu, margin + (bz1 - z) * ppu)

    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img, "RGBA")

    for gx in range(int(bx0), int(bx1) + 1, 20):
        d.line([P(gx, bz0), P(gx, bz1)], fill=GRID, width=1)
    for gz in range(int(bz0), int(bz1) + 1, 20):
        d.line([P(bx0, gz), P(bx1, gz)], fill=GRID, width=1)

    if plan.get("river", {}).get("centerline"):
        poly = [P(x, z) for x, z in citygen.river_polygon(plan)]
        d.polygon(poly, fill=WATER, outline=WATER_EDGE)

    roads = citygen.all_roads(plan)
    for r in roads:
        pts = [P(x, z) for x, z in r["points"]]
        w = max(1, int(r["width"] * ppu))
        col = ROAD if r.get("minor") else ROAD_MAIN
        d.line(pts, fill=col, width=w, joint="curve")
    for b in plan.get("bridges", []):
        d.line([P(*b["from"]), P(*b["to"])], fill=ROAD_MAIN,
               width=max(1, int(b["width"] * ppu)))

    lots = citygen.lot_boxes(plan) if args.lots else []
    if not args.no_fabric and plan.get("districts"):
        for box in citygen.frontage_boxes(plan, roads, lots):
            d.polygon([P(x, z) for x, z in box["corners"]],
                      fill=FABRIC_FILL, outline=FABRIC_EDGE)

    try:
        font = ImageFont.truetype(FONT, 15)
        small = ImageFont.truetype(FONT, 12)
    except OSError:
        font = small = ImageFont.load_default()

    for lot in lots:
        planned = lot["model"] is None
        edge = PLANNED_EDGE if planned else LOT_EDGE
        d.polygon([P(x, z) for x, z in lot["corners"]],
                  fill=LOT_FILL, outline=edge)
        cx, cy = P(*lot["pos"])
        d.text((cx, cy - 2), lot["id"], fill=LABEL if not planned else PLANNED_EDGE,
               font=font, anchor="mm", stroke_width=3, stroke_fill=BG)
        d.text((cx, cy + 13), f"h{lot['height']:g}", fill=(120, 134, 160),
               font=small, anchor="mm", stroke_width=3, stroke_fill=BG)

    for r in plan.get("roads", []):
        mx, mz = r["points"][len(r["points"]) // 2]
        px, py = P(mx, mz)
        d.text((px, py - 9), r["name"], fill=(96, 116, 148), font=small,
               anchor="mm", stroke_width=3, stroke_fill=BG)

    title = args.title or os.path.basename(args.plan)
    d.text((margin, 14), title, fill=LABEL, font=font)
    d.text((W - margin, 14),
           f"{int(bx1-bx0)} × {int(bz1-bz0)} units   ·   1 unit ≈ 8 m   ·   上=北，海在南",
           fill=(120, 134, 160), font=small, anchor="ra")

    img.save(args.out)
    print(f"wrote {args.out}  {W}x{H}")


if __name__ == "__main__":
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    main()
