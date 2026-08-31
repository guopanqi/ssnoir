"""审计生成式 3D 模型的网格质量，输出可跨生成器横向对比的量化指标。

用途：同一张参考图分别喂给不同的生成服务（Hunyuan / Tripo / …），把各自的产物跑一遍
这个脚本，用同一把尺子比较，而不是靠"看着还行"。

    blender --background 模型.blend \
      --python skills/create-3d-assets/scripts/audit_mesh_quality.py

    # 多个文件依次跑，或加 --json 便于收集
    blender --background 模型.blend \
      --python skills/create-3d-assets/scripts/audit_mesh_quality.py -- --json

指标为什么是这几项，见同目录 references/mesh-generation.md。
脚本只做机械测量，不判断轮廓、年代和游戏用途。
"""

import argparse
import json
import math
import sys

import bmesh
import bpy


# 相邻面夹角分档。关键是中间那档：既不是平面也不是设计上的转折，
# 就是重建噪声——本该平的墙面实际是歪的。
ANGLE_BUCKETS = [
    ("coplanar_lt_0_5", 0.5),
    ("ripple_0_5_to_5", 5.0),
    ("ripple_5_to_20", 20.0),
    ("crease_20_to_60", 60.0),
    ("hard_gt_60", 181.0),
]

# 生成器留下的默认物体名。留着它们，Unity 那边靠名字触发的契约就无从谈起。
GENERATOR_NAME_HINTS = ("mesh_rep", "_ori_repair", "mesh_", "Object_", "model_")

DOUBLE_VERT_THRESHOLD = 1e-4


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", action="store_true", help="输出 JSON 而非可读文本")
    return parser.parse_args(argv)


def audit_object(ob):
    mesh = ob.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()

    tris = quads = ngons = 0
    degenerate = 0
    for face in bm.faces:
        n = len(face.verts)
        if n == 3:
            tris += 1
        elif n == 4:
            quads += 1
        else:
            ngons += 1
        if face.calc_area() < 1e-9:
            degenerate += 1

    angles = {name: 0 for name, _ in ANGLE_BUCKETS}
    interior_edges = 0
    non_manifold = 0
    for edge in bm.edges:
        if not edge.is_manifold:
            non_manifold += 1
        if len(edge.link_faces) != 2:
            continue
        interior_edges += 1
        a, b = edge.link_faces
        if not a.normal.length or not b.normal.length:
            continue
        deg = math.degrees(a.normal.angle(b.normal))
        for name, ceiling in ANGLE_BUCKETS:
            if deg < ceiling:
                angles[name] += 1
                break

    doubles = bmesh.ops.find_doubles(
        bm, verts=bm.verts, dist=DOUBLE_VERT_THRESHOLD)["targetmap"]
    loose_verts = sum(1 for v in bm.verts if not v.link_edges)

    smooth_faces = sum(1 for p in mesh.polygons if p.use_smooth)
    scale = [round(s, 4) for s in ob.scale]

    result = {
        "name": ob.name,
        "looks_generator_named": any(h in ob.name for h in GENERATOR_NAME_HINTS),
        "verts": len(bm.verts),
        "faces": len(bm.faces),
        "tris": tris,
        "quads": quads,
        "ngons": ngons,
        "smooth_faces": smooth_faces,
        "smooth_ratio": round(smooth_faces / max(len(mesh.polygons), 1), 4),
        "has_custom_normals": bool(getattr(mesh, "has_custom_normals", False)),
        "interior_edges": interior_edges,
        "angles": angles,
        "angles_pct": {
            k: round(100.0 * v / max(interior_edges, 1), 2) for k, v in angles.items()
        },
        "non_manifold_edges": non_manifold,
        "duplicate_verts": len(doubles),
        "loose_verts": loose_verts,
        "degenerate_faces": degenerate,
        "uv_layers": len(mesh.uv_layers),
        "materials": len(mesh.materials),
        "unapplied_scale": scale != [1.0, 1.0, 1.0],
        "scale": scale,
    }
    bm.free()
    return result


def main():
    args = parse_args()
    objects = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    report = {
        "file": bpy.data.filepath,
        "mesh_objects": len(objects),
        "objects": [audit_object(o) for o in objects],
    }

    if args.json:
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return

    print(f"FILE {report['file']}  ({report['mesh_objects']} 个网格)")
    for r in report["objects"]:
        flags = []
        if r["has_custom_normals"]:
            flags.append("自定义法线")
        if r["smooth_ratio"] > 0.99:
            flags.append("全 smooth")
        if r["looks_generator_named"]:
            flags.append("生成器默认名")
        if r["unapplied_scale"]:
            flags.append(f"未应用缩放{r['scale']}")
        if r["uv_layers"] == 0:
            flags.append("无 UV")
        if r["non_manifold_edges"]:
            flags.append(f"非流形边×{r['non_manifold_edges']}")
        if r["duplicate_verts"]:
            flags.append(f"重复顶点×{r['duplicate_verts']}")
        if r["degenerate_faces"]:
            flags.append(f"零面积面×{r['degenerate_faces']}")

        print(f"\n  {r['name']}")
        print(f"    面 {r['faces']} (三角 {r['tris']} / 四边 {r['quads']} / ngon {r['ngons']})"
              f"  顶点 {r['verts']}  材质 {r['materials']}")
        print(f"    相邻面夹角: " + "  ".join(
            f"{k}={r['angles_pct'][k]}%" for k, _ in ANGLE_BUCKETS))
        print(f"    问题: {', '.join(flags) if flags else '无'}")


main()
