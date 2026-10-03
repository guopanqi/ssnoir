"""R34不可逆失败压力；有限数学，非正式会话。复用R21概率和伤势。"""
from pathlib import Path
from functools import lru_cache
import runpy,json,time,gc
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE.parent/'R21-伤势与目标偏好/伤势模型.py'))
ODDS,HANDS,PAY=K['ODDS'],K['HANDS'],K['PAY']
POLICIES=('自由','只推进','有负担先修','容量规则','每回合固定偏好')
class Model:
 def __init__(self,objective='completion',policy='自由',fatal_pressure=True):
  assert objective in ('completion','early') and policy in POLICIES
  self.objective,self.policy,self.fatal_pressure=objective,policy,fatal_pressure
  self.decisions={};self.value=lru_cache(None)(self._value);self.end=lru_cache(None)(self._end)
 spend=staticmethod(K['Model'].spend)
 @staticmethod
 def fail(k):return (0.,0.,0.,float(k>0),0.,0.)
 def score(self,v):return sum(v[:3]) if self.objective=='completion' else 3*v[0]+2*v[1]+v[2]
 def choose(self,cs):
  top=max(self.score(v) for v,ch in cs);pool=[(v,ch) for v,ch in cs if top-self.score(v)<=1e-12]
  if self.objective=='early':
   high=max(sum(v[:3]) for v,ch in pool);pool=[(v,ch) for v,ch in pool if high-sum(v[:3])<=1e-12]
  for j in (3,4):
   low=min(v[j] for v,ch in pool);pool=[(v,ch) for v,ch in pool if v[j]-low<=1e-12]
  high=max(v[5] for v,ch in pool)
  return next((v,ch) for v,ch in pool if high-v[5]<=1e-12)
 def _end(self,p,a,b,s,c,k,q,t,pref):
  ns=s+int(a>0)+int(b>0);out=[0.]*6
  for w,nc,nk,nq in self.spend(c,k,q,1):
   if nk>=7 or t<=1 or self.fatal_pressure and ns>=3:children=[(1.,self.fail(nk))]
   else:
    npref=-1 if self.policy=='每回合固定偏好' else pref
    children=[(z,self.value(p,a,b,ns,nc,nk,nq,t-1,h,npref)) for h,z in HANDS[3 if nk>=4 else 4]]
   for z,v in children:
    for j in range(6):out[j]+=w*z*v[j]
  return tuple(out)
 def action_value(self,p,a,b,s,c,k,q,t,h,pref,route,act,d):
  tail=list(h);tail.remove(d);tail=tuple(tail);out=[0.]*6
  skill=0 if 1<=k<=3 and q else 1
  for w,(gain,dc) in zip(ODDS(d,skill),PAY[act]):
   if not w:continue
   for z,nc,nk,nq in self.spend(c,k,q,-dc):
    if nk>=7:v=self.fail(nk)
    else:
     pp,aa,bb=p,a,b
     if route==0:
      pp=min(8,p+gain)
      if p<3<=pp:aa=2
      if p<6<=pp:bb=2
     elif route==1:aa=max(0,a-gain)
     else:bb=max(0,b-gain)
     v=self.value(pp,aa,bb,s,nc,nk,nq,t,tail,pref)
    for j in range(6):out[j]+=w*z*v[j]
  return tuple(out)
 def candidates(self,p,a,b,s,c,k,q,t,h,pref):
  repairs=[r for r,left in ((1,a),(2,b)) if left];routes=[0]+repairs
  if self.policy=='只推进':routes=[0]
  elif self.policy=='有负担先修':routes=repairs or [0]
  elif self.policy=='容量规则':routes=[0] if 8-p<=2*len(h) else repairs or [0]
  elif self.policy=='每回合固定偏好':
   assert pref in (0,1);routes=[0] if pref==0 else repairs or [0]
  return [(self.end(p,a,b,s,c,k,q,t,pref),('结束回合',None,None))]+[(self.action_value(p,a,b,s,c,k,q,t,h,pref,r,act,d),(r,act,d)) for r in routes for act in ('稳做','快做') for d in sorted(set(h))]
 def _value(self,p,a,b,s,c,k,q,t,h,pref=-1):
  if k>=7 or self.fatal_pressure and s>=3:return self.fail(k)
  if p==8:
   out=[0.]*6;out[3-t]=1.;out[3]=float(k>0);out[4]=float(k);out[5]=float(c);return tuple(out)
  assert (p>=3 or a==0) and (p>=6 or b==0) and s>=0
  if self.policy=='每回合固定偏好' and pref==-1:
   v,ch=self.choose([(self.value(p,a,b,s,c,k,q,t,h,x),('选择偏好',x,None)) for x in (0,1)])
  else:v,ch=self.choose(self.candidates(p,a,b,s,c,k,q,t,h,pref))
  self.decisions[(p,a,b,s,c,k,q,t,h,pref)]=ch
  return v
 def summary(self):
  start=time.perf_counter();out=[0.]*6;perhand=[]
  for h,w in HANDS[4]:
   v=self.value(0,0,0,0,3,0,0,3,h);perhand.append(dict(hand=h,probability=w,value=v))
   for j in range(6):out[j]+=w*v[j]
  return dict(objective=self.objective,policy=self.policy,fatal_pressure=self.fatal_pressure,value=out,actual_completion=sum(out[:3]),completion_by_round=out[:3],early_reward=3*out[0]+2*out[1]+out[2],ever_injured=out[3],injury_on_success=out[4]/sum(out[:3]) if sum(out[:3]) else None,cold_on_success=out[5]/sum(out[:3]) if sum(out[:3]) else None,states=self.value.cache_info().currsize,seconds=time.perf_counter()-start,perhand=perhand)
def main():
 rows=[]
 for obj in ('completion','early'):
  families=[]
  for fatal,policy in [(True,x) for x in POLICIES]+[(False,'自由')]:
   m=Model(obj,policy,fatal);r=m.summary();rows.append(r)
   if fatal:families.append(r)
   print(json.dumps({k:v for k,v in r.items() if k!='perhand'},ensure_ascii=False),flush=True)
   m.value.cache_clear();m.end.cache_clear();del m;gc.collect()
  chooser=Model(obj);out=[0.]*6;perhand=[]
  for i,(h,w) in enumerate(HANDS[4]):
   v,ch=chooser.choose([(r['perhand'][i]['value'],r['policy']) for r in families if r['policy']!='自由'])
   assert chooser.score(families[0]['perhand'][i]['value'])>=chooser.score(v)-1e-10
   perhand.append(dict(hand=h,probability=w,value=v,policy=ch))
   for j in range(6):out[j]+=w*v[j]
  row=dict(objective=obj,policy='开局择最佳简单族',fatal_pressure=True,value=out,actual_completion=sum(out[:3]),completion_by_round=out[:3],early_reward=3*out[0]+2*out[1]+out[2],ever_injured=out[3],perhand=perhand)
  rows.append(row);print(json.dumps({k:v for k,v in row.items() if k!='perhand'},ensure_ascii=False),flush=True)
  base=chooser.score(families[0]['value'])
  for row in rows:
   if row['objective']==obj:row['gap_to_free']=base-chooser.score(row['value'])
 (HERE/'压力结果.json').write_text(json.dumps(dict(date='2026-10-03',results=rows,scope='冻结R34数学：负担进全局不可逆压力3，不扣冷静，税1仍存；压力阈值单独消融；共享R21输入；正式0真人0；两目标分别优化'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
