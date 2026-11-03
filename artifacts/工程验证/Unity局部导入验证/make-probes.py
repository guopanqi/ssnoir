import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[3]/'.cache/import-dependency-check'
assert (root/'.import-dependency-check').exists()
for relative,lightname in [('Assets/Resources/Models/Environment/City.fbx','ProbeCityLight'),('Assets/Resources/City/Places/ProbeA.fbx','ProbeALight'),('Assets/Resources/City/Places/ProbeB.fbx',None),('ProbeA-updated.fbx','ProbeALight')]:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.mesh.primitive_cube_add()
 o=bpy.context.object;o.name='河面' if relative.endswith('/City.fbx') else 'ProbeCube'
 if relative=='ProbeA-updated.fbx':o.scale=(2,1,1)
 m=bpy.data.materials.new('M_世界_主体');o.data.materials.append(m)
 if lightname:
  d=bpy.data.lights.new(lightname,'POINT');d.energy=100
  light=bpy.data.objects.new(lightname,d);bpy.context.scene.collection.objects.link(light);light.location=(0,0,2)
 p=root/relative;p.parent.mkdir(parents=True,exist_ok=True)
 bpy.ops.export_scene.fbx(filepath=str(p),bake_anim=False,add_leaf_bones=False)
