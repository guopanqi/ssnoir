"""Geometry-only review frames from shared runtime poses. No source files saved."""
import bpy,sys,json,math
from pathlib import Path
from mathutils import Vector,Matrix
root=Path(__file__).resolve().parents[4]
sys.path.insert(0,str(root/'city-box/pipeline'))
import render as R
batch=Path(__file__).parent
routes=json.loads((batch/'render-input/routes.json').read_text())['Routes']
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.render.resolution_x=640;scene.render.resolution_y=360;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.use_nodes=False
scene.render.use_sequencer=False;scene.render.use_compositing=False
shade=scene.display.shading;shade.light='STUDIO';shade.color_type='OBJECT';shade.show_shadows=True;shade.show_cavity=True;shade.cavity_type='BOTH';shade.background_type='WORLD'
scene.world.color=(.055,.055,.065)
scene.view_settings.view_transform='Standard'
data=bpy.data.cameras.new('_audit');cam=bpy.data.objects.new('_audit',data);scene.collection.objects.link(cam);scene.camera=cam
data.sensor_fit='VERTICAL';data.sensor_height=24;data.clip_end=10000
out=batch/'frames-geometry';out.mkdir(exist_ok=True)
def B(v):return Vector((-v[0],-v[2],v[1]))
manifest=[]
for route in routes:
 if route['From']!='Camera_世界':continue
 name=route['To'].removeprefix('Camera_');target=bpy.data.objects.get(route['To']);top=R._top_root(target)
 for ob in scene.objects:
  if ob.type=='MESH':ob.color=(.88,.48,.15,1) if R._top_root(ob)==top else (.45,.48,.53,1)
 for progress in [0,.25,.5,.75,1]:
  f=min(route['Frames'],key=lambda f:abs(f['Progress']-progress))
  R.set_focus(scene,None if progress==0 else top);scene.use_nodes=False
  for ob in scene.objects:
   if ob.get('preview_only'):ob.hide_render=True
  data.lens=12/math.tan(math.radians(f['Fov']/2));data.clip_start=f['Near']
  forward=B(f['Forward']).normalized();up=B(f['Up']).normalized();right=forward.cross(up).normalized();up=right.cross(forward)
  cam.location=B(f['Position']);cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler()
  path=out/(name+'-'+str(round(progress*100))+'.png');scene.render.filepath=str(path)
  bpy.ops.render.render(write_still=True)
  manifest.append(dict(place=name,progress=progress,path=str(path)))
(batch/'frames.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
