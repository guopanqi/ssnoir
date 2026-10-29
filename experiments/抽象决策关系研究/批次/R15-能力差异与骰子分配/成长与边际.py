from pathlib import Path
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'分配模型.py'))
rows=[]
for base in (1,2,3,4):
 m=K['Model'](skills=(base,base+1))
 for policy in ('全过程调整','最优分区','高骰弱项'):
  r=m.summary(policy);r['base_growth']=base;r['scope']='技能均为基础值，乙额外+1修正；有效5不表示角色永久技能5';rows.append(r)
(HERE/'成长边界.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
margin=[]
for d in range(1,7):
 for route,g in enumerate((1,2)):
  p=K['ODDS'](d,g);margin.append(dict(die=d,route=route,finish_remaining_one_with_risk=p[1]+p[2],finish_remaining_two_with_risk=p[2]))
(HERE/'边际用途.json').write_text(json.dumps(dict(rows=margin,scope='风险动作面对剩1/剩2的完成概率；不代表全局最优动作，其他动作选择需完整求解'),ensure_ascii=False,indent=2)+'\n')
for r in rows:print({k:v for k,v in r.items() if k in ('base_growth','policy','completion')})
