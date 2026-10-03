"""R29有限强控制。Float复用R25转移，Fraction独立公开观察分组；不是新引擎证据。"""
from pathlib import Path
from functools import lru_cache
from fractions import Fraction as F
from collections import defaultdict
import hashlib,json,runpy
HERE=Path(__file__).resolve().parent
SOURCE=HERE.parent/'R25-行动换取目标信息'
K=runpy.run_path(str(SOURCE/'信息投资.py'))
POLICIES=('最多一次','首手最高一次','首手中间一次','首手任骰一次','首手低骰口诀')

class Memory:
 def __init__(self,policy,g):
  self.policy,self.g=policy,g;self.decisions={};self.value=lru_cache(None)(self._value)
 def candidates(self,a,b,target,c,h,count):
  out=[((0.,0.),('结束回合',None,None))]
  routes=[target] if target else ['甲','乙']
  if not target:
   if self.policy=='最多一次':
    if count==0:routes+=['探查']
   elif len(h)==4:routes=['探查']
   elif self.policy=='首手低骰口诀' and count==1 and len(h)==3 and sum(d<=2 for d in h)<=1:
    routes=['探查']
  for route in routes:
   for act in ('稳做',) if route=='探查' else ('稳做','快做'):
    for d in sorted(set(h)):
     if route=='探查' and len(h)==4:
      if self.policy=='首手最高一次' and d!=max(h):continue
      if self.policy=='首手中间一次' and d not in h[1:3]:continue
     out.append((self.action_value(a,b,target,c,h,count,route,act,d),(route,act,d)))
  # End remains legal with zero completion and never beats any positive chance;
  # at zero chance this avoids pretending the policy invests for benefit.
  return out
 def action_value(self,a,b,target,c,h,count,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);out=[0.,0.]
  for grade,(p,(gain,dc)) in enumerate(zip(K['ODDS'](d,self.g),K['PAY'][act])):
   if not p:continue
   aa=min(3,a+gain) if route=='甲' else a;bb=min(3,b+gain) if route=='乙' else b;cc=c+dc
   assert cc>=0
   revealed=route=='探查' and grade==2 or aa==3 or bb==3
   branches=[(1.,target)] if target else [(.5,'甲'),(.5,'乙')] if revealed else [(1.,None)]
   for weight,t in branches:
    v=self.value(aa,bb,t,cc,rest,count+int(route=='探查'))
    for j in range(2):out[j]+=p*weight*v[j]
  return tuple(out)
 def _value(self,a,b,target,c,h,count):
  if target=='甲' and a==3 or target=='乙' and b==3:return 1.,float(c)
  v,ch=K['Model'].choose(self.candidates(a,b,target,c,h,count));self.decisions[(a,b,target,c,h,count)]=ch;return v

class Exact:
 """固定世界0/1；按公开成功、定位、无消息分组。独立限制、效果、递推。"""
 def __init__(self,policy,g):self.policy,self.g=policy,g;self.solve=lru_cache(None)(self._solve)
 def _solve(self,a,b,mask,c,h,n):
  if mask==1 and a==3 or mask==2 and b==3:return F(1),F(c)
  if mask!=3:routes=[0 if mask==1 else 1]
  elif self.policy=='最多一次':routes=[0,1]+([2] if n==0 else [])
  elif len(h)==4:routes=[2]
  elif self.policy=='首手低骰口诀' and n==1 and len(h)==3 and len([x for x in h if x<3])<2:routes=[2]
  else:routes=[0,1]
  best=(F(0),F(0))
  for r in routes:
   for risky in ((False,) if r==2 else (False,True)):
    for d in set(h):
     if r==2 and len(h)==4:
      if self.policy=='首手最高一次' and d!=h[-1]:continue
      if self.policy=='首手中间一次' and d not in (h[1],h[2]):continue
     tail=list(h);tail.remove(d);tail=tuple(tail);out=[F(0),F(0)]
     for grade,prob in enumerate(K['ODDS'](d,self.g)):
      prob=F(prob).limit_denominator(6)
      if not prob:continue
      delta=grade if risky else int(grade==2);cc=c-int(grade==0 or risky and grade==1)
      aa=min(3,a+delta) if r==0 else a;bb=min(3,b+delta) if r==1 else b
      worlds=[i for i in (0,1) if mask&(1<<i)];observations={}
      for world in worlds:
       success=(aa if world==0 else bb)==3
       reveal=mask!=3 or r==2 and grade==2 or aa==3 or bb==3
       obs=(aa,bb,cc,'success' if success else str(world) if reveal else 'unknown')
       mass,newmask=observations.get(obs,(F(0),0));observations[obs]=(mass+F(1,len(worlds)),newmask|(1<<world))
      for (na,nb,nc,label),(w,newmask) in observations.items():
       value=(F(1),F(nc)) if label=='success' else self.solve(na,nb,newmask,nc,tail,n+int(r==2))
       for j in range(2):out[j]+=prob*w*value[j]
     best=max(best,tuple(out))
  return best

def forward(model,g,original=False):
 layers=[defaultdict(float) for _ in range(5)]
 for h,w in K['HANDS']:layers[4][(0,0,None,5,h,0,False)]+=w
 stats=defaultdict(float);hist=defaultdict(float);firstfail=defaultdict(float)
 for left in range(4,-1,-1):
  for state,mass in layers[left].items():
   a,b,t,c,h,n,pending=state;st=state[:6]
   if original:
    key=(a,b,t,c,h);model.value(*key);ch=model.decisions[key]
   else:model.value(*st);ch=model.decisions[st]
   route,act,d=ch
   if pending and n==1:firstfail['repeat' if route=='探查' else 'search' if route in ('甲','乙') else 'end']+=mass
   if route=='结束回合':stats['timeout']+=mass;hist[str(n)]+=mass;continue
   if left==4:stats['initial_probe']+=mass*int(route=='探查')
   if route=='探查':stats['probe_actions_expected']+=mass
   rest=list(h);rest.remove(d);rest=tuple(rest)
   for grade,(p,(gain,dc)) in enumerate(zip(K['ODDS'](d,g),K['PAY'][act])):
    if not p:continue
    aa=min(3,a+gain) if route=='甲' else a;bb=min(3,b+gain) if route=='乙' else b;cc=c+dc
    reveal=route=='探查' and grade==2 or aa==3 or bb==3
    branches=[(1.,t)] if t else [(.5,'甲'),(.5,'乙')] if reveal else [(1.,None)]
    for w,nt in branches:
     ww=mass*p*w;nn=n+int(route=='探查')
     if nt=='甲' and aa==3 or nt=='乙' and bb==3:stats['completion']+=ww;hist[str(nn)]+=ww
     else:layers[left-1][(aa,bb,nt,cc,rest,nn,route=='探查' and grade!=2)]+=ww
 assert abs(stats['completion']+stats['timeout']-1)<1e-10
 stats['ever_probe']=sum(m for n,m in hist.items() if int(n)>=1)
 stats['at_least_two_probes']=sum(m for n,m in hist.items() if int(n)>=2)
 return dict(stats=dict(stats),probe_count_probability=dict(hist),first_unlocated_response_mass=dict(firstfail))

def main():
 rows=[];checks=[];witnesses=[];baselines=[]
 for g in (1,2):
  base=K['Model']('自由',g);free=base.summary();no=K['Model']('不探查',g).summary();baselines.append(dict(skill=g,free=free,no_probe=no,forward=forward(base,g,True)))
  for policy in POLICIES:
   m=Memory(policy,g);e=Exact(policy,g);p=cold=0.;err=0.;perhand=[]
   for h,w in K['HANDS']:
    v=m.value(0,0,None,5,h,0);exact=e.solve(0,0,3,5,h,0);full=base.value(0,0,None,5,h)
    err=max(err,*[abs(x-float(y)) for x,y in zip(v,exact)]);p+=w*v[0];cold+=w*v[1]
    gap=full[0]-v[0];assert gap>=-1e-10
    perhand.append(dict(hand=h,mass=w,completion=v[0],free_completion=full[0],gap_pp=100*gap))
   assert err<1e-10,(policy,g,err)
   fwd=forward(m,g);assert abs(fwd['stats']['completion']-p)<1e-10
   if policy!='首手低骰口诀':assert fwd['stats']['at_least_two_probes']==0
   row=dict(skill=g,policy=policy,completion=p,gap_to_free_pp=100*(free['completion']-p),gain_over_no_probe_pp=100*(p-no['completion']),fraction_of_information_gain=(p-no['completion'])/(free['completion']-no['completion']),cold_on_success=cold/p,initial_hands=126,float_states=m.value.cache_info().currsize,exact_states=e.solve.cache_info().currsize,max_error=err,forward=fwd,hand_mass_gap_gt_one_pp=sum(x['mass'] for x in perhand if x['gap_pp']>1),max_hand_gap_pp=max(x['gap_pp'] for x in perhand))
   rows.append(row);checks.append(dict(skill=g,policy=policy,hands=126,max_error=err,passed=True));print(json.dumps(row,ensure_ascii=False),flush=True)
   if policy=='最多一次':
    witnesses.append(dict(skill=g,policy=policy,hands=sorted(perhand,key=lambda x:x['gap_pp'],reverse=True)[:5],history_state=[0,0,None,5,[2,3,4]],unused_probe_value=m.value(0,0,None,5,(2,3,4),0),used_probe_value=m.value(0,0,None,5,(2,3,4),1)))
 out=dict(date='2026-10-02',baselines=baselines,results=rows,checks=checks,total_checked_initial_states=126*len(POLICIES)*2,at_most_once_witnesses=witnesses,scope='固定R25参数，仅技能1/2；Float复用R25转移，Fraction固定真值与观察分组独立实现；共享判定输入；非引擎/真人证据')
 (HERE/'强控制结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
 deps=[SOURCE/'信息投资.py',SOURCE/'固定真值复核.py',SOURCE/'正式对照.py',SOURCE/'可达选择.py',SOURCE/'正式对照/验证.json',HERE.parent/'R26-信息动作的支配条件/研究记录.md',HERE.parent/'R19-阶段转换与多回合组合/组合模型.py',HERE.parent/'R09-成长与行动中的信息/成长边界.py',HERE.parent/'R07-顺序与选择权/分段筛选.py',HERE/'强控制复核.py']
 (HERE/'版本.json').write_text(json.dumps(dict(date='2026-10-02',source_sha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in deps},scope='来源只读；无Content写入、无新原生会话；R25源路径及hash冻结'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
