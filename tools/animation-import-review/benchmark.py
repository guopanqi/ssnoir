"""Compare animation importers in an owned cache; never edits the source Unity project.

Requires a prepared/instrumented importer; import version must differ for each phase so
Unity performs a real import. Curve snapshots include every key and tangent metadata.
"""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools/unity'))
from staging import sync_project

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--importer', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
parser.add_argument('--asset', action='append', default=[])
parser.add_argument('--cache', type=Path, default=ROOT / '.cache/animation-import-review')
parser.add_argument('--profile-only', action='store_true', help='Record import timings and clip names without full key snapshots')
parser.add_argument('--unity', default='/Applications/Unity/Unity.app/Contents/MacOS/Unity')
args = parser.parse_args()
source = ROOT / 'UnityClient'
cache = args.cache.resolve()
output = args.out.resolve()
marker = cache / '.ssnoir-animation-import-review'
assert cache != source and source not in cache.parents and cache not in source.parents
assert not cache.exists() or marker.is_file(), 'Cache is not owned by animation-import-review'
assert not output.exists() or not any(output.iterdir()), 'Use an empty output directory'
cache.mkdir(parents=True, exist_ok=True)
marker.write_text(json.dumps(dict(source=str(source))))
output.mkdir(parents=True, exist_ok=True)
formal = ROOT / '.cache/citybox-unity-preview-formal'
if not (cache / 'Library').exists() and (formal / 'Library').is_dir():
    lock = formal / 'Temp/UnityLockfile'
    active = subprocess.run(['lsof', '-t', str(lock)], capture_output=True).stdout.strip() if lock.exists() else b''
    assert not active, 'Formal cache is running'
    subprocess.run(['cp', '-cR', str(formal / 'Library'), str(cache / 'Library')], check=True)
sync_project(source, cache)
assets = args.asset or ['Assets/Resources/City/Places/联合起吊.fbx']
assert all((source / p).is_file() and p.endswith('.fbx') for p in assets)
# Keep only selected FBX inputs in this private test cache; source files stay intact.
for path in (cache / 'Assets').rglob('*.fbx'):
    if str(path.relative_to(cache)) not in assets:
        path.unlink()
        Path(str(path) + '.meta').unlink(missing_ok=True)
shutil.copyfile(args.importer.resolve(), cache / 'Assets/Editor/Pipeline/SSNoirModelImporter.cs')
shutil.copyfile(Path(__file__).with_name('AnimationImportBenchmark.cs'), cache / 'Assets/Editor/AnimationImportBenchmark.cs')
(output / 'request.json').write_text(json.dumps(dict(assets=assets, profileOnly=args.profile_only), ensure_ascii=False, indent=2))
with (output / 'launch.log').open('w') as log:
    result = subprocess.run([args.unity, '-batchmode', '-quit', '-nographics', '-job-worker-count', '2',
        '-projectPath', str(cache), '-executeMethod', 'SSNoir.Editor.AnimationImportBenchmark.Run',
        '-logFile', str(output / 'unity.log')], env=dict(os.environ, SSNOIR_ANIMATION_REVIEW_OUTPUT=str(output)),
        stdout=log, stderr=subprocess.STDOUT)
text = (output / 'unity.log').read_text(errors='replace')
errors = re.findall(r'^.*(?:error CS\d+|Shader error|\w+Exception:|Assertion failed|Aborting batchmode).*$', text, re.M)
assert result.returncode == 0 and not errors, (result.returncode, errors[:8])
assert (output / 'completed.json').is_file(), 'No completed snapshot'
profiles = [line for line in text.splitlines() if line.startswith('ANIM_IMPORT_PROFILE|')]
assert profiles, 'No measured animation callbacks; import was cached'
(output / 'profiles.json').write_text(json.dumps(profiles, ensure_ascii=False, indent=2))
print(output)
