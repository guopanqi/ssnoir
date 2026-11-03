import bpy
points={'':(0,2,4),'-起吊':(0,-1.5,5.8),'-摆动':(0,-2,5.6),'-落地':(0,-6.5,3.5)}
c=bpy.data.collections['联合起吊']
for suffix,at in points.items():
 name='FocusTarget_联合起吊'+suffix
 ob=bpy.data.objects.get(name)
 if ob is None:ob=bpy.data.objects.new(name,None);c.objects.link(ob)
 ob.location=at
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,compress=True)
