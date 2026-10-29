"""新即时压力的局部解析/回归核对，非完整独立优化或正式执行。"""
from pathlib import Path
import json,runpy
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'压力模型.py'));M=K['Model']
rows=json.loads((HERE/'压力结果.json').read_text())['results']
old=json.loads((HERE.parent/'R34-不可逆压力与预防/压力结果.json').read_text())['results']
regression=[]
for obj in ('completion','early'):
 current=next(r for r in rows if r['objective']==obj and not r['instant_pressure'])
 control=next(r for r in old if r['objective']==obj and not r['fatal_pressure'])
 error=max(abs(x-y) for a,b in zip(current['perhand'],control['perhand']) for x,y in zip(a['value'],b['value']))
 assert error<1e-12;regression.append(dict(objective=obj,hands=126,max_error=error))
m=M();no=M(instant_pressure=False)
st=(7,1,2,4,3,0,0,2,(1,6),-1)
pulse=m.action_value(*st,0,'快做',1);no_pulse=no.action_value(*st,0,'快做',1);repair=m.action_value(*st,1,'快做',1)
assert abs(sum(pulse[:3])-2/3)<1e-12 and abs(sum(no_pulse[:3])-1)<1e-12 and abs(sum(repair[:3])-1)<1e-12
# 即时世界压力改变同档联合代价；R33修复替成推进的耦合前提不能直接沿用。
head=(7,2,2,4,0,3,1,1,(1,1),-1)
advance=m.action_value(*head,0,'快做',1);fix=m.action_value(*head,1,'快做',1)
assert abs(sum(advance[:3])-.5)<1e-12 and abs(sum(fix[:3])-2/3)<1e-12
out=dict(date='2026-10-03',same_semantics_regression=regression,checked_hands=252,instant_pressure_case=dict(state=st,advance=pulse,no_instant_advance=no_pulse,repair=repair),head_corner=dict(state=head,advance=advance,repair=fix),passed=True,scope='解析坏档概率及旧消融回归；局部许可头伤例不称正常访问或主亮点；共享模型不是独立全局优化；正式0真人0')
(HERE/'边界核对.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(dict(passed=True,checked_hands=252),ensure_ascii=False))
