"""两条替代路线，推进本身在检查点揭示长度。有限公开信息模型。"""
from pathlib import Path
from functools import lru_cache
import runpy,json,time
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE/'成长边界.py'));HANDS,PAY=K['HANDS'],K['PAY']
class Model:
 def __init__(self,short=3,long=6,reveal=2,growth=1,policy='求解',known=False):
  assert 0<reveal<short<long
  self.short,self.long,self.reveal,self.g,self.policy,self.known=short,long,reveal,growth,policy,known
  self.value=lru_cache(None)(self._value);self.decisions={}
 def terminal(self,state):
  a,x,b,y,c,h=state
  return (x>0 and a>=x) or (y>0 and b>=y)
 def routes(self,s):
  a,x,b,y,c,h=s
  if self.policy=='求解':return (0,1)
  if self.policy=='固定A':return (0,)
  if self.policy=='固定B':return (1,)
  if self.policy=='先摸清两路':
   return (0 if x<0 else 1,) if x<0 or y<0 else (0 if x-a<=y-b else 1,)
  if self.policy=='遇长必换':
   if x==self.long and y<0:return (1,)
   if y==self.long and x<0:return (0,)
   if x<0 and y<0:return (0 if a>=b else 1,)
   return (0 if (x if x>0 else (self.short+self.long)/2)-a<=(y if y>0 else (self.short+self.long)/2)-b else 1,)
  if self.policy=='期望剩余最短':
   return (0 if (x if x>0 else (self.short+self.long)/2)-a<=(y if y>0 else (self.short+self.long)/2)-b else 1,)
  raise AssertionError(self.policy)
 def transitions(self,s,i,act,d):
  a,x,b,y,c,h=s;rest=list(h);rest.remove(d);rest=tuple(rest)
  for w,(gain,cost) in zip(K['probabilities'](d,self.g),PAY[act]):
   if not w or cost>c:continue
   p,typ=(a,x) if i==0 else (b,y);p+=gain
   possibilities=[(.5,self.short),(.5,self.long)] if typ<0 and p>=self.reveal else [(1.,typ)]
   for ww,newtype in possibilities:
    pp=min(p,newtype) if newtype>0 else p
    ns=(pp,newtype,b,y,c-cost,rest) if i==0 else (a,x,pp,newtype,c-cost,rest)
    yield w*ww,ns
 def action_value(self,s,i,act,d):
  p=keep=0.
  for w,ns in self.transitions(s,i,act,d):
   v=self.value(ns);p+=w*v[0];keep+=w*v[1]
  return p,keep
 def candidates(self,s):
  return [(self.action_value(s,i,act,d),(i,act,d)) for i in self.routes(s) for d in sorted(set(s[-1])) for act in PAY]
 def _value(self,s):
  if self.terminal(s):return 1.,float(s[-2])
  if not s[-1]:return 0.,0.
  v,ch=max(self.candidates(s),key=lambda z:(round(z[0][0],12),round(z[0][1],12)))
  self.decisions[s]=ch
  return v
 def starts(self):
  for h,w in HANDS:
   types=[(.25,x,y) for x in (self.short,self.long) for y in (self.short,self.long)] if self.known else [(1.,-1,-1)]
   for ww,x,y in types:yield w*ww,(0,x,0,y,5,h)
 def summary(self):
  start=time.perf_counter();p=keep=0.
  for w,s in self.starts():
   v=self.value(s);p+=w*v[0];keep+=w*v[1]
  return dict(short=self.short,long=self.long,reveal=self.reveal,growth=self.g,known=self.known,policy=self.policy,completion=p,composure_on_success=keep/p if p else 0,states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
class Rolling(Model):
 def __init__(self,**kwargs):
  super().__init__(**kwargs);self.oracles=[Model(**(kwargs|{'policy':p})) for p in ('固定A','固定B')]
 def _value(self,s):
  if self.terminal(s):return 1.,float(s[-2])
  if not s[-1]:return 0.,0.
  candidates=[(max(o.action_value(s,i,act,d) for o in self.oracles),(i,act,d)) for i in (0,1) for d in sorted(set(s[-1])) for act in PAY]
  _,ch=max(candidates,key=lambda z:(round(z[0][0],12),round(z[0][1],12)))
  self.decisions[s]=ch
  return self.action_value(s,*ch)
def reachable(m):
 """准确推送公开状态概率；记录最优路线在信息变化后是否严格更好。"""
 frontier=dict((s,w) for w,s in m.starts());switchmass=0.;events=[]
 for step in range(4):
  new={}
  for s,w in frontier.items():
   if m.terminal(s) or not s[-1]:continue
   ch=m.decisions[s];i=ch[0]
   for q,ns in m.transitions(s,*ch):
    new[ns]=new.get(ns,0)+w*q
    if m.terminal(ns) or not ns[-1]:continue
    was=(s[1],s[3])[i];now=(ns[1],ns[3])[i]
    if was<0 and now>0:
     best=[max(v[0] for v,k in m.candidates(ns) if k[0]==j) for j in (0,1)]
     gap=best[1-i]-best[i]
     if gap>.01:
      switchmass+=w*q;events.append(dict(state=s,action=ch,after=ns,probability_mass=w*q,gap=gap))
  frontier=new
 return dict(expected_strict_switch_events=switchmass,scope='每局严格换路事件的期望次数；不是至少一次的概率',witnesses=sorted(events,key=lambda e:e['probability_mass'],reverse=True)[:8])
def main():
 rows=[];models={}
 for p in ('求解','固定A','先摸清两路','遇长必换','期望剩余最短'):
  m=Model(policy=p);r=m.summary();rows.append(r);models[p]=m;print(r,flush=True)
 m=Rolling();r=m.summary();r['policy']='两条固定路线滚动比较';rows.append(r);print(r,flush=True)
 m=Model(known=True);r=m.summary();rows.append(r);print(r,flush=True)
 (HERE/'行动信息.json').write_text(json.dumps(dict(results=rows,reachable=reachable(models['求解']),assumptions=['一回合4个独立均匀d6；技能1、冷静5、无伤、无物品','A/B独立各半短3长6；推进达到2时揭示；任何一条完成就成功','动作沿用惯用收益；未建模伤后的续局；一个回合至多4次冷静消耗，冷静5不会首伤','双方未知时对称选择，不算策略反例；成长概率来自R07同一官方FateStrip映射']),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
