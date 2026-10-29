"""Reuse one full-scene mesh BVH to inspect all selected world/landmark routes.

Run from the repository root:
  blender -b city-box/build/city/city_build.blend --python-exit-code 1 \
    --python experiments/全城镜头巡检/batches/002/audit_geometry.py -- \
    --batch experiments/全城镜头巡检/batches/002
"""
import argparse, hashlib, json, math, sys, time
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

parser=argparse.ArgumentParser()
parser.add_argument('--batch',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
batch=args.batch.resolve(); root=Path(__file__).resolve().parents[4]
routes_data=json.loads((batch/'routes.json').read_text())
selection=json.loads((batch/'selected_landmarks.json').read_text())
place_names=[r['place'] for r in selection['places']]
root_names=set(place_names+['世界'])
scene=bpy.context.scene; deps=bpy.context.evaluated_depsgraph_get()
started=time.monotonic()

def owner_name(ob):
    cur=ob
    while cur is not None:
        if cur.name in root_names: return cur.name
        cur=cur.parent
    return None

def unity(v): return Vector((-v.x,v.z,-v.y))
def blender(v): return Vector((-v[0],-v[2],v[1]))
def vec(v): return Vector((float(v[0]),float(v[1]),float(v[2])))
def bbox_segment_candidates(a,b,mins,maxs):
    d=np.asarray(b-a,dtype=np.float64)
    o=np.asarray(a,dtype=np.float64)
    parallel=np.abs(d)<1e-10
    inv=np.zeros(3,dtype=np.float64)
    inv[~parallel]=1.0/d[~parallel]
    t1=(mins-o)*inv; t2=(maxs-o)*inv
    tlo=np.minimum(t1,t2); thi=np.maximum(t1,t2)
    inside=(o>=mins)&(o<=maxs)
    tlo[:,parallel]=np.where(inside[:,parallel],-np.inf,np.inf)
    thi[:,parallel]=np.where(inside[:,parallel],np.inf,-np.inf)
    near=np.max(tlo,axis=1); far=np.min(thi,axis=1)
    return np.flatnonzero((near<=far)&(far>=0.0)&(near<=1.0))

# Evaluate every mesh once, retaining full assembled geometry and object ownership.
trees=[]; mins=[]; maxs=[]; names=[]; owners=[]; hidden_layers=[]
for instance in deps.object_instances:
    ob=instance.object
    if ob.type!='MESH' or ob.hide_render or ob.get('preview_only') or ob.get('outline')=='proxy' or ob.name.startswith('描线'):
        continue
    mesh=ob.to_mesh()
    try:
        world=[instance.matrix_world@v.co for v in mesh.vertices]
        faces=[list(p.vertices) for p in mesh.polygons]
        if not world or not faces: continue
        tree=BVHTree.FromPolygons(world,faces)
        mn=[min(v[i] for v in world) for i in range(3)]; mx=[max(v[i] for v in world) for i in range(3)]
        trees.append(tree); mins.append(mn); maxs.append(mx); names.append(ob.name)
        owners.append(owner_name(ob)); hidden_layers.append(ob.get('layer')=='interior')
    finally:
        ob.to_mesh_clear()
mins=np.asarray(mins,dtype=np.float64); maxs=np.asarray(maxs,dtype=np.float64)

def nearest_hit(a,b,exclude_owner=None):
    delta=b-a; length=delta.length
    if length<1e-6: return None
    direction=delta/length; nearest=None; nearest_dist=length+1e-6
    for i in bbox_segment_candidates(a,b,mins,maxs):
        if exclude_owner is not None and owners[i]==exclude_owner: continue
        hit=trees[i].ray_cast(a,direction,nearest_dist)
        if hit[0] is not None and hit[3] is not None and hit[3]<nearest_dist:
            nearest_dist=hit[3]; nearest=(names[i],owners[i],hidden_layers[i],float(hit[3]))
    return nearest

def segment_hits(a,b):
    delta=b-a; length=delta.length
    if length<1e-6: return []
    direction=delta/length; found=[]
    for i in bbox_segment_candidates(a,b,mins,maxs):
        hit=trees[i].ray_cast(a,direction,length)
        if hit[0] is not None: found.append(names[i])
    return sorted(set(found))

# Actual exterior geometry per city root, excluding objects declared interior-only.
dep_cache={}; subject_data={}
for place in place_names:
    root_obj=scene.objects.get(place)
    if root_obj is None: raise RuntimeError(f'Missing root: {place}')
    descendants=[]; todo=list(root_obj.children)
    while todo:
        obj=todo.pop(); descendants.append(obj); todo.extend(obj.children)
    object_records=[]; all_vertices=[]
    for obj in descendants:
        if obj.type!='MESH' or obj.hide_render or obj.get('preview_only') or obj.get('outline')=='proxy' or obj.name.startswith('描线') or obj.get('layer')=='interior':
            continue
        evaluated=obj.evaluated_get(deps)
        mesh=evaluated.to_mesh()
        try:
            verts=[obj.matrix_world@v.co for v in mesh.vertices]
            if len(verts)<4: continue
            # Subject projection uses the same Unity-space route frames as Program.cs.
            coords=np.asarray([tuple(unity(v)) for v in verts],dtype=np.float64)
            lo=coords.min(axis=0); hi=coords.max(axis=0)
            dims=hi-lo; area=float(sorted(dims)[1]*sorted(dims)[2])
            object_records.append({'name':obj.name,'vertices':coords,'bounds':[lo.tolist(),hi.tolist()],
                                   'dimensions':dims.tolist(),'projected_area_rank':area,'vertex_count':len(coords)})
            all_vertices.append(coords)
        finally:
            evaluated.to_mesh_clear()
    if not all_vertices: raise RuntimeError(f'No world-visible exterior mesh under {place}')
    points=np.concatenate(all_vertices,axis=0)
    sample_count=min(32,len(points)); sample_idx=np.linspace(0,len(points)-1,sample_count,dtype=int)
    representative=points[sample_idx]
    top_objects=sorted(object_records,key=lambda x:x['projected_area_rank'],reverse=True)[:5]
    subject_data[place]={'points':points,'representative':representative,'objects':object_records,'top_objects':top_objects}

def frame_pose(frame):
    return vec(frame['Position']),vec(frame['Forward']).normalized(),vec(frame['Up']).normalized()
def project_points(frame,points):
    position,forward,up=frame_pose(frame)
    right=up.cross(forward).normalized(); up=forward.cross(right).normalized()
    delta=points-np.asarray(position,dtype=np.float64)
    f=np.asarray(forward,dtype=np.float64); r=np.asarray(right,dtype=np.float64); u=np.asarray(up,dtype=np.float64)
    depth=delta@f
    valid=depth>max(0.01,float(frame['Near']))
    tan_v=math.tan(math.radians(float(frame['Fov'])/2))
    ndcx=np.full(len(points),np.nan); ndcy=np.full(len(points),np.nan)
    ndcx[valid]=(delta[valid]@r)/(depth[valid]*tan_v*16/9)
    ndcy[valid]=(delta[valid]@u)/(depth[valid]*tan_v)
    inside=valid&(np.abs(ndcx)<=1)&(np.abs(ndcy)<=1)
    return ndcx,ndcy,depth,inside

def projected_summary(frame,data):
    x,y,z,inside=project_points(frame,data['points'])
    valid=np.isfinite(x)&np.isfinite(y)
    if not valid.any():
        return {'in_front_fraction':0,'in_frame_fraction':0,'ndc_bounds':None,'median_depth':None}
    xv=x[valid]; yv=y[valid]
    q=lambda a:[float(np.quantile(a,.05)),float(np.quantile(a,.95))]
    return {'in_front_fraction':float(valid.mean()),'in_frame_fraction':float(inside.mean()),
            'ndc_bounds_p05_p95':{'x':q(xv),'y':q(yv)},
            'median_depth':float(np.median(z[valid]))}

def subject_occlusion(frame,place,data):
    p,forward,up=frame_pose(frame)
    x,y,z,inside=project_points(frame,data['representative'])
    ids=np.flatnonzero(inside)
    visible=0; blockers={}; checked=0
    for ix in ids:
        target=vec(data['representative'][ix]); hit=nearest_hit(blender(p),blender(target),exclude_owner=place); checked+=1
        if hit is None: visible+=1
        else: blockers[hit[0]]=blockers.get(hit[0],0)+1
    return {'sampled_surface_points':len(data['representative']),'in_frame_points':int(len(ids)),
            'tested_in_frame_points':checked,'unblocked_by_other_meshes':visible,
            'external_blockers':dict(sorted(blockers.items(),key=lambda x:(-x[1],x[0]))[:8])}

# Confirm destination cameras and report the authored main subject proxies/bounds.
framing={}
for place in place_names:
    root_obj=scene.objects[place]
    cam=scene.objects['Camera_'+place]
    data=subject_data[place]
    frames=[]
    for idx in (0,30,60,90,120):
        route=next(r for r in routes_data['Routes'] if r['From']=='Camera_世界' and r['To']==cam.name)
        frame=route['Frames'][idx]
        frames.append({'progress':frame['Progress'],'camera_position':frame['Position'],
                       'focus_interest':route['TargetInterest'],'body_projection':projected_summary(frame,data),
                       'external_occlusion':subject_occlusion(frame,place,data)})
    framing[place]={'camera':cam.name,'root_position':list(unity(root_obj.matrix_world.translation)),
                    'world_visible_mesh_count':len(data['objects']),'world_visible_vertex_count':len(data['points']),
                    'largest_exterior_meshes':[{'name':x['name'],'dimensions':x['dimensions'],'projected_area_rank':x['projected_area_rank'],'vertices':x['vertex_count']} for x in data['top_objects']],
                    'samples':frames}

# Geometric path and near-plane checks on the full assembled mesh BVH.
geometry=[]
for route in routes_data['Routes']:
    source=route['From']; target=route['To']; crossings=[]; near_hits=[]
    previous=None; previous_corners=None
    for frame in route['Frames']:
        p=blender(frame['Position']); f=blender(frame['Forward']).normalized(); up=blender(frame['Up']).normalized()
        right=f.cross(up).normalized(); center=p+f*frame['Near']
        hh=math.tan(math.radians(frame['Fov']/2))*frame['Near']; hw=hh*16/9
        corners=[center+right*x*hw+up*y*hh for x,y in ((-1,-1),(1,-1),(1,1),(-1,1))]
        if previous is not None:
            hits=segment_hits(previous,p)
            if hits: crossings.append({'progress':frame['Progress'],'objects':hits[:12]})
        names=set()
        for i,c in enumerate(corners):
            names.update(segment_hits(p,c)); names.update(segment_hits(c,corners[(i+1)%4]))
            if previous_corners: names.update(segment_hits(previous_corners[i],c))
        if names: near_hits.append({'progress':frame['Progress'],'objects':sorted(names)[:12]})
        previous=p; previous_corners=corners
    geometry.append({'from':source,'to':target,'path':route['Path'],'crossfade':route['Crossfade'],
                     'crossings':crossings,'near_plane_hits':near_hits})

out={'source':str(bpy.data.filepath),'source_sha256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),
     'evaluated_mesh_count':len(trees),'geometry_scope':'all evaluated meshes from complete city build; same review.py exclusions for render-hidden, preview, proxy and generated outline meshes; no dynamic/runtime presence filtering',
     'geometry_routes':geometry,'landmark_body_framing':framing,
     'limitations':'Routes are authored-center samples, not arbitrary player-pan starts. Geometry checks sample camera center, near-plane perimeter and corner sweeps; not a continuous proof. Body bounds use actual descendant exterior meshes with layer=interior excluded; external ray checks use up to 32 surface vertices per landmark and do not replace Blender image review or Unity visibility.'}
(batch/'geometry_report.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'meshes':len(trees),'routes':len(geometry),'crossing_routes':sum(bool(x['crossings']) for x in geometry),'near_plane_routes':sum(bool(x['near_plane_hits']) for x in geometry),'seconds':time.monotonic()-started},ensure_ascii=False))
