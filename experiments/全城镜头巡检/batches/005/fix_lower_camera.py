import bpy
from mathutils import Vector
cam=bpy.data.objects['Camera_联合起吊-落地'];focus=bpy.data.objects['FocusTarget_联合起吊-落地']
target=Vector((0,-6.5,4.6));focus.location=target;cam.data.lens=24
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,compress=True)
