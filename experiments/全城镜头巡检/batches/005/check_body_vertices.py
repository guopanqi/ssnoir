"""Project every vertex of the rigid machine body on every baked animation frame."""
import bpy,json,math,numpy as np
from pathlib import Path
p=Path(__file__).parent;root=p.parents[3];scene=bpy.context.scene
layout={r['name']:r for r in json.loads((root/'city-box/build/city/places/联合起吊.clips.json').read_text())['clips']}
body=bpy.data.objects['重机_机体'];assert not body.modifiers and not body.data.shape_keys
coords=np.ones((len(body.data.vertices),4));coords[:,:3]=[v.co[:] for v in body.data.vertices];rows=[]
for name,suffix in [('重机__Rise','起吊'),('重机__Lower','落地')]:
 cam=bpy.data.objects['Camera_联合起吊-'+suffix];clip=layout[name];tan=cam.data.sensor_height/(2*cam.data.lens)
 for frame in range(clip['start'],clip['end']+1):
  scene.frame_set(frame);view=np.asarray(cam.matrix_world.inverted()@body.matrix_world,dtype=float);local=coords@view.T;depth=-local[:,2]
  assert np.all(depth>0)
  x=.5+.5*local[:,0]/(depth*tan*(16/9));y=.5+.5*local[:,1]/(depth*tan)
  rows.append(dict(clip=name,frame=frame,minx=float(x.min()),maxx=float(x.max()),miny=float(y.min()),maxy=float(y.max())))
(p/'body-framing-vertices.json').write_text(json.dumps(rows,indent=2))
bar=(1-(16/9)/2.39)/2
bad=[r for r in rows if r['minx']<0 or r['maxx']>1 or r['miny']<bar or r['maxy']>1-bar]
print('FRAMING',len(rows),'frames',len(bad),'clipped within letterbox','maxy',max(r['maxy'] for r in rows))
assert not bad,bad[:2]
