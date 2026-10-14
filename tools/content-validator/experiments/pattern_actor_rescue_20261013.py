#!/usr/bin/env python3
"""ACT-R01/02: autonomous rescue agent vs ordinary state-conditioned capacity.
Player sees independent Boolean damage A,B and outside offer low/high.
Player makes one action: FIX_A, FIX_B or SIDE (+1). Helper then maximizes
own payoff: directly repair damaged B if personal credit 3 exceeds outside
offer 2 or 4; otherwise EXIT. A generic conditional backup is identical.
A fixed_no_signal player sees damage but not offer before choosing.
Exact Fraction enumeration; no claims of human enjoyment or empathy.
"""
from fractions import Fraction as F
from itertools import product
DAMAGE=tuple(product((False,True),repeat=2))
OFFER=('low','high')
ACTIONS=('FIX_A','FIX_B','SIDE')
W=(5,8,1)

def transition(damage,offer,action,mode):
    damage_a,damage_b=damage
    a_safe=not damage_a or action=='FIX_A'
    b_safe=not damage_b or action=='FIX_B'
    side=F(action=='SIDE')
    if mode=='actor':
        ally='FIX_B' if not b_safe and 3>(2 if offer=='low' else 4) else 'EXIT'
        if ally=='FIX_B': b_safe=True
    elif mode=='backup':
        ally='FIX_B' if offer=='low' and not b_safe else 'EXIT'
        if ally=='FIX_B': b_safe=True
    elif mode=='universal':
        ally=('FIX_A' if not a_safe else 'FIX_B') if (not a_safe or not b_safe) else 'EXIT'
        if ally=='FIX_A':a_safe=True
        if ally=='FIX_B':b_safe=True
    elif mode=='none':
        ally='ABSENT'
    else:raise AssertionError(mode)
    return (F(a_safe),F(b_safe),side),ally

def weighted(v,weights=W):
    return sum(x*y for x,y in zip(v,weights))

def dynamic(mode,weights=W):
    result=[F(0)]*3;choices={}
    for damage,offer in product(DAMAGE,OFFER):
        candidates=[(weighted(transition(damage,offer,a,mode)[0],weights),
                     -ACTIONS.index(a),a,transition(damage,offer,a,mode)) for a in ACTIONS]
        _,_,act,(vec,ally)=max(candidates)
        choices[(damage,offer)]=(act,ally,vec)
        for i in range(3):result[i]+=vec[i]/8
    return tuple(result),choices

def fixed_no_signal(mode,weights=W):
    """Offer revealed only after player choice; intentionally altered protocol."""
    result=[F(0)]*3;choices={}
    for damage in DAMAGE:
        candidates=[]
        for a in ACTIONS:
            vs=[transition(damage,o,a,mode)[0] for o in OFFER]
            avg=tuple(sum(v[i] for v in vs)/2 for i in range(3))
            candidates.append((weighted(avg,weights),-ACTIONS.index(a),a,avg))
        _,_,act,vec=max(candidates)
        choices[damage]=(act,vec)
        for i in range(3):result[i]+=vec[i]/4
    return tuple(result),choices

def main():
    for weights in (W,(8,5,1),(5,5,1),(5,8,0)):
        actor=dynamic('actor',weights)
        backup=dynamic('backup',weights)
        assert actor[0]==backup[0]
        assert all(actor[1][state][2]==backup[1][state][2] for state in actor[1])
        none=dynamic('none',weights)
        fixed=fixed_no_signal('actor',weights)
        print('weights',weights,'actor',actor[0],weighted(actor[0],weights),
              'backup',backup[0],'no_agent',none[0],weighted(none[0],weights),
              'offer-blind',fixed[0],weighted(fixed[0],weights))
    assert dynamic('actor')[0]==(F(7,8),F(1),F(3,8))
    assert weighted(dynamic('actor')[0])==F(51,4)
    assert dynamic('none')[0]==(F(3,4),F(1),F(1,4))
    assert weighted(dynamic('none')[0])==F(12)
    assert weighted(fixed_no_signal('actor')[0])==F(49,4)
    d=(True,True)
    low=dynamic('actor')[1][(d,'low')]
    high=dynamic('actor')[1][(d,'high')]
    assert low==('FIX_A','FIX_B',(F(1),F(1),F(0)))
    assert high==('FIX_B','EXIT',(F(0),F(1),F(0)))
    print('strict reached state: both damaged low/high',low,high)
    print('PASS eight public states; both goals and SIDE retained as vector')

if __name__=='__main__':main()
