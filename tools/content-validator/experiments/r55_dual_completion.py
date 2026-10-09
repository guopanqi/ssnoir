#!/usr/bin/env python3
"""R55 exploratory browser-only model: two independently redeemable outcomes.

Contrasts R50's *exclusive, first-completion exits* with retained progress
and independent rewards: A3=>1, B4=>2, complete either/both, five d6 actions.
FateStrip prepared die + skill + echo (same=-1, switch=+1).
Measure the advantage over a powerful baseline chosen after seeing the
initial hand: fixed target sequence, still choosing each remaining die
adaptively. This is a paper model, NOT native C# and not human enjoyment.
"""
from __future__ import annotations
import json
from functools import lru_cache
from itertools import combinations_with_replacement,product
from math import factorial
from fractions import Fraction as F
from r50_recent_action import odds

MAX=(3,4)
REWARD=(1,2)
N=5

def weight(hand):
    w=F(factorial(N),6**N)
    for v in set(hand):
        w/=factorial(hand.count(v))
    return w

def analyze(skill=1):
    @lru_cache(None)
    def q(a,b,prev,hand,target,index,order):
        counts=odds(hand[index],skill,0 if prev<0 else (-1 if prev==target else 1))
        rest=hand[:index]+hand[index+1:]
        return sum(pr*value(min(MAX[0],a+(gain if target==0 else 0)),
                            min(MAX[1],b+(gain if target==1 else 0)),
                            target,rest,order)
                   for gain,pr in enumerate(counts))
    @lru_cache(None)
    def value(a,b,prev,hand,order):
        if not hand or (a==MAX[0] and b==MAX[1]):
            return F(REWARD[0]*(a==MAX[0])+REWARD[1]*(b==MAX[1]))
        # Fixed order means target on each turn is prescribed in advance;
        # any remaining die may still be chosen after observing prior rolls.
        goals=[order[0]] if order else (0,1)
        choices=[q(a,b,prev,hand,g,i,order[1:] if order else ())
                 for g in goals if (a,b)[g]<MAX[g] for i in range(len(hand))]
        return max(choices) if choices else F(REWARD[0]*(a==MAX[0])+REWARD[1]*(b==MAX[1]))
    # Root baseline must allow a fixed script to skip actions on already
    # completed lanes, otherwise it artificially penalizes fixed sequences.
    # Our better baseline uses a *priority ordering* rather than frozen-by-turn
    # scripts: "finish A then B" or "finish B then A", with free die choice.
    @lru_cache(None)
    def priority(a,b,prev,hand,first):
        if not hand or (a==MAX[0] and b==MAX[1]):
            return F(REWARD[0]*(a==MAX[0])+REWARD[1]*(b==MAX[1]))
        target=first if (a,b)[first]<MAX[first] else 1-first
        result=F(0)
        for i,d in enumerate(hand):
            rest=hand[:i]+hand[i+1:]
            v=sum(pr*priority(min(MAX[0],a+(gain if target==0 else 0)),
                              min(MAX[1],b+(gain if target==1 else 0)),
                              target,rest,first)
                  for gain,pr in enumerate(odds(d,skill,0 if prev<0 else (-1 if prev==target else 1))))
            result=max(result,v)
        return result
    rows=[]
    sum_free=sum_fixed=F(0);strict=0
    for hand in combinations_with_replacement(range(1,7),N):
        best=value(0,0,-1,hand,())
        baseline=max(priority(0,0,-1,hand,0),priority(0,0,-1,hand,1))
        assert best>=baseline
        if best>baseline:strict+=1
        w=weight(hand);sum_free+=best*w;sum_fixed+=baseline*w
    return {"skill":skill,"dice":N,"initial_hands":252,
            "adaptive_reward":round(float(sum_free),9),
            "best_precommitted_goal_priority":round(float(sum_fixed),9),
            "gap":round(float(sum_free-sum_fixed),9),
            "root_hands_with_strict_gain":strict,
            "scope":"Exact Fraction for frozen browser-only mechanics, not engine play or human experience"}
if __name__=="__main__":
    out={"study":"R55","relation":"R50 successor, both outcomes independently collectible","results":[analyze(1),analyze(2)]}
    print(json.dumps(out,ensure_ascii=False,indent=2))
