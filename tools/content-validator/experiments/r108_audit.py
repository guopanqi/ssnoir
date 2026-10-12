#!/usr/bin/env python3
"""R108 exact Fraction audit of R105. Paper model; no native C#/Scheme."""
from functools import lru_cache
from fractions import Fraction as F
from itertools import product
import json

# R105: a=clamped progress, k=unexcluded sites (k=1 is located), got=site reward,
# n=actions remaining. work progress +0/+1/+2 with equiprobable results,
# and natural free location with probability q; explore consumes an action,
# success 1/k or eliminates one site; probe consumes one action and reveals site.

def study(K,q):
  q=F(q)
  def transitions(a,k,got,n,action):
    if action=='work':
      assert a<3
      return [(F(1,3)*c,min(3,a+g),kk,got,n-1) for g in range(3)
        for c,kk in ([(q,1),(1-q,k)] if k>1 else [(F(1),1)]) if c]
    if action=='explore':
      assert not got
      return [(F(1,k),a,k,True,n-1),(F(k-1,k),a,k-1,False,n-1)] if k>1 else [(F(1),a,1,True,n-1)]
    if action=='probe':
      assert k>1 and not got
      return [(F(1),a,1,got,n-1)]
    raise ValueError(action)
  def actions(a,k,got,n,allow=True):
    return (["work"] if a<3 else [])+(["explore"] if not got else [])+(["probe"] if allow and k>1 and not got else [])
  def terminal(a,k,got,n):
    return n==0 or (a>=3 and got)
  def payoff(a,k,got,n):return F(a>=3)+2*F(got)
  @lru_cache(None)
  def V(a,k,got,n,allow=True):
    if terminal(a,k,got,n):return payoff(a,k,got,n)
    return max(sum(p*V(aa,kk,gg,nn,allow) for p,aa,kk,gg,nn in transitions(a,k,got,n,act)) for act in actions(a,k,got,n,allow))
  def qvalues(a,k,got,n):
    return {act:sum(p*V(aa,kk,gg,nn,True) for p,aa,kk,gg,nn in transitions(a,k,got,n,act)) for act in actions(a,k,got,n)}
  def simple_policy(family,t):
    # 'work-first': ordinary work until primary complete, then exploration/probe.
    # 'explore-first': exploration/probe until site obtained, then work.
    # 'explore-one': explore once if still K candidates; after failure consider probe;
    # 'work-one': work once first, then pursue side goal unless free information resolves it.
    # t is (largest explore-first k, progress threshold, remaining-actions cutoff).
    site_limit, progress_limit, remaining_cutoff=t
    def policy(a,k,got,n):
      acts=actions(a,k,got,n)
      if 'work' in acts and family=='work-first':return 'work'
      if 'work' in acts and family=='work-one' and n==4:return 'work'
      if 'work' in acts and family=='work-until' and a<progress_limit and n>remaining_cutoff:return 'work'
      if 'explore' in acts and family=='explore-one' and k==K and n==4:return 'explore'
      if 'explore' in acts:
        if k>site_limit and n>1 and 'probe' in acts:return 'probe'
        return 'explore'
      return 'work'
    return policy
  def evaluate(policy):
    @lru_cache(None)
    def val(a,k,got,n):
      if terminal(a,k,got,n):return payoff(a,k,got,n)
      act=policy(a,k,got,n)
      return sum(p*val(aa,kk,gg,nn) for p,aa,kk,gg,nn in transitions(a,k,got,n,act))
    return val(0,K,False,4)
  baselines=[]
  for family in ('work-first','explore-first','explore-one','work-one','work-until'):
    for t in product(range(1,K+1),range(1,4),range(1,4)):
      baselines.append((evaluate(simple_policy(family,t)),family,t))
  best=max(baselines)
  # Normal optimal forward distribution. Tie-breaking same as R105: work > explore > probe.
  visits={}; chosen={}; strict_by_action={};
  def traverse(a,k,got,n,mass):
    if terminal(a,k,got,n):return
    state=(a,k,got,n)
    visits[state]=visits.get(state,F(0))+mass
    qs=qvalues(a,k,got,n); b=max(qs.values())
    act=next(x for x in ('work','explore','probe') if x in qs and qs[x]==b)
    chosen[state]=act
    if len(qs)>1 and b>max(v for x,v in qs.items() if x!=act):
      strict_by_action[act]=strict_by_action.get(act,F(0))+mass
    for p,aa,kk,gg,nn in transitions(a,k,got,n,act):
      traverse(aa,kk,gg,nn,mass*p)
  traverse(0,K,False,4,F(1))
  # Contrast conditions for probe vs explore, including reachable nonoptimal states.
  # Strict normal optimal action counts are the more conservative evidence.
  examples={}
  for state,mass in visits.items():
    qs=qvalues(*state);act=chosen[state]
    if act not in examples and mass and len(qs)>1 and qs[act]>max(v for a,v in qs.items() if a!=act):
      examples[act]={'state':str(state),'mass':str(mass),'qvalues':{a:str(v) for a,v in qs.items()}}
  # Explicit direct ablation: forbid probe; independently first-probe and explore-first.
  no_probe=V(0,K,False,4,False)
  forced_probe=V(0,1,False,3,True)
  forced_explore=F(1,K)*V(0,K,True,3,True)+F(K-1,K)*V(0,K-1,False,3,True)
  return {
    'K':K,'q':str(q),'free':str(V(0,K,False,4,True)),
    'disabled_probe':str(no_probe),'marginal_probe':str(V(0,K,False,4,True)-no_probe),
    'forced_probe_first':str(forced_probe),'forced_explore_first':str(forced_explore),
    'best_simple':{'value':str(best[0]),'family':best[1],'threshold':best[2], 'gap':str(V(0,K,False,4,True)-best[0])},
    'strict_optimal_visits':{a:str(v) for a,v in strict_by_action.items()},
    'strict_examples':examples,
    'reachable_public_states':len(visits)
  }

def main():
  rows=[study(k,q) for k in (2,3,4,5) for q in (F(0),F(1,4),F(1,2),F(1))]
  for r in rows:
    assert F(r['free'])>=F(r['disabled_probe'])
    assert F(r['free'])>=F(r['best_simple']['value'])
    assert F(r['free'])>=F(r['forced_probe_first'])
    assert F(r['free'])>=F(r['forced_explore_first'])
    if r['K']==2 or r['q']=='1':assert F(r['marginal_probe'])==0
  print(json.dumps({'study':'R108','evidence':'exact Fraction paper R105 model; no C#/Scheme','rows':rows},ensure_ascii=False,indent=2))
if __name__=='__main__':main()