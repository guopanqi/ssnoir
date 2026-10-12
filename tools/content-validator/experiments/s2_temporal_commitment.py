#!/usr/bin/env python3
"""SSNoir S2-C exact Fraction review: R116 dual day deadlines;
R117 strongest legible public rule for R115; R118 pay to preserve.
Synthetic bounded paper ONLY; no human or native C#/Scheme evidence.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json
G=((0,F(1,3)),(1,F(1,3)),(2,F(1,3)))
def r116():
 def study(av,bv):
  def available(a,b,n,deadline):return (['A'] if a<2 and (not deadline or n>2) else [])+(['B'] if b<3 else [])
  def go(a,b,n,act):return [(pr,min(2,a+g) if act=='A' else a,min(3,b+g) if act=='B' else b,n-1) for g,pr in G]
  def score(a,b):return av*F(a>=2)+bv*F(b>=3)
  @lru_cache(None)
  def DP(a,b,n,deadline):
   acts=available(a,b,n,deadline)
   if n==0 or not acts:return score(a,b)
   return max(sum(pr*DP(aa,bb,nn,deadline) for pr,aa,bb,nn in go(a,b,n,act)) for act in acts)
  def value(rule):
   @lru_cache(None)
   def V(a,b,n):
    acts=available(a,b,n,True)
    if n==0 or not acts:return score(a,b)
    return sum(pr*V(aa,bb,nn) for pr,aa,bb,nn in go(a,b,n,rule(a,b,n,acts)))
   return V(0,0,4)
  controls=[]
  for first in ('A','B'):
   controls.append((value(lambda a,b,n,opts,first=first:first if first in opts else ('A' if 'A' in opts else 'B')),first+'-first'))
  for cutoff,min_progress in product(range(1,5),range(3)):
   def rule(a,b,n,opts,cutoff=cutoff,min_progress=min_progress):
    if 'A' in opts and (n>cutoff or a>=min_progress):return 'A'
    return 'B' if 'B' in opts else 'A'
   controls.append((value(rule),f'A if n>{cutoff} or progress>={min_progress}'))
  best=max(controls)
  full=DP(0,0,4,True);free=DP(0,0,4,False)
  assert free>=full>=best[0]
  cases=[]
  for a,b,n in [(0,0,4),(0,0,3),(1,0,3),(0,1,3)]:
   qs={act:str(sum(pr*DP(aa,bb,nn,True) for pr,aa,bb,nn in go(a,b,n,act))) for act in available(a,b,n,True)}
   cases.append({'state':[a,b,n],'q':qs})
  return {'A_reward':str(av),'B_reward':str(bv),'optimal':str(full),'remove_day1_deadline':str(free),'deadline_cost':str(free-full),'best_simple':{'rule':best[1],'score':str(best[0]),'gap':str(full-best[0])},'example_states':cases}
 return {'r':'R116','scope':'Day1 2 actions; A goal2 expires end day1; B goal3 until end day2','rows':[study(a,F(2)) for a in (F(1),F(3,2),F(2),F(3))]}
def r117():
 def study(side,tax):
  def solve(heuristic=False):
   @lru_cache(None)
   def V(p,hot,done,n):
    if n==0:return 2*F(p>=4)+side*F(done)
    charge=tax*F(hot)
    qs={'steady':V(min(4,p+1),False,done,n-1)-charge,
        'risk':(V(p,False,done,n-1)+V(min(4,p+2),True,done,n-1))/2-charge}
    if hot:qs['repair']=V(p,False,done,n-1)
    if not done:qs['side']=V(p,False,True,n-1)
    if not heuristic:return max(qs.values())
    # Human-readable fixed public rule; never reads a DP Q value.
    if hot and not done:return qs['side']
    if done:return qs['steady' if p+n>=4 else 'risk']
    if n==1:return qs['side']
    if n==2:return qs['steady']
    return qs['risk']
   return V(0,False,False,4)
  optimal=solve();rule=solve(True)
  assert optimal>=rule
  return {'side_reward':str(side),'tax':str(tax),'optimal':str(optimal),'simple_public_rule':str(rule),'remaining_gap':str(optimal-rule)}
 rows=[study(s,t) for s,t in product((F(1,2),F(1),F(3,2)),(F(1,2),F(1)))]
 assert all(r['remaining_gap']=='0' for r in rows if r['side_reward']!='1/2')
 return {'r':'R117','scope':'Directly falsify R115 complexity with 5-clause transparent state rule','rows':rows}
def r118():
 def study(ra,rb):
  def reward(a,b):return ra*F(a>=3)+rb*F(b>=3)
  def acts(a,b,h,n,permit):return (['A'] if a<3 else [])+(['B'] if b<3 else [])+(['preserve'] if permit and n>2 and not h and 0<a<3 else [])
  def trans(a,b,h,n,act):
   if act=='preserve':return [(F(1),a,b,True,n-1)]
   return [(pr,min(3,a+g) if act=='A' else a,min(3,b+g) if act=='B' else b,h,n-1) for g,pr in G]
  def solve(can=True,free=False,rule=None):
   @lru_cache(None)
   def V(a,b,h,n):
    if n==0:return reward(a,b)
    actions=acts(a,b,h,n,can)
    if not actions:return reward(a,b)
    opts={}
    for x in actions:
     total=F(0)
     for pr,aa,bb,hh,nn in trans(a,b,h,n,x):
      if nn==2 and aa<3 and not hh:aa=0
      total+=pr*V(aa,bb,hh,nn)
     opts[x]=total
    return opts[rule(a,b,h,n,actions)] if rule else max(opts.values())
   return V(0,0,free,4)
  full=solve();none=solve(can=False);free=solve(free=True)
  controls=[]
  for first in ('A','B'):
   def rule(a,b,h,n,acts,first=first):
    if first=='A' and 'preserve' in acts and n==3:return 'preserve'
    if first in acts:return first
    return 'B' if 'B' in acts else 'A'
   controls.append((solve(rule=rule),first))
  best=max(controls)
  assert free>=full>=none and full>=best[0]
  return {'A_reward':str(ra),'B_reward':str(rb),'optimal':str(full),'never_preserve':str(none),'automatic_free_preserve':str(free),'paid_preserve_gain':str(full-none),'best_simple':{'priority':best[1],'score':str(best[0]),'gap':str(full-best[0])}}
 return {'r':'R118','scope':'Two days, two actions each; incomplete A resets unless day1 preserve action; B persists','rows':[study(a,b) for a,b in product((F(1),F(2),F(3)),(F(1),F(2)))]}
def main():
 report={'stage':'S2-C','scope':'Three synthetic exact Fraction paper studies, no native or human evidence','studies':[r116(),r117(),r118()]}
 print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
