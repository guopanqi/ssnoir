#!/usr/bin/env python3
"""REC01/REC02: Reciprocal productive actions vs interruptible routines.

Full four-turn exact Fraction DP over public, independently sampled blockers.
A/B production changes the OTHER action's productivity on the next turn.
All rewards are end-state goals except SIDE=1. No human/agent simulation.
"""
from functools import lru_cache
from fractions import Fraction as Q
from itertools import product

MODES = ("none", "blockA", "blockB")
PROBS = (Q(1, 2), Q(1, 4), Q(1, 4))
GOALS = (3, 3)
REWARDS = (10, 8)
DAYS = 4
PRIMING = ("mutual", "a_to_b", "b_to_a", "none", "any_previous", "repeat")

def legal(mode, blocked=True):
    return tuple(x for x in ("A", "B", "SIDE") if
                 not (blocked and (mode == "blockA" and x == "A"
                                   or mode == "blockB" and x == "B")))

def enabled(priming, last, action):
    assert priming in PRIMING
    if action == "SIDE":
        return False
    if priming == "mutual":
        return (last, action) in (("A", "B"), ("B", "A"))
    if priming == "a_to_b":
        return (last, action) == ("A", "B")
    if priming == "b_to_a":
        return (last, action) == ("B", "A")
    if priming == "any_previous":
        return last in ("A", "B")
    if priming == "repeat":
        return last == action
    return False

def step(a, b, last, action, priming):
    if action == "SIDE":
        return (a, b, "SIDE")
    extra = int(enabled(priming, last, action))
    if action == "A":
        return (min(4, a + 1 + extra), b, "A")
    return (a, min(4, b + 1 + extra), "B")

def terminal(a, b):
    return Q(REWARDS[0] * (a >= GOALS[0])
             + REWARDS[1] * (b >= GOALS[1]))

def solver(priming="mutual", blocked=True):
    @lru_cache(None)
    def solve(day, a, b, last):
        if day == DAYS:
            return terminal(a, b), ()
        entries = []
        for mode, prob in zip(MODES, PROBS):
            choices = []
            for action in legal(mode, blocked):
                next_a, next_b, next_last = step(a, b, last, action, priming)
                value = Q(action == "SIDE") + solve(
                    day + 1, next_a, next_b, next_last)[0]
                choices.append((value, action))
            best_value, best_action = max(choices, key=lambda p: p[0])
            entries.append((mode, best_action, best_value, tuple(choices), prob))
        return sum(weight * value for _, _, value, _, weight in entries), tuple(entries)
    return solve

def evaluate(policy, priming="mutual", blocked=True):
    @lru_cache(None)
    def go(day, a, b, last):
        if day == DAYS:
            return terminal(a, b)
        result = Q(0)
        for mode, prob in zip(MODES, PROBS):
            moves = legal(mode, blocked)
            action = policy(day, a, b, last, mode, moves)
            assert action in moves, (action, moves, day)
            na, nb, nl = step(a, b, last, action, priming)
            result += prob * (Q(action == "SIDE") + go(day + 1, na, nb, nl))
        return result
    return go(0, 0, 0, "-")

def routine(pattern):
    assert len(pattern) == DAYS
    def act(day, a, b, last, mode, moves):
        first = pattern[day]
        if first in moves:
            return first
        return "B" if first == "A" else "A"
    return act

def alternate(day, a, b, last, mode, moves):
    desired = "B" if last == "A" else "A"
    return desired if desired in moves else ("A" if "A" in moves else "B")

def one_step_lookahead(base=alternate):
    @lru_cache(None)
    def base_value(day, a, b, last):
        if day == DAYS:
            return terminal(a, b)
        total = Q(0)
        for mode, p in zip(MODES, PROBS):
            moves = legal(mode)
            act = base(day, a, b, last, mode, moves)
            na, nb, nl = step(a, b, last, act, "mutual")
            total += p * (Q(act == "SIDE") + base_value(day+1, na, nb, nl))
        return total

    def improved(day, a, b, last, mode, moves):
        options = []
        for action in moves:
            na, nb, nl = step(a, b, last, action, "mutual")
            options.append((Q(action == "SIDE")
                            + base_value(day+1, na, nb, nl), action))
        return max(options, key=lambda x:x[0])[1]
    return improved

def mode_choice(solve, day, a, b, last, mode):
    for m, action, val, other, prob in solve(day, a, b, last)[1]:
        if m == mode:
            return action, val, dict((x,v) for v,x in other)
    raise AssertionError(mode)

def outcome_vector(policy, priming="mutual", blocked=True):
    """E[complete_A, complete_B, SIDE count], not a scalar welfare claim.

    Enumerate every exogenous public signal string, using only observations
    available at the current step to the supplied policy.
    """
    totals = [Q(0), Q(0), Q(0)]
    for draws in product(MODES, repeat=DAYS):
        a=b=side=0
        last="-"
        weight=Q(1)
        for day,mode in enumerate(draws):
            weight *= PROBS[MODES.index(mode)]
            moves=legal(mode,blocked)
            action=policy(day,a,b,last,mode,moves)
            assert action in moves
            a,b,last=step(a,b,last,action,priming)
            side+=int(action=="SIDE")
        for i,x in enumerate((int(a>=3),int(b>=3),side)):
            totals[i]+=weight*x
    return tuple(totals)


def main():
    baseline = solver("mutual")
    root = baseline(0, 0, 0, "-")[0]
    assert root == Q(939, 64), root
    static = [(p, evaluate(routine(p))) for p in product(("A", "B"), repeat=DAYS)]
    best_fixed = max(v for p,v in static)
    assert best_fixed == Q(801, 64)
    assert { "".join(p) for p,v in static if v==best_fixed } == {
        "ABAB", "ABBA", "BAAB", "BABA"}
    basic = evaluate(alternate)
    improved = evaluate(one_step_lookahead())
    assert basic == Q(227,16) and improved == root
    print("REC01 mutual",root,"best_fixed",best_fixed,"alternate",basic,
          "one_step_improved",improved,
          "fixed_gap",root-best_fixed,"alternate_gap",root-basic)

    # Natural opening first A from two separate first-turn blocker signals,
    # then the next public blocker makes a STRICT choice reversal.
    for mode in ("none", "blockB"):
        assert mode_choice(baseline, 0, 0, 0, "-", mode)[0] == "A"
    open_action, open_value, open_opts = mode_choice(
        baseline, 1, 1, 0, "A", "none")
    blocked_action, blocked_value, blocked_opts = mode_choice(
        baseline, 1, 1, 0, "A", "blockB")
    assert open_action == "B" and open_value == Q(129, 8)
    assert blocked_action == "A" and blocked_value == Q(85,8)
    assert open_opts["B"] > open_opts["A"]
    assert blocked_opts["A"] > blocked_opts["SIDE"]
    no_priming = solver("none")
    plain_action,plain_value,plain_opts=mode_choice(
        no_priming,1,1,0,"A","none")
    assert plain_action == "A" and plain_opts["A"] > plain_opts["B"]
    print("REC01 reachable reversal next public unblocked->B, blockedB->A",
          "and with no aftereffect unblocked->A",open_opts,plain_opts)

    expected = {
        "mutual":Q(939,64),
        "a_to_b":Q(2567,256),
        "b_to_a":Q(2691,256),
        "none":Q(1193,128),
        "any_previous":Q(2027,128),
        "repeat":Q(431,32),
    }
    for prime, expected_value in expected.items():
        value = solver(prime)(0,0,0,"-")[0]
        assert value == expected_value,(prime,value,expected_value)
        print("REC02 ablation",prime,value,float(value))
    # Scalar optimization is a research *instrument*, not the definition of
    # what an individual player should value. Keep the actual expected
    # outcomes of the two distinct tasks plus independent SIDE results.
    def exact_action(day,a,b,last,mode,moves):
        return mode_choice(baseline,day,a,b,last,mode)[0]
    vectors = {
        "oracle": outcome_vector(exact_action),
        "alternate":outcome_vector(alternate),
        "fixed":outcome_vector(routine(("A","B","A","B"))),
    }
    assert vectors["oracle"] == (Q(117,128),Q(83,128),Q(11,32))
    assert vectors["alternate"] == (Q(27,32),Q(23,32),Q(0))
    assert vectors["fixed"] == (Q(89,128),Q(89,128),Q(0))
    for name,(a_prob,b_prob,side) in vectors.items():
        scalar=10*a_prob+8*b_prob+side
        assert scalar==({"oracle":root,"alternate":basic,
                         "fixed":best_fixed}[name])
        print("REC03 outcome vector",name,"A",a_prob,
              "B",b_prob,"SIDE",side)
    # Even though optimal aggregate payoff is higher, the optimal policy
    # puts LESS completion probability on B than the easy alternation rule.
    assert vectors["oracle"][1] < vectors["alternate"][1]
    assert vectors["oracle"][0] > vectors["alternate"][0]

    # If there is no blockade, deterministic ABAB/BABA hits BOTH goal
    # thresholds. Hence intermittent blocking is what makes revision useful.
    no_block=solver("mutual",blocked=False)(0,0,0,"-")[0]
    assert no_block == 18
    assert evaluate(routine(("A","B","A","B")),blocked=False)==18
    print("REC02 without blockers",no_block,"fixed ABAB already perfect")

    # Exact enumeration of every public blocker string (3^4=81), choosing
    # only what the oracle can see so far. It does not peek at future draws.
    histories=0
    action_histories=set()
    for draws in product(MODES,repeat=DAYS):
        a=b=0
        last="-"
        actions=[]
        for day,mode in enumerate(draws):
            action,_,_=mode_choice(baseline,day,a,b,last,mode)
            assert action in legal(mode)
            a,b,last=step(a,b,last,action,"mutual")
            actions.append(action)
        histories+=1
        action_histories.add(tuple(actions))
    assert histories==81 and len(action_histories)>=4
    print("REC01 full natural signals",histories,"distinct oracle action paths",
          len(action_histories))
    print("PASS productive switches, complete public-history reachability,"
          " strong short strategy and symmetric counterfactuals")

if __name__=="__main__":
    main()
