#!/usr/bin/env python3
"""SSNoir S2-D R119/R120: one two-day / three-objective playable-paper
network and a strong public policy falsification. Independent Fraction model,
not authored canon, Scheme/C# engine or human experience.

Six actions (three each day). A letter pursuit goal3 worth2. B fighter-help
goal2 worth configurable reward, only actionable during first three actions.
C follows messenger evidence goal2 worth2, available only once A>=2 but closes
if A reaches3. Every action draws 0/1/2 equiprobably.
Important semantic defect: an A jump from 1 to 3 can skip the C opportunity
entirely. This model MUST NOT be directly promoted as a playable scene.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import permutations,product
import json
G=((0,F(1,3)),(1,F(1,3)),(2,F(1,3)))
def study(B):
 def payoff(a,b,c):return 2*F(a>=3)+B*F(b>=2)+2*F(c>=2)
 def acts(a,b,c,n,gate=True,close=True,due=True,side=True):
  return (['A'] if a<3 else [])+(['B'] if b<2 and (n>3 or not due) else [])+(['C'] if side and c<2 and (not gate or a>=2) and (not close or a<3) else [])
 def walk(a,b,c,n,x):
  return [(pr,min(3,a+g) if x=='A' else a,min(2,b+g) if x=='B' else b,min(2,c+g) if x=='C' else c,n-1) for g,pr in G]
 @lru_cache(None)
 def V(a,b,c,n,gate,close,due,side):
  options=acts(a,b,c,n,gate,close,due,side)
  if n==0 or not options:return payoff(a,b,c)
  return max(sum(pr*V(aa,bb,cc,nn,gate,close,due,side) for pr,aa,bb,cc,nn in walk(a,b,c,n,x)) for x in options)
 def solve(gate=True,close=True,due=True,side=True):return V(0,0,0,6,gate,close,due,side)
 def policy_value(decide):
  @lru_cache(None)
  def score(a,b,c,n):
   options=acts(a,b,c,n)
   if n==0 or not options:return payoff(a,b,c)
   x=decide(a,b,c,n,options)
   assert x in options
   return sum(pr*score(aa,bb,cc,nn) for pr,aa,bb,cc,nn in walk(a,b,c,n,x))
  return score(0,0,0,6)
 orders=[(policy_value(lambda a,b,c,n,options,order=o:next(x for x in order if x in options)),o) for o in permutations('ABC')]
 strongest_order=max(orders)
 options=[]
 for abandon_b,abandon_c,put_b_first,finish_early_a in product(range(3),range(3),(True,False),(True,False)):
  def rule(a,b,c,n,available,abandon_b=abandon_b,abandon_c=abandon_c,put_b_first=put_b_first,finish_early_a=finish_early_a):
   if 'C' in available:
    if put_b_first and 'B' in available and n<=4 and b>=1:return 'B'
    if 'A' in available and n<=abandon_c+1 and c==0:return 'A'
    return 'C'
   if 'B' in available:
    if 'A' in available and n==4 and b<abandon_b:return 'A'
    if 'A' in available and finish_early_a and n>=5 and a>=2:return 'A'
    return 'B'
   return 'A'
  options.append((policy_value(rule),abandon_b,abandon_c,put_b_first,finish_early_a))
 strongest=max(options)
 free=solve();off=solve(side=False);early=solve(gate=False);open_side=solve(close=False);relaxed=solve(due=False)
 assert free>=off and early>=free and open_side>=free and relaxed>=free
 assert free>=strongest_order[0] and free>=strongest[0]
 examples=[]
 strict_visits=F(0)
 def traverse(a,b,c,n,mass):
  nonlocal strict_visits
  available=acts(a,b,c,n)
  if not available or n==0:return
  qs={x:sum(pr*V(aa,bb,cc,nn,True,True,True,True) for pr,aa,bb,cc,nn in walk(a,b,c,n,x)) for x in available}
  best=max(qs.values())
  action=next(x for x in ('B','C','A') if x in qs and qs[x]==best)
  fixed=next(x for x in strongest_order[1] if x in available)
  if action!=fixed and qs[action]>qs[fixed]:
   strict_visits+=mass
   if len(examples)<3:examples.append({'state':[a,b,c,n],'weight':str(mass),'optimal':action,'fixed_priority':fixed,'q_values':{k:str(v) for k,v in qs.items()}})
  for pr,aa,bb,cc,nn in walk(a,b,c,n,action):
   traverse(aa,bb,cc,nn,mass*pr)
 traverse(0,0,0,6,F(1))
 return {'B_reward':str(B),'optimal':str(free),'disable_side_C':str(off),'remove_unlock_gate':str(early),'remove_closure':str(open_side),'remove_B_day1_due':str(relaxed),'added_C_value':str(free-off),'closure_opportunity_cost':str(open_side-free),'fixed_priority':{'value':str(strongest_order[0]),'order':list(strongest_order[1]),'gap':str(free-strongest_order[0])},'strong_public_rule':{'value':str(strongest[0]),'gap':str(free-strongest[0]),'abandon_last_day1_B_below':strongest[1],'prefer_A_over_unstarted_C_when_remaining_le':strongest[2]+1,'B_first_if_B_at_1_on_day1':strongest[3],'secure_A_early_if_progress_at_2':strongest[4]},'strict_dynamic_vs_fixed_expected_visits':str(strict_visits),'examples':examples}
def main():
 rows=[study(v) for v in (F(1),F(3,2),F(2))]
 core=rows[1]
 assert core['optimal']=='2359/729'
 assert core['fixed_priority']['gap']=='1/6'
 assert core['strong_public_rule']['gap']=='0'
 assert all(F(x['strong_public_rule']['gap'])>=0 for x in rows)
 print(json.dumps({'stage':'S2-D','studies':['R119','R120'],'evidence':'Six-action Fraction micro-city paper. NOT playable-native/canon/human. Unsound overshoot closure must be addressed before playtest.','results':rows},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
