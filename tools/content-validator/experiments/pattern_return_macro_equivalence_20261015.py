"""RETURN-MACRO: compile W->L conditional choices, compare all information states.
A serial screen flow is not proof of new two-scale planning structure.
"""
from fractions import Fraction as F
from itertools import product
from pattern_return_world_local_loop_20261015 import (
    STARTS,world_policy,local_action_value,ready_after,world_final,root)

def macro_policy(start,h,pw,see_world=True,alt=5):
    scripts=[('DECLINE',(a,a)) for a in ('MEND','KEEP')]
    scripts += [('ACCEPT',(a,b)) for a,b in product(('LINK','CUT','ABANDON'),repeat=2)]
    values={script:sum(F(1,2)*local_action_value(start,h,script[0],script[1][k],k,pw,see_world,alt) for k in (0,1)) for script in scripts}
    best=max(values.values())
    return best,tuple(k for k,v in values.items() if v==best)

def equivalent_return_states():
    seen={}
    for start,h,choice,k in product(STARTS,(0,1),('DECLINE','ACCEPT'),(0,1)):
        if start=='GUARD' and h:continue
        actions=('MEND','KEEP') if choice=='DECLINE' else ('LINK','CUT','ABANDON')
        for action in actions:
            for chance,a,b,pen,feedback in ready_after(start,h,choice,action,k):
                if not chance:continue
                key=(a,b,pen)
                for w in (0,1):
                    assert world_final(a,b,w)==world_final(key[0],key[1],w)
                seen.setdefault(key,[]).append((start,h,choice,k,action,feedback))
    assert any(len(v)>1 for v in seen.values())
    return seen

def regression():
    for start,h,y in product(STARTS,(0,1),(0,1)):
        if start=='GUARD' and h:continue
        pw=F(3,4) if y else F(1,4)
        assert macro_policy(start,h,pw)[0]==world_policy(start,h,pw)[0]
    assert ('ACCEPT',('CUT','LINK')) in macro_policy('OPEN',0,F(3,4))[1]
    assert root()[0]==F(1509,128)
    states=equivalent_return_states()
    print('MACRO all condition states equal staged solver; downstream state classes',len(states))
    print('RETURN-MACRO PASS')

if __name__=='__main__':regression()
