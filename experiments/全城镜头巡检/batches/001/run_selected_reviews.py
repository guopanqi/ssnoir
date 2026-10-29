"""Filter selected cameras in memory and run reviews against the full city build.

Run from the repository root:
  blender -b city-box/build/city/city_build.blend \
    --python-exit-code 1 --python experiments/全城镜头巡检/batches/001/run_selected_reviews.py -- \
    --batch experiments/全城镜头巡检/batches/001

Only Camera_* objects are filtered in memory. The original assembled geometry,
collection instances, transforms, visibility flags, and evaluated meshes remain loaded.
No modified .blend is saved.
"""
import argparse
import hashlib
import json
import sys
import time
from pathlib import Path
import runpy

import bpy

parser = argparse.ArgumentParser()
parser.add_argument("--batch", type=Path, required=True)
parser.add_argument("--dotnet", default="/usr/local/share/dotnet/dotnet")
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
batch = args.batch.resolve()
root = Path(__file__).resolve().parents[4]
review = root / "tools/camera-review/review.py"
source = Path(bpy.data.filepath).resolve()
if not source.is_file():
    raise RuntimeError(f"Expected a saved full-scene input, got {source}")

samples = {
    "world_to_harbor": ["Camera_世界", "Camera_码头"],
    "stage_internal": [
        "Camera_别给他们想要的",
        "Camera_别给他们想要的-叫喊",
        "Camera_别给他们想要的-退让",
    ],
}
for label, names in samples.items():
    missing = [name for name in names if bpy.data.objects.get(name) is None]
    if missing:
        raise RuntimeError(f"Missing selected camera(s) for {label}: {missing}")

def filter_cameras(keep):
    scene = bpy.context.scene
    removed = []
    for obj in list(scene.objects):
        if obj.type == "CAMERA" and obj.name.startswith("Camera_") and obj.name not in keep:
            removed.append(obj.name)
            bpy.data.objects.remove(obj, do_unlink=True)
    present = sorted(obj.name for obj in scene.objects
                     if obj.type == "CAMERA" and obj.name.startswith("Camera_"))
    if present != sorted(keep):
        raise RuntimeError(f"Camera filter mismatch: kept {present}, expected {sorted(keep)}")
    return {"kept_cameras": present, "removed_camera_count": len(removed),
            "evaluated_scene_objects": len(scene.objects)}

records = []
inputs = {}
preparation_seconds = 0.0
for label, names in samples.items():
    if label == "stage_internal":
        bpy.ops.wm.open_mainfile(filepath=str(source))
    prep_start = time.monotonic()
    inputs[label] = filter_cameras(names)
    preparation_seconds += time.monotonic() - prep_start
    out = batch / label
    if out.exists() and any(out.iterdir()):
        raise RuntimeError(f"Output must be empty: {out}")
    out.mkdir(parents=True, exist_ok=True)
    command = ["runpy.run_path", str(review), "--out", str(out), "--dotnet", args.dotnet]
    old_argv = sys.argv
    sys.argv = [str(review), "--", "--out", str(out), "--dotnet", args.dotnet]
    started = time.monotonic()
    try:
        runpy.run_path(str(review), run_name=f"camera_review_{label}")
    finally:
        seconds = time.monotonic() - started
        sys.argv = old_argv
    records.append({"label": label, "command": command, "returncode": 0,
                    "seconds": seconds, "log": None})

snapshot = {
    "source": str(source),
    "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
    "blender_version": bpy.app.version_string,
    "review_script": str(review),
    "review_script_sha256": hashlib.sha256(review.read_bytes()).hexdigest(),
    "shared_math": str(root / "UnityClient/Assets/Scripts/Runtime/FocusTravelMath.cs"),
    "shared_math_sha256": hashlib.sha256(
        (root / "UnityClient/Assets/Scripts/Runtime/FocusTravelMath.cs").read_bytes()).hexdigest(),
    "selected_camera_sets": samples,
    "preparation_seconds": preparation_seconds,
    "preparation": inputs,
    "runs": records,
}
(batch / "execution.json").write_text(json.dumps(snapshot, ensure_ascii=False, indent=2) + "\n",
                                      encoding="utf-8")
print(json.dumps({"preparation_seconds": preparation_seconds,
                  "runs": [{"label": x["label"], "returncode": x["returncode"],
                            "seconds": x["seconds"]} for x in records]}, ensure_ascii=False))
