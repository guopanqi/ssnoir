"""公开手牌的两个必做目标；单回合初始冷静5，成功前最多四次代价，不会受伤。"""
from pathlib import Path
from functools import lru_cache
from itertools import combinations
import json,runpy,time
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE.parent/'R09-成长与行动中的信息'/'成长边界.py'))
HANDS,ODDS,PAY=K['HANDS'],K['probabilities'],K['PAY']
def key(value):return (round(value[0],12),-round(value[1],12))
class Model:
 def __init__(self,targets=(3,3),skills=(1,3)):
  self.targets,self.skills=targets,skills;self.decisions={}
  self.value=lru_cache(None)(self._value);self.single=lru_cache(None)(self._single)
 def _single(self,r,h,g):
  if r==0:return 1.,0.
  if 2*len(h)<r:return 0.,0.
  out=[(0.,0.)]
  for d in sorted(set(h)):
   rest=list(h);rest.remove(d);rest=tuple(rest)
   for act in PAY:
    total=[0.,0.]
    for w,(gain,cost) in zip(ODDS(d,g),PAY[act]):
     if not w:continue
     p,harm=self.single(max(0,r-gain),rest,g);total[0]+=w*p;total[1]+=w*(harm+cost*p)
    out.append(tuple(total))
  return max(out,key=key)
 def action_value(self,a,b,h,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);total=[0.,0.]
  for w,(gain,cost) in zip(ODDS(d,self.skills[route]),PAY[act]):
   if not w:continue
   aa,bb=(max(0,a-gain),b) if route==0 else (a,max(0,b-gain))
   p,harm=self.value(aa,bb,rest);total[0]+=w*p;total[1]+=w*(harm+cost*p)
  return tuple(total)
 def candidates(self,a,b,h):
  return [((0.,0.),('结束回合',None,None))]+[(self.action_value(a,b,h,i,act,d),(i,act,d)) for i,r in enumerate((a,b)) if r>0 for d in sorted(set(h)) for act in PAY]
 def _value(self,a,b,h):
  if a==b==0:return 1.,0.
  # 不可能同时完成时仍给出正式可执行的结束回合决定。
  if (a+1)//2+(b+1)//2>len(h):self.decisions[(a,b,h)]=('结束回合',None,None);return 0.,0.
  value,choice=max(self.candidates(a,b,h),key=lambda x:key(x[0]));self.decisions[(a,b,h)]=choice;return value
 def partitions(self,h,kind='最优分区'):
  count=len(h);out=[]
  for n in range((self.targets[0]+1)//2,count-(self.targets[1]+1)//2+1):
   subsets=list(combinations(range(count),n)) if kind=='最优分区' else [tuple(range(count-n,count))] if kind=='高骰弱项' else [tuple(range(n))]
   for inds in subsets:
    left=tuple(h[i] for i in inds);right=tuple(h[i] for i in range(count) if i not in inds)
    pa,ha=self.single(self.targets[0],left,self.skills[0]);pb,hb=self.single(self.targets[1],right,self.skills[1])
    out.append(((pa*pb,ha*pb+hb*pa),(left,right)))
  return max(out,key=lambda x:key(x[0]))
 def summary(self,policy='全过程调整'):
  t=time.perf_counter();p=harm=0.
  for h,w in HANDS:
   v=self.value(*self.targets,h) if policy=='全过程调整' else self.partitions(h,policy)[0]
   p+=w*v[0];harm+=w*v[1]
  return dict(targets=self.targets,skills=self.skills,policy=policy,completion=p,cold_on_success=5-harm/p if p else 0.,states=self.value.cache_info().currsize,seconds=time.perf_counter()-t)
def main():
 rows=[]
 for targets,skills in [((3,3),(1,3)),((3,3),(1,1)),((3,3),(1,2)),((3,3),(1,4)),((2,3),(1,3))]:
  m=Model(targets,skills)
  for policy in ('全过程调整','最优分区','高骰弱项','高骰强项'):
   row=m.summary(policy);rows.append(row);print(row,flush=True)
 (HERE/'首试结果.json').write_text(json.dumps(dict(results=rows,scope='单回合四骰独立公平d6，冷静5，两个目标都完成；成功平手保留冷静；最优分区内仍根据结果优化，没有未来信息'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
