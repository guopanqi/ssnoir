"""R39独立2骰小树穷举和选定朝目标策略的精确前向传播。"""
from pathlib import Path
from fractions import Fraction as F
from itertools import combinations_with_replacement
from math import factorial
from collections import defaultdict
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'幅度模型.py'))
LITERAL={1:(2,3,1),2:(1,3,2),3:(1,2,3),4:(0,2,4),5:(0,1,5),6:(0,0,6)}
# 独立小树用距离目标区间的函数，无缓存、字面三档，不调用主solve。
def enumerate_tree(position,dice,target,policy):
 lo,hi=target
 if lo<=position<=hi:return F(1)
 if not dice:return F(0)
 signs=(1,-1) if policy=='free' else (1,) if policy=='forward' else (1,) if position<lo else (-1,)
 faces=(max(dice),) if policy=='highest' else set(dice);values=[]
 for face in faces:
  rest=list(dice);rest.remove(face)
  for sign in signs:
   for risky in (False,True):
    value=F(0)
    for result,num in enumerate(LITERAL[face]):
     shift=(0,1,2)[result] if risky else (0,0,1)[result]
     value+=F(num,6)*enumerate_tree(position+sign*shift,rest,target,policy)
    values.append(value)
 return max(values)
def av(m,x,h,d,sign,risky):
 tail=list(h);tail.remove(d);tail=tuple(tail)
 return sum((p*m.solve(x+sign*(g if risky else int(g==2)),tail) for g,p in enumerate(K['STRIPS'][d])),F(0))
def forward():
 m=K['Model']((3,3),'toward');summary=m.summary();layers=[defaultdict(F) for _ in range(5)]
 for h in combinations_with_replacement(range(1,7),4):
  mult=factorial(4)
  for d in set(h):mult//=factorial(h.count(d))
  layers[4][(0,h,0)]+=F(mult,6**4)
 hist=defaultdict(F);success_hist=defaultdict(F);won=F(0);witnesses={};states=0
 for n in range(4,-1,-1):
  for (x,h,flags),w in layers[n].items():
   states+=1
   if x==3 or not h:
    hist[flags]+=w;won+=w*int(x==3)
    if x==3:success_hist[flags]+=w
    continue
   sign,d,risky=m.choices[x,h]
   high=max(h);one_high=max(av(m,x,h,high,sg,rr) for sg in (1,-1) for rr in (False,True))
   if m.solve(x,h)>one_high:
    flags|=1
    entry=witnesses.setdefault((x,h),dict(x=x,hand=h,chosen=(sign,d,risky),path_mass='0',full=str(m.solve(x,h)),first_high_then_free=str(one_high)))
    entry['path_mass']=str(F(entry['path_mass'])+w)
   if x>3:
    flags|=2
    one_forward=max(av(m,x,h,dd,1,rr) for dd in set(h) for rr in (False,True))
    if m.solve(x,h)>one_forward and sign==-1:flags|=4
   tail=list(h);tail.remove(d);tail=tuple(tail)
   for grade,p in enumerate(K['STRIPS'][d]):
    if p:layers[n-1][(x+sign*(grade if risky else int(grade==2)),tail,flags)]+=w*p
 assert sum(hist.values())==1 and won==F(summary['exact_probability'])
 return dict(policy='toward',exact_completion=str(won),completion=float(won),history_states=states,events={name:float(sum(w for flags,w in hist.items() if flags&bit)) for bit,name in ((1,'at_least_one_strict_nonhighest_choice'),(2,'at_least_one_overshoot'),(4,'at_least_one_strict_return'))},overshoot_then_success=float(sum(w for flags,w in success_hist.items() if flags&2)),witnesses=sorted(witnesses.values(),key=lambda x:F(x['path_mass']),reverse=True)[:8],scope='选定朝目标最优策略；精确前向质量；严格非最高骰是本手限最高、续局仍自由，不是统计所有平手策略或真人行为')
def main():
 for d in range(1,7):
  assert K['STRIPS'][d]==tuple(F(x,6) for x in LITERAL[d])
  assert all(abs(float(F(num,6))-raw)<1e-12 for num,raw in zip(LITERAL[d],K['SOURCE']['ODDS'](d,1)))
 checks=0
 for target in ((3,4),(3,3)):
  for policy in K['POLICIES']:
   m=K['Model'](target,policy)
   for x in (0,2,4,6):
    for h in ((1,1),(1,6),(2,5),(3,4),(6,6)):
     assert m.solve(x,h)==enumerate_tree(x,list(h),target,policy);checks+=1
 flow=forward()
 # 同一局面所有结果：留6适配或校正1骰的0/1/2，但高骰先也可直接稳做，不能冒充严格反例。
 m=K['Model']((3,3),'free');case=dict(position=2,hand=[1,1],free=str(m.solve(2,(1,1))),forward=str(K['Model']((3,3),'forward').solve(2,(1,1))))
 assert case['free']=='3/4' and case['forward']=='2/3'
 out=dict(batch='R39',independent_small_tree_checks=checks,probability_table_checks=18,forward=flow,licensed_return_example=case,scope='独立无缓存2骰小树160条件＋字面概率18项；前向复用主递推；未独立重算全部四骰最优、原生0真人0',passed=True)
 (HERE/'复核.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v if k!='forward' else {key:val for key,val in v.items() if key!='witnesses'} for k,v in out.items()},ensure_ascii=False))
if __name__=='__main__':main()
