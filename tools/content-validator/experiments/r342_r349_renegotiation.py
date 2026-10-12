#!/usr/bin/env python3
"""R342-R349: continuing counterpart reply creates an enforceable right,
and next round's new demand can justify paying to renegotiate.
Exact hidden-type/demand event enumeration, strong reply-blind policies,
and two conditional choices. Not a real negotiation or human playtest.
"""
import json
from fractions import Fraction as F
from itertools import product
TYPES=("A","B","F")
SIDES=("A","B")
PRIOR={theta:F(1,3) for theta in TYPES}
def other(s):return "B" if s=="A" else "A"
def reply(theta,offer):return "endorse" if theta in (offer,"F") else "counter"
def demand_probability(theta,side,correlation=True):
    if not correlation or theta=="F":return F(1,2)
    return F(7,8) if side==theta else F(1,8)

def terminal(right,demand,allow_break=True,break_cost=2):
    # Genuine third-stage choice: honor limited right, or break exclusivity
    # paying a public penalty to execute what new demand now requires.
    if right is None or right==demand:return F(4)
    return max(F(0), F(4-break_cost) if allow_break else F(0))

def contract_right(offer,response):
    return offer if response=="endorse" else other(offer)
def contract_pay(response,accepted):
    if not accepted:return F(0)
    return F(1,2) if response=="endorse" else F(1)

def value(offer,decide,allow_break=True,correlation=True,break_cost=2):
    total=F(0)
    contributions={}
    for theta in TYPES:
        response=reply(theta,offer)
        accepted=decide(response)
        right=contract_right(offer,response) if accepted else None
        instant=contract_pay(response,accepted)
        subtotal=F(0)
        for demand in SIDES:
            subtotal+=demand_probability(theta,demand,correlation)*(
                instant+terminal(right,demand,allow_break,break_cost))
        total+=PRIOR[theta]*subtotal
        contributions[theta]=subtotal
    return total,contributions

def enumeration(allow_break=True,correlation=True,break_cost=2):
    plans={}
    for offer in SIDES:
        for yes,no in product((False,True),repeat=2):
            label=f"{offer}|{int(yes)}{int(no)}"
            plans[label]=value(offer,lambda r,yes=yes,no=no: yes if r=="endorse" else no,
                               allow_break,correlation,break_cost)[0]
    full=max(plans.values())
    best=[k for k,v in plans.items() if v==full]
    blind={}
    for offer in SIDES:
        for action in (False,True):
            blind[f"{offer}|{int(action)}"]=value(offer,lambda r,action=action:action,
                                                allow_break,correlation,break_cost)[0]
    blindbest=max(blind.values())
    return {"optimal":full,"best_precommitted_accept_all_or_none":blindbest,
            "best_response_policies":best,
            "best_blind_policies":[k for k,v in blind.items() if v==blindbest],
            "all_public_contingent_policies":plans}

def stagewise(offer="A",allow_break=True,correlation=True,break_cost=2):
    # Independent aggregation of Q for each actually received reply:
    # no use of full contingent-plan enumeration.
    q={}
    for r in ("endorse","counter"):
        q[r]={}
        for accept in (False,True):
            gain=F(0)
            for theta in TYPES:
                if reply(theta,offer)!=r:continue
                right=contract_right(offer,r) if accept else None
                for demand in SIDES:
                    gain+=PRIOR[theta]*demand_probability(theta,demand,correlation)*(
                        contract_pay(r,accept)+terminal(right,demand,allow_break,break_cost))
            q[r]["accept" if accept else "defer"]=gain
    return sum(max(x.values()) for x in q.values()),q

def readable(offer="A",allow_break=True,correlation=True,break_cost=2):
    # Two-line public policy. Note real participants might not discover it!
    return value(offer,lambda r:r=="counter",allow_break,correlation,break_cost)[0]

def serialized(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):serialized(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [serialized(y) for y in x]
    return x

def audit():
    standard=enumeration()
    backwards=enumeration(break_cost=3)
    no_break=enumeration(allow_break=False)
    independence=enumeration(correlation=False)
    full,branchq=stagewise()
    readable_policy=readable()
    assert standard["optimal"]==full==readable_policy==F(17,4)
    assert standard["best_precommitted_accept_all_or_none"]==F(25,6)
    assert branchq["endorse"]["defer"]>branchq["endorse"]["accept"]
    assert branchq["counter"]["accept"]>branchq["counter"]["defer"]
    assert no_break["optimal"]==F(25,6)
    assert independence["optimal"]==4
    assert backwards["optimal"]==F(101,24)
    assert backwards["optimal"]>=4
    assert standard["best_response_policies"]==["A|01","B|01"]
    assert all(len(x["all_public_contingent_policies"])==8
               for x in (standard,backwards,no_break,independence))
    return serialized({
      "schema":"ssnoir.r342-r349.renegotiation-and-rights.v1",
      "scope":"exact paper with independent hidden counterpart type and future demand; no native agents or humans",
      "rules":"Offer A/B. Hidden counterpart type A/B/F each 1/3: endorse if likes offer or flexible, otherwise counter; BOTH continue. After reply, defer (no grant, both rights preserved) or accept (endorse grants original side with bonus1/2; counter grants opposite side with bonus1). Later future demand D becomes public: theta A/B predicts matching D with 7/8, F neutral. Contract locks legal action to granted side; can honor or pay penalty2 to break and perform demanded action for4-penalty. Deferral retains both sides, pays4 at final. No information about D at offer/accept time.",
      "R342_two_round_commitment":standard,
      "R343_independent_response_Q":branchq,
      "R344_readable_defer_endorse_accept_counter":readable_policy,
      "R345_remove_renegotiation":no_break,
      "R346_make_future_demand_independent_of_response":independence,
      "R347_raise_break_penalty":backwards,
      "R348_strong_blind_reply_baseline":standard["best_precommitted_accept_all_or_none"],
      "R349_judgement":"Response alters which exclusive right can be accepted and later public demand can force an honor/break decision. But a two-line readable policy reaches full value and it remains an option/commitment-cost + conditional-reply composition, not certified original negotiation gameplay."
    })
if __name__=="__main__":print(json.dumps(audit(),indent=2,ensure_ascii=False))
