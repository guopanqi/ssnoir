#!/usr/bin/env python3
"""META01-03: a regression against conflating equal DP values and isomorphism.

PRF/PUB have verified reachable-state action-transition equivalence.
CUST has equal scalar root but different Pareto fronts and protected goals.
This is a structural test, not a human fun or originality classifier.
"""
from fractions import Fraction as F
import pattern_endogenous_proof_20261013 as prf
import pattern_disclosure_consent_20261013 as pub
import pattern_evidence_custody_20261013 as cust
import pattern_protected_stakes_20261013 as protected

def test_prf_full_transition_equivalence():
    frontier={(0,0,0,False,False,False)}
    seen=set(frontier)
    for _ in range(3):
        nxt=set()
        for st in frontier:
            for act in prf.ACTS:
                assert prf.legal('proof',st,act)==prf.legal('matched',st,act)
                if prf.legal('proof',st,act):
                    x=prf.advance('proof',st,act)
                    y=prf.advance('matched',st,act)
                    assert x==y
                    nxt.add(x)
        seen|=nxt
        frontier=nxt
    assert prf.value('proof')[0]==prf.value('matched')[0]==F(29,2)
    return len(seen)

def test_pub_full_transition_equivalence():
    seen=pub.all_states('npc')
    assert seen==pub.all_states('generic_menu')
    for st in seen:
        for act in pub.ACT:
            assert pub.step('npc',st,act)==pub.step('generic_menu',st,act)
    assert pub.solve('npc')[0]==pub.solve('generic_menu')[0]==14
    return len(seen)

def test_cust_same_root_different_feasible_goals():
    x=protected.summarize('original')
    y=protected.summarize('copy')
    z=protected.summarize('matched_preserve')
    assert x['root']==y['root']==z['root']==19
    assert x['fixed']==y['fixed']==17
    assert x['pareto']!=y['pareto']
    assert len(x['pareto'])==18 and len(y['pareto'])==23
    assert x['protected']==17 and y['protected']==F(37,2)
    assert y['pareto']==z['pareto'] and y['protected']==z['protected']
    st=cust.advance('copy',cust.INIT,'A')
    assert cust.legal('copy',st,'COPY',True)
    assert not cust.legal('original',st,'COPY',True)
    return x['unique'],y['unique']

def main():
    print('PRF true same-cost full transition equivalence states',
          test_prf_full_transition_equivalence())
    print('PUB true full reachable-state response equivalence states',
          test_pub_full_transition_equivalence())
    print('CUST equal aggregate roots but different Pareto/goal feasibility',
          test_cust_same_root_different_feasible_goals())
    print('PASS: root equality is not structural equivalence or preference equivalence')

if __name__=='__main__':main()
