"""One-time import check in the existing owned Unity preview cache; not the daily Blender loop."""
import argparse,os,shutil,subprocess,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(root/'tools/unity'))
from staging import sync_project
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
project=root/'.cache/camera-review-unity'
marker=project/'.ssnoir-camera-review'
assert not project.exists() or marker.is_file(),'Cache is not owned by camera-review'
project.mkdir(parents=True,exist_ok=True);marker.write_text('Camera metadata check only; generated cache')
# Reuse a stopped formal preview Library through APFS copy-on-write, never the running source Editor.
formal=root/'.cache/citybox-unity-preview-formal'
if not (project/'Library').exists() and (formal/'Library').is_dir():
    lock=formal/'Temp/UnityLockfile'
    active=subprocess.run(['lsof','-t',str(lock)],capture_output=True).stdout.strip() if lock.exists() else b''
    assert not active,'Formal preview cache is in use'
    subprocess.run(['cp','-cR',str(formal/'Library'),str(project/'Library')],check=True)
a.out=a.out.resolve();assert not a.out.exists() or not any(a.out.iterdir()),'Use a fresh output directory'
a.out.mkdir(parents=True,exist_ok=True)
sync_project(root/'UnityClient',project)
# This check only consumes the semantic City model. Exclude animated geometry from this owned cache.
# Source assets and the normal preview cache stay intact.
for model in (project/'Assets').rglob('*.fbx'):
    if model == project/'Assets/Resources/Models/Environment/City.fbx':continue
    model.unlink();Path(str(model)+'.meta').unlink(missing_ok=True)
script=project/'Assets/Editor/UnityCameraReview.cs'
assert not script.exists(),'A previous camera-review script is still installed'
script.write_bytes(Path(__file__).with_name('UnityCameraReview.cs').read_bytes())
try:
    with (a.out/'launch.log').open('w') as log:
        subprocess.run(['/Applications/Unity/Unity.app/Contents/MacOS/Unity','-batchmode','-nographics','-job-worker-count','2','-buildTarget','WebGL','-projectPath',str(project),'-executeMethod','SSNoir.Editor.UnityCameraReview.Verify','-logFile',str(a.out/'unity.log')],env=dict(os.environ,SSNOIR_CAMERA_REVIEW_OUTPUT=str(a.out)),stdout=log,stderr=subprocess.STDOUT,check=True,timeout=300)
    assert (a.out/'result.json').is_file(),'No completed import check result'
finally:
    script.unlink(missing_ok=True);Path(str(script)+'.meta').unlink(missing_ok=True)
print(a.out)
