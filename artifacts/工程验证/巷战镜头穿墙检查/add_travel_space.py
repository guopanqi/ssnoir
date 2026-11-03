"""Patch semantic metadata only; preserve existing animation and mesh work."""
import bpy
coll=bpy.data.collections['巷子里在打人']
name='CameraTravelBounds_'+coll.name
assert name not in bpy.data.objects
root=bpy.data.objects.new(name,None);coll.objects.link(root)
for suffix,position in (('_min',(-2.65,-11,.3)),('_max',(2.65,8.9,8.5))):
    corner=bpy.data.objects.new(suffix,None);coll.objects.link(corner);corner.parent=root;corner.location=position
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
