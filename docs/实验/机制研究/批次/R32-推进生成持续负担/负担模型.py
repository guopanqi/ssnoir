"""R32：推进触发负担，分别优化完成与3/2/1早完成收益。有限数学，非正式会话。"""
from pathlib import Path
from functools import lru_cache
import runpy,json,time,gc
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE.parent/'R21-伤势与目标偏好/伤势模型.py'))
ODDS,HANDS,PAY=K['ODDS'],K['HANDS'],K['PAY']
POLICIES=('自由','只推进','有负担先修','容量规则','当前技能正概率容量','每回合固定偏好')
class Model:
 def __init__(self,objective='completion',policy='自由'):
  assert objective in ('completion','early');assert policy in POLICIES
  self.objective,self.policy=objective,policy
  self.value=lru_cache(None)(self._value);self.end=lru_cache(None)(self._end)
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
  high=max(v[5] for v,ch in pool);return next((v,ch) for v,ch in pool if high-v[5]<=1e-12)
 def _end(self,p,a,b,c,k,q,t,pref):
  out=[0.]*6
  for w,cc,kk,qq in self.spend(c,k,q,1+int(a>0)+int(b>0)):
   if kk>=7 or t<=1:children=[(1.,self.fail(kk))]
   else:
    nextpref=-1 if self.policy=='每回合固定偏好' else pref
    children=[(z,self.value(p,a,b,cc,kk,qq,t-1,hh,nextpref)) for hh,z in HANDS[3 if kk>=4 else 4]]
   for z,v in children:
    for j in range(6):out[j]+=w*z*v[j]
  return tuple(out)
 def action_value(self,p,a,b,c,k,q,t,h,pref,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);out=[0.]*6
  for w,(gain,dc) in zip(ODDS(d,0 if 1<=k<=3 and q else 1),PAY[act]):
   if not w:continue
   for z,cc,kk,qq in self.spend(c,k,q,-dc):
    if kk>=7:v=self.fail(kk)
    else:
     pp,aa,bb=p,a,b
     if route==0:
      pp=min(8,p+gain)
      if p<3<=pp:aa=2
      if p<6<=pp:bb=2
     elif route==1:aa=max(0,a-gain)
     else:bb=max(0,b-gain)
     v=self.value(pp,aa,bb,cc,kk,qq,t,rest,pref)
    for j in range(6):out[j]+=w*z*v[j]
  return tuple(out)
 def candidates(self,p,a,b,c,k,q,t,h,pref):
  repairs=[route for route,left in ((1,a),(2,b)) if left]
  routes=[0]+repairs
  if self.policy=='只推进':routes=[0]
  elif self.policy=='有负担先修':routes=repairs or [0]
  elif self.policy=='容量规则':routes=[0] if 8-p<=2*len(h) else repairs or [0]
  elif self.policy=='当前技能正概率容量':
   skill=0 if 1<=k<=3 and q else 1
   capacity=sum(max(gain for w,(gain,dc) in zip(ODDS(d,skill),PAY['快做']) if w>0) for d in h)
   routes=[0] if 8-p<=capacity else repairs or [0]
  elif self.policy=='每回合固定偏好':
   assert pref in (0,1)
   routes=[0] if pref==0 else repairs or [0]
  return [(self.end(p,a,b,c,k,q,t,pref),('结束回合',None,None))]+[(self.action_value(p,a,b,c,k,q,t,h,pref,r,act,d),(r,act,d)) for r in routes for act in ('稳做','快做') for d in sorted(set(h))]
 def _value(self,p,a,b,c,k,q,t,h,pref=-1):
  if k>=7:return self.fail(k)
  if p==8:
   v=[0.]*6;v[3-t]=1.;v[3]=float(k>0);v[4]=float(k);v[5]=float(c);return tuple(v)
  assert (p>=3 or a==0) and (p>=6 or b==0),(p,a,b)
  if self.policy=='每回合固定偏好' and pref==-1:
   return self.choose([(self.value(p,a,b,c,k,q,t,h,x),('选择偏好',x,None)) for x in (0,1)])[0]
  return self.choose(self.candidates(p,a,b,c,k,q,t,h,pref))[0]
 def summary(self):
  start=time.perf_counter();out=[0.]*6;perhand=[]
  for h,w in HANDS[4]:
   v=self.value(0,0,0,3,0,0,3,h,-1);perhand.append(dict(hand=h,probability=w,value=v))
   for j in range(6):out[j]+=w*v[j]
  return dict(objective=self.objective,policy=self.policy,value=out,actual_completion=sum(out[:3]),completion_by_round=out[:3],early_reward=3*out[0]+2*out[1]+out[2],ever_injured=out[3],injury_on_success=out[4]/sum(out[:3]) if sum(out[:3]) else None,cold_on_success=out[5]/sum(out[:3]) if sum(out[:3]) else None,states=self.value.cache_info().currsize,seconds=time.perf_counter()-start,perhand=perhand)
def main():
 rows=[]
 for objective in ('completion','early'):
  family=[]
  for policy in POLICIES:
   m=Model(objective,policy);r=m.summary();family.append(r);rows.append(r);print(json.dumps({k:v for k,v in r.items() if k!='perhand'},ensure_ascii=False),flush=True)
   (HERE/'计算中间结果.json').write_text(json.dumps(dict(results=rows,status='未完成全部条件，不用于裁决'),ensure_ascii=False,indent=2)+'\n')
   m.value.cache_clear();m.end.cache_clear();del m;gc.collect()
  chooser=Model(objective);out=[0.]*6;choices=[]
  for i,(h,w) in enumerate(HANDS[4]):
   v,ch=chooser.choose([(r['perhand'][i]['value'],r['policy']) for r in family if r['policy']!='自由']);choices.append(dict(hand=h,probability=w,policy=ch,value=v))
   for j in range(6):out[j]+=w*v[j]
   assert chooser.score(family[0]['perhand'][i]['value'])>=chooser.score(v)-1e-10,(objective,h)
  row=dict(objective=objective,policy='开局择最佳简单族',value=out,actual_completion=sum(out[:3]),completion_by_round=out[:3],early_reward=3*out[0]+2*out[1]+out[2],ever_injured=out[3],injury_on_success=out[4]/sum(out[:3]) if sum(out[:3]) else None,cold_on_success=out[5]/sum(out[:3]) if sum(out[:3]) else None,perhand=choices)
  rows.append(row);print(json.dumps({k:v for k,v in row.items() if k!='perhand'},ensure_ascii=False),flush=True)
  base=chooser.score(family[0]['value'])
  for r in rows:
   if r['objective']==objective:r['objective_gap']=base-chooser.score(r['value']);assert r['objective_gap']>=-1e-10,r
 (HERE/'负担结果.json').write_text(json.dumps(dict(results=rows,initial_hand_inclusion_checks=252,scope='R32冻结目标8与跨3/6各生成需修2负担；纯完成及早完成收益3/2/1；完整伤势；公平独立骰/首次部位均匀为假设；原生0真人0'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
