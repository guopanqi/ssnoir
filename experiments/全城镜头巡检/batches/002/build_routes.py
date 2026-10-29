"""Sample only Camera_世界 <-> one top-level city landmark at a time.

Run from the repository root:
  blender -b city-box/build/city/city_build.blend --python-exit-code 1 \
    --python experiments/全城镜头巡检/batches/002/build_routes.py -- \
    --batch experiments/全城镜头巡检/batches/002
"""
import argparse, hashlib, json, math, subprocess, sys, tempfile, time
from pathlib import Path
import bpy
from mathutils import Vector, Matrix

parser=argparse.ArgumentParser()
parser.add_argument('--batch',type=Path,required=True)
parser.add_argument('--dotnet',default='/usr/local/share/dotnet/dotnet')
parser.add_argument('--samples',type=int,default=120)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
batch=args.batch.resolve(); root=Path(__file__).resolve().parents[4]
selected=json.loads((batch/'selected_landmarks.json').read_text())
source=Path(bpy.data.filepath).resolve()
def unity(v): return Vector((-v.x,v.z,-v.y))
scene=bpy.context.scene

def shot(cam):
    mode=cam.get('drag')
    if mode not in ('pan','orbit','static'): raise RuntimeError(f'{cam.name}: missing/invalid drag={mode}')
    q=cam.matrix_world.to_quaternion()
    f=unity(q@Vector((0,0,-1))).normalized(); up=unity(q@Vector((0,1,0))).normalized()
    right=up.cross(f).normalized(); up=f.cross(right).normalized()
    rotation=Matrix((right,up,f)).transposed().to_quaternion()
    film=math.degrees(2*math.atan(cam.data.sensor_height/(2*cam.data.lens)))
    fov=math.degrees(2*math.atan(math.tan(math.radians(film/2))/(16/9))) if abs(film-39.5978)<=.5 else film
    item={'Name':cam.name,'Mode':mode,'Position':list(unity(cam.matrix_world.translation)),
          'Rotation':[rotation.x,rotation.y,rotation.z,rotation.w],'Fov':fov,'Near':cam.data.clip_start}
    suffix=cam.name.removeprefix('Camera_')
    pivot=scene.objects.get('OrbitPivot_'+suffix); focus=scene.objects.get('FocusTarget_'+suffix)
    if focus is not None: item['Pivot']=list(unity(focus.matrix_world.translation))
    elif mode=='orbit':
        if pivot is None: raise RuntimeError(f'{cam.name}: Orbit camera has no OrbitPivot or FocusTarget')
        item['Pivot']=list(unity(pivot.matrix_world.translation))
    scope=list(cam.parent.children) if cam.parent is not None else list(cam.users_collection[0].objects)
    spaces=[o for o in scope if o.name.startswith('CameraTravelBounds_')]
    if len(spaces)>1: raise RuntimeError(f'{cam.name}: multiple same-root travel spaces')
    if spaces:
        space=spaces[0]; lo=next((o for o in space.children if o.name.split('.')[0]=='_min'),None)
        hi=next((o for o in space.children if o.name.split('.')[0]=='_max'),None)
        if lo is None or hi is None: raise RuntimeError(f'{space.name}: missing _min/_max')
        center=space.matrix_world@((lo.location+hi.location)*.5); half=(hi.location-lo.location)*.5
        if min(half)<=0: raise RuntimeError(f'{space.name}: invalid bounds')
        mapping=Matrix(((-1,0,0),(0,0,1),(0,-1,0)))
        rotation=(mapping@space.matrix_world.to_3x3().normalized()@mapping.inverted()).to_quaternion()
        half=Vector((half.x*abs(space.matrix_world.to_scale().x),half.y*abs(space.matrix_world.to_scale().y),half.z*abs(space.matrix_world.to_scale().z)))
        item['Volume']={'Center':list(unity(center)),'HalfSize':[half.x,half.z,half.y],
                        'Rotation':[rotation.x,rotation.y,rotation.z,rotation.w]}
    return item

world=bpy.data.objects.get('Camera_世界')
if world is None: raise RuntimeError('Missing Camera_世界 in full assembly')
world_shot=shot(world)
shots=[]; roots=[]
for row in selected['places']:
    root_ob=scene.objects.get(row['root']); cam=scene.objects.get(row['camera'])
    if root_ob is None or cam is None: raise RuntimeError(f"Missing root/camera for {row['place']}")
    if cam.parent != root_ob: raise RuntimeError(f"{cam.name} is not a direct child of {root_ob.name}")
    item=shot(cam); shots.append(item)
    roots.append({'place':row['place'],'root':root_ob.name,'root_position':list(unity(root_ob.matrix_world.translation)),
                  'root_scale':list(root_ob.matrix_world.to_scale()),'camera':cam.name,
                  'camera_position':item['Position'],'camera_rotation':item['Rotation'],'forward':list(unity(cam.matrix_world.to_quaternion()@Vector((0,0,-1)).normalized())),
                  'mode':item['Mode'],'fov':item['Fov'],'near':item['Near'],'focusTarget':('FocusTarget_'+cam.name.removeprefix('Camera_')) in bpy.data.objects,
                  'orbitPivot':('OrbitPivot_'+cam.name.removeprefix('Camera_')) in bpy.data.objects,'travelVolume':bool(item.get('Volume'))})

dotnet_dll=root/'tools/camera-review/bin/Debug/net9.0/CameraReview.dll'
if not dotnet_dll.exists(): raise RuntimeError(f'Missing compiled shared Program: {dotnet_dll}')
start=time.monotonic(); routes=[]; commands=[]
with tempfile.TemporaryDirectory(prefix='ssnoir-city-routes-') as td:
    tmp=Path(td)
    for idx,destination in enumerate(shots):
        inp=tmp/f'pair-{idx:02d}.json'; out=tmp/f'pair-{idx:02d}-routes.json'
        inp.write_text(json.dumps({'Cameras':[world_shot,destination],'Samples':args.samples,'ModelScale':1,'DefaultDuration':2}))
        cmd=[args.dotnet,str(dotnet_dll),str(inp),str(out)]
        p=subprocess.run(cmd,cwd=root,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
        commands.append({'place':selected['places'][idx]['place'],'command':[args.dotnet,str(dotnet_dll),'<pair-input.json>','<pair-routes.json>'],
                         'returncode':p.returncode,'stdout':p.stdout.strip()})
        if p.returncode: raise RuntimeError(f"Shared Program failed for {selected['places'][idx]['place']}: {p.stdout}")
        pair=json.loads(out.read_text())['Routes']
        if len(pair)!=2: raise RuntimeError(f"Expected 2 directed routes for {destination['Name']}, got {len(pair)}")
        routes.extend(pair)
runtime=time.monotonic()-start

payload={'Source':'Shared tools/camera-review/Program.cs + FocusTravelMath.cs; only Camera_世界 <-> selected city roots',
         'ModelScale':1,'Samples':args.samples,'DefaultDuration':2,'Routes':routes}
(batch/'routes.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n')
(batch/'camera_inputs.json').write_text(json.dumps({'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
    'landmarks':roots,'world_camera':world_shot,'shared_math_sha256':hashlib.sha256((root/'UnityClient/Assets/Scripts/Runtime/FocusTravelMath.cs').read_bytes()).hexdigest(),
    'program_sha256':hashlib.sha256((root/'tools/camera-review/Program.cs').read_bytes()).hexdigest()},ensure_ascii=False,indent=2)+'\n')
(batch/'route_execution.json').write_text(json.dumps({'routes':len(routes),'landmarks':len(shots),'samples_per_route':args.samples,
    'duration_seconds':2,'elapsed_seconds':runtime,'shared_program':str(dotnet_dll),'per_landmark':commands},ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'routes':len(routes),'landmarks':len(shots),'samples_per_route':args.samples,'elapsed_seconds':runtime},ensure_ascii=False))
