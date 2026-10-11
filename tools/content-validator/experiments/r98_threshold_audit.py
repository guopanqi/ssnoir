#!/usr/bin/env python3
"""R98: strong comprehensible policy family for R95's paper reserve timing.
Not original P10 native play. Competes on same 126 hand set, can select
best threshold policy per opening hand and best available die after results.
"""
from itertools import combinations_with_replacement,product
from fractions import Fraction as F
from functools import lru_cache
from math import factorial
import json,sys
H=tuple(combinations_with_replacement(range(1,7),4))
def wt(h):
 w=F(24,1296)
 for v in set(h):w/=factorial(h.count(v))
 return w
assert sum(wt(h) for h in H)==1
def dice(d):
 b,m={1:(2,3),2:(1,3),3:(1,2),4:(0,2),5:(0,1),6:(0,0)}[d]
 return [(0,F(b,6)),(1,F(m,6)),(2,F(6-b-m,6))]
def solve(nonlinear,decay,goal=6):
 def payout(r):return r*(r+1)//2 if nonlinear else 2*r
 def nexts(p,r,hand,act,die):
  for outcome,pr in (dice(die) if act=='P' else [(0,F(1))]):
   if not pr:continue
   if act=='P':pp,rr=p,max(0,min(5,r+{0:-2,1:1,2:2}[outcome]))
   elif act=='C':pp,rr=min(goal,p+payout(r)),0
   else:pp,rr=min(goal,p+1),r
   if len(hand)==3:rr=max(0,rr-decay)
   yield pr,pp,rr
 def q(p,r,h,a,i,cont):
  rem=h[:i]+h[i+1:]
  return sum(pr*cont(pp,rr,rem) for pr,pp,rr in nexts(p,r,h,a,h[i]))
 @lru_cache(None)
 def opt(p,r,h):
  if p>=goal:return F(1)
  if not h:return F(0)
  return max(q(p,r,h,a,i,opt) for a in ('P','G','C') if a!='C' or r>0 for i in range(len(h)))
 # Candidates are state-conditional but require no probability arithmetic.
 # k: cash at reserve >=k, boundary: whether cash before decay,
 # finish: cash if sufficient to finish, spare: whether guaranteed +1 to finish.
 rules=list(product(range(1,6),(False,True),(False,True),(False,True)))
 @lru_cache(None)
 def scripted(p,r,h,rule):
  if p>=goal:return F(1)
  if not h:return F(0)
  k,boundary,finish,spare=rule
  if spare and p==goal-1:a='G'
  elif r>0 and finish and p+payout(r)>=goal:a='C'
  elif r>0 and boundary and len(h)==3:a='C'
  elif r>=k:a='C'
  elif len(h)==1:a='C' if r else 'G'
  else:a='P'
  return max(q(p,r,h,a,i,lambda pp,rr,rem:scripted(pp,rr,rem,rule)) for i in range(len(h)))
 expected_free=F(0);expected_simple=F(0);deficit_hands=0;root_examples=[]
 for hand in H:
  b=opt(0,0,hand);a=max(scripted(0,0,hand,rule) for rule in rules)
  assert b>=a
  expected_free+=wt(hand)*b
  expected_simple+=wt(hand)*a
  if b>a:
   deficit_hands+=1
   if len(root_examples)<5:root_examples.append({'hand':list(hand),'optimal':float(b),'threshold':float(a)})
 global_rule=max(rules,key=lambda rule:sum(wt(h)*scripted(0,0,h,rule) for h in H))
 global_score=sum(wt(h)*scripted(0,0,h,global_rule) for h in H)
 return {'nonlinear':nonlinear,'decay':decay,'optimal':float(expected_free),
         'strong_threshold':float(expected_simple),'remaining_gain':float(expected_free-expected_simple),
         'global_threshold':float(global_score),'global_rule':list(global_rule),
         'deficit_hands':deficit_hands,'tested_rules':len(rules),'examples':root_examples}

def main():
 x={'scope':'Exact Fraction, R95 compressed two-round model; NOT full native P10',
    'baseline':'best of 40 state-conditional threshold rules separately per opening hand, optimal die after every observation',
    'cases':[solve(nl,dec) for nl,dec in product((True,False),(0,2))]}
 s=json.dumps(x,ensure_ascii=False,indent=2)+'\n'
 if len(sys.argv)>1:open(sys.argv[1],'w',encoding='utf8').write(s)
 else:print(s)
if __name__=='__main__':main()
