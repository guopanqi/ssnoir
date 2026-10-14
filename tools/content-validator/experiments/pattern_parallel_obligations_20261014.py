"""Two concurrent obligations, shock after first action; exact policy enumeration."""
from fractions import Fraction
from itertools import product

def payoff(actions, shock):
    assert len(actions) == 4
    a = actions[:2].count("A")
    b = actions.count("B")
    return (8 if a >= 2 + shock else 0) + (10 if b >= 2 else 0) + actions.count("S")

def solve(adaptive=True):
    candidates = []
    for first in "ABS":
        if adaptive:
            branches = []
            for shock in (0, 1):
                best = max((payoff(first+"".join(rest),shock),"".join(rest))
                           for rest in product("ABS",repeat=3))
                branches.append(best)
            candidates.append((Fraction(sum(x[0] for x in branches),2), first, branches))
        else:
            plans = [(Fraction(payoff(first+"".join(rest),0)+payoff(first+"".join(rest),1),2),"".join(rest))
                     for rest in product("ABS",repeat=3)]
            val,rest=max(plans)
            candidates.append((val,first,[(val,rest)]))
    return max(candidates, key=lambda x:x[0]), candidates

if __name__ == "__main__":
    dynamic,per_first=solve(True)
    fixed,_=solve(False)
    assert dynamic[0] == Fraction(29,2), dynamic
    assert fixed[0] == 14, fixed
    assert {k:v for v,k,_ in per_first}["B"] == 12
    assert payoff("AABB",0)==18 and payoff("ASBB",1)==11
    print("Adaptive:",dynamic)
    print("Precommitted:",fixed)
    print("PASS: adaptive 29/2, fixed 14, B-first 12")
