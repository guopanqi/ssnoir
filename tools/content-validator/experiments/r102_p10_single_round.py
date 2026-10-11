#!/usr/bin/env python3
"""R102: original P10 FOUR actions per round, but ONE ROUND reward lens.

Exact Fraction 126 weighted starting hands, reserve 0-5, prepare
-2/+1/+2, cash triangular / linear, safe +1. Compare full action+die
optimal to ten understandable threshold rules with OPTIMAL die choice.
Not a native three-round completion/hospital model. R99 is a different
study of the R95 two-round *repertoire* and must not be overwritten.
"""
import json,sys
from functools import lru_cache
from fractions import Fraction as F
from itertools import combinations_with_replacement,product
from math import factorial
H=tuple(combinations_with_replacement(range(1,7),4))
def weight(h):
    v=F(24,1296)
    for d in set(h):v/=factorial(h.count(d))
    return v
assert sum(map(weight,H))==1
def outcomes(d):
    bad,mid={1:(2,3),2:(1,3),3:(1,2),4:(0,2),5:(0,1),6:(0,0)}[d]
    return ((0,F(bad,6)),(1,F(mid,6)),(2,F(6-bad-mid,6)))
def study(nonlinear):
    reward=(lambda r:r*(r+1)//2) if nonlinear else (lambda r:2*r)
    def apply(r,h,act,i,cont):
        rem=h[:i]+h[i+1:]
        if act=='G':return 1+cont(r,rem)
        if act=='C':return reward(r)+cont(0,rem)
        return sum(prob*cont(max(0,min(5,r+{0:-2,1:1,2:2}[g])),rem)
                   for g,prob in outcomes(h[i]) if prob)
    @lru_cache(None)
    def optimal(r,h):
        if not h:return F(0)
        return max(apply(r,h,a,i,optimal) for a in ('G','P','C') if a!='C' or r>0
                   for i in range(len(h)))
    @lru_cache(None)
    def simple(r,h,threshold,cash_with_two):
        if not h:return F(0)
        if r and (r>=threshold or len(h)==1 or (cash_with_two and len(h)==2)):act='C'
        elif len(h)>=2 and r<5:act='P'
        else:act='G'
        return max(apply(r,h,act,i,lambda rr,hh:simple(rr,hh,threshold,cash_with_two))
                   for i in range(len(h)))
    rules=tuple(product(range(1,6),(False,True)))
    best_rule=max(rules,key=lambda rule:sum(weight(h)*simple(0,h,*rule) for h in H))
    opt=sum(weight(h)*optimal(0,h) for h in H)
    single=sum(weight(h)*simple(0,h,*best_rule) for h in H)
    per_hand=sum(weight(h)*max(simple(0,h,*rule) for rule in rules) for h in H)
    assert opt>=per_hand>=single
    return dict(nonlinear=nonlinear,optimal_mean_progress=float(opt),
         global_threshold_mean_progress=float(single),
         per_hand_threshold_mean_progress=float(per_hand),
         best_global_rule=list(best_rule),
         remaining_expected_progress=float(opt-per_hand),
         relative_gap_percent=float((opt-per_hand)/opt*100),
         strict_starting_hands=sum(optimal(0,h)>max(simple(0,h,*r) for r in rules) for h in H),
         threshold_policy_count=len(rules))
def main():
    cases=[study(True),study(False)]
    assert cases[0]['relative_gap_percent']<0.02
    assert cases[0]['best_global_rule']==[5,False]
    assert cases[0]['strict_starting_hands']==9
    assert cases[1]['relative_gap_percent']<0.02
    out=dict(id='R102',evidence='One-round exact Fraction mathematical screen only; ignores 3-round target and composure',
             distinction='R99 tested 40-policy repertoire in 2-round / 4-total-die model; this tests one actual-size four-die round without future carry',
             cases=cases)
    s=json.dumps(out,ensure_ascii=False,indent=2)+'\n'
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(s,encoding='utf8')
    else:print(s)
if __name__=='__main__':main()
