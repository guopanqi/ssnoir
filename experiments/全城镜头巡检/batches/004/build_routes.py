"""Build six shared FocusTravelMath routes from actual world PanBounds corners."""
import argparse, hashlib, json, math, subprocess, sys, tempfile, time
from pathlib import Path
import bpy
from mathutils import Vector, Matrix

parser=argparse.ArgumentParser(); parser.add_argument('--batch',type=Path,required=True)
parser.add_argument('--dotnet',default='/usr/local/share/dotnet/dotnet'); parser.add_argument('--samples',type=int,default=120)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
batch=args.batch.resolve(); root=Path(__file__).resolve().parents[4]; scene=bpy.context.scene
selected=json.loads((batch/'selected_landmarks.json').read_text()); source=Path(bpy.data.filepath).resolve()
def unity(v): return Vector((-v.x,v.z,-v.y))

def shot(cam,name=None,position=None):
    mode=cam.get('drag')
    if mode not in ('pan','orbit','static'): raise RuntimeError(f'{cam.name}: invalid drag={mode}')
    q=cam.matrix_world.to_quaternion(); f=unity(q@Vector((0,0,-1))).normalized(); up=unity(q@Vector((0,1,0))).normalized()
    right=up.cross(f).normalized(); up=f.cross(right).normalized(); rotation=Matrix((right,up,f)).transposed().to_quaternion()
    film=math.degrees(2*math.atan(cam.data.sensor_height/(2*cam.data.lens)))
    fov=math.degrees(2*math.atan(math.tan(math.radians(film/2))/(16/9))) if abs(film-39.5978)<=.5 else film
    item={'Name':name or cam.name,'Mode':mode,'Position':list(position if position is not None else unity(cam.matrix_world.translation)),
          'Rotation':[rotation.x,rotation.y,rotation.z,rotation.w],'Fov':fov,'Near':cam.data.clip_start}
    suffix=cam.name.removeprefix('Camera_'); pivot=scene.objects.get('OrbitPivot_'+suffix); focus=scene.objects.get('FocusTarget_'+suffix)
    if focus is not None: item['Pivot']=list(unity(focus.matrix_world.translation))
    elif mode=='orbit':
        if pivot is None: raise RuntimeError(f'{cam.name}: missing OrbitPivot')
        item['Pivot']=list(unity(pivot.matrix_world.translation))
    return item

world=scene.objects.get('Camera_世界'); bounds=scene.objects.get('PanBounds_世界')
if world is None or bounds is None: raise RuntimeError('Missing world camera or PanBounds_世界')
corners=[o for o in bounds.children if o.name.split('.')[0] in ('PanBounds_世界_min','PanBounds_世界_max')]
if len(corners)!=2: raise RuntimeError('PanBounds_世界 must have _min and _max child empties')
source_pts=[o.matrix_world.translation.copy() for o in corners]
source_bounds=[min(v.x for v in source_pts),min(v.y for v in source_pts),max(v.x for v in source_pts),max(v.y for v in source_pts)]
pts=[unity(o.matrix_world.translation) for o in corners]
lo=[min(v[i] for v in pts) for i in range(3)]; hi=[max(v[i] for v in pts) for i in range(3)]
asset_bounds=world.get('pan_bounds')
if asset_bounds is None: raise RuntimeError('Camera_世界 has no cached pan_bounds property')
# Cached exporter property stores original Blender model-space XY [minx,miny,maxx,maxy].
cache=[float(x) for x in asset_bounds]
if len(cache)!=4: raise RuntimeError(f'Invalid cached pan_bounds: {asset_bounds}')
if max(abs(cache[i]-source_bounds[i]) for i in range(4))>0.02:
    raise RuntimeError(f'Cached pan_bounds disagree with _min/_max: {cache} vs {source_bounds}')
base=shot(world); y=base['Position'][1]
if not (lo[0]<=base['Position'][0]<=hi[0] and lo[2]<=base['Position'][2]<=hi[2]): raise RuntimeError('Authored Camera_世界 is outside PanBounds')
starts=[('Camera_世界-偏移边界西南',Vector((lo[0],y,lo[2]))),('Camera_世界-偏移边界东北',Vector((hi[0],y,hi[2])))]
start_shots=[shot(world,n,p) for n,p in starts]
targets=[]; roots=[]
for row in selected['places']:
    ro=scene.objects.get(row['root']); cam=scene.objects.get(row['camera'])
    if ro is None or cam is None: raise RuntimeError(f'Missing {row}')
    item=shot(cam); targets.append(item)
    roots.append({'place':row['place'],'root':ro.name,'root_position':list(unity(ro.matrix_world.translation)),
                  'camera':cam.name,'camera_position':item['Position'],'camera_rotation':item['Rotation'],'pivot':item.get('Pivot'),
                  'mode':item['Mode'],'fov':item['Fov'],'near':item['Near']})
dll=root/'tools/camera-review/bin/Debug/net9.0/CameraReview.dll'
if not dll.exists(): raise RuntimeError(f'Missing shared Program {dll}')
routes=[]; execution=[]; started=time.monotonic()
with tempfile.TemporaryDirectory(prefix='ssnoir-pan-offset-routes-') as td:
    tmp=Path(td)
    for si,src in enumerate(start_shots):
        for ti,dst in enumerate(targets):
            idx=si*len(targets)+ti; inp=tmp/f'p{idx}.json'; out=tmp/f'r{idx}.json'
            inp.write_text(json.dumps({'Cameras':[src,dst],'Samples':args.samples,'ModelScale':1,'DefaultDuration':2}))
            p=subprocess.run([args.dotnet,str(dll),str(inp),str(out)],cwd=root,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
            execution.append({'from':src['Name'],'to':dst['Name'],'returncode':p.returncode,'stdout':p.stdout.strip()})
            if p.returncode: raise RuntimeError(p.stdout)
            pair=json.loads(out.read_text())['Routes']; route=next(r for r in pair if r['From']==src['Name'] and r['To']==dst['Name'])
            routes.append(route)
elapsed=time.monotonic()-started
payload={'Source':'Shared tools/camera-review/Program.cs + FocusTravelMath.cs; stable PanBounds corners to selected destination cameras',
         'ModelScale':1,'Samples':args.samples,'DefaultDuration':2,'Routes':routes}
(batch/'routes.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n')
(batch/'camera_inputs.json').write_text(json.dumps({'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
 'bounds_asset_property_blender_xy':cache,'bounds_frame_blender_xy':source_bounds,'bounds_unity_xz_from_frame':[lo[0],lo[2],hi[0],hi[2]],'authored_world_pose':base,
 'unity_pan_body_height':y,'start_poses':[{'Name':s['Name'],'Position':s['Position'],'Rotation':s['Rotation'],'Mode':s['Mode'],'Fov':s['Fov'],'Near':s['Near']} for s in start_shots],
 'landmarks':roots,'shared_math_sha256':hashlib.sha256((root/'UnityClient/Assets/Scripts/Runtime/FocusTravelMath.cs').read_bytes()).hexdigest(),
 'program_sha256':hashlib.sha256((root/'tools/camera-review/Program.cs').read_bytes()).hexdigest()},ensure_ascii=False,indent=2)+'\n')
(batch/'route_execution.json').write_text(json.dumps({'routes':len(routes),'samples_per_route':args.samples,'elapsed_seconds':elapsed,'shared_program':str(dll),'per_route':execution},ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'routes':len(routes),'samples':args.samples,'elapsed_seconds':elapsed,'cache_bounds_blender_xy':cache,'frame_bounds_blender_xy':source_bounds,'frame_bounds_unity_xz':[lo[0],lo[2],hi[0],hi[2]],
 'starts':[{'Name':s['Name'],'Position':s['Position']} for s in start_shots]},ensure_ascii=False))
