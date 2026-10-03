"""固定目标次序仍允许骰子与稳险随结果调整；与固定骰子分区区分。"""
from pathlib import Path
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'分配模型.py'))
class EachFirst(K['Model']):
 def candidates(self,a,b,h):
  cs=super().candidates(a,b,h)
  if len(h)==3:
   # 首手零进度后已不可能四手完成两项3格；允许直接结束。
   if a==3 and b==3:return [x for x in cs if x[1][0]=='结束回合']
   second=1 if a<3 else 0
   return [x for x in cs if x[1][0] in ('结束回合',second)]
  return cs
class Alternating(K['Model']):
 def __init__(self,first,**kwargs):self.first=first;super().__init__(**kwargs)
 def candidates(self,a,b,h):
  route=(self.first+4-len(h))%2
  return [x for x in super().candidates(a,b,h) if x[1][0] in ('结束回合',route)]
rows=[]
for skills in ((1,1),(1,2),(1,3),(1,4)):
 for cls,label,first in [(K['Model'],'全过程调整',None),(EachFirst,'前两步各做一次，之后自由',None),(Alternating,'固定甲乙甲乙，骰子及稳险仍优化',0),(Alternating,'固定乙甲乙甲，骰子及稳险仍优化',1)]:
  m=cls(skills=skills) if first is None else cls(first,skills=skills);row=m.summary();row['policy']=label;rows.append(row);print({k:v for k,v in row.items() if k in ('skills','policy','completion','cold_on_success')})
for skills in ((1,1),(1,2),(1,3),(1,4)):
 left=Alternating(0,skills=skills);right=Alternating(1,skills=skills);p=harm=0.
 for h,w in K['HANDS']:
  v=max((left.value(3,3,h),right.value(3,3,h)),key=K['key']);p+=w*v[0];harm+=w*v[1]
 rows.append(dict(targets=(3,3),skills=skills,policy='首手选定交错次序，骰子及稳险随结果优化',completion=p,cold_on_success=5-harm/p))
(HERE/'交错顺序.json').write_text(json.dumps(dict(results=rows,scope='仅两目标各3、单回合四骰、同收益与冷静充足；固定目标次序内的骰子选择仍根据已经出现的结果完整优化；不得称整个决策已固定'),ensure_ascii=False,indent=2)+'\n')
