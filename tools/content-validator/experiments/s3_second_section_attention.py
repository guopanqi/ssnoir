#!/usr/bin/env python3
"""S3-II: source-constrained concurrent objectives, Exact Fraction PAPER ONLY.

This is a CONDITIONAL slice after Eddie's bar-arrival incident has been
witnessed, not the world's initial state. Current official Eddie side-story
is disabled (side-story-open? #f), so this is NOT a playable official scene.

Frozen toy horizon: 2 days, 4 actions each, random gain 0/1/2 equally.
D dock witness progress2, O compulsory solo old street inquiry2 on day1
which *never itself yields key evidence*, L Nightingale accompanies next day
only if O completed (L progress2), and E optional Eddie response progress2
only after witnessed bar encounter and before that evening ends.
This progress2 for E is a mathematical cost PROXY, not Eddie's real encounter.
Two main leads D+L are combined to identify Ryan in this hypothetical horizon.
Actual dates/progress/rewards are NOT given by the chapter manuscript.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import permutations,product
import json
from pathlib import Path
import sys

DAY=4
N=8
OUTCOME=((0,F(1,3)),(1,F(1,3)),(2,F(1,3)))
ACTIONS=("D","O","L","E")
def study(eddie=True,solo_gate=True,eddie_expires=True,joint=True,
          eddie_reward=F(3,2),calculate_paths=True):
    def allowed(state):
        d,o,l,e,n=state
        if n==0:return ()
        opts=[]
        if d<2:opts.append("D")
        if n>DAY and o<2:opts.append("O")
        if n<=DAY and (o>=2 or not solo_gate) and l<2:opts.append("L")
        if eddie and e<2 and (n>DAY or not eddie_expires):opts.append("E")
        return tuple(opts)
    def advance(state,act):
        d,o,l,e,n=state
        for gain,p in OUTCOME:
            yield p,(min(2,d+gain) if act=="D" else d,
                min(2,o+gain) if act=="O" else o,
                min(2,l+gain) if act=="L" else l,
                min(2,e+gain) if act=="E" else e,n-1)
    def prize(state):
        d,o,l,e,n=state
        return (F(2)*F(d==2)+F(2)*F(l==2)
               +(F(2)*F(d==2 and l==2) if joint else F(0))
               +(eddie_reward*F(e==2) if eddie else F(0)))
    @lru_cache(None)
    def V(state):
        opts=allowed(state)
        if not opts:return prize(state)
        return max(sum(p*V(s) for p,s in advance(state,x)) for x in opts)
    def Q(s):return {x:sum(p*V(ss) for p,ss in advance(s,x)) for x in allowed(s)}
    ROOT=(0,0,0,0,N)
    optimal=V(ROOT)
    if not calculate_paths:
        return {"optimal":str(optimal)}
    def evaluate(policy):
        @lru_cache(None)
        def P(s):
            opts=allowed(s)
            if not opts:return prize(s)
            x=policy(s,opts)
            assert x in opts,(s,x,opts)
            return sum(p*P(ss) for p,ss in advance(s,x))
        return P(ROOT)
    all_orders=list(permutations(ACTIONS))
    fixed=sorted([(evaluate(lambda s,opts,order=order:next(x for x in order if x in opts)),order)
                 for order in all_orders],reverse=True)[0]
    per_day=sorted([(evaluate(lambda s,opts,first=a,second=b:
          next(x for x in (first if s[4]>DAY else second) if x in opts)),a,b)
          for a in permutations(("D","O","E"))
          for b in permutations(("D","L","E"))],reverse=True)[0]
    # Explicit public, legible cutoff rules; does not read V/Q.
    # The family may fail to match optimal -- such a failure is recorded,
    # not renamed "complexity".
    heuristics=[]
    for o_cutoff,e_cutoff,prioritize_started_e,finish_dock in product(
        range(5,9),range(5,9),(False,True),(False,True)):
        def choice(s,opts,oc=o_cutoff,ec=e_cutoff,pe=prioritize_started_e,fd=finish_dock):
            d,o,l,e,n=s
            if n>DAY:
                if "E" in opts and pe and e>0:return "E"
                if "O" in opts and (n>=oc or o>0):return "O"
                if "E" in opts and n<=ec:return "E"
                return next(x for x in ("D","O","E") if x in opts)
            if fd and d>0 and "D" in opts:return "D"
            return next(x for x in ("L","D","E") if x in opts)
        heuristics.append((evaluate(choice),(o_cutoff,e_cutoff,prioritize_started_e,finish_dock)))
    best_heuristic=max(heuristics)
    witnesses=[]
    expected_strict_vs_fixed=F(0)
    expected_visible_two=F(0)
    expected_strict_eddie=F(0)
    expected_strict_solo=F(0)
    expected_both_main=F(0)
    expected_eddie=F(0)
    normal_states={}
    # Gather distinct public states under ONE deterministic tie-broken optimal policy.
    def forward(s,mass):
        nonlocal expected_strict_vs_fixed,expected_visible_two,expected_strict_eddie,expected_strict_solo,expected_both_main,expected_eddie
        opts=allowed(s)
        if not opts:
            if s[0]==2 and s[2]==2: expected_both_main+=mass
            if s[3]==2:expected_eddie+=mass
            return
        normal_states[s]=normal_states.get(s,F(0))+mass
        q=Q(s)
        best=max(q.values())
        chosen=next(x for x in ("E","O","D","L") if x in q and q[x]==best)
        alternate=next(x for x in fixed[1] if x in opts)
        if len(opts)>1:expected_visible_two+=mass
        if chosen!=alternate and q[chosen]>q[alternate]:
            expected_strict_vs_fixed+=mass
            if len(witnesses)<8:
                witnesses.append({"state":list(s),"path_mass":str(mass),
                    "optimal":chosen,"fixed":alternate,"Q":{k:str(v) for k,v in q.items()}})
        if "E" in q and "O" in q and q["E"]>q["O"] and chosen=="E":expected_strict_eddie+=mass
        if "E" in q and "O" in q and q["O"]>q["E"] and chosen=="O":expected_strict_solo+=mass
        for p,ss in advance(s,chosen):
            forward(ss,mass*p)
    forward(ROOT,F(1))
    assert optimal>=fixed[0] and optimal>=per_day[0] and optimal>=best_heuristic[0]
    return {"optimal":str(optimal),"strong_global":str(fixed[0]),"fixed_order":list(fixed[1]),
        "gap_to_fixed":str(optimal-fixed[0]),
        "strong_day_specific":str(per_day[0]),"gap_to_day_specific":str(optimal-per_day[0]),
        "day_specific_priorities":[list(per_day[1]),list(per_day[2])],
        "best_public_rule":str(best_heuristic[0]),"gap_to_public":str(optimal-best_heuristic[0]),
        "rule_parameters":{"solo_cutoff":best_heuristic[1][0],"eddie_cutoff":best_heuristic[1][1],
           "continue_started_eddie":best_heuristic[1][2],"finish_dock_first_when_started":best_heuristic[1][3]},
        "normal_optimal_expected_visits_with_multiple_actions":str(expected_visible_two),
        "strict_departure_from_strong_fixed_expected_visits":str(expected_strict_vs_fixed),
        "strict_eddie_over_solo_expected_visits":str(expected_strict_eddie),
        "strict_solo_over_eddie_expected_visits":str(expected_strict_solo),
        "probability_joint_dock_and_help":str(expected_both_main),
        "probability_completed_eddie":str(expected_eddie),
        "normal_public_state_count":len(normal_states),
        "strict_witnesses":witnesses}

def main():
    core=study()
    off=study(eddie=False,calculate_paths=False)["optimal"]
    no_gate=study(solo_gate=False,calculate_paths=False)["optimal"]
    no_expire=study(eddie_expires=False,calculate_paths=False)["optimal"]
    no_joint=study(joint=False,calculate_paths=False)["optimal"]
    assert F(core["optimal"])>=F(off)
    assert F(no_gate)>=F(core["optimal"])
    assert F(no_expire)>=F(core["optimal"])
    assert F(no_joint)<=F(core["optimal"])
    # Sensitivity is a separate boundary, not part of the official chapter.
    low=study(eddie_reward=F(1))
    high=study(eddie_reward=F(2))
    results={"study":"S3-II section-two source-constrained concurrent decision relation",
        "scope":"Exact Fraction synthetic conditional scene after Eddie actually seen in bar; 2 days x 4 actions, NOT official probabilities or canonical timings",
        "rules":"Dock witness D=2; solo old-street attempt O=2 never yields direct clue, unlocks next-day help L=2; observed Eddie incident E=2 must be responded to that evening; optional reward and reunified D+L evidence.",
        "core":core,"ablations":{"disable_eddie":off,"remove_solo_first_gate":no_gate,
            "remove_observed_eddie_same_evening_deadline":no_expire,
            "remove_two_lead_convergence_reward":no_joint},
        "eddie_reward_boundaries":{"reward_one":low,"reward_two":high},
        "source_boundary":{"nightingale_next_day":True,"eddie_global_world_entry_currently_disabled":True,
          "eddie_unobserved_event_has_no_public_clock":True,
          "solo_exploration_never_directly_identifies_ryan":True},
        "evidence_identity":"PAPER math only; no native S3 execution, no human play"};
    payload=json.dumps(results,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(payload,encoding="utf8")
    else:sys.stdout.write(payload)
if __name__=="__main__":main()
