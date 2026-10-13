#!/usr/bin/env python3
"""INV01-06: protecting a site removes the incident trace used to identify a cause.

Initial hidden cause is R/B, equiprobable. First action SHIELD prevents any
damage and suppresses an informative trace; WATCH learns the true cause from
an incident but has 1/2 probability of irreversible loss at this stage.
Three later actions: PROBE (once, noisy signal accuracy3/4); STOP_R/B
(once, earns a distinct future-prevention outcome if correct); SIDE earns1.
Reports are observed by the planner. DP optimizes exact Bayesian expectations
and never lets a policy see hidden cause without an observation.
All comparisons are abstract -- neither ethics nor player enjoyment follows.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

Q=F(3,4)
ACTIONS=('PROBE','STOP_R','STOP_B','SIDE')

def bayes(p,signal,accuracy):
    likelihood_r=accuracy if signal=='R' else 1-accuracy
    likelihood_b=1-accuracy if signal=='R' else accuracy
    evidence=p*likelihood_r+(1-p)*likelihood_b
    return evidence,(p*likelihood_r/evidence if evidence else F(0))

def dynamic(safety=6,correct=12,side=1,mode='endogenous',
            hard_safety=False,allow_probe=True):
    assert mode in ('endogenous','exogenous','safe_perfect_probe')
    @lru_cache(None)
    def dp(hands,p,probed,stopped):
        if hands==0:return F(0)
        choices=[F(side)+dp(hands-1,p,probed,stopped)]
        if not stopped:
            choices.extend((F(correct)*(p if x=='R' else 1-p)
                            +dp(hands-1,p,probed,True)) for x in ('R','B'))
        if allow_probe and not probed:
            acc=F(1) if mode=='safe_perfect_probe' else Q
            n=F(0)
            for label in ('R','B'):
                mass,post=bayes(p,label,acc)
                if mass:n+=mass*dp(hands-1,post,True,stopped)
            choices.append(n)
        return max(choices)
    vals={}
    for first in ('SHIELD','WATCH'):
        if hard_safety and first=='WATCH':continue
        safe=F(1) if first=='SHIELD' else F(1,2)
        if first=='WATCH' or mode=='exogenous':
            tail=(dp(3,F(1),False,False)+dp(3,F(0),False,False))/2
        else:tail=dp(3,F(1,2),False,False)
        vals[first]=F(safety)*safe+tail
    return max(vals.values()),vals

def strong_fixed(safety=6,correct=12,side=1):
    best=F(-10**5)
    for first in ('SHIELD','WATCH'):
        for seq in product(ACTIONS,repeat=3):
            reward=F(safety)*(F(1) if first=='SHIELD' else F(1,2))
            probed=False;stopped=False
            for action in seq:
                if action=='PROBE' and not probed:
                    probed=True
                elif action in ('STOP_R','STOP_B') and not stopped:
                    stopped=True;reward+=F(correct,2)
                else:reward+=F(side)
            best=max(best,reward)
    return best

def transparent_vectors():
    # P(no initial harm), P(correctly halted future source), E(SIDE).
    return {'SHIELD_PROBE_STOP_SIDE':(F(1),F(3,4),F(1)),
            'SHIELD_GUESS_SIDE_SIDE':(F(1),F(1,2),F(2)),
            'WATCH_STOP_SIDE_SIDE':(F(1,2),F(1),F(2))}

def main():
    for weights,root,best_first,fixed in [
        ((6,12,1),17,'WATCH',14),
        ((12,8,1),19,'SHIELD',18),
        ((8,8,1),15,'SHIELD',14)
    ]:
        score,first=dynamic(*weights)
        assert score==root and strong_fixed(*weights)==fixed
        assert first[best_first]==root
        print('weights',weights,'adaptive',score,'first branches',first,'fixed',fixed)
    vec=transparent_vectors()
    for i,a in vec.items():
        for j,b in vec.items():
            if i!=j:assert not all(x>=y for x,y in zip(a,b))
        print('terminal outcome',i,tuple(map(str,a)))
    assert dynamic(6,12,1,hard_safety=True)[0]==16
    assert dynamic(6,12,1,mode='exogenous')[0]==20
    assert dynamic(6,12,1,mode='safe_perfect_probe')[0]==19
    assert dynamic(6,12,1,allow_probe=False)[0]==17
    for signal in ('R','B'):
        mass,p=bayes(F(1,2),signal,Q)
        assert mass==F(1,2)
        assert p==(F(3,4) if signal=='R' else F(1,4))
    # A longer horizon raises the weight of stopping the right source.
    for horizon,expected in [(1,12),(2,15),(3,18),(4,22)]:
        v,initial=dynamic(8,4*horizon,1)
        assert v==expected,(horizon,v,initial)
        print('future cycles',horizon,'root',v,'first',initial)
    print('PASS causal observation loss, noncompensable safety and strong matching information menus')

if __name__=='__main__':main()
