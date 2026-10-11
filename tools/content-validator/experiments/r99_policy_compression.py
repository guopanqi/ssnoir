#!/usr/bin/env python3
"""R99: how many understandable reserve-cashout policies explain R98?

Exact Fraction, same four-die compressed R95/R98 toy rules, NOT full
three-cycle native P10. Choose a GLOBAL repertoire of K simple threshold
policies before any hand is seen. Player chooses the best of those K policies
after seeing the starting dice, and may choose dice adaptively on every step.
1 policy: exhaustive; 2 policies: exhaustive among 780 pairs;
K>=3: greedy additions (NOT globally optimal K-policy subsets).
The stronger 40-policy per-hand envelope is a bound, not a human behavior.
"""
from __future__ import annotations
from functools import lru_cache
from fractions import Fraction as F
from itertools import combinations_with_replacement,product,combinations
from math import factorial
import json,sys

H=tuple(combinations_with_replacement(range(1,7),4))
W=tuple(F(24,1296)/__import__("functools").reduce(lambda a,v:a*factorial(h.count(v)),set(h),1) for h in H)
assert sum(W)==1
RULES=tuple(product(range(1,6),(False,True),(False,True),(False,True)))
ROLLS={d:tuple((g,F(p,6)) for g,p in enumerate((b,m,6-b-m)) if p)
  for d,(b,m) in {1:(2,3),2:(1,3),3:(1,2),4:(0,2),5:(0,1),6:(0,0)}.items()}

def audit(nonlinear=True,decay=2,goal=6):
    def reward(r):return r*(r+1)//2 if nonlinear else 2*r
    @lru_cache(None)
    def policy(p,r,h,rule):
        if p>=goal:return F(1)
        if not h:return F(0)
        k,boundary,finish,spare=rule
        if spare and p==goal-1:a='G'
        elif r and finish and p+reward(r)>=goal:a='C'
        elif r and boundary and len(h)==3:a='C'
        elif r>=k:a='C'
        elif len(h)==1:a='C' if r else 'G'
        else:a='P'
        choices=[]
        for i,d in enumerate(h):
            rest=h[:i]+h[i+1:]
            val=F(0)
            outcomes=ROLLS[d] if a=='P' else ((0,F(1)),)
            for gain,pr in outcomes:
                if a=='P':pp,rr=p,max(0,min(5,r+{0:-2,1:1,2:2}[gain]))
                elif a=='C':pp,rr=min(goal,p+reward(r)),0
                else:pp,rr=min(goal,p+1),r
                if len(h)==3:rr=max(0,rr-decay)
                val+=pr*policy(pp,rr,rest,rule)
            choices.append(val)
        return max(choices)
    matrix=[tuple(policy(0,0,h,rule) for h in H) for rule in RULES]
    def score(indices):
        return sum(w*max(matrix[i][j] for i in indices) for j,w in enumerate(W))
    alone=[score((i,)) for i in range(len(RULES))]
    best1=max(range(len(RULES)),key=lambda i:alone[i])
    # Full pair-search, not greedy.
    best2=max(combinations(range(len(RULES)),2),key=score)
    envelope=sum(w*max(matrix[i][j] for i in range(len(RULES)))
                  for j,w in enumerate(W))
    selected=list(best2)
    rows=[]
    def result(k,choice,optimal_subset):
        value=score(choice)
        return dict(k=k,policies=[dict(reserve_threshold=RULES[i][0],
                 cash_before_decay=RULES[i][1],
                 cash_if_finishes=RULES[i][2],
                 guaranteed_last_step=RULES[i][3]) for i in choice],
                 exact=float(value),gap_to_40=float(envelope-value),
                 retained_fraction_of_40=float(value/envelope) if envelope else 1,
                 selection_method=optimal_subset)
    rows.append(result(1,[best1],"exhaustive best single"))
    rows.append(result(2,list(best2),"exhaustive all 780 pairs"))
    for k in (3,4,5):
        remaining=[i for i in range(len(RULES)) if i not in selected]
        selected.append(max(remaining,key=lambda i:score(selected+[i])))
        rows.append(result(k,selected[:],"greedy extension from globally optimal 2"))
    full=float(envelope)
    if nonlinear and decay==2:
        assert abs(full-0.719453732)<1e-8,full
        assert abs(float(alone[best1])-0.708962048)<1e-8
    return dict(nonlinear=nonlinear,decay=decay,goal=goal,
                global_policy_candidates=len(RULES),hands=len(H),
                envelope_40=float(envelope),repertoires=rows,
                semantics="policy set chosen globally, which one to apply chosen from starting visible hand; die selection fully reactive",
                caveat="Even five-rule choice may be cognitively hard; no human UX evidence")

def main():
    cases=[audit(True,2),audit(False,2)]
    output=dict(study="R99",method="exact policy-repertoire compression with full K1/K2, greedy K3-K5",
      boundaries="R95 simplified four dice / two rounds; not native P10 and not player testing",cases=cases)
    text=json.dumps(output,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(text,encoding="utf8")
    else:print(text)
if __name__=="__main__":main()
