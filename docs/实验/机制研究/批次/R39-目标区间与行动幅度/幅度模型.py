"""R39单回合位置目标：精确Fraction，未接入正式游戏。"""
from pathlib import Path
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial
import json,runpy
HERE=Path(__file__).resolve().parent
SOURCE=runpy.run_path(str(HERE.parent/'R36-资源余量与选择增量/压力模型.py'))
STRIPS={d:tuple(F(x).limit_denominator(6) for x in SOURCE['ODDS'](d,1)) for d in range(1,7)}
TARGETS=((3,4),(3,3));POLICIES=('free','forward','toward','highest')
class Model:
 def __init__(self,target,policy):
  self.low,self.high=target;self.policy=policy;self.choices={};self.solve=lru_cache(None)(self._solve)
 def _solve(self,x,h):
  if self.low<=x<=self.high:return F(1)
  if not h:return F(0)
  directions=(1,-1) if self.policy=='free' else (1,) if self.policy=='forward' else (1,) if x<self.low else (-1,)
  dice=(max(h),) if self.policy=='highest' else sorted(set(h));candidates=[]
  for direction in directions:
   for d in dice:
    tail=list(h);tail.remove(d);tail=tuple(tail)
    for risky in (False,True):
     value=sum((p*self.solve(x+direction*(g if risky else int(g==2)),tail) for g,p in enumerate(STRIPS[d])),F(0))
     candidates.append((value,(direction,d,risky)))
  value,choice=max(candidates,key=lambda z:z[0]);self.choices[x,h]=choice;return value
 def summary(self):
  rows=[];total=F(0)
  for h in combinations_with_replacement(range(1,7),4):
   mult=factorial(4)
   for d in set(h):mult//=factorial(h.count(d))
   w=F(mult,6**4);v=self.solve(0,h);total+=w*v;rows.append(dict(hand=h,weight=str(w),exact=str(v)))
  return dict(target=[self.low,self.high],policy=self.policy,exact_probability=str(total),probability=float(total),states=self.solve.cache_info().currsize,initial_hands=126,perhand=rows)
def main():
 rows=[]
 for target in TARGETS:
  free=None
  for policy in POLICIES:
   m=Model(target,policy);row=m.summary();rows.append(row)
   if policy=='free':free=row
   else:
    for a,b in zip(row['perhand'],free['perhand']):assert a['hand']==b['hand'] and F(a['exact'])<=F(b['exact'])
   print(json.dumps({k:v for k,v in row.items() if k!='perhand'},ensure_ascii=False),flush=True)
 (HERE/'幅度结果.json').write_text(json.dumps(dict(batch='R39',results=rows,scope='两终点×四族×126初始手；精确完成概率，公平骰假设；位置终点不同于累计进度；没有Content/原生/真人'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
