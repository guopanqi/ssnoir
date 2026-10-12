#!/usr/bin/env python3
"""R302–R309: cost-sensitive, decision-relevant diagnosis.

Four hidden states, at most two subset tests A={0,1}, B={0}, C={2}.
Each test spends c of utility, terminal action score 1 if it matches
the hidden state's required action. Both DP and fixed question schedules
may stop early after public answers. No pre-reading the hidden type.
"""
from fractions import Fraction as F
from itertools import product
from functools import lru_cache
import json

H=(0,1,2,3); TEST={"A":frozenset((0,1)),"B":frozenset((0,)),"C":frozenset((2,))}
GROUPS={"decision_3":("X","X","Y","Z"),
        "two_actions":("X","X","Y","Y"),
        "identity_4":("W","X","Y","Z")}
INIT=tuple(H)
def terminal(states,targets):
    return max(F(sum(targets[h]==a for h in states),4) for a in set(targets))
def split(states,question):
    return tuple(part for ans in (False,True)
                 if (part:=tuple(h for h in states if (h in TEST[question])==ans)))

def unrestricted(targets,c=F(1,8),maxq=2):
    @lru_cache(None)
    def dp(states,remain):
        stop=terminal(states,targets)
        if remain==0:return stop
        return max([stop]+[-c*F(len(states),4)+sum(dp(part,remain-1) for part in split(states,question))
                           for question in TEST])
    initial={question:-c+sum(dp(part,maxq-1) for part in split(INIT,question))
             for question in TEST}
    root=dp(INIT,maxq)
    return root,initial

def fixed_question_order(targets,questions,c=F(1,8),conditional_stop=True):
    # Independently evaluate a precommitted order, allowing rational
    # stopping on each observed branch before any further paid test.
    @lru_cache(None)
    def rec(states,position):
        stop=terminal(states,targets)
        if position==len(questions):return stop
        q=questions[position]
        paid=-c*F(len(states),4)+sum(rec(part,position+1) for part in split(states,q))
        return max(stop,paid) if conditional_stop else paid
    return rec(INIT,0)

def strongest_fixed(targets,c=F(1,8),conditional_stop=True):
    vals={q:fixed_question_order(targets,q,c,conditional_stop) for q in product(TEST,repeat=2)}
    v=max(vals.values())
    return v,["".join(q) for q,x in vals.items() if x==v]

def expected_stop_under_A_then_C(targets):
    # Independent event-by-event accounting (not a Bellman evaluation).
    # On A=yes choose X immediately. A=no use C to distinguish Y/Z.
    utility=F(0);tests=F(0);correct=0
    for h in H:
        ask2=(h not in TEST["A"])
        count=1+ask2;tests+=F(count,4)
        guess="X" if not ask2 else "Y" if h in TEST["C"] else "Z"
        correct+=int(guess==targets[h])
        utility+=F(int(guess==targets[h]),4)-F(count,32)
    return {"gross_accuracy":F(correct,4),"expected_tests":tests,"net_utility":utility}

def render(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):render(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [render(y) for y in x]
    return x

def main():
    out={}
    for name,targets in GROUPS.items():
        optimum,qvalues=unrestricted(targets)
        precommit,plans=strongest_fixed(targets)
        forced,forced_plans=strongest_fixed(targets,conditional_stop=False)
        noquery=terminal(INIT,targets)
        assert optimum>=precommit>=forced
        out[name]={"action_per_hidden_state":targets,"full_adaptive":optimum,
                   "precommit_questions_with_answer_based_stop":precommit,
                   "optimal_precommitted_orders":plans,
                   "forced_ask_two_precommitted":forced,
                   "forced_orders":forced_plans,
                   "no_questions":noquery,
                   "first_question_Q":qvalues}
    x=out["decision_3"];y=out["two_actions"];z=out["identity_4"]
    assert (x["full_adaptive"],x["precommit_questions_with_answer_based_stop"],x["forced_ask_two_precommitted"])==(F(13,16),F(13,16),F(3,4))
    assert (y["full_adaptive"],y["no_questions"])==(F(7,8),F(1,2))
    assert (z["full_adaptive"],z["precommit_questions_with_answer_based_stop"])==(F(3,4),F(9,16))
    direct=expected_stop_under_A_then_C(GROUPS["decision_3"])
    assert direct=={"gross_accuracy":1,"expected_tests":F(3,2),"net_utility":F(13,16)}
    fees={}
    for c in (F(0),F(1,16),F(1,8),F(1,4),F(1,2)):
        opt,_=unrestricted(GROUPS["decision_3"],c)
        fixed,_=strongest_fixed(GROUPS["decision_3"],c)
        fees[str(c)]={"adaptive":opt,"precommitted_with_stop":fixed}
        assert opt==fixed
    assert fees["1/2"]["adaptive"]==F(1,2) # Do not buy worthless tests.
    return render({
        "schema":"ssnoir.r302-r309.decision-relevant-tests.v1",
        "scope":"exact abstract paper; all information published only after action; no participant evidence",
        "rule":"Four equal hidden types, public question sets A={0,1} B={0} C={2}; max 2 yes/no tests at cost c=1/8, any time STOP and choose action. Only correct *action* wins +1, not necessarily identifying the complete type.",
        "R302_action_equivalence_three_decisions":x,
        "R303_direct_strategy_independent_trace":direct,
        "R304_both_questions_forced_benchmark":x["forced_ask_two_precommitted"],
        "R305_strong_fixed_order_conditional_stop":x["precommit_questions_with_answer_based_stop"],
        "R306_two_decision_classes":y,
        "R307_four_distinct_actions":z,
        "R308_cost_and_zero_information_limits":fees,
        "R309_judgement":"Stopping after sufficient evidence matters, but in the three-action model a *precommitted A,C question order with publicly conditional stopping* reaches complete adaptive optimum. Four distinct actions revive branching-question value. Do not credit every saving to question redesign or claim a new unique mechanic."
    })
if __name__=="__main__":print(json.dumps(main(),ensure_ascii=False,indent=2))
