#!/usr/bin/env python3
"""R390-R397: strategic, not automatically myopic, counterpart veto.

This is a precise pre-announced three-turn rule plan comparison, NOT
the adaptive online player game of R374. Counterpart knows the plan,
has fixed preferred action theta, may use one veto when rule excludes
theta, and chooses when to veto to maximize ITS OWN total preferences.
Among own-optimal veto timings the player outcome is not identified
without an explicit tie-break assumption; report both hostile/friendly.
"""
from fractions import Fraction as F
from itertools import product
import json

DEMAND="ABA"
RULES="OAB"

def resolve(seq,theta,veto_time=None,cost=F(1,4)):
    old_rule="O";already=False;own=0;player=F(0);trace=[]
    for t,proposed in enumerate(seq):
        allowed=(not already and proposed!="O" and proposed!=theta)
        veto=(veto_time==t and allowed)
        rule="O" if veto else proposed
        if veto:already=True
        performed=theta if rule=="O" else rule
        own+=int(performed==theta)
        player+=int(performed==DEMAND[t])-cost*int(proposed!=old_rule)
        trace.append({"t":t,"requested":proposed,"veto":veto,"performed":performed})
        old_rule=rule
    return {"partner_score":own,"player_score":player,"trace":trace}

def strategic(seq,theta):
    responses=[resolve(seq,theta,x) for x in (None,0,1,2)]
    high=max(r["partner_score"] for r in responses)
    selfish=[r for r in responses if r["partner_score"]==high]
    return min(selfish,key=lambda r:r["player_score"]),max(selfish,key=lambda r:r["player_score"]),high

def naive(seq,theta):
    for i,p in enumerate(seq):
        if p!="O" and p!=theta:return resolve(seq,theta,i)
    return resolve(seq,theta,None)

def enumerate_plans():
    vals={}
    for tup in product(RULES,repeat=3):
        seq="".join(tup)
        naive_value=sum(naive(seq,k)["player_score"] for k in "AB")/2
        hostile=sum(strategic(seq,k)[0]["player_score"] for k in "AB")/2
        friendly=sum(strategic(seq,k)[1]["player_score"] for k in "AB")/2
        vals[seq]={"naive":naive_value,"strategic_hostile":hostile,
                   "strategic_friendly":friendly}
    return vals

def check():
    plans=enumerate_plans()
    assert len(plans)==27
    maxima={key:max(v[key] for v in plans.values()) for key in
            ("naive","strategic_hostile","strategic_friendly")}
    assert maxima=={"naive":F(13,8),
                    "strategic_hostile":F(3,2),
                    "strategic_friendly":F(17,8)}
    assert plans["OOO"]=={"naive":F(3,2),
                          "strategic_hostile":F(3,2),
                          "strategic_friendly":F(3,2)}
    assert plans["OAA"]=={"naive":F(13,8),
                          "strategic_hostile":F(3,4),
                          "strategic_friendly":F(13,8)}
    optimal_hostile=sorted(p for p,v in plans.items()
                         if v["strategic_hostile"]==maxima["strategic_hostile"])
    assert optimal_hostile==["OOO"]
    # Evidence of partner's *own* rational indifference:
    # theta=B obtains 2 personal successes for early or late veto.
    on_early=resolve("OAA","B",1)
    on_late=resolve("OAA","B",2)
    assert on_early["partner_score"]==on_late["partner_score"]==2
    assert on_early["player_score"]==F(3,2)
    assert on_late["player_score"]==F(-1,4)
    assert strategic("OAA","B")[0]["player_score"]==F(-1,4)
    return maxima,plans,{"early":on_early,"late":on_late},optimal_hostile

def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [enc(v) for v in x]
    return x

def main():
    m,plans,witness,hostile_winners=check()
    return enc({"schema":"ssnoir.r390-r397.strategic-veto-commitment.v1",
      "scope":"all 27 pre-announced public rule schedules; counterpart optimizes its three-turn objective with tie range; no adaptive player or full social agent claimed",
      "rule":"Player pre-announces three OPEN/ONLY-A/ONLY-B rules for demand A,B,A; changing previous enforced rule costs1/4. Counterpart independently has fixed private preferred action theta=A/B, one veto usable on a turn the rule excludes theta, restoring OPEN. Counterpart knows full announced schedule and maximizes own total matched actions. If several veto timings are own-optimal, report both worst and best player payoff instead of inventing a unique preference.",
      "R390_all_27_preannounced_schedules":len(plans),
      "R391_opponent_myopic_veto_best":m["naive"],
      "R392_self_interested_strategic_ties_hostile_best":m["strategic_hostile"],
      "R393_self_interested_strategic_ties_friendly_best":m["strategic_friendly"],
      "R394_indifference_witness_OAA_typeB":witness,
      "R395_hostile_robust_player_plan":hostile_winners,
      "R396_each_plan_and_tie_range":plans,
      "R397_verdict":"When an independently motivated counterpart strategically times its single veto knowing the whole announced plan, the supposed strategic effect depends strongly on how ties in counterpart utility are resolved. Hostile equilibrium player's best is OPEN,OPEN,OPEN (3/2); friendly best can be 17/8. The earlier R374 one-line inducement strategy is NOT robust to this different information/commitment protocol. No assertion of human-agent optimality or unique equilibrium is justified."
    })
if __name__=="__main__":print(json.dumps(main(),ensure_ascii=False,indent=2))
