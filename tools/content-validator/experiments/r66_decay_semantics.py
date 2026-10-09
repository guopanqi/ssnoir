#!/usr/bin/env python3
"""R66: falsify interpretation of R61 and test a reward-preserving repair.

Pure exact Fraction, 4 d6 (126 unordered roots); FateStrip skill1.
R61 prior changes BOTH score and unbanked completed progress. We distinguish:
old volatile-progress terminal score vs retained awarded completions.
"""
import json
from functools import lru_cache
from itertools import combinations_with_replacement
from fractions import Fraction as F
from r56_r65_autonomous_search import odds, hand_weight, evaluate

N=4
MAX=(2,3)

def advance(s,g,gain,banked):
    a,b=s
    if g==0:
        a=min(2,a+gain)
        b=b if banked and b==3 else max(0,b-1)
    else:
        b=min(3,b+gain)
        a=a if banked and a==2 else max(0,a-1)
    return (a,b)

def solve(banked):
    @lru_cache(None)
    def opt(s,hand):
        if not hand or (banked and s==(2,3)):return F(s[0]>=2)+2*F(s[1]>=3)
        return max(q(s,hand,g,i,opt) for g in range(2) if s[g]<MAX[g] or not banked
                   for i in range(len(hand)))
    @lru_cache(None)
    def fixed(s,hand,g):
        if not hand or (banked and s==(2,3)):return F(s[0]>=2)+2*F(s[1]>=3)
        if banked and s[g]==MAX[g]:return F(s[0]>=2)+2*F(s[1]>=3)
        return max(q(s,hand,g,i,lambda next_s,next_hand:fixed(next_s,next_hand,g)) for i in range(len(hand)))
    @lru_cache(None)
    def priority(s,hand,first):
        if not hand or (banked and s==(2,3)):return F(s[0]>=2)+2*F(s[1]>=3)
        g=first if not banked or s[first]<MAX[first] else 1-first
        return max(q(s,hand,g,i,lambda next_s,next_hand:priority(next_s,next_hand,first))
                   for i in range(len(hand)))
    def q(s,hand,g,i,fn):
        rest=hand[:i]+hand[i+1:]
        return sum(p*fn(advance(s,g,k,banked),rest) for k,p in enumerate(odds(hand[i])) if p)
    @lru_cache(None)
    def path(s,hand,prev):
        if not hand or (banked and s==(2,3)):
            return (F(0),F(0),F(0),F(s==(2,3)))
        choices=[(q(s,hand,g,i,opt),g,i) for g in range(2) if s[g]<MAX[g] or not banked
                 for i in range(len(hand))]
        best=max(v for v,_,_ in choices)
        v,g,i=next((v,g,i) for v,g,i in choices if v==best)
        # Strictly better to switch than *any* allowed action continuing prev.
        stay=max((q(s,hand,prev,j,opt) for j in range(len(hand))),
                 default=F(-1)) if prev>=0 and (not banked or s[prev]<MAX[prev]) else F(-1)
        changed=int(prev>=0 and g!=prev)
        strict=int(changed and best>stay)
        result=[F(changed),F(strict),F(1 if changed and len(hand)==2 and strict else 0),F(0)]
        rest=hand[:i]+hand[i+1:]
        for k,p in enumerate(odds(hand[i])):
            if not p:continue
            child=path(advance(s,g,k,banked),rest,g)
            for j in range(4):result[j]+=p*child[j]
        return tuple(result)
    avg=F(0);root_priority=F(0);root_fixed=F(0);strict_roots=0
    move=[F(0)]*4
    for hand in combinations_with_replacement(range(1,7),N):
        w=hand_weight(hand);root=opt((0,0),hand)
        fixed_best=max(fixed((0,0),hand,0),fixed((0,0),hand,1))
        priority_best=max(priority((0,0),hand,0),priority((0,0),hand,1))
        assert root>=priority_best>=fixed_best
        strict_roots+=root>priority_best
        avg+=w*root;root_fixed+=w*fixed_best;root_priority+=w*priority_best
        paths=path((0,0),hand,-1)
        for i in range(4):move[i]+=w*paths[i]
    return {"version":"completion-banked" if banked else "R61-volatile-progress",
            "expected_optimal":round(float(avg),9),
            "best_root_fixed_lane":round(float(root_fixed),9),
            "best_root_goal_priority_adaptive_dice":round(float(root_priority),9),
            "advantage_over_priority":round(float(avg-root_priority),9),
            "hands_strictly_exceeding_priority":strict_roots,
            "expected_switches_per_trial":round(float(move[0]),9),
            "expected_strict_switches_per_trial":round(float(move[1]),9),
            "strict_switch_on_third_move_probability":round(float(move[2]),9),
            "both_terminal_completion_probability":round(float(move[3]),9)}

def invariant():
    states={(0,0)}
    reachable=[len(states)]
    for turn in range(1,5):
        states={advance(s,g,k,False) for s in states for g in range(2) for k in (0,1,2)}
        assert all(not (a==2 and b==3) for a,b in states), "R61 completion unexpectedly persistent"
        reachable.append(len(states))
    return {"R61_both_completed_impossible":True,"reachable_state_counts":reachable,
            "reason":"The last move always decreases the OTHER lane by one, even if it was previously complete."}

def main():
    # Legacy check: the exact R61 expected value is in the old 10-round record.
    legacy=evaluate("R61","decay")
    volatile=solve(False)
    assert abs(volatile["expected_optimal"]-legacy["free"])<1e-6
    result={"id":"R66","purpose":"Distinguish real mid-course redirection from revocable completion accounting",
            "legacy_model":legacy,"invariant":invariant(),
            "ablation":[volatile,solve(True)],
            "scope":"Exact paper model, not C# engine / human play; ties use first action in A/B and ascending remaining-die order"}
    print(json.dumps(result,ensure_ascii=False,indent=2))
if __name__=="__main__":main()
