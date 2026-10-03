"""R34许可状态的边界判别，复用主模型；不称独立优化或正常访问质量。"""
from pathlib import Path
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'压力模型.py'));Model=K['Model']
rows=[]
for obj in ('completion','early'):
 m=Model(obj);control=Model(obj,fatal_pressure=False)
 state=(4,1,0,2,3,0,0,2,(6,),-1)
 advance=m.action_value(*state,0,'快做',6);repair=m.action_value(*state,1,'快做',6)
 assert m.score(advance)==0 and m.score(repair)>0
 # 已累积压力2在修复后仍为2；没有退款，但无活跃负担故新轮仍可继续。
 shadow=m.end(4,0,0,2,3,0,0,2,-1)
 assert all(abs(x-y)<1e-12 for x,y in zip(repair,shadow))
 cap=m.end(6,2,2,1,3,0,0,2,-1);uncapped=control.end(6,2,2,1,3,0,0,2,-1)
 assert m.score(cap)==0 and control.score(uncapped)>0
 immediate=m.action_value(7,2,2,2,3,0,0,2,(6,),-1,0,'快做',6)
 assert immediate==(0.,1.,0.,0.,0.,3.)
 roundmodel=Model(obj,'每回合固定偏好')
 pushlock=roundmodel.value(*state[:-1],0);repairlock=roundmodel.value(*state[:-1],1)
 assert roundmodel.score(pushlock)==0 and roundmodel.score(repairlock)>0
 rows.append(dict(objective=obj,state=state,advance=advance,repair=repair,repair_same_as_unchanged_pressure_continuation=True,pressure_cap=cap,no_cap=uncapped,immediate_completion=immediate,locked_push=pushlock,locked_repair=repairlock))
(HERE/'局部边界.json').write_text(json.dumps(dict(date='2026-10-03',results=rows,passed=True,scope='两目标的许可局部状态与严格边界；共享主转移不算独立优化，不是正常路径质量；原生0真人0'),ensure_ascii=False,indent=2)+'\n')
print(json.dumps(dict(passed=True,objectives=2),ensure_ascii=False))
