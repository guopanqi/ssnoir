"""CONFIRM01-04: complete-assignment batch feedback versus per-identity checks.
Only a deliberately tiny 4-identity/9-world toy, not Obra Dinn's UI.
For the SAME initial nine permutations, minimize expected number of
queries until one possible world remains (no paid final announcement).
"""
from functools import lru_cache
from fractions import Fraction as F
from pattern_constraint_deduction_20261015 import build_worlds,CANDIDATES

WORLD=build_worlds()

@lru_cache(None)
def solve(states,feedback):
    if len(states)<=1:return F(0),None
    if feedback=='individual':
        actions=[(i,v) for i in range(4) for v in CANDIDATES[i]]
    elif feedback=='batch3':
        actions=list(WORLD)
    elif feedback=='direct':
        actions=list(range(4))
    else:raise ValueError(feedback)
    best=(F(99999),None)
    for a in actions:
        groups={}
        for idx in states:
            w=WORLD[idx]
            if feedback=='individual':response=int(w[a[0]]==a[1])
            elif feedback=='batch3':
                response=int(sum(x==y for x,y in zip(w,a))>=3)
            else:response=w[a]
            groups.setdefault(response,[]).append(idx)
        if len(groups)<2:continue
        val=F(1)+sum(F(len(group),len(states))*solve(tuple(group),feedback)[0]
                      for group in groups.values())
        if val<best[0]:best=(val,a)
    return best

def regression():
    states=tuple(range(len(WORLD)))
    costs={mode:solve(states,mode) for mode in
           ('individual','batch3','direct')}
    assert costs['individual'][0]==F(10,3)
    assert costs['batch3'][0]==F(44,9)
    assert costs['direct'][0]==F(2)
    for true_world in WORLD:
        for full_guess in WORLD:
            matches=sum(a==b for a,b in zip(true_world,full_guess))
            assert (matches>=3)==(true_world==full_guess)
    for name,(value,first) in costs.items():
        print('feedback',name,'expected additional queries',value,
              'best initial query',first)
    print('CONFIRM01-04 PASS')

if __name__=='__main__':regression()
