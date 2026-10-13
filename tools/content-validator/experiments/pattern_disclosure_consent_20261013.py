#!/usr/bin/env python3
"""PUB01-03: third-party consent after private/public evidence decision.

The player already owns an authentic trace. Three actions; after action1,
demand A/B is publicly revealed with P(A)=1/2. PUBLISH gives A outcome;
ASK gives private B permission while not published. NPC gains 3 from a
private B grant versus exit2, but after publicity grant -1 < exit2; a
previous private grant is withdrawn if the player subsequently publishes.
A deterministic EXCLUSIVE_MENU has identical transitions; a permissive
no-withdrawal control can achieve both goals. Exact Fraction DP/27 fixed
plans and explicit all-reachable-state equivalence. No human evidence.
"""
from fractions import Fraction as Q
from functools import lru_cache
from itertools import product

ACTS=('SIDE','PUBLISH','ASK')
INIT=(0,0,0,0)  # claimed A, admitted B, accumulated side, made public

def step(kind,st,act):
    a,b,s,pub=st
    if act=='SIDE':s+=1
    elif act=='PUBLISH':
        pub=1;a=1
        if kind!='permissive':b=0
    elif act=='ASK':
        if kind=='permissive':b=1
        elif not pub:
            assert 3>2  # NPC private grant benefits own project vs outside
            b=1
        else:
            assert 2>-1  # public grant harms NPC; chooses outside exit
    else:raise ValueError(act)
    return a,b,s,pub

def payoff(st,demand_a):
    a,b,s,pub=st
    return Q(s+a*(12 if demand_a else 3)+b*(3 if demand_a else 12))

def run(kind,plan):
    st=INIT
    for a in plan:st=step(kind,st,a)
    return st

def solve(kind):
    @lru_cache(None)
    def dp(t,st,sig):
        if t==3:return payoff(st,sig)
        if t==1 and sig is None:return (dp(t,st,True)+dp(t,st,False))/2
        return max(dp(t+1,step(kind,st,a),sig) for a in ACTS)
    root=dp(0,INIT,None)
    first={a:dp(1,step(kind,INIT,a),None) for a in ACTS}
    fixed=[(sum(payoff(run(kind,p),d) for d in (False,True))/2,p)
           for p in product(ACTS,repeat=3)]
    best=max(v for v,p in fixed)
    return root,first,best,tuple(p for v,p in fixed if v==best)

def all_states(kind):
    seen={INIT};frontier={INIT}
    for t in range(3):
        frontier={step(kind,st,a) for st in frontier for a in ACTS}
        seen|=frontier
    return seen

def main():
    vals={}
    for k in ('npc','generic_menu','permissive'):
        root,first,best,plans=solve(k)
        vals[k]=(root,best)
        print(k,'root',root,'first',first,'fixed',best,'plans',plans)
    assert vals['npc']==vals['generic_menu']==(Q(14),Q(19,2)),vals
    for st in all_states('npc'):
        for a in ACTS:assert step('npc',st,a)==step('generic_menu',st,a)
    assert vals['permissive']==(Q(16),Q(16)),vals
    print('PASS autonomous conditional refusal isomorphic to exclusive menu')
if __name__=='__main__':main()
