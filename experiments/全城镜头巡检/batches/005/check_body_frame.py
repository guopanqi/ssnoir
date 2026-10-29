import bpy,json
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
p=Path(__file__).parent;root=p.parents[3];scene=bpy.context.scene
layout={r['name']:r for r in json.loads((root/'city-box/build/city/places/联合起吊.clips.json').read_text())['clips']}
body=bpy.data.objects['重机_机体'];rows=[]
scene.render.resolution_x=640;scene.render.resolution_y=360;scene.render.resolution_percentage=100
for name,suffix in [('重机__Rise','起吊'),('重机__Lower','落地')]:
 cam=bpy.data.objects['Camera_联合起吊-'+suffix];cam.data.sensor_fit='VERTICAL'
 clip=layout[name]
 for i in range(clip['end']-clip['start']+1):
  frame=clip['start']+i;scene.frame_set(frame)
  evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
  v=[world_to_camera_view(scene,cam,evaluated.matrix_world@Vector(corner)) for corner in evaluated.bound_box]
  rows.append(dict(clip=name,progress=i/(clip['end']-clip['start']),frame=frame,minx=min(v.x for v in v),maxx=max(v.x for v in v),miny=min(v.y for v in v),maxy=max(v.y for v in v)))
(p/'body-framing-full.json').write_text(json.dumps(rows,indent=2))
print([r for r in rows if r['minx']<0 or r['maxx']>1 or r['miny']<0 or r['maxy']>1])
