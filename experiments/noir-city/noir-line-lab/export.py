import bpy,bmesh,json,math,os
from mathutils import Vector
from pathlib import Path
ROOT=str(Path(__file__).resolve().parents[3]/'city-box')
OUT=os.path.dirname(os.path.abspath(__file__))

def extract(name,path,cut=False):
    bpy.ops.wm.open_mainfile(filepath=path)
    c=bpy.data.collections[name]
    verts=[];tri=[];edges=[]
    for ob in c.all_objects:
        if ob.type!='MESH' or ob.get('outline')=='proxy' or ob.get('preview_only'):continue
        pts=[ob.matrix_world@v.co for v in ob.data.vertices]
        if cut and min(p.z for p in pts)>3.5:continue
        tag=0
        n=ob.name
        if any(s in n for s in ('窗','门套','门框','门楣','雨篷','遮篷')):tag=1
        if any(s in n for s in ('窗中梃','窗横梃','楼层腰线')):tag=2
        if '分层檐口' in n and abs(ob.location.z-13.38)>.02:tag=2
        if any(s in n for s in ('栏杆','扶手','台阶')):tag=3
        if cut and ob.get('layer')=='interior':tag=4
        authored=ob.get('outline')!='none' and tag!=2
        # 为作者选择保留栏杆端柱和中柱，其他柱不强行占据同等视觉权重。
        if '栏杆' in n and '.' in n:
            try:authored=authored and int(n.split('.')[-1])%3==0
            except ValueError:pass
        offset=len(verts)//3
        verts.extend(round(x,4) for p in pts for x in p)
        ob.data.calc_loop_triangles()
        for f in ob.data.loop_triangles:tri.extend(offset+i for i in f.vertices)
        bm=bmesh.new();bm.from_mesh(ob.data);bm.normal_update()
        norm=ob.matrix_world.to_3x3().inverted().transposed()
        for e in bm.edges:
            if not e.link_faces:continue
            a=ob.matrix_world@e.verts[0].co;b=ob.matrix_world@e.verts[1].co
            if (a-b).length<.015:continue
            ns=[(norm@f.normal).normalized() for f in e.link_faces[:2]]
            if len(ns)==1:ns.append(Vector((0,0,0)));angle=180
            else:angle=math.degrees(ns[0].angle(ns[1],0))
            # 共面内部三角边不是建筑轮廓，不作为随机线噪声。
            if angle<.1:continue
            edges.append([*[round(v,4) for p in (a,b,ns[0],ns[1]) for v in p],round(angle,1),tag,int(authored)])
        bm.free()
    return dict(name=name,vertices=verts,triangles=tri,edges=edges)
models=[extract('诊所',ROOT+'/prefabs/诊所.blend'),extract('街角咖啡馆',ROOT+'/prefabs/街角咖啡馆.blend',True)]
with open(OUT+'/models.json','w') as f:json.dump(models,f,ensure_ascii=False,separators=(',',':'))
print('MODEL_DATA|',[(m['name'],len(m['vertices'])//3,len(m['triangles'])//3,len(m['edges'])) for m in models],os.path.getsize(OUT+'/models.json'))
