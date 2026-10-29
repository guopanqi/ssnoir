"""信息有显式行动机会成本：只在目标身份未知时以等概率分支揭晓，不重抽已知身份。"""
from pathlib import Path
from functools import lru_cache
import json,time,runpy
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE.parent/'R19-阶段转换与多回合组合/组合模型.py'));ODDS=K['ODDS'];PAY=K['PAY'];HANDS=K['HANDS']
class Model:
 def __init__(self,policy='自由',g=1):
  self.policy,self.g=policy,g;self.decisions={};self.value=lru_cache(None)(self._value)
 @staticmethod
 def choose(cs):
  top=max(v[0] for v,ch in cs);return max(((v,ch) for v,ch in cs if top-v[0]<=1e-12),key=lambda x:x[0][1])
 def candidates(self,a,b,target,c,h):
  out=[((0.,0.),('结束回合',None,None))]
  routes=[target] if target else ['甲','乙','探查']
  if self.policy=='不探查':routes=[r for r in routes if r!='探查']
  if not target and (self.policy=='直到定位先探查' or self.policy=='首手探查' and len(h)==4):routes=['探查']
  if not target and self.policy=='固定搜索顺序':routes=[r for r in routes if r!='乙']
  for route in routes:
   for act in ('稳做',) if route=='探查' else ('稳做','快做'):
    for d in sorted(set(h)):
     if route=='探查' and self.policy=='探查最低骰' and d!=min(h):continue
     if route=='探查' and self.policy=='探查最高骰' and d!=max(h):continue
     out.append((self.action_value(a,b,target,c,h,route,act,d),(route,act,d)))
  return out
 def action_value(self,a,b,target,c,h,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);out=[0.,0.]
  for grade,(prob,(gain,dc)) in enumerate(zip(ODDS(d,self.g),PAY[act])):
   if not prob:continue
   cc=c+dc;assert cc>=0
   aa=min(3,a+gain) if route=='甲' else a;bb=min(3,b+gain) if route=='乙' else b
   revealed=route=='探查' and grade==2 or aa==3 or bb==3
   branches=[(1.,target)] if target else [(.5,'甲'),(.5,'乙')] if revealed else [(1.,None)]
   for w,tr in branches:
    v=self.value(aa,bb,tr,cc,rest)
    for j in range(2):out[j]+=prob*w*v[j]
  return tuple(out)
 def _value(self,a,b,target,c,h):
  if target=='甲' and a==3 or target=='乙' and b==3:return 1.,float(c)
  v,ch=self.choose(self.candidates(a,b,target,c,h));self.decisions[(a,b,target,c,h)]=ch;return v
 def summary(self):
  start=time.perf_counter();v=[0.,0.]
  for h,w in HANDS:
   vals=[self.value(0,0,None,5,h)] if self.policy!='开局明示' else [self.value(0,0,r,5,h) for r in ('甲','乙')]
   for x in vals:
    for j in range(2):v[j]+=w*x[j]/len(vals)
  return dict(policy=self.policy,skill=self.g,completion=v[0],cold_on_success=v[1]/v[0],states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
def main():
 rows=[]
 for g in (1,2):
  for policy in ('自由','不探查','直到定位先探查','首手探查','探查最低骰','探查最高骰','固定搜索顺序','开局明示'):
   r=Model(policy,g).summary();rows.append(r);print(r,flush=True)
 (HERE/'信息投资结果.json').write_text(json.dumps(dict(results=rows,scope='公开先验有限模型，目标身份整局固定；未知时对称揭晓等价惰性抽样，需独立固定真值核对；一回合冷静不会溢出，实际成功，未正式/真人验证'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
