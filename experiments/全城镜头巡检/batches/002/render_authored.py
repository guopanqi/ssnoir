import bpy,sys
from pathlib import Path
p=Path(__file__).parent;sys.path.insert(0,str(p.parents[3]/'city-box/pipeline'));import render as R
s=bpy.context.scene;s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=640;s.render.resolution_y=360;s.render.resolution_percentage=100;s.render.use_compositing=False;s.render.use_sequencer=False;s.display.shading.color_type='OBJECT'
for name in ['布告栏','货运公司','老街酒馆']:
 cam=bpy.data.objects['Camera_'+name];top=R._top_root(cam);R.set_focus(s,top);s.use_nodes=False
 for ob in s.objects:
  if ob.type=='MESH':ob.color=(.88,.48,.15,1) if R._top_root(ob)==top else (.45,.48,.53,1)
  if ob.get('preview_only'):ob.hide_render=True
 cam.data.sensor_fit='VERTICAL';s.camera=cam;s.render.filepath=str(p/('authored-'+name+'.png'));bpy.ops.render.render(write_still=True)
