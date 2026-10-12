#!/usr/bin/env python3
"""R128–R130: witness collateral and public time-window falsification.

Exact Fraction paper model only, not SSNoir C# / Scheme or human-playtest rules.
"""
import json
from functools import lru_cache
from fractions import Fraction

# 3 actions (t=0,1,2), public tier(B) revealed at t=1.
# A: ask only at t0, succeeds independently with probability 1/2.
# If A refuses, coerce at t1 or lose A, unless window is extended through t2.
# B: ask at t1 or t2, probability 1/2; if refused at t1, coerce at t2.
# Coercing A before B is approached makes B refuse permanently (collateral).
# Permanent evidence: A=3, B=2 or 4 (equal prior); no partial credit.
# status: 0=not asked; 1=refused; 2=evidence; 3=closed (B only).

def solve(collateral=True, extend_a_window=False, policy="free"):
    assert policy in {"free", "force_A_on_refusal", "force_B_on_refusal"}

    @lru_cache(None)
    def V(t, a, b, tier):
        if t == 3:
            return Fraction(3 * (a == 2) + tier * (b == 2))
        options = [V(t+1, a, b, tier)]  # wait
        if a == 1 and (t == 1 or extend_a_window):
            if not (t == 1 and b == 0 and policy == "force_B_on_refusal"):
                new_b = 3 if collateral and b == 0 else b
                options.append(V(t+1, 2, new_b, tier))
        if b == 0:
            if not (t == 1 and a == 1 and policy == "force_A_on_refusal"):
                options.append((V(t+1, a, 1, tier) + V(t+1, a, 2, tier))/2)
        if b == 1:
            options.append(V(t+1, a, 2, tier))
        return max(options)

    # At t0 the same first action must be chosen WITHOUT knowing B's tier.
    # Each (tier, A response) pair has probability exactly 1/4.
    ask_a = sum((V(1, 2, 0, tier) + V(1, 1, 0, tier))
                for tier in (2,4)) / 4
    wait = (V(1,0,0,2) + V(1,0,0,4)) / 2
    value = max(ask_a, wait)
    counterfactual = {}
    if policy == "free":
        for tier in (2,4):
            new_b = 3 if collateral else 0
            coerce = V(2,2,new_b,tier)
            ask_b = (V(2,1,1,tier) + V(2,1,2,tier))/2
            counterfactual[str(tier)] = {
                "coerce_A":str(coerce),
                "ask_B":str(ask_b),
                "strict_preference":"A" if coerce>ask_b else "B" if ask_b>coerce else "tie"}
    return {"value":value, "ask_A_at_t0":ask_a, "wait_t0":wait,
            "rejected_A_choices":counterfactual}

def report():
    base=solve()
    fixed_a=solve(policy="force_A_on_refusal")
    fixed_b=solve(policy="force_B_on_refusal")
    disabled=solve(collateral=False)
    late=solve(extend_a_window=True)
    late_disabled=solve(collateral=False,extend_a_window=True)

    assert base["ask_A_at_t0"]>base["wait_t0"]
    assert base["value"]==Fraction(19,4)
    assert fixed_a["value"]==fixed_b["value"]==Fraction(9,2)
    assert disabled["value"]==Fraction(21,4)
    assert late["value"]==late_disabled["value"]==Fraction(43,8)
    assert base["rejected_A_choices"]["2"]["strict_preference"]=="A"
    assert base["rejected_A_choices"]["4"]["strict_preference"]=="B"
    assert late["rejected_A_choices"]["2"]["strict_preference"]=="B"
    assert late["rejected_A_choices"]["4"]["strict_preference"]=="B"
    assert base["value"]-max(fixed_a["value"],fixed_b["value"])==Fraction(1,4)
    assert disabled["value"]-base["value"]==Fraction(1,2)
    assert late_disabled["value"]-late["value"]==0

    def clean(value):
        return {k:(str(x) if isinstance(x,Fraction) else x)
                for k,x in value.items()}

    return {
      "schema":"ssnoir.R128-R130.exact-paper/v1",
      "scope":"Only frozen mathematical toy, NOT current SSNoir gameplay or human playtest",
      "rules":{"actions":3,"askA_window":[0],"coerceA_window":[1],
               "askB_window":[1,2],"B_tier":[2,4],"coop_probability":"1/2",
               "A_reward":3,"B_reward":"revealed tier",
               "completed_persists":True},
      "R128_collateral_true":clean(base),
      "R128_strong_fixed":{
        "always_A":str(fixed_a["value"]),
        "always_B":str(fixed_b["value"]),
        "adaptive_gain":str(base["value"]-max(fixed_a["value"],fixed_b["value"])),
        "strict_A_or_B_mass_each":"1/4 under optimal initial askA"},
      "R129_disable_collateral":{
        "value":str(disabled["value"]),
        "increment_without_collateral":str(disabled["value"]-base["value"])},
      "R130_extend_A_coercion_window":{
        "value":str(late["value"]),
        "without_collateral_value":str(late_disabled["value"]),
        "collateral_increment":str(late_disabled["value"]-late["value"]),
        "branches":late["rejected_A_choices"]},
      "verdict":"Public tier and short coercion deadline jointly create strictly different rational choices. Simple public rule still suffices; later access to A erases collateral's optimal value."
    }

if __name__=="__main__":
    print(json.dumps(report(),ensure_ascii=False,indent=2))
