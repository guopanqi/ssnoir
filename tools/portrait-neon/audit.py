#!/usr/bin/env python3
"""Inspect a generated SSNoir line portrait BEFORE processing/import.

This is a source-art quality gate, not a runtime brightness effect or an
automatic "make a bad picture look right" filter. It catches broad gray fills,
nonblack backgrounds and large inconsistencies between nominally white strokes.
The final identity, silhouette and stage-scale review remains visual.

Usage:
    python3 tools/portrait-neon/audit.py path/to/portrait.png [...]
Exit 1 if any input does not satisfy the line-art baseline.
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageFilter


def quantile(values, fraction):
    values.sort()
    return values[min(len(values) - 1, int(fraction * (len(values) - 1)))] if values else 0


def inspect(path):
    image = Image.open(path).convert("RGBA")
    background = Image.new("RGBA", image.size, (0, 0, 0, 255))
    source = Image.alpha_composite(background, image).convert("RGB")
    w, h = source.size
    pixels = list(source.getdata())
    channels = source.split()
    max_channel = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2])
    # Brightness comparisons use local *ridge peaks*, not all visible glow pixels.
    # Soft antialiasing and bloom halos are intentionally dim around a bright tube.
    local_maxima = list(max_channel.filter(ImageFilter.MaxFilter(7)).getdata())
    neutral_core = []
    painted = 0
    colored = 0
    for (r, g, b), local_peak in zip(pixels, local_maxima):
        hi, lo = max(r, g, b), min(r, g, b)
        if hi < 24:
            continue
        painted += 1
        if hi - lo > max(18, int(hi * .26)):
            colored += 1  # valid accent ink, excluded from white-ink analysis
        elif hi >= 90 and hi + 1 >= local_peak:
            neutral_core.append(hi)

    if painted == 0:
        return {"path": str(path), "ok": False, "issues": ["没有可读的发光描线"]}
    # An eleven-pixel erosion excludes thin outlines and antialias halos;
    # surviving neutral midtones suggest gray filled regions rather than ink.
    min_channel = ImageChops.darker(ImageChops.darker(channels[0], channels[1]), channels[2])
    heavy = min_channel.filter(ImageFilter.MinFilter(11))
    gray_fill = sum(1 for value in heavy.getdata() if 45 <= value <= 185)
    gray_fraction = gray_fill / (w * h)
    p10 = quantile(neutral_core, .10)
    p90 = quantile(neutral_core, .90)

    issues = []
    if gray_fraction > .015:
        issues.append(f"存在大块灰色填充：厚实灰区占画布 {gray_fraction:.1%}（要求 ≤1.5%）")
    if len(neutral_core) > 150 and p10 < 155 and p90 > 222:
        issues.append(f"白描线核心亮度不一致：10–90 分位 {p10}–{p90}（需重新出图或重制源线稿）")
    if painted < 50:
        issues.append("描线像素太少，无法确认是可用的人物资产")
    return {
        "path": str(path), "ok": not issues, "size": [w, h],
        "neutral_p10": p10, "neutral_p90": p90,
        "colored_ink_pixels": colored,
        "gray_fill_fraction": round(gray_fraction, 5),
        "issues": issues,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("images", nargs="+", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    reports = [inspect(path) for path in args.images]
    if args.json:
        print(json.dumps(reports, ensure_ascii=False, indent=2))
    else:
        for r in reports:
            print(("通过" if r["ok"] else "不合格") + ": " + r["path"])
            for issue in r["issues"]:
                print("  - " + issue)
    return 0 if all(r["ok"] for r in reports) else 1


if __name__ == "__main__":
    raise SystemExit(main())
