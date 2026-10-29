import bpy,json,sys
from pathlib import Path
p=Path(__file__).parent
names={'Camera_世界'}|{r['camera'] for r in json.loads((p/'selected_landmarks.json').read_text())['places']}
for ob in list(bpy.context.scene.objects):
 if ob.type=='CAMERA' and ob.name.startswith('Camera_') and ob.name not in names:bpy.data.objects.remove(ob,do_unlink=True)
review=p.parents[3]/'tools/camera-review/review.py'
out=p/'render-input';out.mkdir(exist_ok=True)
sys.argv=[str(review),'--','--out',str(out)]
source=review.read_text().split('# Evaluated scene instances')[0]
exec(compile(source,str(review),'exec'),{'__file__':str(review),'__name__':'__main__'})
x=json.loads((out/'routes.json').read_text());x['Routes']=[r for r in x['Routes'] if r['From']=='Camera_世界' or r['To']=='Camera_世界'];(out/'routes.json').write_text(json.dumps(x,ensure_ascii=False))
