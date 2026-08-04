"""Validate an SSNoir Blender asset.

Run with:
  blender --background asset.blend --python validate_blend_asset.py -- [options]
"""

import argparse
import json
import math
import sys

import bpy


def parse_args():
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--require-camera", action="store_true")
    parser.add_argument("--require-outline", action="store_true")
    parser.add_argument("--require-orbit-pivot", action="store_true")
    parser.add_argument("--require-anchor", action="append", default=[])
    parser.add_argument("--require-quads", action="store_true")
    parser.add_argument("--max-polygons", type=int)
    return parser.parse_args(args)


def normalized_marker_name(name):
    return name.replace(" ", "").replace("_", "").replace("-", "").lower()


options = parse_args()
objects = list(bpy.context.scene.objects)
meshes = [obj for obj in objects if obj.type == "MESH"]
cameras = [obj for obj in objects if obj.type == "CAMERA"]
anchors = [obj for obj in objects if obj.name.lower().startswith("anchor")]
orbit_pivots = [
    obj for obj in objects if normalized_marker_name(obj.name) == "orbitpivot"
]

polygon_count = sum(len(obj.data.polygons) for obj in meshes)
quad_count = sum(
    1 for obj in meshes for polygon in obj.data.polygons if len(polygon.vertices) == 4
)
triangle_count = sum(
    1 for obj in meshes for polygon in obj.data.polygons if len(polygon.vertices) == 3
)
ngon_count = polygon_count - quad_count - triangle_count

bad_vertices = []
for obj in meshes:
    for vertex in obj.data.vertices:
        if not all(math.isfinite(value) for value in vertex.co):
            bad_vertices.append(f"{obj.name}:{vertex.index}")

outline_objects = []
for obj in objects:
    material_names = {
        slot.material.name for slot in obj.material_slots if slot.material is not None
    }
    if obj.name.startswith("OutlineLines") or "M_White_Emission_Lines" in material_names:
        outline_objects.append(obj.name)

errors = []
if not meshes:
    errors.append("scene contains no mesh")
if bad_vertices:
    errors.append(f"found {len(bad_vertices)} non-finite vertices")
if options.require_camera and not cameras:
    errors.append("camera is required")
if options.require_outline and not outline_objects:
    errors.append("outline object/material is required")
if options.require_orbit_pivot and not orbit_pivots:
    errors.append("orbit pivot is required")
if options.max_polygons is not None and polygon_count > options.max_polygons:
    errors.append(
        f"polygon count {polygon_count} exceeds limit {options.max_polygons}"
    )
if options.require_quads and polygon_count != quad_count:
    errors.append(
        f"quad-only topology required; tris={triangle_count}, ngons={ngon_count}"
    )

anchor_names = {obj.name for obj in anchors}
for node_name in options.require_anchor:
    expected = f"Anchor_{node_name}"
    if expected not in anchor_names:
        errors.append(f"missing required anchor '{expected}'")

anchor_node_names = []
for anchor in anchors:
    underscore = anchor.name.find("_")
    node_name = anchor.name[underscore + 1 :] if underscore >= 0 else ""
    if not node_name:
        errors.append(f"anchor '{anchor.name}' has an empty NodeName")
    anchor_node_names.append(node_name)

duplicates = sorted(
    {name for name in anchor_node_names if name and anchor_node_names.count(name) > 1}
)
if duplicates:
    errors.append(f"duplicate anchor NodeNames: {duplicates}")

report = {
    "file": bpy.data.filepath,
    "meshes": len(meshes),
    "vertices": sum(len(obj.data.vertices) for obj in meshes),
    "polygons": polygon_count,
    "quads": quad_count,
    "triangles": triangle_count,
    "ngons": ngon_count,
    "bad_vertices": len(bad_vertices),
    "cameras": [obj.name for obj in cameras],
    "anchors": [obj.name for obj in anchors],
    "orbit_pivots": [obj.name for obj in orbit_pivots],
    "outlines": outline_objects,
    "non_unit_scales": {
        obj.name: [round(value, 6) for value in obj.scale]
        for obj in objects
        if any(abs(value - 1.0) > 1e-5 for value in obj.scale)
    },
    "errors": errors,
}
print("SSNOIR_BLEND_VALIDATION " + json.dumps(report, ensure_ascii=False))
if errors:
    raise SystemExit(1)
