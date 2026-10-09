#!/usr/bin/env python3
"""R54 capacity ablation prompted by first five human sketch sessions.

Frozen parameters: A=3/reward1, B=5/reward2, attack after action 2 and
action 4 if no completion, publicly choose larger progress (tie->A), hit=2,
FateStrip at skill 1, single batch of 4 vs 5 independently sampled dice.

Exact rational optimal policy, 2**N frozen per-hand goal plans (dice still
adaptively assigned), and forward *chosen optimal policy* evidence.
No claim of human frequency or native engine parity for 5-die sketch.
"""
from __future__ import annotations
import argparse
import json
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement,product
from math import factorial

from r50_recent_action import odds


def weight(hand:tuple[int,...]) -> F:
    result=F(factorial(len(hand)),6**len(hand))
    for v in set(hand):
        result/=factorial(hand.count(v))
    return result

def analyze(total:int,skill:int=1) -> dict:
    @lru_cache(None)
    def after(a,b,turn,goal,gain):
        na=min(3,a+(gain if goal==0 else 0))
        nb=min(5,b+(gain if goal==1 else 0))
        attacked=None
        if na<3 and nb<5 and turn in (1,3):
            attacked=0 if na>=nb else 1
            if attacked==0:na=max(0,na-2)
            else:nb=max(0,nb-2)
        return na,nb,attacked

    @lru_cache(None)
    def q(a:int,b:int,turn:int,hand:tuple[int,...],goal:int,i:int)->F:
        die=hand[i];rest=hand[:i]+hand[i+1:]
        return sum(prob*optimal(*after(a,b,turn,goal,gain)[:2],
                                turn+1,rest)
                   for gain,prob in enumerate(odds(die,skill,0)))

    @lru_cache(None)
    def optimal(a,b,turn,hand)->F:
        if a>=3:return F(1)
        if b>=5:return F(2)
        if not hand:return F(0)
        return max(q(a,b,turn,hand,g,i) for g in (0,1) for i in range(len(hand)))

    @lru_cache(None)
    def fixed(a,b,turn,hand,plan)->F:
        if a>=3:return F(1)
        if b>=5:return F(2)
        if not hand:return F(0)
        g=plan[0];tail=plan[1:]
        return max(sum(prob*fixed(*after(a,b,turn,g,gain)[:2],turn+1,
                                  hand[:i]+hand[i+1:],tail)
                       for gain,prob in enumerate(odds(die,skill,0)))
                   for i,die in enumerate(hand))

    # forward indexes:
    # diverted at 2; beneficial counterfactual; diverted+B success;
    # B completion; A completion; timed out; strictly profitable
    # direction change on turn3 (after first opponent reaction).
    @lru_cache(None)
    def forward(a,b,turn,hand,diverted,beneficial,prev,switched):
        if a>=3:return (F(diverted),F(beneficial),F(0),F(0),F(1),F(0),F(switched))
        if b>=5:return (F(diverted),F(beneficial),F(diverted),F(1),F(0),F(0),F(switched))
        if not hand:return (F(diverted),F(beneficial),F(0),F(0),F(0),F(1),F(switched))
        choices=[(q(a,b,turn,hand,g,i),g,hand[i],i)
                 for g in (0,1) for i in range(len(hand))]
        best=max(x[0] for x in choices)
        _,goal,_,index=max(x for x in choices if x[0]==best)
        strict_switch=switched
        if turn==2 and prev>=0 and goal!=prev:
            stay=max(q(a,b,turn,hand,prev,i) for i in range(len(hand)))
            strict_switch=best>stay
        accum=[F(0)]*7;rest=hand[:index]+hand[index+1:]
        for gain,prob in enumerate(odds(hand[index],skill,0)):
            # Detect diversion using the *pre-attack* progress from this action.
            na=min(3,a+(gain if goal==0 else 0))
            nb=min(5,b+(gain if goal==1 else 0))
            is_diversion=turn==1 and na<3 and nb<5 and na>=nb and na>0 and nb>0
            strict_benefit=False
            if is_diversion:
                future_hit_a=optimal(max(0,na-2),nb,turn+1,rest)
                future_hit_b=optimal(na,max(0,nb-2),turn+1,rest)
                strict_benefit=future_hit_a>future_hit_b
            na,nb,_=after(a,b,turn,goal,gain)
            r=forward(na,nb,turn+1,rest,diverted or is_diversion,
                      beneficial or strict_benefit,goal,strict_switch)
            for k in range(7):accum[k]+=prob*r[k]
        return tuple(accum)

    agg=[F(0)]*7;avg_free=F(0);avg_fixed=F(0)
    witness=0
    orders=tuple(product((0,1),repeat=total))
    for hand in combinations_with_replacement(range(1,7),total):
        w=weight(hand)
        free=optimal(0,0,0,hand)
        frozen=max(fixed(0,0,0,hand,plan) for plan in orders)
        if free>frozen:witness+=1
        avg_free+=w*free
        avg_fixed+=w*frozen
        result=forward(0,0,0,hand,False,False,-1,False)
        for k in range(7):agg[k]+=w*result[k]

    assert sum(agg[k] for k in (3,4,5))==1, "terminal partitions incomplete"
    assert avg_free>=avg_fixed
    assert agg[2]<=agg[0] and agg[1]<=agg[0]
    labels=["diverted_A_protecting_nonzero_B",
            "strict_counterfactual_benefit_from_diversion",
            "diverted_then_completed_B",
            "completed_B","completed_A","timeout",
            "strictly_profitable_switch_at_action3"]
    def number(v):return {"exact":str(v),"decimal":round(float(v),9)}
    return {"dice":total,"skill":skill,
            "initial_hands":len(tuple(combinations_with_replacement(range(1,7),total))),
            "weighted_initial_hand_mass":str(sum(weight(h) for h in combinations_with_replacement(range(1,7),total))),
            "roots_with_strict_adaptation_gain":witness,
            "expected_reward":{"optimal":number(avg_free),"fixed_goal_order":number(avg_fixed),
                               "advantage":number(avg_free-avg_fixed)},
            "optimal_policy_path_masses":dict(zip(labels,map(number,agg))),
            "policy_tie_break":"B before A, then larger die",
            "boundary":"abstract skill1 exact Fraction DP, not player observation"}

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--output")
    args=ap.parse_args()
    rows=[analyze(4),analyze(5)]
    # R54 prior's 4-die model result, held out as implementation regression test.
    assert abs(rows[0]["expected_reward"]["optimal"]["decimal"]-1.259075)<0.000001
    assert abs(rows[0]["optimal_policy_path_masses"]["diverted_A_protecting_nonzero_B"]["decimal"]-0.221664952)<0.00000001
    result={"scope":"R54 4-v-5 dice, rest of rules fixed, no human claims","comparison":rows}
    text=json.dumps(result,ensure_ascii=False,indent=2)+"\n"
    if args.output:
        from pathlib import Path
        Path(args.output).write_text(text,encoding="utf-8")
    else:print(text)

if __name__=="__main__":main()
