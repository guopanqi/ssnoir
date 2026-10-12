#!/usr/bin/env python3
"""SSNoir S2-B: R113 goal-unlock, R114 slack-capacity debt negative control,
R115 binding-capacity productive detour. All exact-Fraction synthetic paper."""
from fractions import Fraction as F
from functools import lru_cache
from itertools import permutations,product
import json
G=((0,F(1,3)),(1,F(1,3)),(2,F(1,3)))
def r113():
 def study(B):
  def reward(A,Bp,C):return F(A>=2,2)+B*F(Bp>=2)+2*F(C>=2)
  def actions(A,Bp,C,kind):
   return (['A'] if A<2 else [])+(['B'] if Bp<2 else [])+(['C'] if C<2 and (kind=='ungated' or kind=='gated' and A>=2) else [])
  def transitions(A,Bp,C,n,act):
   return [(pr,(min(2,A+g) if act=='A' else A,min(2,Bp+g) if act=='B' else Bp,min(2,C+g) if act=='C' else C,n-1)) for g,pr in G]
  @lru_cache(None)
  def V(A,Bp,C,n,kind):
   aa=actions(A,Bp,C,kind)
   if n==0 or not aa:return reward(A,Bp,C)
   return max(sum(pr*V(*child[:3],child[3],kind) for pr,child in transitions(A,Bp,C,n,act)) for act in aa)
  def fixed(order):
   @lru_cache(None)
   def U(A,Bp,C,n):
    aa=actions(A,Bp,C,'gated')
    if n==0 or not aa:return reward(A,Bp,C)
    act=next(x for x in order if x in aa)
    return sum(pr*U(*child) for pr,child in transitions(A,Bp,C,n,act))
   return U(0,0,0,4)
  full=V(0,0,0,4,'gated');off=V(0,0,0,4,'disabled');free=V(0,0,0,4,'ungated')
  best=max((fixed(order),order) for order in permutations(('A','B','C')))
  assert free>=full>=off and full>=best[0]
  return {'B_reward':str(B),'full':str(full),'no_C_option':str(off),'C_open_from_start':str(free),'added_value':str(full-off),'gating_cost':str(free-full),'best_fixed_priority_order':{'value':str(best[0]),'order':list(best[1]),'gap':str(full-best[0])}}
 rows=[study(v) for v in (F(1),F(3,2),F(2),F(5,2))]
 return {'r':'R113','scope':'Each of A/B/C needs2 progress; completing A unlocks C; four actions','results':rows}
def debt(goal,side_reward,penalty,with_threshold=False):
 def solve(allow_side=True,cools=True,issue=True,policy=None):
  @lru_cache(None)
  def V(p,hot,done,n):
   if n==0:return 2*F(p>=goal)+side_reward*F(done)
   tax=penalty*F(hot and issue)
   opts={'steady':V(min(goal,p+1),False,done,n-1)-tax,
         'risk':(V(p,False,done,n-1)+V(min(goal,p+2),issue,done,n-1))/2-tax}
   if hot and issue:opts['repair']=V(p,False,done,n-1)
   if allow_side and not done:opts['side']=V(p,False if cools else hot,True,n-1)
   if policy is not None:return opts[policy(p,hot,done,n,opts)]
   return max(opts.values())
  return V(0,False,False,4)
 full=solve();none=solve(allow_side=False);uncoupled=solve(cools=False);safe=solve(issue=False)
 assert safe>=full>=none and full>=uncoupled
 result={'goal':goal,'side_reward':str(side_reward),'work_tax':str(penalty),'optimal':str(full),'no_side':str(none),'side_does_not_clear_problem':str(uncoupled),'problem_disabled':str(safe),'coupling_gain':str(full-uncoupled)}
 if with_threshold:
  candidates=[]
  for only_hot,hot_min,progress_limit,risk_before,risk_catchup in product((True,False),range(1,5),range(0,5),range(0,5),(True,False)):
   def policy(p,hot,done,n,opts,only_hot=only_hot,hot_min=hot_min,progress_limit=progress_limit,risk_before=risk_before,risk_catchup=risk_catchup):
    if 'side' in opts and (hot and n>=hot_min or not only_hot and p>=progress_limit):return 'side'
    if p<risk_before or risk_catchup and p+n<goal:return 'risk'
    return 'steady'
   candidates.append((solve(policy=policy),only_hot,hot_min,progress_limit,risk_before,risk_catchup))
  best=max(candidates)
  assert full>=best[0]
  result['best_simple_threshold']={'value':str(best[0]),'gap':str(full-best[0]),'only_help_when_hot':best[1],'min_remaining_for_hot_help':best[2],'help_if_progress_ge':best[3],'risky_if_progress_lt':best[4],'risky_if_safe_finish_impossible':best[5]}
 else:
  def always(p,hot,done,n,opts):return 'side' if 'side' in opts else 'steady'
  def steady(p,hot,done,n,opts):return 'steady'
  def repair(p,hot,done,n,opts):return 'repair' if 'repair' in opts else ('side' if 'side' in opts else 'steady')
  simple=max(solve(policy=x) for x in (always,steady,repair))
  result['best_of_three_fixed_priorities']=str(simple)
  result['gap_above_priorities']=str(full-simple)
 return result
def r114():
 rows=[debt(3,v,c) for v,c in product((F(1,2),F(1),F(3,2)),(F(1,2),F(1)))]
 assert all(r['coupling_gain']=='0' for r in rows)
 return {'r':'R114','description':'With goal3 and four actions, guaranteed three steady jobs + one side job trivializes pressure','results':rows}
def r115():
 rows=[debt(4,v,c,with_threshold=True) for v,c in product((F(1,2),F(1),F(3,2)),(F(1,2),F(1)))]
 assert next(r for r in rows if r['side_reward']=='1' and r['work_tax']=='1')['coupling_gain']=='1/4'
 return {'r':'R115','description':'One additional required main progress forces tradeoff; positive coupling vs no-clear ablation','results':rows}
def main():
 print(json.dumps({'stage':'S2-B screening','scope':'Exact Fraction independent bounded paper models, no native/human evidence','studies':[r113(),r114(),r115()]},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
