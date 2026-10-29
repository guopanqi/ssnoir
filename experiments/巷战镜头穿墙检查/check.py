"""Blender-only diagnostic approximation; not a capture of the Unity runtime.
Run: blender -b city-box/prefabs/巷子里在打人.blend --python experiments/巷战镜头穿墙检查/check.py
Checks structural geometry only, between authored cameras, with no player offsets.
"""
import bpy, math, json, time
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
started=time.monotonic()
root=Path(__file__).resolve().parent
scene=bpy.context.scene
walls=[o for o in scene.objects if o.type=='MESH' and any(o.name.startswith(p) for p in ('酒馆侧墙','对面墙','巷尾墙'))]
trees=[]
for ob in walls:
    trees.append((ob.name,BVHTree.FromPolygons([ob.matrix_world@v.co for v in ob.data.vertices],[list(p.vertices) for p in ob.data.polygons])))
def hits(a,b):
    d=b-a
    if d.length<1e-7:return []
    return [name for name,tree in trees if tree.ray_cast(a,d.normalized(),d.length)[0] is not None]
def interest(cam):
    p=cam.matrix_world.translation; f=cam.matrix_world.to_quaternion()@Vector((0,0,-1))
    assert f.z < -1e-5,cam.name
    return p+f*(-p.z/f.z)
def polar(v):return math.atan2(v.x,v.y),math.atan2(v.z,math.hypot(v.x,v.y)),v.length
def sample(a,b,t,kind):
    pa,pb=a.matrix_world.translation,b.matrix_world.translation
    if kind=='direct':
        p=pa.lerp(pb,t);p.z+=math.sin(math.pi*t)*(pb-pa).length*.04;return p
    ia,ib=interest(a),interest(b)
    ya,pi,ra=polar(pa-ia);yb,pj,rb=polar(pb-ib)
    y=ya+((yb-ya+math.pi)%(2*math.pi)-math.pi)*t;p=pi+(pj-pi)*t;r=ra+(rb-ra)*t
    return ia.lerp(ib,t)+Vector((math.sin(y)*math.cos(p)*r,math.cos(y)*math.cos(p)*r,math.sin(p)*r))
cams=sorted([o for o in scene.objects if o.type=='CAMERA'],key=lambda o:o.name)
results=[]
for a in cams:
 for b in cams:
  if a==b:continue
  for kind in ('direct','arc'):
   crossings=[];blocked=[];prev=sample(a,b,0,kind)
   for i in range(101):
    t=i/100;p=sample(a,b,t,kind)
    h=hits(prev,p)
    if h:crossings.append({'progress':t,'walls':h,'position':list(p)})
    h=hits(p,interest(b))
    if h:blocked.append(t)
    prev=p
   if crossings or blocked:results.append({'from':a.name,'to':b.name,'path':kind,'crossings':crossings,'target_ground_occluded_samples':len(blocked)})
report={'scope':'Authored camera endpoints; approximate position curves; structural walls only. Ground-interest visibility is not actor visibility. No runtime path selection or drag state captured.','cameras':{o.name:list(o.matrix_world.translation) for o in cams},'wall_objects':len(walls),'seconds_after_load':time.monotonic()-started,'risks':results}
(root/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
print('CHECK_RESULT',json.dumps(report,ensure_ascii=False))
