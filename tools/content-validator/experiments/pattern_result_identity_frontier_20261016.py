"""Enumerate every two-stage conditional policy and check outcome identity.

Source is custody's full executable action transitions. This is not a player
preference model; it measures physically possible outcome vectors and how
omitting demand-matching can lead to a misleading Pareto judgment.
"""
from fractions import Fraction as F
from itertools import product
from math import prod
from pattern_dual_stakes_custody_20261016 import WORLDS, sequences, outcome, single_early_ledger


def vector(worlds, picks, need_validity=True):
    A=sum((F(res[1],4) for _acts,res in picks), F())
    B=sum((F(res[2],4) for _acts,res in picks), F())
    clean=sum((F(not res[3],4) for _acts,res in picks), F())
    side=sum((F(res[4],4) for _acts,res in picks), F())
    v=(A,B,clean,side)
    if need_validity:
        match=sum((F((w=='A' and res[1]) or (w=='B' and res[2]),4)
                   for (w,_h),(_acts,res) in zip(worlds,picks)), F())
        return v+(match,)
    return v


def scan(include_match=True):
    samples={}
    policy_count=0
    for early in ('PLEDGE','PASS'):
        choices=[[(actions,outcome(early,w,st)) for actions,st in sequences(early,w,h)]
                 for (w,h) in WORLDS]
        policy_count+=prod(len(x) for x in choices)
        for picks in product(*choices):
            k=vector(WORLDS,picks,include_match)
            samples.setdefault(k,(early,picks))
    front=set()
    for v in samples:
        if not any(u!=v and all(a>=b for a,b in zip(u,v)) for u in samples):
            front.add(v)
    return policy_count,samples,front


def regression():
    four_count,four,four_pareto=scan(False)
    five_count,five,five_pareto=scan(True)
    assert four_count==five_count==5440
    assert (len(four),len(four_pareto))==(82,15)
    assert (len(five),len(five_pareto))==(178,18)
    optimal_policy=single_early_ledger('PLEDGE')
    picks=tuple((actions,res) for w,h,actions,res in optimal_policy)
    scalar_opt_four=vector(WORLDS,picks,False)
    scalar_opt_five=vector(WORLDS,picks,True)
    assert scalar_opt_four==(F(1,2),F(1,2),F(1,2),F(1,4))
    assert scalar_opt_five==scalar_opt_four+(F(1),)
    # A same-rate A/B policy appears superior on a simplistic 4D vector,
    # but satisfies the actual late-demand need only half the time.
    counter=(F(1,2),F(1,2),F(1,2),F(1,2),F(1,2))
    assert counter in five_pareto
    assert counter[:4] in four_pareto
    assert all(a>=b for a,b in zip(counter[:4],scalar_opt_four))
    assert counter[:4]!=scalar_opt_four
    assert scalar_opt_four not in four_pareto
    assert scalar_opt_five in five_pareto
    assert counter[4] < scalar_opt_five[4]
    hard_a=(F(1),F(0),F(1),F(1,2),F(1,2))
    safe_b=(F(0),F(1),F(1),F(1),F(1,2))
    assert hard_a in five_pareto and safe_b in five_pareto
    print('IDENTITY-FRONTIER PASS:',four_count,'complete W/H-contingent policies')
    print('4D: 82 unique/15 non-dominated; 5D(+matched actual need): 178/18')
    print('Scalar-opt vector 4D=',scalar_opt_four,'5D=',scalar_opt_five)
    print('4D apparent dominator loses real demand-matching:',counter)
    print('Protected A and safe B have incomparable physical outcome identities')
    print('LIMIT: vector Pareto and numeric rewards cannot establish what any human cares about')


if __name__=='__main__':regression()
