"""Full-assembly body projection, center path and near-plane checks for 005's six routes."""
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
root_names={'联合起吊','码头'}
scene=bpy.context.scene; deps=bpy.context.evaluated_depsgraph_get()
started=time.monotonic()
letterbox_bar_fraction=0.128

def owner_name(ob):
    cur=ob
    while cur is not None:
        if cur.name in root_names:return cur.name
        cur=cur.parent
    return None

def unity(v):return Vector((-v.x,v.z,-v.y))
def blender(v):return Vector((-v[0],-v[2],v[1]))
def vec(v):return Vector(tuple(float(x) for x in v))
def bbox_segment_candidates(a,b,mins,maxs):
    d=np.asarray(b-a,dtype=np.float64);o=np.asarray(a,dtype=np.float64)
    parallel=np.abs(d)<1e-10;inv=np.zeros(3,dtype=np.float64);inv[~parallel]=1.0/d[~parallel]
    t1=(mins-o)*inv;t2=(maxs-o)*inv;tlo=np.minimum(t1,t2);thi=np.maximum(t1,t2)
    inside=(o>=mins)&(o<=maxs)
    tlo[:,parallel]=np.where(inside[:,parallel],-np.inf,np.inf)
    thi[:,parallel]=np.where(inside[:,parallel],np.inf,-np.inf)
    near=np.max(tlo,axis=1);far=np.min(thi,axis=1)
    return np.flatnonzero((near<=far)&(far>=0)&(near<=1))

trees=[];mins=np.empty((0,3));maxs=np.empty((0,3));names=[];owners=[];body={}

def build_state_geometry():
    global trees,mins,maxs,names,owners,body,deps
    deps=bpy.context.evaluated_depsgraph_get()
    trees=[];mins=[];maxs=[];names=[];owners=[]
    for instance in deps.object_instances:
        ob=instance.object
        if ob.type!='MESH' or ob.hide_render or ob.get('preview_only') or ob.get('outline')=='proxy' or ob.name.startswith('描线'):
            continue
        mesh=ob.to_mesh()
        try:
            world=[instance.matrix_world@v.co for v in mesh.vertices]
            faces=[list(poly.vertices) for poly in mesh.polygons]
            if not world or not faces:continue
            trees.append(BVHTree.FromPolygons(world,faces))
            mins.append([min(v[i] for v in world) for i in range(3)])
            maxs.append([max(v[i] for v in world) for i in range(3)])
            names.append(ob.name);owners.append(owner_name(ob))
        finally:ob.to_mesh_clear()
    mins=np.asarray(mins,dtype=np.float64);maxs=np.asarray(maxs,dtype=np.float64)

    body={}
    for root_name in sorted(root_names):
        points=[];mesh_names=[]
        for instance in deps.object_instances:
            ob=instance.object
            subject_name=(ob.name.startswith('重机_') if root_name=='联合起吊' else ob.name.startswith('货船_'))
            if owner_name(ob)!=root_name or not subject_name or ob.type!='MESH' or ob.hide_render or ob.get('preview_only') or ob.get('outline')=='proxy' or ob.name.startswith('描线'):
                continue
            mesh=ob.to_mesh()
            try:
                if len(mesh.vertices)<4:continue
                points.extend(tuple(unity(instance.matrix_world@v.co)) for v in mesh.vertices)
                mesh_names.append(ob.name)
            finally:ob.to_mesh_clear()
        array=np.asarray(points,dtype=np.float64).reshape((-1,3))
        body[root_name]={'points':array,'sample_indices':np.linspace(0,len(array)-1,min(32,len(array)),dtype=int) if len(array) else np.asarray([],dtype=int),
                         'mesh_count':len(mesh_names),'vertex_count':len(array),
                         'bounds_min':array.min(axis=0).tolist() if len(array) else None,
                         'bounds_max':array.max(axis=0).tolist() if len(array) else None,
                         'mesh_names':mesh_names}

def segment_hits(a,b):
    delta=b-a;length=delta.length
    if length<1e-6:return []
    direction=delta/length;found=[]
    for i in bbox_segment_candidates(a,b,mins,maxs):
        hit=trees[i].ray_cast(a,direction,length)
        if hit[0] is not None:found.append((names[i],owners[i]))
    return sorted(set(found))

def frame_pose(frame):
    return vec(frame['Position']),vec(frame['Forward']).normalized(),vec(frame['Up']).normalized()
def project(frame,points):
    p,f,u=frame_pose(frame);right=u.cross(f).normalized();u=f.cross(right).normalized()
    delta=points-np.asarray(p,dtype=np.float64);fa=np.asarray(f);ra=np.asarray(right);ua=np.asarray(u)
    depth=delta@fa;valid=depth>max(.01,float(frame['Near']))
    tan_v=math.tan(math.radians(float(frame['Fov'])/2))
    x=np.full(len(points),np.nan);y=np.full(len(points),np.nan)
    x[valid]=(delta[valid]@ra)/(depth[valid]*tan_v*16/9)
    y[valid]=(delta[valid]@ua)/(depth[valid]*tan_v)
    y_screen=(y+1)*.5
    inside=valid&(np.abs(x)<=1)&(y_screen>=letterbox_bar_fraction)&(y_screen<=1-letterbox_bar_fraction)
    return x,y,depth,inside

def body_sample(frame,root_name):
    data=body[root_name];points=data['points'];sampled=points[data['sample_indices']]
    if len(points)==0:return {'status':'no exterior mesh in compiled root; body projection unavailable'}
    x,y,z,inside=project(frame,sampled)
    valid=np.isfinite(x)&np.isfinite(y)
    ids=np.flatnonzero(inside);blocked={}
    p=blender(frame['Position'])
    for ix in ids:
        target=blender(sampled[ix]);d=target-p
        if d.length<1e-6:continue
        d.normalize()
        nearest=None;nearest_dist=(target-p).length+1e-6
        for j in bbox_segment_candidates(p,target,mins,maxs):
            if owners[j]==root_name:continue
            hit=trees[j].ray_cast(p,d,nearest_dist)
            if hit[0] is not None and hit[3] is not None and hit[3]<nearest_dist:
                nearest_dist=hit[3];nearest=(names[j],owners[j])
        if nearest:blocked[nearest[0]]=blocked.get(nearest[0],0)+1
    return {'sampled_body_vertices':int(len(sampled)),'sampled_in_front_fraction':float(valid.mean()),
            'in_frame_fraction':float(inside.mean()),
            'visible_mesh_points_tested':int(len(ids)),
            'other_root_blockers':dict(sorted(blocked.items(),key=lambda kv:(-kv[1],kv[0]))[:8]),
            'ndc_bounds_p05_p95':None if not valid.any() else {
                'x':[float(np.quantile(x[valid],.05)),float(np.quantile(x[valid],.95))],
                'y':[float(np.quantile(y[valid],.05)),float(np.quantile(y[valid],.95))]},
            'median_depth':None if not valid.any() else float(np.median(z[valid]))}

geometry=[];frame_indices=[0,30,60,90,120]
state_by_route={
 ('Camera_联合起吊','Camera_联合起吊-起吊'):{'clip':'重机__Hold','frame':1},
 ('Camera_联合起吊-起吊','Camera_联合起吊'):{'clip':'重机__Suspended','frame':36},
 ('Camera_联合起吊','Camera_联合起吊-落地'):{'clip':'重机__Suspended','frame':36},
 ('Camera_联合起吊-落地','Camera_联合起吊'):{'clip':'重机__Landed','frame':71},
 ('Camera_码头','Camera_码头-靠岸'):{'clip':'货船__Offshore','frame':1},
 ('Camera_码头-靠岸','Camera_码头'):{'clip':'货船__Berthed','frame':36}}
state_mesh_counts={}
for route in routes_data['Routes']:
    source=route['From'];target=route['To']
    state=state_by_route[(source,target)]
    scene.frame_set(state['frame']);build_state_geometry()
    state_mesh_counts.setdefault(state['clip'],len(trees))
    target_cam=scene.objects.get(target)
    if target_cam is None:raise RuntimeError(f'Missing target camera {target}')
    target_root=owner_name(target_cam)
    if target_root not in root_names:raise RuntimeError(f'Cannot resolve target root for {target}')
    center_crossings=[];near_hits=[]
    previous=None;previous_corners=None
    for frame in route['Frames']:
        p=blender(frame['Position']);f=blender(frame['Forward']).normalized();up=blender(frame['Up']).normalized()
        right=f.cross(up).normalized();center=p+f*frame['Near']
        hh=math.tan(math.radians(frame['Fov']/2))*frame['Near'];hw=hh*16/9
        corners=[center+right*x*hw+up*y*hh for x,y in ((-1,-1),(1,-1),(1,1),(-1,1))]
        if previous is not None:
            hits=segment_hits(previous,p)
            if hits:center_crossings.append({'progress':frame['Progress'],'objects':[{'name':n,'owner':o} for n,o in hits[:12]]})
        touched=set()
        for i,c in enumerate(corners):
            touched.update(segment_hits(p,c));touched.update(segment_hits(c,corners[(i+1)%4]))
            if previous_corners:touched.update(segment_hits(previous_corners[i],c))
        if touched:
            near_hits.append({'progress':frame['Progress'],'objects':[{'name':n,'owner':o} for n,o in sorted(touched)[:12]]})
        previous=p;previous_corners=corners
    framing=[]
    for idx in frame_indices:
        frame=route['Frames'][idx]
        framing.append({'progress':frame['Progress'],'camera_position':frame['Position'],
                        'target_interest':route['TargetInterest'],'target_root':target_root,
                        'body_projection':body_sample(frame,target_root)})
    geometry.append({'from':source,'to':target,'target_root':target_root,'clip_state':state['clip'],'blender_frame':state['frame'],'path':route['Path'],
                     'duration':route['Duration'],'crossfade':route['Crossfade'],
                     'center_path_crossings':center_crossings,'near_plane_hits':near_hits,
                     'body_samples':framing})

elapsed=time.monotonic()-started
out={'source':str(bpy.data.filepath),'source_sha256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),
     'routes_sha256':hashlib.sha256((batch/'routes.json').read_bytes()).hexdigest(),
     'elapsed_seconds':elapsed,'evaluated_mesh_count':len(trees),'roots':{n:{k:v for k,v in d.items() if k not in ('points','sample_indices')} for n,d in body.items()},
     'letterbox_visible_y_screen_fraction':[letterbox_bar_fraction,1-letterbox_bar_fraction],
     'geometry_scope':'Complete compiled city assembly; one BVH per evaluated render-visible mesh after preview/proxy/outline exclusions, rebuilt at each exact steady animation clip state used for each route. Camera path checks sample all 120 Program frames. Body bounds use actual motion subject meshes (重机_* for 联合起吊; 货船_* for 码头), with up to 32 representative vertices and external rays at 0/25/50/75/100 percent.',
     'clip_state_mesh_counts':state_mesh_counts,
     'routes':geometry,
     'limitations':'For each route, compiled animation is evaluated only at its corresponding steady state (Hold, Suspended, Landed, Offshore, or Berthed); transition-clip frames and Unity runtime visibility are not tested. Near-plane checks sweep camera center and near-plane edges across sampled route frames; not a continuous proof. Body projection samples up to 32 representative subject mesh vertices at the steady state and applies the letterbox visible region; the independent all-vertex animation projection is recorded by the root agent and neither check replaces final rendered image review.'}
(batch/'geometry_report.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'meshes':len(trees),'routes':len(geometry),
 'center_crossing_routes':sum(bool(x['center_path_crossings']) for x in geometry),
 'near_plane_routes':sum(bool(x['near_plane_hits']) for x in geometry),
 'seconds':elapsed},ensure_ascii=False))
