from pathlib import Path
from collections import defaultdict
from functools import lru_cache
from fractions import Fraction as F
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'分配模型.py'));TABLE=K['K']['T']
def reach(skills):
 m=K['Model'](skills=skills);expected=m.summary();levels=defaultdict(lambda:defaultdict(float));success=harm=0.;failure=0.;events=0.;any_event=0.;examples=[]
 for h,w in K['HANDS']:levels[4][(3,3,h,5,False)]+=w
 for rank in range(4,-1,-1):
  for state,mass in list(levels[rank].items()):
   a,b,h,c,flag=state
   if a==b==0:
    success+=mass;harm+=mass*(5-c)
    if flag:any_event+=mass
    continue
   m.value(a,b,h);route,act,d=m.decisions[(a,b,h)]
   if route=='结束回合':
    failure+=mass
    if flag:any_event+=mass
    continue
   strict=False
   if a>0 and b>0 and len(set(h))>1:
    high_weak=max(v[0] for v,ch in m.candidates(a,b,h) if ch[0]==0 and ch[2]==max(h))
    gap=m.value(a,b,h)[0]-high_weak
    if gap>.01:
     strict=True;events+=mass
     examples.append(dict(remaining=[a,b],hand=h,cold=c,visit_mass=mass,choice=[route,act,d],optimal_completion=m.value(a,b,h)[0],best_highest_die_on_weak_now=high_weak,gap=gap))
   rest=list(h);rest.remove(d);rest=tuple(rest)
   for w,(gain,cost) in zip(K['ODDS'](d,skills[route]),K['PAY'][act]):
    if not w:continue
    aa,bb=(max(0,a-gain),b) if route==0 else (a,max(0,b-gain));assert c-cost>=1
    levels[len(rest)][(aa,bb,rest,c-cost,flag or strict)]+=mass*w
 assert abs(success+failure-1)<1e-12 and abs(success-expected['completion'])<1e-12
 assert abs(5-harm/success-expected['cold_on_success'])<1e-12
 examples.sort(key=lambda x:x['visit_mass'],reverse=True)
 return dict(skills=skills,completion=success,probability_at_least_one_strict_high_weak_exception=any_event,expected_exception_events=events,examples=examples[:8],scope='最优公开策略正常起局；例外表示此刻强制最大骰给弱项会损失超过1个百分点；不是完整固定分区策略差异，也不是人类犹豫率')
def prob(d,g):
 b=d+g;return tuple(F(v,6) for v in TABLE['<= 1' if b<=1 else '_' if b>=7 else str(b)])
@lru_cache(None)
def exact(a,b,h,g0,g1):
 if a==b==0:return F(1),F(0)
 values=[(F(0),F(0))]
 for route,r in enumerate((a,b)):
  if r==0:continue
  for d in set(h):
   rest=list(h);rest.remove(d);rest=tuple(rest)
   for outcomes in (((0,1),(0,0),(1,0)),((0,1),(1,1),(2,0))):
    total=[F(0),F(0)]
    for w,(gain,cost) in zip(prob(d,(g0,g1)[route]),outcomes):
     if not w:continue
     aa=max(0,a-gain) if route==0 else a;bb=max(0,b-gain) if route==1 else b
     p,damage=exact(aa,bb,rest,g0,g1);total[0]+=w*p;total[1]+=w*(damage+cost*p)
    values.append(tuple(total))
 return max(values,key=lambda v:(v[0],-v[1]))
def main():
 reaches=[reach((1,1)),reach((1,2)),reach((1,3))];(HERE/'可达分析.json').write_text(json.dumps(reaches,ensure_ascii=False,indent=2)+'\n')
 checks=[]
 for skills in ((1,1),(1,2),(1,3)):
  m=K['Model'](skills=skills)
  for h,w in K['HANDS']:
   independent=exact(3,3,h,*skills);actual=m.value(3,3,h)
   assert all(abs(float(a)-b)<1e-12 for a,b in zip(independent,actual))
   checks.append(dict(skills=skills,hand=h,completion=str(independent[0]),weighted_harm=str(independent[1]),passed=True))
 (HERE/'独立核对.json').write_text(json.dumps(dict(cases=len(checks),checks=checks,scope='3组能力×全部126手牌；独立Fraction全单回合递推，概率输入共用正式表'),ensure_ascii=False,indent=2)+'\n')
 for r in reaches:print({k:v for k,v in r.items() if k!='examples'});print(r['examples'][:2])
 print('独立核对',len(checks),'全部通过')
if __name__=='__main__':main()
