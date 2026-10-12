#!/usr/bin/env python3
"""R318–R325: two continuations in a bilateral protocol.

Responder has independent preference theta=A/B/Flexible, revealed only
through 'endorse' vs 'counter'. BOTH replies continue to a second move.
The player first proposes A or B then may STICK or REVISE (opposite).
The responder's binding reply changes which actions can succeed.
No storytelling, emotions, or hidden advance knowledge.
"""
from fractions import Fraction as F
from itertools import product
import json
STATES=("A","B","F")
PRIOR={t:F(1,3) for t in STATES}
def response(theta,offer):
    return "endorse" if theta in (offer,"F") else "counter"
def other(offer):return "B" if offer=="A" else "A"
def payoff(theta,offer,action,player_goal,live_counter=True):
    r=response(theta,offer)
    if action=="stick":
        return 0 if r=="counter" else (3 if offer==player_goal else 2)
    assert action=="revise"
    if r=="endorse" or not live_counter:return 0
    return 3 if other(offer)==player_goal else 2

def exhaustive(goal="A",prior=PRIOR,live_counter=True):
    # Two possible first offers, four complete second-stage policies
    # contingent on actual response, independently evaluated across
    # all three possible responder preferences. No Bayesian shortcuts.
    vals={}
    for offer in ("A","B"):
        for on_yes,on_no in product(("stick","revise"),repeat=2):
            name=f"{offer}|{on_yes}|{on_no}"
            vals[name]=sum(prior[t]*payoff(t,offer,on_yes if response(t,offer)=="endorse" else on_no,goal,live_counter)
                           for t in prior)
    best=max(vals.values())
    fixed={f"{offer}|{act}":sum(prior[t]*payoff(t,offer,act,goal,live_counter) for t in prior)
           for offer in ("A","B") for act in ("stick","revise")}
    return {"full":best,
            "best_response_blind":max(fixed.values()),
            "best_contingent_policies":sorted(k for k,v in vals.items() if v==best),
            "best_blind_policies":sorted(k for k,v in fixed.items() if v==max(fixed.values())),
            "all_four_blind":fixed}

def stagewise(goal="A",prior=PRIOR,live_counter=True):
    # Independent response-distribution + stagewise choice solver, NOT
    # enumeration of contingent second-stage policy trees.
    q={}
    for offer in ("A","B"):
        branches={}
        for r in ("endorse","counter"):
            branches[r]={a:sum(prior[t]*payoff(t,offer,a,goal,live_counter) for t in prior if response(t,offer)==r)
                         for a in ("stick","revise")}
        q[offer]=branches
    val=max(sum(max(branch.values()) for branch in per_reply.values()) for per_reply in q.values())
    return val,q

def readable(goal,prior=PRIOR,live_counter=True):
    # "Offer my favored product. If endorsed, fulfill; if countered,
    # revise." This short policy is treated as a strong rival.
    offer=goal
    return sum(prior[t]*payoff(t,offer,"stick" if response(t,offer)=="endorse" else "revise",goal,live_counter)
               for t in prior)

def format_json(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):format_json(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [format_json(v) for v in x]
    return x

def audit():
    main={}
    for goal in ("A","B"):
        brute=exhaustive(goal)
        dyn,branches=stagewise(goal)
        rule=readable(goal)
        assert brute["full"]==dyn==rule==F(8,3)
        assert brute["best_response_blind"]==2
        main[goal]={**brute,"independent_stagewise":dyn,"readable_rule":rule,
                    "stagewise_Q_weighted":branches}
    no_counter=exhaustive("A",live_counter=False)
    assert (no_counter["full"],no_counter["best_response_blind"])==(2,2)
    without_flexible={"A":F(1,2),"B":F(1,2)}
    two_types=exhaustive("A",without_flexible)
    assert (two_types["full"],two_types["best_response_blind"])==(F(5,2),F(3,2))
    skew={"A":F(1,4),"B":F(1,4),"F":F(1,2)}
    skewed=exhaustive("A",skew)
    assert (skewed["full"],skewed["best_response_blind"])==(F(11,4),F(9,4))
    # Public reply branches both have positive probability and produce
    # different strict choices along the actual best proposal.
    q=main["A"]["stagewise_Q_weighted"]["A"]
    assert q["endorse"]["stick"]>q["endorse"]["revise"]
    assert q["counter"]["revise"]>q["counter"]["stick"]
    # If responder has no binding effect on legal payoffs, a simple final
    # free choice of player's preferred action always pays 3. There is
    # no reason to infer or care about responder preference.
    no_binding=F(3)
    return format_json({
      "schema":"ssnoir.r318-r325.bilateral-reply.v1",
      "scope":"exact paper, no independent human agent simulation or native game integration",
      "rules":"One proposal A/B; independent responder theta=A/B/F equally likely; endorse if theta likes offer or flexible, counter if dislikes; BOTH continue. Choose STICK (reward 3 if initial offer is player's desired type else 2, provided endorsed) or REVISE (take opposing offer, reward 3/2 by preference only if countered).",
      "R318_player_goal_A_full_branching":main["A"],
      "R319_player_goal_B_reverse_preference":main["B"],
      "R320_independent_stagewise_solver":main["A"]["independent_stagewise"],
      "R321_strong_readable_follow_reply":main["A"]["readable_rule"],
      "R322_no_actionable_counter":no_counter,
      "R323_only_two_responder_types":two_types,
      "R324_flexible_responder_twice_likely":skewed,
      "R325_no_binding_and_novelty":{"if_responder_reply_has_no_consequences":no_binding,
        "verdict":"Having TWO continuing replies matters relative to one-ended R294 offer ladder; but short 'propose favorite, fulfill if endorsed, revise if countered' is already globally optimal. This is response-conditioned menu selection, not a newly certified bargaining loop."}
    })
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
