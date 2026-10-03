"""R28有限数学模型；复用R21伤势语义与概率输入，不是正式执行或玩家证据。"""
from pathlib import Path
from functools import lru_cache
import json,runpy,time,gc,hashlib
HERE=Path(__file__).resolve().parent
R21=HERE.parent/'R21-伤势与目标偏好/伤势模型.py'
K=runpy.run_path(str(R21));HANDS=K['HANDS'];ODDS=K['ODDS'];PAY=K['PAY']
POLICIES=('自由','禁止探查','开局先探查','每回合首手探查','最多探查一次','首回合只取信息','直到定位先探查')
class Model:
 def __init__(self,reset=True,policy='自由',penalty=0.):
  self.reset,self.policy,self.penalty=reset,policy,penalty
  self.value=lru_cache(None)(self._value);self.end=lru_cache(None)(self._end)
 def score(self,v):return v[0]-self.penalty*v[1]
 def choose(self,cs):
  top=max(self.score(v) for v,ch in cs);band=[(v,ch) for v,ch in cs if top-self.score(v)<=1e-12]
  for j in (1,2):
   low=min(v[j] for v,ch in band);band=[(v,ch) for v,ch in band if v[j]-low<=1e-12]
  top=max(v[3] for v,ch in band);return next((v,ch) for v,ch in band if top-v[3]<=1e-12)
 @staticmethod
 def fail(k):return (0.,float(k>0),0.,0.)
 spend=staticmethod(K['Model'].spend)
 def _end(self,a,b,target,c,k,q,t,u):
  out=[0.]*4
  for p,cc,kk,qq in self.spend(c,k,q,1):
   if kk>=7 or t<=1:vals=[(1.,self.fail(kk))]
   else:
    aa=0 if self.reset and a<3 else a;bb=0 if self.reset and b<3 else b
    vals=[(w,self.value(aa,bb,target,cc,kk,qq,t-1,u,hh)) for hh,w in HANDS[3 if kk>=4 else 4]]
   for w,v in vals:
    for j in range(4):out[j]+=p*w*v[j]
  return tuple(out)
 def action_value(self,a,b,target,c,k,q,t,u,h,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);out=[0.]*4
  used=int(u or route==2) if self.policy=='最多探查一次' else 0
  for grade,(w,(gain,dc)) in enumerate(zip(ODDS(d,0 if 1<=k<=3 and q else 1),PAY[act])):
   if not w:continue
   aa=min(3,a+gain) if route==0 else a;bb=min(3,b+gain) if route==1 else b
   reveal=route==2 and grade==2 or aa==3 or bb==3
   worlds=[(1.,target)] if target>=0 else [(0.5,0),(0.5,1)] if reveal else [(1.,-1)]
   for p,cc,kk,qq in self.spend(c,k,q,-dc):
    for z,tr in worlds:
     v=self.fail(kk) if kk>=7 else self.value(aa,bb,tr,cc,kk,qq,t,used,rest)
     for j in range(4):out[j]+=w*p*z*v[j]
  return tuple(out)
 def candidates(self,a,b,target,c,k,q,t,u,h):
  end=(self.end(a,b,target,c,k,q,t,u),('结束回合',None,None));out=[end]
  routes=[target] if target>=0 else [0,1,2]
  if self.policy=='禁止探查' or self.policy=='最多探查一次' and u:routes=[r for r in routes if r!=2]
  if target<0 and (self.policy=='直到定位先探查' or self.policy=='开局先探查' and t==2 and len(h)==4 or self.policy=='每回合首手探查' and len(h)==(3 if k>=4 else 4)):
   routes=[2]
   if h:out=[] # 第一手的明确参照，不允许偷偷提前结束回合规避限制
  if self.policy=='首回合只取信息' and t==2:routes=[2] if target<0 else []
  for route in routes:
   for act in ('稳做',) if route==2 else ('稳做','快做'):
    for d in sorted(set(h)):
     out.append((self.action_value(a,b,target,c,k,q,t,u,h,route,act,d),(route,act,d)))
  return out
 def _value(self,a,b,target,c,k,q,t,u,h):
  if k>=7:return self.fail(k)
  if target==0 and a==3 or target==1 and b==3:return (1.,float(k>0),float(k),float(c))
  return self.choose(self.candidates(a,b,target,c,k,q,t,u,h))[0]
 def summary(self):
  start=time.perf_counter();v=[0.]*4
  for h,w in HANDS[4]:
   x=self.value(0,0,-1,5,0,0,2,0,h)
   for j in range(4):v[j]+=w*x[j]
  return dict(reset=self.reset,policy=self.policy,penalty=self.penalty,actual_completion=v[0],ever_injured=v[1],injury_on_success=v[2]/v[0] if v[0] else None,cold_on_success=v[3]/v[0] if v[0] else None,research_utility=self.score(v),states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
def main():
 rows=[];initial=[]
 for reset in (True,False):
  for policy in POLICIES:
   m=Model(reset,policy);r=m.summary();rows.append(r);print(json.dumps(r,ensure_ascii=False),flush=True)
   if policy=='自由':
    for h,w in HANDS[4]:
     s=(0,0,-1,5,0,0,2,0,h);cs=m.candidates(*s);best,ch=m.choose(cs)
     rv={str(route):m.choose([(v,x) for v,x in cs if x[0]==route]) for route in (0,1,2)}
     initial.append(dict(reset=reset,hand=h,probability=w,choice=ch,value=best,route_values={r:dict(value=v,choice=x) for r,(v,x) in rv.items()},probe_completion_gap=rv['2'][0][0]-max(rv['0'][0][0],rv['1'][0][0])))
   m.value.cache_clear();m.end.cache_clear();del m;gc.collect()
 (HERE/'结果.json').write_text(json.dumps(dict(results=rows,initial=initial,scope='有限数学模型；定位整局保留；固定真值未知时对称惰性揭晓；完整伤势；公平独立骰/首次四部位均匀是假设；本批正式0局真人0局',dependencies={str(R21):hashlib.sha256(R21.read_bytes()).hexdigest()}),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
