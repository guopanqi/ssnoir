"""只调用R30末回合最多两骰状态，与R31独立Fraction小树比较，不跑三回合初始求解。"""
from pathlib import Path
import json,runpy,hashlib
HERE=Path(__file__).resolve().parent
R30=HERE.parent/'R30-非对称敌人与重排顺序'
P=runpy.run_path(str(R30/'非对称模型.py'))
E=runpy.run_path(str(HERE/'末回合独立核查.py'))
rows=[]
for name,hp,c,k,q,h in (
 ('无伤冷静空',(1,2,0),0,0,0,(1,6)),
 ('轻伤头部3',(1,2,0),0,3,1,(1,6)),
 ('重伤4',(1,2,0),0,4,0,(1,6)),
 ('行动先倒下不得推进',(1,0,0),0,6,0,(1,)),
):
 m=P['Model']();primary=m.value(hp,c,k,q,1,h,-1);exact=E['solve'](hp,c,k,bool(q),h)
 error=max(abs(x-float(y)) for x,y in zip(primary,exact));assert error<1e-12,(name,primary,exact)
 routes={}
 for route in range(3):
  if not hp[route]:continue
  value,choice=m.choose([x for x in m.candidates(hp,c,k,q,1,h,-1) if x[1][0]==route]);routes[P['NAMES'][route]]=dict(value=value,choice=choice)
 rows.append(dict(name=name,state=[hp,c,k,q,1,h],primary=primary,exact=[str(x) for x in exact],max_error=error,first_target=routes,primary_states=m.value.cache_info().currsize))
# Explicit locked orders: sentinel reset cannot occur after action because children preserve positive order.
m=P['Model']('round');headst=((1,2,0),0,3,1,1,(1,6))
aidx=P['ORDERS'].index((0,1,2));bidx=P['ORDERS'].index((1,0,2))
locked_a=m.value(*headst,aidx);locked_b=m.value(*headst,bidx)
assert abs(locked_a[0]-.5)<1e-12 and abs(locked_b[0]-5/12)<1e-12
out=dict(date='2026-10-02',results=rows,locked_round_values=dict(A_first=locked_a,B_first=locked_b),max_error=max(r['max_error'] for r in rows),scope='只比较4个末回合小状态；独立Fraction源码不加载R30；R30调用不是独立解；未复算三回合完整最优')
(HERE/'小状态主模型对照.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(out,ensure_ascii=False))
deps=[R30/'有界研究计划.md',R30/'非对称模型.py',R30/'非对称结果.json',R30/'研究记录.md',HERE.parent/'R21-伤势与目标偏好/伤势模型.py',HERE/'末回合独立核查.py',HERE/'小状态主模型对照.py']
(HERE/'版本.json').write_text(json.dumps(dict(date='2026-10-02',source_sha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in deps},scope='R30源只读；主结果没有独立完整优化复核，本批仅小状态精确核对；native0真人0'),ensure_ascii=False,indent=2)+'\n')
