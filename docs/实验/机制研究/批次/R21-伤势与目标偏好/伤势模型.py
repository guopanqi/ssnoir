"""R19不改规则，补完整伤势/骰池；效用只是研究偏好。"""
from pathlib import Path
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial
import runpy,json,time
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE.parent/'R19-阶段转换与多回合组合'/'组合模型.py'))
PAY=K['PAY'];ODDS=K['ODDS'];HANDS={}
for n in (3,4):
 hands=[]
 for h in combinations_with_replacement(range(1,7),n):
  count=factorial(n)
  for d in set(h):count//=factorial(h.count(d))
  hands.append((h,count/6**n))
 HANDS[n]=hands
class Model:
 def __init__(self,reset=True,penalty=0.):
  self.reset,self.penalty=reset,penalty;self.decisions={};self.value=lru_cache(None)(self._value);self.end=lru_cache(None)(self._end)
 def score(self,v):return v[0]-self.penalty*v[1]
 def choose(self,cs):
  top=max(self.score(v) for v,ch in cs)
  band=[(v,ch) for v,ch in cs if top-self.score(v)<=1e-12]
  for j in (1,2):
   low=min(v[j] for v,ch in band);band=[(v,ch) for v,ch in band if v[j]-low<=1e-12]
  return max(band,key=lambda x:x[0][3])
 @staticmethod
 def fail(k):return (0.,float(k>0),0.,0.)
 @staticmethod
 def spend(c,k,q,cost):
  if cost<=c:return [(1.,c-cost,k,q)]
  severity=k+cost-c
  if severity>=7:return [(1.,0,7,0)]
  if k==0:return [(.25,0,severity,1 if severity<4 else 0),(.75,0,severity,0)]
  return [(1.,0,severity,q if severity<4 else 0)]
 def _end(self,a,b,c,k,q,t):
  out=[0.,0.,0.,0.]
  for p,cc,kk,qq in self.spend(c,k,q,1):
   if kk>=7 or t<=1:
    v=self.fail(kk)
    for j in range(4):out[j]+=p*v[j]
    continue
   aa=5 if self.reset and a else a;bb=3 if self.reset and b else b
   for hh,w in HANDS[3 if kk>=4 else 4]:
    v=self.value(aa,bb,cc,kk,qq,t-1,hh)
    for j in range(4):out[j]+=p*w*v[j]
  return tuple(out)
 def actions(self,a,b,c):
  return [(r,act) for r,left in (('甲',a),('乙',b)) if left for act in ('稳做','快做')]+([('整顿','整顿')] if c<5 else [])
 def action_value(self,a,b,c,k,q,t,h,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);out=[0.,0.,0.,0.]
  for w,(gain,dc) in zip(ODDS(d,0 if 1<=k<=3 and q else 1),PAY[act]):
   if not w:continue
   branches=[(1.,min(5,c+dc),k,q)] if dc>=0 else self.spend(c,k,q,-dc)
   for p,cc,kk,qq in branches:
    if kk>=7:v=self.fail(kk)
    else:
     aa,bb=(max(0,a-gain),b) if route=='甲' else (a,max(0,b-gain)) if route=='乙' else (a,b)
     v=self.value(aa,bb,cc,kk,qq,t,rest)
    for j in range(4):out[j]+=w*p*v[j]
  return tuple(out)
 def candidates(self,a,b,c,k,q,t,h):
  return [(self.end(a,b,c,k,q,t),('结束回合',None,None))]+[(self.action_value(a,b,c,k,q,t,h,r,act,d),(r,act,d)) for r,act in self.actions(a,b,c) for d in sorted(set(h))]
 def _value(self,a,b,c,k,q,t,h):
  if k>=7:return self.fail(k)
  if a==b==0:return 1.,float(k>0),float(k),float(c)
  v,ch=self.choose(self.candidates(a,b,c,k,q,t,h));self.decisions[(a,b,c,k,q,t,h)]=ch;return v
 def summary(self):
  start=time.perf_counter();v=[0.,0.,0.,0.]
  for h,w in HANDS[4]:
   x=self.value(5,3,3,0,0,3,h)
   for j in range(4):v[j]+=w*x[j]
  return dict(reset=self.reset,injury_event_cost=self.penalty,actual_completion=v[0],ever_injured=v[1],injury_on_success=v[2]/v[0] if v[0] else None,cold_on_success=v[3]/v[0] if v[0] else None,research_utility=self.score(v),states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
def main():
 rows=[]
 for reset in (True,False):
  for penalty in (0.,.25,1.):
   row=Model(reset,penalty).summary();rows.append(row);print(row,flush=True)
 (HERE/'伤势结果.json').write_text(json.dumps(dict(results=rows,scope='完整有限伤势模型；仍假设公平独立骰与首次伤部位均匀；效用系数不是游戏奖励或玩家估值，未独立最优复核'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
