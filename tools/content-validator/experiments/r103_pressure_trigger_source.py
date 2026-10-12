#!/usr/bin/env python3
"""R103: SAME exogenous 1/3 trigger frequency, different causal source.
Four actions, work outcomes 0/1/2 equally likely; work on good (2)
or bad (0) creates one repairable problem. Work reaches primary goal5;
completion instantly settles, with remaining problems penalized.
Repair spends one action to erase one problem. Compare unrestricted DP,
never repair, and strongest publicly reactive repair threshold policy.
Independent bounded PAPER proxy of P11; not native R37, not human.
"""
from functools import lru_cache
from fractions import Fraction as F
from itertools import product
import json,sys

def evaluate(mode,penalty):
 def score(p,h):return F(min(5,p)+2*(p>=5)-penalty*h)
 def hit(g):return int((g==2) if mode=="success" else (g==0))
 @lru_cache(None)
 def dp(p,h,n):
  if n==0 or p>=5:return score(p,h)
  w=sum(dp(min(5,p+g),h+hit(g),n-1) for g in (0,1,2))/3
  r=dp(p,h-1,n-1) if h else F(-100)
  return max(w,r)
 @lru_cache(None)
 def nofix(p,h,n):
  if n==0 or p>=5:return score(p,h)
  return sum(nofix(min(5,p+g),h+hit(g),n-1) for g in (0,1,2))/3
 def threshold(k,minleft):
  @lru_cache(None)
  def v(p,h,n):
   if n==0 or p>=5:return score(p,h)
   if h>=k and n>=minleft:return v(p,h-1,n-1)
   return sum(v(min(5,p+g),h+hit(g),n-1) for g in (0,1,2))/3
  return v(0,0,4)
 rules={(k,left):threshold(k,left) for k,left in product(range(1,5),range(1,5))}
 strongest=max(rules,key=rules.get)
 @lru_cache(None)
 def forward(p,h,n):
  if n==0 or p>=5:return F(0)
  w=sum(dp(min(5,p+g),h+hit(g),n-1) for g in (0,1,2))/3
  r=dp(p,h-1,n-1) if h else F(-100)
  if r>w:
   return F(1)+forward(p,h-1,n-1)
  return sum(forward(min(5,p+g),h+hit(g),n-1) for g in (0,1,2))/3
 free=dp(0,0,4);strong=rules[strongest];baseline=nofix(0,0,4)
 assert free>=strong>=baseline
 return {'source':mode,'penalty_per_problem':penalty,'optimal_utility':float(free),
         'never_repair':float(baseline),'best_threshold_utility':float(strong),
         'best_threshold_h_min_left':list(strongest),
         'additional_adaptivity':float(free-strong),
         'strict_repair_visits_on_optimal_paths':float(forward(0,0,4))}
def main():
 rows=[evaluate(m,p) for p in (1,2,3) for m in ('success','failure')]
 low={x['source']:x for x in rows if x['penalty_per_problem']==1}
 high={x['source']:x for x in rows if x['penalty_per_problem']==2}
 assert low['failure']['strict_repair_visits_on_optimal_paths']>low['success']['strict_repair_visits_on_optimal_paths']
 assert low['failure']['additional_adaptivity']>0
 assert high['failure']['additional_adaptivity']==high['success']['additional_adaptivity']==0
 out={'study':'R103','scope':'Exact Fraction paper only, identical nominal per-work trigger chance 1/3; source correlated with progress; completion auto-closes repair','results':rows}
 s=json.dumps(out,ensure_ascii=False,indent=2)+'\n'
 if len(sys.argv)>1:
  from pathlib import Path
  Path(sys.argv[1]).write_text(s,encoding='utf8')
 else:print(s)
if __name__=='__main__':main()
