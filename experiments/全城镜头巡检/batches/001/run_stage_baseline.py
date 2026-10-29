"""Run Stage internal routes without a wrongly collection-inherited travel volume.

The selected Stage has no CameraTravelBounds in its authored root. This script
filters cameras in memory and invokes review.py's documented no-volume baseline.
"""
import argparse, json, runpy, sys, time
from pathlib import Path
import bpy

parser = argparse.ArgumentParser()
parser.add_argument("--batch", type=Path, required=True)
parser.add_argument("--dotnet", default="/usr/local/share/dotnet/dotnet")
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
batch = args.batch.resolve()
root = Path(__file__).resolve().parents[4]
review = root / "tools/camera-review/review.py"
keep = {"Camera_别给他们想要的", "Camera_别给他们想要的-叫喊", "Camera_别给他们想要的-退让"}
for name in keep:
    if bpy.data.objects.get(name) is None:
        raise RuntimeError(f"Missing camera: {name}")
for obj in list(bpy.context.scene.objects):
    if obj.type == "CAMERA" and obj.name.startswith("Camera_") and obj.name not in keep:
        bpy.data.objects.remove(obj, do_unlink=True)
present = {o.name for o in bpy.context.scene.objects if o.type == "CAMERA" and o.name.startswith("Camera_")}
if present != keep:
    raise RuntimeError(f"Camera filter mismatch: {present}")
out = batch / "stage_internal_no_travel_space"
if out.exists() and any(out.iterdir()):
    raise RuntimeError(f"Output must be empty: {out}")
out.mkdir(parents=True, exist_ok=True)
old_argv = sys.argv
sys.argv = [str(review), "--", "--out", str(out), "--dotnet", args.dotnet, "--without-travel-space"]
started = time.monotonic()
try:
    runpy.run_path(str(review), run_name="camera_review_stage_without_volume")
finally:
    seconds = time.monotonic() - started
    sys.argv = old_argv
execution = {"source": bpy.data.filepath, "selected_cameras": sorted(keep),
             "volume_override": "--without-travel-space (diagnostic baseline)",
             "review": str(review), "seconds": seconds}
(batch / "stage_baseline_execution.json").write_text(
    json.dumps(execution, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(execution, ensure_ascii=False))
