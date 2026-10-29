"""R33仅末回合小状态对照；不调用R32.summary/main。"""
from pathlib import Path
import runpy,json,hashlib
HERE=Path(__file__).resolve().parent;R32=HERE.parent/'R32-推进生成持续负担'
P=runpy.run_path(str(R32/'负担模型.py'));E=runpy.run_path(str(HERE/'末回合独立核查.py'))
rows=[]
for objective in ('completion','early'):
 for name,st in E['CASES']:
  p,a,b,c,k,q,h=st;m=P['Model'](objective);v=m.value(p,a,b,c,k,int(q),1,h,-1);x=E['solve'](*st)
  err=max(abs(z-float(y)) for z,y in zip(v,x));assert err<1e-12,(objective,name,v,x)
  rows.append(dict(objective=objective,name=name,state=[p,a,b,c,k,q,1,h],primary=v,exact=[str(y) for y in x],max_error=err,states=m.value.cache_info().currsize))
trigger_rows=[]
for label,st in (
 ('首次跨3产生甲',(2,0,0,1,0,False,(6,))),
 ('甲已修后继续不重生',(3,0,0,1,0,False,(6,))),
 ('首次跨6产生乙',(5,0,0,1,0,False,(6,))),
 ('乙已修后继续不重生',(6,0,0,1,0,False,(6,))),
):
 p,a,b,c,k,q,h=st;m=P['Model']();v=m.action_value(p,a,b,c,k,int(q),1,h,-1,0,'稳做',6);x=E['action'](*st,0,False,6)
 err=max(abs(z-float(y)) for z,y in zip(v,x));assert err<1e-12,(label,v,x)
 # Cards for never spawned/already repaired burdens are absent.
 cards=[ch[0] for v,ch in m.candidates(p,a,b,c,k,int(q),1,h,-1)]
 assert 1 not in cards and 2 not in cards,(label,cards)
 trigger_rows.append(dict(name=label,state=st,primary=v,exact=[str(y) for y in x],max_error=err))
p,a,b,c,k,q,h=(5,2,0,0,3,True,(1,1));policy_rows=[]
for policy in ('自由','容量规则','当前技能正概率容量','每回合固定偏好'):
 m=P['Model']('completion',policy);v=m.value(p,a,b,c,k,int(q),1,h,-1)
 policy_rows.append(dict(policy=policy,value=v,primary=sum(v[:3])))
assert abs(policy_rows[0]['primary']-1/12)<1e-12 and policy_rows[2]['primary']==0
m=P['Model']('completion','每回合固定偏好');locked={str(pref):m.value(p,a,b,c,k,int(q),1,h,pref) for pref in (0,1)}
assert abs(locked['0'][2]-1/12)<1e-12 and locked['1'][2]==0
out=dict(date='2026-10-02',results=rows,trigger_checks=trigger_rows,capacity_policy_case=policy_rows,locked_preferences=locked,max_error=max(r['max_error'] for r in rows+trigger_rows),scope='两目标×5个末回合小状态＋4个强制动作；R32调用仅作对照，独立Fraction不导入主模型；不重复三回合全量DP')
(HERE/'小状态主模型对照.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
deps=[R32/'有界研究计划.md',R32/'负担模型.py',R32/'负担结果.json',R32/'研究记录.md',HERE.parent/'R21-伤势与目标偏好/伤势模型.py',HERE/'末回合独立核查.py',HERE/'小状态主模型对照.py']
(HERE/'版本.json').write_text(json.dumps(dict(date='2026-10-02',source_sha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in deps},scope='只读R32；独立只核末回合小状态；原生0真人0；日期依适用developer指令'),ensure_ascii=False,indent=2)+'\n')
