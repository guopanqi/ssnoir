"""Build exact shared runtime routes for three documented Second Act camera pairs."""
import argparse,hashlib,json,math,subprocess,sys,tempfile,time
from pathlib import Path
import bpy
from mathutils import Vector,Matrix

p=argparse.ArgumentParser();p.add_argument('--batch',type=Path,required=True);p.add_argument('--dotnet',default='/usr/local/share/dotnet/dotnet');p.add_argument('--samples',type=int,default=120)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);batch=a.batch.resolve();root=Path(__file__).resolve().parents[4]
scene=bpy.context.scene;source=Path(bpy.data.filepath).resolve();selected=json.loads((batch/'selected_routes.json').read_text())
def unity(v):return Vector((-v.x,v.z,-v.y))
def shot(cam):
 mode=cam.get('drag')
 if mode not in ('pan','orbit','static'):raise RuntimeError(f'{cam.name}: bad drag={mode}')
 q=cam.matrix_world.to_quaternion();f=unity(q@Vector((0,0,-1))).normalized();up=unity(q@Vector((0,1,0))).normalized()
 right=up.cross(f).normalized();up=f.cross(right).normalized();rotation=Matrix((right,up,f)).transposed().to_quaternion()
 film=math.degrees(2*math.atan(cam.data.sensor_height/(2*cam.data.lens)))
 fov=math.degrees(2*math.atan(math.tan(math.radians(film/2))/(16/9))) if abs(film-39.5978)<=.5 else film
 item={'Name':cam.name,'Mode':mode,'Position':list(unity(cam.matrix_world.translation)),
       'Rotation':[rotation.x,rotation.y,rotation.z,rotation.w],'Fov':fov,'Near':cam.data.clip_start}
 suffix=cam.name.removeprefix('Camera_')
 pivot=scene.objects.get('OrbitPivot_'+suffix);focus=scene.objects.get('FocusTarget_'+suffix)
 if focus is not None:item['Pivot']=list(unity(focus.matrix_world.translation))
 elif mode=='orbit':
  if pivot is None:raise RuntimeError(f'{cam.name}: Orbit camera has no OrbitPivot or FocusTarget')
  item['Pivot']=list(unity(pivot.matrix_world.translation))
 scope=list(cam.parent.children) if cam.parent is not None else list(cam.users_collection[0].objects)
 spaces=[o for o in scope if o.name.startswith('CameraTravelBounds_')]
 if len(spaces)>1:raise RuntimeError(f'{cam.name}: multiple same-root travel spaces')
 if spaces:
  space=spaces[0];lo=next((o for o in space.children if o.name.split('.')[0]=='_min'),None)
  hi=next((o for o in space.children if o.name.split('.')[0]=='_max'),None)
  if lo is None or hi is None:raise RuntimeError(f'{space.name}: missing _min/_max')
  center=space.matrix_world@((lo.location+hi.location)*.5);half=(hi.location-lo.location)*.5
  if min(half)<=0:raise RuntimeError(f'{space.name}: invalid bounds')
  mapping=Matrix(((-1,0,0),(0,0,1),(0,-1,0)))
  rotation=(mapping@space.matrix_world.to_3x3().normalized()@mapping.inverted()).to_quaternion()
  half=Vector((half.x*abs(space.matrix_world.to_scale().x),half.y*abs(space.matrix_world.to_scale().y),half.z*abs(space.matrix_world.to_scale().z)))
  item['Volume']={'Center':list(unity(center)),'HalfSize':[half.x,half.z,half.y],
                  'Rotation':[rotation.x,rotation.y,rotation.z,rotation.w]}
 return item

def anchor_info(name):
 ob=scene.objects.get('Anchor_'+name)
 if ob is None:return None
 return {'name':ob.name,'position':list(unity(ob.matrix_world.translation))}

def camera_interest_info(cam):
 suffix=cam.name.removeprefix('Camera_')
 focus=scene.objects.get('FocusTarget_'+suffix);pivot=scene.objects.get('OrbitPivot_'+suffix)
 if focus is not None:return {'kind':'FocusTarget','name':focus.name,'position':list(unity(focus.matrix_world.translation))}
 if cam.get('drag')=='orbit' and pivot is not None:return {'kind':'OrbitPivot','name':pivot.name,'position':list(unity(pivot.matrix_world.translation))}
 return {'kind':'GroundInterest fallback','reason':'No FocusTarget; target camera is not Orbit'}

dll=root/'tools/camera-review/bin/Debug/net9.0/CameraReview.dll'
if not dll.exists():raise RuntimeError(f'Missing shared Program: {dll}')
routes=[];rows=[];started=time.monotonic()
with tempfile.TemporaryDirectory(prefix='ssnoir-second-act-camera-routes-') as td:
 tmp=Path(td)
 for idx,pair in enumerate(selected['pairs']):
  src=scene.objects.get(pair['from']);dst=scene.objects.get(pair['to'])
  if src is None or dst is None:raise RuntimeError(f"Missing documented camera pair {pair['from']} -> {pair['to']}")
  s,t=shot(src),shot(dst);inp=tmp/f'pair-{idx}.json';out=tmp/f'routes-{idx}.json'
  inp.write_text(json.dumps({'Cameras':[s,t],'Samples':a.samples,'ModelScale':1,'DefaultDuration':2}))
  proc=subprocess.run([a.dotnet,str(dll),str(inp),str(out)],cwd=root,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
  if proc.returncode:raise RuntimeError(f"Shared Program failed for {pair['sequence']}: {proc.stdout}")
  pair_routes=json.loads(out.read_text())['Routes']
  if len(pair_routes)!=2:raise RuntimeError(f"Expected both directions for {pair['sequence']}")
  for route in pair_routes:
   expected_start=s.get('Pivot');expected_target=t.get('Pivot')
   if route['From']==t['Name']:
    expected_start,expected_target=expected_target,expected_start
   if expected_start is not None and any(abs(x-y)>1e-4 for x,y in zip(route['StartInterest'],expected_start)):
    raise RuntimeError(f"{pair['sequence']}: StartInterest does not match runtime camera interest for {route['From']}")
   if expected_target is not None and any(abs(x-y)>1e-4 for x,y in zip(route['TargetInterest'],expected_target)):
    raise RuntimeError(f"{pair['sequence']}: TargetInterest does not match runtime camera interest for {route['To']}")
  routes.extend(pair_routes)
  roots=[]
  for c in (src,dst):
   parent=c.parent
   roots.append(parent.name if parent else None)
  rows.append({'sequence':pair['sequence'],'trigger':pair['trigger'],'source':s,'destination':t,
    'source_root':roots[0],'destination_root':roots[1],
    'joint_focus_interest_assertion':'passed' if pair['from'].startswith('Camera_联合起吊') else 'not_applicable',
    'source_anchor':anchor_info(pair['from'].removeprefix('Camera_')),
    'destination_anchor':anchor_info(pair['to'].removeprefix('Camera_')),
    'source_focus_interest':camera_interest_info(src),'destination_focus_interest':camera_interest_info(dst),
    'shared_program_stdout':proc.stdout.strip()})
elapsed=time.monotonic()-started
(batch/'routes.json').write_text(json.dumps({'Source':'Shared tools/camera-review/Program.cs + FocusTravelMath.cs; only three documented Second Act pairs, both directions',
 'ModelScale':1,'Samples':a.samples,'DefaultDuration':2,'Routes':routes},ensure_ascii=False,indent=2)+'\n')
(batch/'camera_inputs.json').write_text(json.dumps({'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
 'shared_math_sha256':hashlib.sha256((root/'UnityClient/Assets/Scripts/Runtime/FocusTravelMath.cs').read_bytes()).hexdigest(),
 'program_sha256':hashlib.sha256((root/'tools/camera-review/Program.cs').read_bytes()).hexdigest(),
 'evidence_sha256':{p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in [
  'UnityClient/Assets/Resources/Content/scenes/world/第二幕节奏.scm',
  'UnityClient/Assets/Resources/Content/scripts/theatre/第二幕城市镜头.scm',
  'UnityClient/Assets/Resources/Content/scenes/scenarios/大船靠岸（联合起吊）.scm',
  'UnityClient/Assets/Resources/Content/scenes/world/第二幕剧院.scm',
  'UnityClient/Assets/Resources/Content/scenes/scenarios/最后一排的观众.scm',
  'UnityClient/Assets/Scripts/Runtime/SSNoirCameraManager.cs',
  'UnityClient/Assets/Scripts/Runtime/Cutscene/CutscenePlayer.cs',
  'city-box/build/city/report.json']},
 'pairs':rows,'last_row_scenario':'第二幕剧院.scm D13 play-dialogue then start-scenario 最后一排的观众; no Stage/Anchor/Camera/motion/FocusNode; no 3D route was fabricated.'},ensure_ascii=False,indent=2)+'\n')
(batch/'route_execution.json').write_text(json.dumps({'routes':len(routes),'camera_pairs':len(rows),'samples_per_route':a.samples,'elapsed_seconds':elapsed,
 'shared_program':str(dll)},ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'routes':len(routes),'pairs':len(rows),'samples':a.samples,'elapsed_seconds':elapsed},ensure_ascii=False))
