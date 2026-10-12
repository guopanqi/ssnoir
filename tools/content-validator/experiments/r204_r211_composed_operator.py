#!/usr/bin/env python3
"""R204–R211: constructing an operator from two independently editable slots.

Abstract only. Single repeated operation processes unobserved next quality;
a precision/filter slot and a router slot determine the result. Exact
Fraction Bellman and independent fixed-sequence forward distribution.
"""
import json
from collections import defaultdict
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

T=6
GOAL=(5,5)
INITIAL=(0,0,0,0) # A,B,precision-mode (0 broad, 1 selective),router(0 A,1 B)

def transition(state, action, quality, goal=GOAL, transform="precision"):
    a,b,p,r=state
    if action=="P":p=1-p
    elif action=="R":r=1-r
    elif action!="K":raise ValueError(action)
    # Quality is NOT known when an edit is chosen.
    if transform=="precision":out=quality if p==0 else (3 if quality==2 else 0)
    elif transform=="additive":out=quality+int(p==1 and quality==2)
    elif transform=="independent":out=quality # precision has no real effect
    else:raise ValueError(transform)
    if r==0:a=min(goal[0],a+out)
    else:b=min(goal[1],b+out)
    return (a,b,p,r)

def terminal(state,goal=GOAL):
    return F(4*int(state[0]>=goal[0])+4*int(state[1]>=goal[1]))

def solve(actions="KPR",goal=GOAL,transform="precision"):
    @lru_cache(None)
    def v(t,state):
        if t==T:return terminal(state,goal)
        return max(q(t,state,act) for act in actions)
    def q(t,state,action):
        return sum(v(t+1,transition(state,action,quality,goal,transform))*F(1,2)
                   for quality in (1,2))
    # Independent exhaustive open-loop enumerator does NOT use Bellman values.
    def evaluate_plan(plan):
        masses={INITIAL:F(1)}
        for act in plan:
            new=defaultdict(F)
            for state,mass in masses.items():
                for quality in (1,2):
                    new[transition(state,act,quality,goal,transform)]+=mass/2
            masses=new
        assert sum(masses.values())==1
        return sum(mass*terminal(state,goal) for state,mass in masses.items())
    fixed=defaultdict(list)
    for seq in product(actions,repeat=T):
        fixed[evaluate_plan(seq)].append("".join(seq))
    best_fixed=max(fixed)
    masses={INITIAL:F(1)}
    witnesses=[]
    for t in range(T):
        nextm=defaultdict(F)
        for state,mass in masses.items():
            vs={a:q(t,state,a) for a in actions}
            winning=max(vs.values())
            bests=[a for a,x in vs.items() if x==winning]
            if len(bests)==1:
                witnesses.append({"turn":t,"state":list(state),"mass":mass,
                                  "Q":vs,"strict_best":bests[0]})
            # Public, deterministic tie-break. These are REAL reachable prefixes.
            action=bests[0]
            for quality in (1,2):
                nextm[transition(state,action,quality,goal,transform)]+=mass/2
        masses=nextm
    assert sum(masses.values())==1
    return {"optimal":v(0,INITIAL),"best_fixed":best_fixed,
            "open_loop_gap":v(0,INITIAL)-best_fixed,
            "best_fixed_schedules":sorted(fixed[best_fixed])[:8],
            "strict_reachable_witnesses":witnesses,
            "optimal_terminal_mass":sum(masses.values())}

def public_heuristics():
    def play(threshold,release):
        masses={INITIAL:F(1)}
        for t in range(T):
            nextm=defaultdict(F)
            for state,mass in masses.items():
                a,b,p,r=state
                remaining=GOAL[r]-(a,b)[r]
                # Three readable conditions, not a search-policy table.
                if remaining<=0:action="R"
                elif p==1 and remaining<=release:action="P"
                elif p==0 and remaining>=threshold:action="P"
                else:action="K"
                for quality in (1,2):
                    nextm[transition(state,action,quality)]+=mass/2
            masses=nextm
        return sum(mass*terminal(s) for s,mass in masses.items())
    candidates={(enter,leave):play(enter,leave)
                for enter in range(1,6) for leave in range(5)}
    winner=max(candidates.values())
    return {"best_value":winner,"best_conditions":
            [{"enter_selective_if_remaining_at_least":h,
              "return_broad_if_remaining_at_most":l}
             for (h,l),val in candidates.items() if val==winner],
            "heuristic_gap":solve()["optimal"]-winner}

def encode(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):encode(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [encode(y) for y in x]
    return x

def audit():
    full=solve()
    router=solve(actions="KR")
    filter_only=solve(actions="KP")
    remove_filter_effect=solve(transform="independent")
    additive=solve(transform="additive")
    heuristics=public_heuristics()
    assert full["optimal"]==F(95,16)
    assert full["best_fixed"]==F(19,4)
    assert full["open_loop_gap"]==F(19,16)
    assert router["optimal"]==F(5)
    assert filter_only["optimal"]==F(4)
    assert remove_filter_effect["optimal"]==router["optimal"]
    assert additive["optimal"]==F(115,16)
    assert additive["best_fixed"]==F(7)
    assert heuristics["best_value"]==F(23,4)
    assert heuristics["heuristic_gap"]==F(3,16)
    actions={w["strict_best"] for w in full["strict_reachable_witnesses"]}
    assert actions=={"K","P","R"}
    # Disallow credit for a knob that does not change the mapping.
    assert full["optimal"]>router["optimal"]>filter_only["optimal"]
    return encode({
      "schema":"ssnoir.r204-r211.composed-operator.v1",
      "evidence":"exact six-stage quality feedback DP; independent 3^6 open-loop schedules",
      "rules":{"turns":T,"initial_state":INITIAL,
               "each_quality":"after editing, unobserved before action; low/high equiprobable",
               "edit_P":"toggle broad (input q stays q) vs selective (low -> 0, high -> 3)",
               "edit_R":"toggle output A/B; first route A",
               "keep_K":"process same configured filter+route",
               "goals":GOAL,"reward":"4 per completed goal"},
      "R204_constructed_filter_and_router":full,
      "R205_independent_fixed_schedule":full["best_fixed"],
      "R206_router_edit_only":router,
      "R207_filter_edit_only":filter_only,
      "R208_normal_public_optimal_path":full["strict_reachable_witnesses"],
      "R209_best_readable_threshold_family":heuristics,
      "R210_knob_ablation":{
         "filter_does_nothing":remove_filter_effect["optimal"],
         "filter_is_nonrejecting_bonus":additive},
      "R211_novelty_review":"Non-trivial tradeoff between shaping input and routing output; stronger than predetermined plans, but simple threshold family loses only 3/16. This is an ORIGINAL COMPOSITION CANDIDATE (unvalidated), not a certified new mode or existing engine behavior."
    })
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
