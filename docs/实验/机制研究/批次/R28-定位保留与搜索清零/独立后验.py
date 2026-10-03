"""独立固定真值后验递推。只共享正式概率输入，不调用主模型转移/限制/选择。"""
from pathlib import Path
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial
import json,runpy,re,gc
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
source=(ROOT/'Engine/Runtime/Core/FateStrip.cs').read_text();entries=re.findall(r'(<= 1|[2-6]|_) => \((\d), (\d)\)',source)
T={key:(int(b),int(m),6-int(b)-int(m)) for key,b,m in entries};assert len(T)==7
HANDS={}
for n in (3,4):
 HANDS[n]=[]
 for h in combinations_with_replacement(range(1,7),n):
  count=factorial(n)
  for d in set(h):count//=factorial(h.count(d))
  HANDS[n].append((h,count/6**n))
class Independent:
 def __init__(self,reset,policy,penalty=0.):
  self.reset,self.policy,self.penalty=reset,policy,penalty;self.solve=lru_cache(None)(self._solve);self.finish=lru_cache(None)(self._finish)
 def best(self,vs):
  goal=max(v[0]-self.penalty*v[1] for v in vs);pool=[v for v in vs if goal-(v[0]-self.penalty*v[1])<=1e-12]
  for j in (1,2):
   x=min(v[j] for v in pool);pool=[v for v in pool if v[j]-x<=1e-12]
  x=max(v[3] for v in pool);return next(v for v in pool if x-v[3]<=1e-12)
 def harm(self,c,severity,head,cost):
  deficit=max(0,cost-c);new=severity+deficit
  if new>=7:return [(1.,0,7,False)]
  if severity==0 and deficit:return [(1/4,max(0,c-cost),new,True),(3/4,max(0,c-cost),new,False)]
  return [(1.,max(0,c-cost),new,head)]
 def _finish(self,a,b,mask,c,k,head,rounds,used):
  accum=[0.]*4
  for w,cc,kk,hh in self.harm(c,k,head,1):
   if kk>=7 or rounds==1:children=[(1.,(0.,float(kk>0),0.,0.))]
   else:
    aa=0 if self.reset and a!=3 else a;bb=0 if self.reset and b!=3 else b
    children=[(p,self.solve(aa,bb,mask,cc,kk,hh,rounds-1,used,dice)) for dice,p in HANDS[3 if kk>=4 else 4]]
   for p,v in children:
    for j in range(4):accum[j]+=w*p*v[j]
  return tuple(accum)
 def _solve(self,a,b,mask,c,k,head,rounds,used,dice):
  if k>=7:return (0.,1.,0.,0.)
  if mask==1 and a==3 or mask==2 and b==3:return (1.,float(k>0),float(k),float(c))
  values=[self.finish(a,b,mask,c,k,head,rounds,used)]
  known=mask!=3;routes=[0 if mask==1 else 1] if known else [0,1,2]
  if self.policy=='禁止探查' or self.policy=='最多探查一次' and used:routes=[r for r in routes if r!=2]
  if not known and (self.policy=='直到定位先探查' or self.policy=='开局先探查' and rounds==2 and len(dice)==4 or self.policy=='每回合首手探查' and len(dice)==(3 if k>=4 else 4)):
   routes=[2]
   if dice:values=[]
  if self.policy=='首回合只取信息' and rounds==2:routes=[2] if not known else []
  for route in routes:
   for risky in (False,) if route==2 else (False,True):
    for die in set(dice):
     remaining=list(dice);remaining.remove(die);remaining=tuple(remaining);v=[0.]*4
     skill=0 if head and 1<=k<=3 else 1;total=die+skill;prob=T['<= 1' if total<=1 else '_' if total>=7 else str(total)]
     for grade,count in enumerate(prob):
      if not count:continue
      cost=int(grade==0 or risky and grade==1);gain=grade if risky else int(grade==2)
      aa=min(3,a+gain) if route==0 else a;bb=min(3,b+gain) if route==1 else b
      # 在整局固定的甲、乙世界中枚举共同动作；同一公开观察分组，不读取尚未公开真值选择动作。
      groups={};worlds=[world for world in (0,1) if mask & (1<<world)]
      for world in worlds:
       completed=(aa if world==0 else bb)==3
       reveal=known or route==2 and grade==2 or aa==3 or bb==3
       observation='成功' if completed else str(world) if reveal else '未知'
       prior_weight,newmask=groups.get(observation,(0.,0));groups[observation]=(prior_weight+1/len(worlds),newmask|(1<<world))
      nextused=int(used or route==2) if self.policy=='最多探查一次' else 0
      for p,cc,kk,hh in self.harm(c,k,head,cost):
       for observation,(weight,newmask) in groups.items():
        x=(0.,1.,0.,0.) if kk>=7 else (1.,float(kk>0),float(kk),float(cc)) if observation=='成功' else self.solve(aa,bb,newmask,cc,kk,hh,rounds,nextused,remaining)
        for j in range(4):v[j]+=count/6*p*weight*x[j]
     values.append(tuple(v))
  return self.best(values)
def main():
 P=runpy.run_path(str(HERE/'跨回合信息.py'));rows=[]
 for reset in (True,False):
  for policy in P['POLICIES']:
   model=P['Model'](reset,policy);ind=Independent(reset,policy);error=0.
   for h,w in HANDS[4]:
    x=model.value(0,0,-1,5,0,0,2,0,h);y=ind.solve(0,0,3,5,0,False,2,0,h)
    error=max(error,max(abs(a-b) for a,b in zip(x,y)))
   assert error<1e-10,(reset,policy,error)
   row=dict(reset=reset,policy=policy,initial_hands=126,dimensions=4,max_error=error,states=ind.solve.cache_info().currsize);rows.append(row);print(row,flush=True)
   model.value.cache_clear();model.end.cache_clear();ind.solve.cache_clear();ind.finish.cache_clear();del model,ind;gc.collect()
 (HERE/'独立后验.json').write_text(json.dumps(dict(results=rows,initial_hands=126*len(rows),max_error=max(r['max_error'] for r in rows),scope='独立float固定两世界后验，概率从正式FateStrip读取；分别实现效果/伤势/清零/策略限制；逐手四维核对，不是正式或真人证据'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
