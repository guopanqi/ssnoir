"""自由策略公开状态前向质量；复用转移，不是独立最优或真人选择频率。"""
from pathlib import Path
from collections import defaultdict
from functools import lru_cache
import runpy,json,gc
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'压力模型.py'))
Model,ODDS,HANDS,PAY=K['Model'],K['ODDS'],K['HANDS'],K['PAY'];GOAL=K['GOAL']

def run(objective):
 m=Model(objective);summary=m.summary()
 layers=[defaultdict(float) for _ in range(20)]
 for h,w in HANDS[4]:layers[19][(0,0,0,0,3,0,0,3,h,-1,0,False)]+=w
 total=[0.]*6;hist=defaultdict(float);stats=defaultdict(float);witnesses={};checked=0
 def finish(weight,p,c,k,t,flags,won):
  hist[flags]+=weight
  if won:
   total[3-t]+=weight;total[4]+=weight*k;total[5]+=weight*c
  total[3]+=weight*int(k>0)
 @lru_cache(None)
 def inspect(st,previous_bad):
  m.value(*st);choice=m.decisions[st];cs=m.candidates(*st)
  adv=[x for x in cs if x[1][0]==0];rep=[x for x in cs if x[1][0] in (1,2)]
  va=m.choose(adv)[0] if adv else None;vr=m.choose(rep)[0] if rep else None
  strict_repair=vr is not None and m.score(vr)>m.score(va)+1e-12
  strict_advance=vr is not None and m.score(va)>m.score(vr)+1e-12
  flipped=False;counter=None
  if previous_bad and strict_repair and choice[0] in (1,2) and st[3]>0:
   cf=list(st);cf[3]-=1;cf=tuple(cf)
   ccs=m.candidates(*cf);ca=m.choose([x for x in ccs if x[1][0]==0])[0];cr=m.choose([x for x in ccs if x[1][0] in (1,2)])[0]
   cv,cch=m.choose(ccs)
   flipped=m.score(ca)>m.score(cr)+1e-12 and cch[0]==0
   counter=dict(pressure_without_last_bad=cf[3],advance=ca,repair=cr,full_choice=cch,full_value=cv)
  return choice,strict_repair,strict_advance,flipped,va,vr,counter
 for layer in range(19,4,-1):
  current=layers[layer];layers[layer]=defaultdict(float)
  for extended,weight in current.items():
   st=extended[:10];flags,previous_bad=extended[10:]
   p,a,b,s,c,k,q,t,h,pref=st;checked+=1
   choice,sr,sa,flipped,va,vr,counter=inspect(st,previous_bad)
   route,act,d=choice
   if route in (1,2):
    flags|=1;stats['repair_actions_expected']+=weight
    if sr:flags|=2
    if flipped:
     flags|=8
     if not (1<=k<=3 and q):flags|=16
     key=(st,previous_bad)
     entry=witnesses.setdefault(key,dict(state=st,path_mass=0.,choice=choice,advance=va,repair=vr,counterfactual=counter,head_light=bool(1<=k<=3 and q)))
     entry['path_mass']+=weight
   elif route==0 and sa:flags|=4
   if route=='结束回合':
    ns=s+int(a>0)+int(b>0)
    for z,nc,nk,nq in m.spend(c,k,q,1):
     w=weight*z
     if nk>=7 or ns>=5 or t<=1:finish(w,p,nc,nk,t,flags,False)
     else:
      for hh,mass in HANDS[3 if nk>=4 else 4]:
       nst=(p,a,b,ns,nc,nk,nq,t-1,hh,-1,flags,False)
       nl=5*(t-1)+len(hh);assert nl<layer;layers[nl][nst]+=w*mass
    continue
   tail=list(h);tail.remove(d);tail=tuple(tail);skill=0 if 1<=k<=3 and q else 1
   for grade,(prob,(gain,dc)) in enumerate(zip(ODDS(d,skill),PAY[act])):
    if not prob:continue
    ns=s+int(route==0 and grade==0)
    for z,nc,nk,nq in m.spend(c,k,q,-dc):
     w=weight*prob*z
     if nk>=7 or ns>=5:finish(w,p,nc,nk,t,flags,False);continue
     pp,aa,bb=p,a,b
     if route==0:
      pp=min(GOAL,p+gain)
      if p<3<=pp:aa=2
      if p<6<=pp:bb=2
     elif route==1:aa=max(0,a-gain)
     else:bb=max(0,b-gain)
     if pp==GOAL:finish(w,pp,nc,nk,t,flags,True)
     else:
      nst=(pp,aa,bb,ns,nc,nk,nq,t,tail,-1,flags,route==0 and grade==0)
      nl=5*t+len(tail);assert nl<layer;layers[nl][nst]+=w
 assert abs(sum(hist.values())-1)<1e-10
 error=max(abs(x-y) for x,y in zip(total,summary['value']));assert error<1e-9,(objective,total,summary['value'])
 assert total[0]==0
 labels={1:'at_least_one_repair',2:'at_least_one_strict_repair',4:'at_least_one_strict_advance_with_repair_available',8:'at_least_one_immediate_pressure_flip',16:'at_least_one_flip_without_current_head_light'}
 out=dict(objective=objective,forward_value=total,max_error=error,probability_mass=sum(hist.values()),public_history_states=checked,events={name:sum(w for f,w in hist.items() if f&bit) for bit,name in labels.items()},repair_actions_expected=stats['repair_actions_expected'],witnesses=sorted(witnesses.values(),key=lambda x:x['path_mass'],reverse=True)[:10],scope='选定自由最优策略的模型质量；即时压力翻转保持当前其他公开状态，仅取消上一步坏档那1压力，且反事实全局最优所选动作确为推进；无头部轻伤指翻转当时，不是全程无伤；非真人频率或正式访问')
 print(json.dumps({k:v for k,v in out.items() if k!='witnesses'},ensure_ascii=False),flush=True)
 m.value.cache_clear();m.end.cache_clear();inspect.cache_clear();del m;gc.collect()
 return out

def main():
 rows=[run(x) for x in ('completion','early')]
 mean=sum(sum(w*gain for w,(gain,dc) in zip(ODDS(d,1),PAY['快做'])) for d in range(1,7))/6
 (HERE/'可达调整.json').write_text(json.dumps(dict(date='2026-10-03',results=rows,nominal_risk_progress_per_die=mean,nominal_twelve_risk_progress=12*mean,scope='前向概率核对及公开反事实分类；共享主转移，不算独立优化；正式0真人0'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
