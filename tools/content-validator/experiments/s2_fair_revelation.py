#!/usr/bin/env python3
"""R121 fairness repair of R119's same-action reveal+close overshoot.
A jump 1->3 can now leave one (and only one) following-action chance to
pursue C. This changes only the opportunity closure ordering, not rewards,
durations or dice. Independently executable exact-Fraction paper model.
Not official authored content or a human-validated player choice.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json
G=((0,F(1,3)),(1,F(1,3)),(2,F(1,3)))
def study(B):
 def legal(a,b,c,n,grace,on):
  allow_C=c<2 and (a==2 or on and a==3 and grace)
  return (['A'] if a<3 else [])+(['B'] if b<2 and n>3 else [])+(['C'] if allow_C else [])
 def steps(a,b,c,n,grace,act,on):
  for gain,pr in G:
   aa=min(3,a+gain) if act=='A' else a
   bb=min(2,b+gain) if act=='B' else b
   cc=min(2,c+gain) if act=='C' else c
   new_grace=bool(on and act=='A' and a==1 and gain==2)
   yield pr,aa,bb,cc,n-1,new_grace
 def reward(a,b,c):return 2*F(a>=3)+B*F(b>=2)+2*F(c>=2)
 @lru_cache(None)
 def V(a,b,c,n,grace,on):
  aa=legal(a,b,c,n,grace,on)
  if n==0 or not aa:return reward(a,b,c)
  return max(sum(p*V(aa,bb,cc,nn,gg,on) for p,aa,bb,cc,nn,gg in steps(a,b,c,n,grace,x,on)) for x in aa)
 def policy_value(abandon_b,abandon_c,urgent_b,secure_a):
  @lru_cache(None)
  def U(a,b,c,n,grace):
   options=legal(a,b,c,n,grace,True)
   if n==0 or not options:return reward(a,b,c)
   if grace and 'C' in options:choice='C'
   elif 'C' in options:
    if urgent_b and 'B' in options and n<=4 and b>=1:choice='B'
    elif 'A' in options and n<=abandon_c+1 and c==0:choice='A'
    else:choice='C'
   elif 'B' in options:
    if 'A' in options and n==4 and b<abandon_b:choice='A'
    elif 'A' in options and secure_a and n>=5 and a>=2:choice='A'
    else:choice='B'
   else:choice='A'
   return sum(p*U(aa,bb,cc,nn,gg) for p,aa,bb,cc,nn,gg in steps(a,b,c,n,grace,choice,True))
  return U(0,0,0,6,False)
 full=V(0,0,0,6,False,True);old=V(0,0,0,6,False,False)
 assert full>=old
 best=max((policy_value(ab,ac,pb,sa),ab,ac,pb,sa) for ab,ac,pb,sa in product(range(3),range(3),(True,False),(True,False)))
 assert full>=best[0]
 grace_mass=F(0);strict_C_mass=F(0)
 def traverse(a,b,c,n,grace,m):
  nonlocal grace_mass,strict_C_mass
  aa=legal(a,b,c,n,grace,True)
  if n==0 or not aa:return
  if grace and 'C' in aa:grace_mass+=m
  q={act:sum(pr*V(aaa,bbb,ccc,nn,gg,True) for pr,aaa,bbb,ccc,nn,gg in steps(a,b,c,n,grace,act,True)) for act in aa}
  best_value=max(q.values());action=next(x for x in ('B','C','A') if x in q and q[x]==best_value)
  if grace and action=='C' and len(q)>1 and q[action]>max(v for x,v in q.items() if x!=action):strict_C_mass+=m
  for pr,aaa,bbb,ccc,nn,gg in steps(a,b,c,n,grace,action,True):
   traverse(aaa,bbb,ccc,nn,gg,m*pr)
 traverse(0,0,0,6,False,F(1))
 return {'B_reward':str(B),'old_same_action_closure':str(old),'single_response_window':str(full),'value_gain':str(full-old),'best_public_priority':{'score':str(best[0]),'gap':str(full-best[0])},'normal_path_grace_window_visits':str(grace_mass),'strict_choice_C_over_other_in_grace_visits':str(strict_C_mass)}
def main():
 rows=[study(b) for b in (F(1),F(3,2),F(2))]
 mid=next(r for r in rows if r['B_reward']=='3/2')
 assert mid['old_same_action_closure']=='2359/729'
 assert mid['single_response_window']=='2447/729'
 assert mid['best_public_priority']['gap']=='0'
 print(json.dumps({'study':'R121','scope':'Exact Fraction semantic one-rule counterfactual; no native/human evidence','results':rows},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
