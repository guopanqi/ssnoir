"""独立精确后验：固定两个真值世界，以公开观察分组，不调用主模型的选择或转移。"""
from pathlib import Path
from fractions import Fraction as F
from functools import lru_cache
import runpy,json
HERE=Path(__file__).resolve().parent;P=runpy.run_path(str(HERE/'信息投资.py'))
POLICIES=('自由','不探查','直到定位先探查','首手探查','探查最低骰','探查最高骰','固定搜索顺序','开局明示')
class Exact:
 def __init__(self,policy,g):self.policy,self.g=policy,g;self.solve=lru_cache(None)(self._solve)
 def _solve(self,a,b,mask,c,h):
  if mask==1 and a==3 or mask==2 and b==3:return F(1),F(c)
  best=(F(0),F(0));known=mask!=3;routes=[0 if mask==1 else 1] if known else [0,1,2]
  if self.policy=='不探查':routes=[r for r in routes if r!=2]
  if not known and (self.policy=='直到定位先探查' or self.policy=='首手探查' and len(h)==4):routes=[2]
  if not known and self.policy=='固定搜索顺序':routes=[r for r in routes if r!=1]
  for route in routes:
   for risky in ((False,) if route==2 else (False,True)):
    for d in set(h):
     if route==2 and self.policy=='探查最低骰' and d!=min(h):continue
     if route==2 and self.policy=='探查最高骰' and d!=max(h):continue
     rest=list(h);rest.remove(d);rest=tuple(rest);out=[F(0),F(0)]
     for grade,pf in enumerate(P['ODDS'](d,self.g)):
      probability=F(pf).limit_denominator(6)
      if not probability:continue
      cost=int(grade==0 or risky and grade==1);cc=c-cost
      delta=grade if risky else int(grade==2);aa=min(3,a+delta) if route==0 else a;bb=min(3,b+delta) if route==1 else b
      observations={}
      worlds=[world for world in (0,1) if mask&(1<<world)]
      for world in worlds:
       success=(aa if world==0 else bb)==3
       reveal=known or route==2 and grade==2 or aa==3 or bb==3
       observation=(aa,bb,cc,'success' if success else str(world) if reveal else 'unknown')
       if observation not in observations:observations[observation]=[F(0),0]
       observations[observation][0]+=F(1,len(worlds));observations[observation][1]|=1<<world
      for (na,nb,nc,label),(weight,newmask) in observations.items():
       v=(F(1),F(nc)) if label=='success' else self.solve(na,nb,newmask,nc,rest)
       for j in range(2):out[j]+=probability*weight*v[j]
     best=max(best,tuple(out))
  return best
def main():
 rows=[];checked=0;maximum=0.
 for g in (1,2):
  for policy in POLICIES:
   exact=Exact(policy,g);primary=P['Model'](policy,g);error=0.;count=0
   for h,w in P['HANDS']:
    for mask in (1,2) if policy=='开局明示' else (3,):
     x=exact.solve(0,0,mask,5,h);y=primary.value(0,0,'甲' if mask==1 else '乙' if mask==2 else None,5,h)
     error=max(error,max(abs(float(a)-b) for a,b in zip(x,y)));count+=1
   assert error<1e-10,(g,policy,error)
   rows.append(dict(skill=g,policy=policy,initial_states=count,max_error=error,exact_states=exact.solve.cache_info().currsize,passed=True));checked+=count;maximum=max(maximum,error)
 out=dict(results=rows,initial_states=checked,max_error=maximum,scope='独立Fraction优化；整局固定两个真值世界，判定概率共用官方校准输入，动作效果/策略限制/后验公开观察分组独立实现；不是游戏执行或体验证据')
 (HERE/'固定真值复核.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v for k,v in out.items() if k!='results'},ensure_ascii=False))
if __name__=='__main__':main()
