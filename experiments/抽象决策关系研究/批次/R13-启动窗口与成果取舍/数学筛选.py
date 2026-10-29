"""两项关系的有限公开状态模型；不执行、也不修改正式内容。"""
from pathlib import Path
from functools import lru_cache
from math import ceil
import json,runpy,time
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE.parent/'R09-成长与行动中的信息'/'成长边界.py'))
HANDS,ODDS,PAY=K['HANDS'],K['probabilities'],K['PAY']
def best(candidates):return max(candidates,key=lambda x:tuple(round(v,12) for v in x[0]))
class Window:
 def __init__(self,prep=3,payload=4,turns=3,growth=1,policy='求解',gate=4,window=True):
  self.prep,self.payload,self.turns,self.g,self.policy,self.gate,self.window=prep,payload,turns,growth,policy,gate,window
  self.value=lru_cache(None)(self._value);self.end=lru_cache(None)(self._end);self.decisions={}
 def _end(self,p,q,c,t):
  if t<=1 or c<1 or (p==0 and self.window):return (0.,0.)
  total=[0.,0.]
  for h,w in HANDS:
   v=self.value(p,q,c-1,t-1,h)
   for i in range(2):total[i]+=w*v[i]
  return tuple(total)
 def action_value(self,p,q,c,t,h,a,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);total=[0.,0.]
  for w,(gain,cost) in zip(ODDS(d,self.g),PAY[a]):
   if not w or cost>c:continue
   pp,qq=(max(0,p-gain),q) if p else (0,max(0,q-gain))
   v=self.value(pp,qq,c-cost,t,rest)
   for i in range(2):total[i]+=w*v[i]
  return tuple(total)
 def candidates(self,p,q,c,t,h):
  out=[(self.end(p,q,c,t),('结束回合',None))]
  if self.policy=='用完当手' and h:out=[]
  for d in sorted(set(h)):
   rest=list(h);rest.remove(d)
   for a in PAY:
    may_open=p>0 and any(w and gain>=p for w,(gain,cost) in zip(ODDS(d,self.g),PAY[a]))
    if may_open and self.policy=='容量门槛' and 2*len(rest)<q:continue
    if may_open and self.policy=='质量门槛' and sum(x>=self.gate for x in rest)<ceil(q/2):continue
    out.append((self.action_value(p,q,c,t,h,a,d),(a,d)))
  return out
 def _value(self,p,q,c,t,h):
  if q==0:return (1.,float(c))
  value,choice=best(self.candidates(p,q,c,t,h));self.decisions[(p,q,c,t,h)]=choice;return value
 def summary(self):
  start=time.perf_counter();result=[0.,0.]
  for h,w in HANDS:
   v=self.value(self.prep,self.payload,5,self.turns,h)
   for i in range(2):result[i]+=w*v[i]
  return dict(prep=self.prep,payload=self.payload,turns=self.turns,growth=self.g,policy=self.policy,gate=self.gate,window=self.window,injury_free_completion=result[0],cold_on_success=result[1]/result[0] if result[0] else 0,states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
class Harvest:
 def __init__(self,targets=(3,6),rewards=(1,2),growth=1,policy='求解'):
  assert sum(targets)>8,'首试容量须不足同时完成'
  self.targets,self.rewards,self.g,self.policy=targets,rewards,growth,policy
  self.value=lru_cache(None)(self._value);self.decisions={}
 def settle(self,a,b,c):
  score=(self.rewards[0] if a==0 else 0)+(self.rewards[1] if b==0 else 0)
  return (float(score),float(score>0),float(max(0,c-1)) if score>0 else 0.)
 def action_value(self,a,b,c,h,commit,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);total=[0.,0.,0.]
  for w,(gain,cost) in zip(ODDS(d,self.g),PAY[act]):
   if not w:continue
   assert cost<=c,'正常首轮模型不会突破初始5冷静'
   aa,bb=(max(0,a-gain),b) if route==0 else (a,max(0,b-gain))
   v=self.value(aa,bb,c-cost,rest,route if self.policy=='开局选定' else commit)
   for i in range(3):total[i]+=w*v[i]
  return tuple(total)
 def candidates(self,a,b,c,h,commit):
  routes=[i for i,r in enumerate((a,b)) if r>0]
  if self.policy=='固定小':routes=[0] if a else []
  if self.policy=='固定大':routes=[1] if b else []
  if self.policy=='开局选定' and commit>=0:routes=[commit] if (a,b)[commit]>0 else []
  return [(self.settle(a,b,c),('结束回合',None,None))]+[(self.action_value(a,b,c,h,commit,i,act,d),(i,act,d)) for i in routes for d in sorted(set(h)) for act in PAY]
 def _value(self,a,b,c,h,commit):
  value,choice=best(self.candidates(a,b,c,h,commit));self.decisions[(a,b,c,h,commit)]=choice;return value
 def summary(self):
  start=time.perf_counter();v=[0.,0.,0.]
  for h,w in HANDS:
   value=self.value(*self.targets,5,h,-1)
   for i in range(3):v[i]+=w*value[i]
  return dict(targets=self.targets,rewards=self.rewards,growth=self.g,policy=self.policy,expected_score=v[0],positive_score_probability=v[1],cold_on_positive=v[2]/v[1] if v[1] else 0.,states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
def main():
 windows=[]
 for policy,gate in [('求解',4),('用完当手',4),('容量门槛',4)]+[('质量门槛',k) for k in (2,3,4,5,6)]:
  m=Window(policy=policy,gate=gate);row=m.summary();windows.append(row);print(row,flush=True)
 m=Window(window=False);row=m.summary();windows.append(row);print(row,flush=True)
 harvest=[]
 for policy in ('求解','固定小','固定大','开局选定'):
  m=Harvest(policy=policy);row=m.summary();harvest.append(row);print(row,flush=True)
 (HERE/'首试结果.json').write_text(json.dumps(dict(window=windows,harvest=harvest,scope='窗口：健康起局无伤完成，首伤后续局未建模；成果：单回合冷静5不会受伤，最大化声明的研究成果点，平手争取非零成果再保留冷静'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
