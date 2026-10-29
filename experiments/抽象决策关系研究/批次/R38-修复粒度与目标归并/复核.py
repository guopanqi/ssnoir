"""强族包含/精确融合核对，以及复用独立末回合Fraction核对局部修复。"""
from pathlib import Path
from itertools import combinations_with_replacement
from math import factorial,comb
from fractions import Fraction as F
from functools import lru_cache
import json,runpy
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE/'修复目标.py'));M=K['K']['Model']
I=runpy.run_path(str(HERE.parent/'R36-资源余量与选择增量/未来一轮独立核对.py'))
used_future_cases=set()
@lru_cache(None)
def future(left,k,s,n):
 used_future_cases.add((left,k,s,n))
 total=F(0)
 for h in combinations_with_replacement(range(1,7),n):
  w=factorial(n)
  for d in set(h):w//=factorial(h.count(d))
  total+=F(w,6**n)*I['exact'](left,k,s,h)
 return total

def local(a,b,d,r,risky):
 total=F(0)
 for grade,count in enumerate(I['STRIPS'][d]):
  gain=grade if risky else int(grade==2);cost=int(grade==0 or risky and grade==1)
  aa,bb=a,b
  if r==1:aa=max(0,a-gain)
  else:bb=max(0,b-gain)
  nk=2+cost+1;ns=3+int(aa>0)+int(bb>0)
  if ns<5 and nk<7:total+=F(count,6)*future(4,nk,ns,3 if nk>=4 else 4)
 return total

def main():
 rows=json.loads((HERE/'修复目标结果.json').read_text())['results']
 frozen=json.loads((HERE.parent/'R36-资源余量与选择增量/压力结果.json').read_text())['results']
 fusion=containment=free_matches=0;fusion_error=full_error=0.
 for objective in ('completion','early'):
  by={r['repair_target_style']:r for r in rows if r['objective']==objective};free=next(r for r in frozen if r['objective']==objective and r['policy']=='自由' and r['instant_pressure']);m=M(objective)
  for i in range(126):
   v,style=m.choose([(by[x]['perhand'][i]['value'],x) for x in ('short','long')]);actual=by['initial']['perhand'][i]['value'];fusion_error=max(fusion_error,max(abs(a-b) for a,b in zip(v,actual)));fusion+=1
   scores={x:m.score(by[x]['perhand'][i]['value']) for x in by}
   assert scores['initial']>=max(scores['short'],scores['long'])-1e-10 and scores['round']>=scores['initial']-1e-10 and m.score(free['perhand'][i]['value'])>=scores['round']-1e-10;containment+=1
   for a,b in zip(by['short']['perhand'][i]['value'],free['perhand'][i]['value']):full_error=max(full_error,abs(a-b));free_matches+=1
 assert fusion_error<1e-10 and full_error<1e-10
 m=M();checks=[]
 for d in (1,6):
  st=(8,1,2,3,0,2,0,2,(d,),-1)
  for r in (1,2):
   for act,risky in (('稳做',False),('快做',True)):
    expected=local(1,2,d,r,risky);actual=sum(m.action_value(*st,r,act,d)[:3]);assert abs(float(expected)-actual)<1e-12
    checks.append(dict(die=d,route=r,action=act,exact=str(expected),completion=float(expected),max_error=abs(float(expected)-actual)))
 ending=[]
 for a,b in ((2,0),(1,1)):
  s=3+int(a>0)+int(b>0);expected=F(0) if s>=5 else future(4,3,s,4);actual=sum(m.end(8,a,b,3,0,2,0,2,-1)[:3]);assert abs(float(expected)-actual)<1e-12
  ending.append(dict(remaining=[a,b],total=a+b,pressure_after_end=s,completion=float(expected),exact=str(expected)))
 sym=[]
 for st in ((8,1,2,3,0,2,0,2,(1,6),-1),(8,1,2,2,0,3,1,2,(1,6),-1),(6,0,2,2,1,0,0,2,(2,5),-1),(9,1,2,3,0,4,1,2,(1,4),-1)):
  swap=list(st);swap[1],swap[2]=st[2],st[1];v=m.value(*st);w=m.value(*swap);error=max(abs(a-b) for a,b in zip(v,w));assert error<1e-10;sym.append(dict(state=st,max_error=error))
 out=dict(batch='R38',initial_fusion_checks=fusion,initial_fusion_max_error=fusion_error,strong_family_containment_checks=containment,short_vs_free_six_component_checks=free_matches,short_vs_free_max_error=full_error,local_independent_action_checks=checks,independent_ending_checks=ending,independent_future_hand_conditions=sum(comb(n+5,n) for left,k,s,n in used_future_cases),independent_future_cases=sorted(used_future_cases),swap_checks=sym,scope='初始族融合/包含与换名核对共享主转移；8首动作及2结束状态复用独立Fraction末回合递推，182未来手牌条件；非全三回合独立优化，许可状态不称正常访问；正式0真人0',passed=True)
 (HERE/'复核.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v for k,v in out.items() if k not in ('local_independent_action_checks','swap_checks')},ensure_ascii=False))
if __name__=='__main__':main()
