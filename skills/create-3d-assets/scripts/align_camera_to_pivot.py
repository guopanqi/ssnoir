"""Report or fix camera / orbit-pivot alignment in an SSNoir Blender asset.

A camera that does not look at its orbit pivot reads as a bug in game: the shot
arrives on its authored aim, then jumps the moment the player drags, because the
runtime orbit re-aims at the pivot (SSNoirVirtualCameraConfig.ApplyOrbitFromDrag).
The importer (ConfigureOrbit) widens [minPitch, maxPitch] to contain the authored
pitch, so an out-of-band shot is reported as a note, never an error.

Run with:
  blender --background asset.blend --python align_camera_to_pivot.py -- [options]

Reports by default and exits 1 when something is off, so it can gate a build.
Pass --apply to re-aim the cameras (position is never touched), and --save to
write the .blend back.
"""

import argparse
import json
import math
import re
import sys

import bpy
from mathutils import Matrix


def parse_args():
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--camera", action="append", default=[],
        help="camera object name; repeatable. Default: every camera in the scene")
    parser.add_argument(
        "--apply", action="store_true",
        help="write the fix chosen by --fix (report only without it)")
    parser.add_argument(
        "--fix", choices=("pivot", "camera"), default="pivot",
        help="pivot: slide the pivot onto the camera's view axis — the authored shot "
             "is untouched (default). camera: re-aim the camera at the pivot — the "
             "pivot stays put and the framing changes.")
    parser.add_argument(
        "--save", action="store_true", help="save the .blend after --apply")
    parser.add_argument(
        "--tolerance", type=float, default=0.5,
        help="aim error in degrees that still counts as aligned (default 0.5)")
    # SSNoirVirtualCameraConfig 默认带；导入器会把每台 Orbit 相机的带撑到包含出厂机位，所以出带只提示不拦截。
    parser.add_argument("--min-pitch", type=float, default=16.0)
    parser.add_argument("--max-pitch", type=float, default=35.0)
    return parser.parse_args(args)


def normalized_marker_name(name):
    name = re.sub(r"\.\d+$", "", name)
    return name.replace(" ", "").replace("_", "").replace("-", "").lower()


def is_same_or_child_of(candidate, scope):
    current = candidate
    while current is not None:
        if current == scope:
            return True
        current = current.parent
    return False


def nearest_pivot(camera, pivots):
    """Mirror SSNoirModelImporter.FindClosestOrbitPivot.

    A camera binds only a pivot that sits directly under the same parent — its own
    Prefab root — so the nested alley's camera cannot borrow the bar's pivot, and
    the bar's camera cannot bind the alley's. In a Prefab .blend both objects are
    top-level (parent None); in the built FBX both hang under the Prefab root.
    A whole-asset fallback applies only when the file holds exactly one pivot.
    """
    origin = camera.matrix_world.translation
    siblings = [p for p in pivots if p.parent == camera.parent]
    if siblings:
        return min(siblings, key=lambda p: (p.matrix_world.translation - origin).length)
    return pivots[0] if len(pivots) == 1 else None


options = parse_args()
objects = list(bpy.context.scene.objects)
# 规范名 OrbitPivot_<名>；导入器按"归一化后以 orbitpivot 开头"识别，独立资产里的 `orbit pivot` 也认。
pivots = [obj for obj in objects if normalized_marker_name(obj.name).startswith("orbitpivot")]
cameras = [obj for obj in objects if obj.type == "CAMERA"]
if options.camera:
    wanted = set(options.camera)
    cameras = [obj for obj in cameras if obj.name in wanted]
    missing = sorted(wanted - {obj.name for obj in cameras})
else:
    missing = []

rows = []
notes = []
errors = list(f"no camera named '{name}'" for name in missing)

if not pivots:
    errors.append("scene has no orbit pivot; nothing to align against")

# Moving a pivot is only unambiguous while a single camera owns it.
pivot_owners = {}
for camera in cameras:
    owned = nearest_pivot(camera, pivots) if pivots else None
    if owned is not None:
        pivot_owners.setdefault(owned.name, []).append(camera.name)

for camera in cameras:
    pivot = nearest_pivot(camera, pivots) if pivots else None
    if pivot is None:
        rows.append({"camera": camera.name, "pivot": None, "aligned": None})
        errors.append(
            f"camera '{camera.name}' binds no orbit pivot in its own subtree; "
            "it will import as a Pan camera")
        continue

    origin = camera.matrix_world.translation
    offset = pivot.matrix_world.translation - origin
    distance = offset.length
    if distance < 1e-4:
        errors.append(f"camera '{camera.name}' sits on pivot '{pivot.name}'")
        continue

    # Blender is Z-up: elevation is measured off the XY plane.
    horizontal = math.hypot(offset.x, offset.y)
    pitch = math.degrees(math.atan2(-offset.z, horizontal))
    # A Blender camera looks down its own -Z.
    forward = -camera.matrix_world.to_3x3().col[2].normalized()
    aim_error = math.degrees(forward.angle(offset))

    aligned = aim_error <= options.tolerance
    in_band = options.min_pitch - 1e-3 <= pitch <= options.max_pitch + 1e-3
    rows.append({
        "camera": camera.name,
        "pivot": pivot.name,
        "distance": round(distance, 4),
        "pitch": round(pitch, 3),
        "aim_error_degrees": round(aim_error, 3),
        "aligned": aligned,
        "pitch_in_band": in_band,
    })

    if not aligned and not options.apply:
        errors.append(
            f"camera '{camera.name}' misses pivot '{pivot.name}' by {aim_error:.1f} deg")
    # A pivot move changes the pitch, so let the post-move check report it instead.
    if not in_band and not (options.apply and options.fix == "pivot"):
        notes.append(
            f"camera '{camera.name}' pitch {pitch:.1f} deg is outside the default "
            f"orbit band [{options.min_pitch:.0f}, {options.max_pitch:.0f}]; "
            "the importer widens the band to contain it")

    if options.apply and not aligned:
        if options.fix == "camera":
            rotation = offset.to_track_quat("-Z", "Y")
            _, _, scale = camera.matrix_world.decompose()
            camera.matrix_world = Matrix.LocRotScale(origin, rotation, scale)
            rows[-1]["applied"] = "camera re-aimed"
        elif len(pivot_owners.get(pivot.name, [])) > 1:
            errors.append(
                f"pivot '{pivot.name}' is shared by {pivot_owners[pivot.name]}; "
                "moving it would break the other shot. Re-aim those cameras instead.")
        else:
            # Slide the pivot to the point on the view axis nearest to where it already
            # is: the smallest move that puts it dead centre, and the shot never changes.
            along_view = offset.dot(forward)
            if along_view <= 0.0:
                errors.append(
                    f"pivot '{pivot.name}' is behind camera '{camera.name}'; "
                    "re-aim the camera instead")
            else:
                moved = origin + forward * along_view
                translation, rotation, scale = pivot.matrix_world.decompose()
                pivot.matrix_world = Matrix.LocRotScale(moved, rotation, scale)
                rows[-1]["applied"] = "pivot moved onto the view axis"
                rows[-1]["pivot_moved_by"] = round((moved - translation).length, 4)
                new_offset = moved - origin
                new_pitch = math.degrees(
                    math.atan2(-new_offset.z, math.hypot(new_offset.x, new_offset.y)))
                rows[-1]["pitch_after"] = round(new_pitch, 3)
                if not (options.min_pitch - 1e-3 <= new_pitch <= options.max_pitch + 1e-3):
                    notes.append(
                        f"camera '{camera.name}' pitch is {new_pitch:.1f} deg after the "
                        f"move, still outside [{options.min_pitch:.0f}, "
                        f"{options.max_pitch:.0f}]")

if options.apply and options.save:
    bpy.ops.wm.save_mainfile()

report = {
    "file": bpy.data.filepath,
    "applied": bool(options.apply),
    "saved": bool(options.apply and options.save),
    "cameras": rows,
    "notes": notes,
    "errors": errors,
}
print("SSNOIR_CAMERA_ALIGNMENT " + json.dumps(report, ensure_ascii=False))
if errors:
    raise SystemExit(1)
