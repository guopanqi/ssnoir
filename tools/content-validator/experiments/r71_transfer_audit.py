#!/usr/bin/env python3
"""R71 detailed option audit: does costly progress transfer actually get used?

Frozen R71: A cap2 value1, B cap3 value2; both only scored at TERMINAL.
Action transfer takes one die, converts one existing A progress to one B
progress; no FateStrip outcome is rolled for conversion. Completed-but-
unbanked A progress can be sacrificed. This is NOT the R55 banked system.
"""
from functools import lru_cache
from fractions import Fraction as F
from itertools import combinations_with_replacement
from r56_r65_autonomous_search import odds,hand_weight

N=4
def step(s,action,gain):
 a,b=s
 if action==0:a=min(2,a+gain)
 elif action==1:b=min(3,b+gain)
 elif action==2:
  assert a>=1
  a-=1;b=min(3,b+1)
 return a,b
def payoff(s):return F(s[0]>=2)+2*F(s[1]>=3)
def solve(transfer=True):
 @lru_cache(None)
 def optimal(s,hand):
  if not hand or s==(2,3):return payoff(s)
  allowed=(0,1,2) if transfer and s[0]>=1 else (0,1)
  return max(q(s,hand,g,i) for g in allowed for i in range(len(hand)))
 def q(s,hand,g,i):
  rest=hand[:i]+hand[i+1:]
  if g==2:return optimal(step(s,g,0),rest)
  return sum(p*optimal(step(s,g,k),rest) for k,p in enumerate(odds(hand[i])) if p)
 @lru_cache(None)
 def forward(s,hand):
  if not hand or s==(2,3):
   return F(0),F(0),F(0),F(0),F(s[0]>=2),F(s[1]>=3),F(s==(2,3))
  allowed=(0,1,2) if transfer and s[0]>=1 else (0,1)
  choices=[(q(s,hand,g,i),g,i) for g in allowed for i in range(len(hand))]
  best=max(v for v,g,i in choices)
  # Prefer nontransfer when tied, lower die id for reproducible path.
  _,g,i=next((v,g,i) for v,g,i in choices if v==best)
  competitors=max((v for v,g2,i2 in choices if g2!=2),default=F(-999))
  strict=F(g==2 and best>competitors)
  result=[F(g==2),strict,strict,F(g==2),F(0),F(0),F(0)]
  branches=[(F(1),step(s,g,0))] if g==2 else [
       (p,step(s,g,k)) for k,p in enumerate(odds(hand[i])) if p]
  rest=hand[:i]+hand[i+1:]
  for prob,newstate in branches:
   arr=forward(newstate,rest)
   result[0]+=prob*arr[0]
   result[1]+=prob*arr[1]
   result[2]+=prob*(F(0) if strict else arr[2])
   result[3]+=prob*(F(0) if g==2 else arr[3])
   for j in (4,5,6):result[j]+=prob*arr[j]
  return tuple(result)
 ev=F(0); root_witness=0; masses=[F(0)]*7
 all_hands=list(combinations_with_replacement(range(1,7),N))
 for hand in all_hands:
  w=hand_weight(hand);ev+=w*optimal((0,0),hand)
  if transfer:
   v=optimal((0,0),hand)
   if v>solve_no_transfer_root(hand):root_witness+=1
  metrics=forward((0,0),hand)
  for j in range(7):masses[j]+=w*metrics[j]
 return {"expected_reward":round(float(ev),9),"roots_where_transfer_is_useful":root_witness if transfer else None,
   "expected_transfer_count":round(float(masses[0]),9),
   "expected_strict_transfer_count":round(float(masses[1]),9),
   "probability_of_any_strict_transfer":round(float(masses[2]),9),
   "probability_of_any_transfer":round(float(masses[3]),9),
   "terminal_A":round(float(masses[4]),9),
   "terminal_B":round(float(masses[5]),9),
   "terminal_both":round(float(masses[6]),9)}
@lru_cache(None)
def solve_no_transfer_root(hand):
 @lru_cache(None)
 def f(s,rest):
  if not rest or s==(2,3):return payoff(s)
  return max(sum(p*f(step(s,g,k),rest[:i]+rest[i+1:])
                 for k,p in enumerate(odds(d)) if p)
             for g in (0,1) for i,d in enumerate(rest))
 return f((0,0),hand)
def main():
 import json
 with_transfer=solve(True)
 without_transfer=solve(False)
 assert with_transfer["expected_reward"]>=without_transfer["expected_reward"]
 assert with_transfer["probability_of_any_strict_transfer"]<=with_transfer["probability_of_any_transfer"]
 print(json.dumps({"id":"R71","version":"R67-R75 frozen paper R71",
   "transfer_allowed":with_transfer,"transfer_forbidden":without_transfer,
   "expected_option_value":round(with_transfer["expected_reward"]-without_transfer["expected_reward"],9),
   "provenance":"Exact Fraction; 126 root hands; chosen optimal policy tie-break nontransfer; not a human trial."},
   ensure_ascii=False,indent=2))
if __name__=="__main__":main()
