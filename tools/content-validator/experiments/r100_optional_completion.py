#!/usr/bin/env python3
"""R100: P12 'complete A ends scene and closes unfinished B' decision audit.

Four prepared d6 skill1, A and B require 2 progress each. As soon as A
completes the scene ends, optional B is only rewarded if it was previously
completed. Reward = 1 for finishing A; plus bonus for B if A also finishes.
All other terminal states score 0. This is a minimal paper-only model,
NOT P12 native Scheme (no composure, risk actions, or game time).

Compare DP vs strong simple rules:
 - all A
 - B-until-complete then A
 - fixed number of first B actions then A (0..4), with dice freely chosen
   after observing earlier outcomes.
 - choose the strongest of these per visible starting hand.

Track genuine mid-course B abandonment: B is partially complete and still
available, but next action A strictly beats choosing B, plus reverse witness.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial
import json,sys

D={1:(2,3),2:(1,3),3:(1,2),4:(0,2),5:(0,1),6:(0,0)}
H=tuple(combinations_with_replacement(range(1,7),4))
def wt(h):
    w=F(24,1296)
    for v in set(h):w/=factorial(h.count(v))
    return w
assert sum(wt(h) for h in H)==1
def odds(d):
    bad,mid=D[d]
    return tuple((g,F(n,6)) for g,n in enumerate((bad,mid,6-bad-mid)) if n)
def run(bonus):
    @lru_cache(None)
    def optimal(a,b,h):
        if a==2:return F(1)+bonus*F(b==2)
        if not h:return F(0)
        return max(q(a,b,h,g,i,optimal) for g in (0,1) if (g==0 and a<2) or (g==1 and b<2)
            for i in range(len(h)))
    def q(a,b,h,g,i,continuation):
        rest=h[:i]+h[i+1:]
        return sum(p*continuation(min(2,a+(v if g==0 else 0)),
                    min(2,b+(v if g==1 else 0)),rest) for v,p in odds(h[i]))
    @lru_cache(None)
    def fixed(a,b,h,b_steps):
        if a==2:return F(1)+bonus*F(b==2)
        if not h:return F(0)
        used=4-len(h)
        g=1 if used<b_steps and b<2 else 0
        return max(q(a,b,h,g,i,lambda na,nb,rest:fixed(na,nb,rest,b_steps))
                   for i in range(len(h)))
    @lru_cache(None)
    def priority(a,b,h):
        if a==2:return F(1)+bonus*F(b==2)
        if not h:return F(0)
        g=1 if b<2 else 0
        return max(q(a,b,h,g,i,priority) for i in range(len(h)))
    @lru_cache(None)
    def forward(a,b,h):
        # Normal optimal policy, deterministic tie A then smallest die.
        if a==2 or not h:return F(0),F(0),F(0)
        choices={(g,i):q(a,b,h,g,i,optimal) for g in (0,1) if (g==0 or b<2)
                 for i in range(len(h))}
        best=max(choices.values())
        g,i=next((g,i) for g,i in choices if choices[(g,i)]==best)
        result=[F(0)]*3
        if 0<b<2:
            bestA=max(q(a,b,h,0,j,optimal) for j in range(len(h)))
            bestB=max(q(a,b,h,1,j,optimal) for j in range(len(h)))
            if bestA>bestB and g==0:result[0]+=1
            if bestB>bestA and g==1:result[1]+=1
        if g==1 and b==2:raise AssertionError("Completed B not legal")
        for value,p in odds(h[i]):
            na=min(2,a+(value if g==0 else 0))
            nb=min(2,b+(value if g==1 else 0))
            sub=forward(na,nb,h[:i]+h[i+1:])
            for j in range(3):result[j]+=p*sub[j]
        return tuple(result)
    adaptive=frozen=family=always_a=F(0)
    switchA=switchB=F(0);strict_roots=0
    for h in H:
        weight=wt(h)
        best=optimal(0,0,h)
        scripted=max(fixed(0,0,h,t) for t in range(5))
        strong=max(scripted,priority(0,0,h))
        assert best>=strong
        adaptive+=weight*best;frozen+=weight*scripted
        family+=weight*strong;always_a+=weight*fixed(0,0,h,0)
        strict_roots+=best>strong
        move=forward(0,0,h)
        switchA+=weight*move[0];switchB+=weight*move[1]
    return dict(optional_reward_multiplier=str(bonus),
       best_adaptive=float(adaptive),
       fixed_prefix_best=float(frozen),
       strongest_simple=float(family),
       forced_A_only=float(always_a),
       adaptivity_advantage=float(adaptive-family),
       value_of_optional_outcome=float(adaptive-always_a),
       initial_hands_strict_vs_simple=strict_roots,
       expected_strict_abandon_partly_done_B=float(switchA),
       expected_strict_continue_partly_done_B=float(switchB),
       caveat="Counts expected visits to intermediate states, not probability of at least one; tie-breaks A then smaller die")

def main():
    results=[run(x) for x in (F(1,4),F(1,2),F(1),F(2))]
    for r in results:
        assert r["best_adaptive"]>=r["strongest_simple"]
        assert r["strongest_simple"]>=r["forced_A_only"]
    data=dict(id="R100",semantics="A completion auto-ends; B reward only if complete before A",
      evidence="Exact Fraction,126 weighted four-die initial hands, simplified risk-free P12 proxy, NOT native or human",
      results=results)
    s=json.dumps(data,indent=2,ensure_ascii=False)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(s,encoding="utf8")
    else:print(s)
if __name__=="__main__":main()
