"""Blender entry: export authored endpoints, run shared C# planner, inspect mesh geometry.
blender -b <scene.blend> --python tools/camera-review/review.py -- --out <directory>
"""
import argparse,hashlib,json,math,subprocess,sys,time
from pathlib import Path
import bpy
from mathutils import Vector,Matrix,Quaternion
from mathutils.bvhtree import BVHTree
start_time=time.monotonic()
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--out',type=Path,required=True)
parser.add_argument('--samples',type=int,default=120)
parser.add_argument('--without-travel-space',action='store_true',help='Diagnostic baseline: omit authored travel spaces')
parser.add_argument('--unity-import',type=Path,help='Completed Unity metadata result.json for endpoint parity')
parser.add_argument('--dotnet',default='/usr/local/share/dotnet/dotnet')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
out=args.out.resolve();assert not out.exists() or not any(out.iterdir()),'Use a fresh output directory';out.mkdir(parents=True,exist_ok=True)
# Blender -> published Unity axes, in asset metres. Main City scale is 1.
def unity(v):return Vector((-v.x,v.z,-v.y))
def blender(v):return Vector((-v[0],-v[2],v[1]))
scene=bpy.context.scene
cameras=[]
for cam in sorted((o for o in scene.objects if o.type=='CAMERA' and o.name.startswith('Camera_')),key=lambda o:o.name):
    mode=cam.get('drag');assert mode in ('pan','orbit','static'),(cam.name,'missing drag')
    q=cam.matrix_world.to_quaternion()
    f=unity(q@Vector((0,0,-1))).normalized();up=unity(q@Vector((0,1,0))).normalized()
    right=up.cross(f).normalized();up=f.cross(right).normalized()
    rotation=Matrix((right,up,f)).transposed().to_quaternion()
    # Native FBX import reports the film-height angle (validated against imported camera metadata).
    # Preserve the current importer's legacy horizontal-angle correction when applicable.
    imported_fov=math.degrees(2*math.atan(cam.data.sensor_height/(2*cam.data.lens)))
    fov=math.degrees(2*math.atan(math.tan(math.radians(imported_fov/2))/(16/9))) if abs(imported_fov-39.5978)<=.5 else imported_fov
    item=dict(Name=cam.name,Mode=mode,Position=list(unity(cam.matrix_world.translation)),Rotation=[rotation.x,rotation.y,rotation.z,rotation.w],Fov=fov,Near=cam.data.clip_start)
    pivot=scene.objects.get('OrbitPivot_'+cam.name.removeprefix('Camera_'))
    focus=scene.objects.get('FocusTarget_'+cam.name.removeprefix('Camera_'))
    if focus is not None:
        item['Pivot']=list(unity(focus.matrix_world.translation))
    elif mode=='orbit':
        assert pivot is not None,cam.name
        item['Pivot']=list(unity(pivot.matrix_world.translation))
    # Full assembly flattens collections; the importer scopes declarations to siblings.
    # Standalone author prefabs have no parent and retain their own collection.
    scope=list(cam.parent.children) if cam.parent is not None else list(cam.users_collection[0].objects)
    spaces=[o for o in scope if o.name.startswith('CameraTravelBounds_')]
    assert len(spaces)<=1,(cam.name,'multiple travel spaces')
    if spaces and not args.without_travel_space:
        space=spaces[0];lo=next((o for o in space.children if o.name.split('.')[0]=='_min'),None)
        hi=next((o for o in space.children if o.name.split('.')[0]=='_max'),None)
        assert lo is not None and hi is not None,space.name
        center=space.matrix_world@((lo.location+hi.location)*.5)
        half=(hi.location-lo.location)*.5
        assert min(half)>0,space.name
        mapping=Matrix(((-1,0,0),(0,0,1),(0,-1,0)))
        rotation=(mapping@space.matrix_world.to_3x3().normalized()@mapping.inverted()).to_quaternion()
        half=Vector((half.x*abs(space.matrix_world.to_scale().x),half.y*abs(space.matrix_world.to_scale().y),half.z*abs(space.matrix_world.to_scale().z)))
        item['Volume']=dict(Center=list(unity(center)),HalfSize=[half.x,half.z,half.y],Rotation=[rotation.x,rotation.y,rotation.z,rotation.w])
    cameras.append(item)
endpoint_parity=None
if args.unity_import:
    imported=json.loads(args.unity_import.read_text())
    lookup={r['name'].removesuffix('_VCam'):r for r in imported['cameras']}
    def q_input(c):return Quaternion((c['Rotation'][3],*c['Rotation'][:3]))
    def q_import(r):return Quaternion(tuple(r['rotation'][k] for k in ('w','x','y','z')))
    def p_import(r):return Vector(tuple(r['position'][k] for k in ('x','y','z')))
    first=cameras[0];reference=lookup[first['Name']]
    orientation=q_import(reference)@q_input(first).inverted()
    translation=p_import(reference)-orientation@Vector(first['Position'])
    errors=[]
    for c in cameras:
        row=lookup[c['Name']]
        position_error=(p_import(row)-(translation+orientation@Vector(c['Position']))).length
        angle_error=math.degrees((orientation@q_input(c)).rotation_difference(q_import(row)).angle)
        angle_error=min(angle_error,abs(360-angle_error))
        fov_error=abs(c['Fov']-row['fov'])
        assert position_error<.001 and angle_error<.1 and fov_error<.001,(c['Name'],position_error,angle_error,fov_error)
        errors.append(dict(camera=c['Name'],position_error=position_error,angle_error=angle_error,fov_error=fov_error))
    endpoint_parity=errors
(out/'cameras.json').write_text(json.dumps(dict(Cameras=cameras,Samples=args.samples,ModelScale=1,DefaultDuration=2),ensure_ascii=False,indent=2))
project=Path(__file__).with_name('CameraReview.csproj')
subprocess.run([args.dotnet,'run','--project',str(project),'--',str(out/'cameras.json'),str(out/'routes.json')],check=True)
routes=json.loads((out/'routes.json').read_text())['Routes']
# Evaluated scene instances include nested geometry. Inspect rendered geometry, excluding preview/proxy/outline.
deps= bpy.context.evaluated_depsgraph_get();trees=[]
for instance in deps.object_instances:
    ob=instance.object
    if ob.type!='MESH' or ob.hide_render or ob.get('preview_only') or ob.get('outline')=='proxy' or ob.name.startswith('描线'):continue
    mesh=ob.to_mesh()
    try:
        vertices=[instance.matrix_world@v.co for v in mesh.vertices]
        if vertices and len(mesh.polygons):trees.append((ob.name,BVHTree.FromPolygons(vertices,[list(p.vertices) for p in mesh.polygons])))
    finally:ob.to_mesh_clear()
def hit(a,b):
    delta=b-a
    if delta.length<1e-6:return []
    return [name for name,tree in trees if tree.ray_cast(a,delta.normalized(),delta.length)[0] is not None]
risks=[]
for route in routes:
    crossings=[];near_hits=[];previous=None;previous_corners=None
    for frame in route['Frames']:
        p=blender(frame['Position']);f=blender(frame['Forward']).normalized();up=blender(frame['Up']).normalized()
        right=f.cross(up).normalized();center=p+f*frame['Near']
        half_h=math.tan(math.radians(frame['Fov']/2))*frame['Near'];half_w=half_h*16/9
        corners=[center+right*x*half_w+up*y*half_h for x,y in ((-1,-1),(1,-1),(1,1),(-1,1))]
        if previous is not None:
            names=hit(previous,p)
            if names:crossings.append(dict(progress=frame['Progress'],objects=names))
        # Near-plane perimeter, camera-to-corner rays, and swept corner trajectories.
        names=set()
        for i,c in enumerate(corners):
            names.update(hit(p,c));names.update(hit(c,corners[(i+1)%4]))
            if previous_corners:names.update(hit(previous_corners[i],c))
        if names:near_hits.append(dict(progress=frame['Progress'],objects=sorted(names)))
        previous=p;previous_corners=corners
    risks.append(dict(source=route['From'],target=route['To'],path=route['Path'],crossfade=route['Crossfade'],crossings=crossings,near_plane_hits=near_hits,start_interest=route['StartInterest'],target_interest=route['TargetInterest']))
report=dict(travel_spaces_omitted=args.without_travel_space,endpoint_parity=endpoint_parity,source=bpy.data.filepath,source_sha256=hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),math_sha256=hashlib.sha256((Path(__file__).resolve().parents[2]/'UnityClient/Assets/Scripts/Runtime/FocusTravelMath.cs').read_bytes()).hexdigest(),seconds=time.monotonic()-start_time,meshes=len(trees),routes=risks,
    limitations='Authored endpoints only; evaluated Blender visibility, no runtime presence/drag/stage changes. Sampled near-plane tests are not continuous volume collision. Subject visibility requires a semantic subject, not a ground-ray hit.')
(out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
# SVG overview: actual selected centre paths, walls/background mesh bounds, interests.
points=[blender(frame['Position']) for route in routes for frame in route['Frames']]
minx=min(p.x for p in points)-1;maxx=max(p.x for p in points)+1;miny=min(p.y for p in points)-1;maxy=max(p.y for p in points)+1
w=280;h=330;scale=min((w-25)/(maxx-minx),(h-55)/(maxy-miny))
def xy(p,ox,oy):return ox+12+(p.x-minx)*scale,oy+40+(maxy-p.y)*scale
source_name='Camera_'+Path(bpy.data.filepath).stem
if source_name not in {c['Name'] for c in cameras}:source_name=cameras[0]['Name']
selected=[(r,risk) for r,risk in zip(routes,risks) if r['From']==source_name]
rows=max(1,math.ceil(len(selected)/3))
svg=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{w*3}" height="{h*rows}" viewBox="0 0 {w*3} {h*rows}"><rect width="100%" height="100%" fill="#eeeae1"/>']
for idx,(route,risk) in enumerate(selected):
    ox=idx%3*w;oy=idx//3*h
    label='dissolve' if route['Crossfade'] else route['Path']
    svg.append(f'<text x="{ox+12}" y="{oy+17}" font-size="12">{route["To"]}</text><text x="{ox+12}" y="{oy+32}" font-size="11">{label}</text>')
    for ob in scene.objects:
        if ob.type!='MESH' or '墙' not in ob.name:continue
        corners=[ob.matrix_world@Vector(v) for v in ob.bound_box]
        a=xy(Vector((min(p.x for p in corners),max(p.y for p in corners),0)),ox,oy)
        b=xy(Vector((max(p.x for p in corners),min(p.y for p in corners),0)),ox,oy)
        svg.append(f'<rect x="{a[0]}" y="{a[1]}" width="{b[0]-a[0]}" height="{b[1]-a[1]}" fill="#444"/>')
    coords=' '.join(f'{x:.2f},{y:.2f}' for x,y in (xy(blender(f['Position']),ox,oy) for f in route['Frames']))
    dash=' stroke-dasharray="4 3"' if route['Crossfade'] else ''
    svg.append(f'<polyline points="{coords}" fill="none" stroke="#b23e35" stroke-width="2"{dash}/>')
    for label,key in [('A','StartInterest'),('B','TargetInterest')]:
        x,y=xy(blender(route[key]),ox,oy);svg.append(f'<circle cx="{x}" cy="{y}" r="3" fill="#257c91"/><text x="{x+4}" y="{y}" font-size="10">{label}</text>')
svg.append('</svg>');(out/'overview.svg').write_text(''.join(svg))
print(json.dumps(dict(routes=len(routes),crossing_routes=sum(bool(r['crossings']) and not r['crossfade'] for r in risks),near_plane_routes=sum(bool(r['near_plane_hits']) and not r['crossfade'] for r in risks),crossfade_routes=sum(r['crossfade'] for r in risks),seconds=report['seconds']),ensure_ascii=False))
