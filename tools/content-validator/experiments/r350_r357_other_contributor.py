#!/usr/bin/env python3
"""R350-R357: wait, observe autonomous progress, or contribute personally.

Shared target 3 in 3 turns. Independent counterpart private fixed rule:
AUTO contributes 1 every turn; RECIP contributes 1 iff PLAYER contributed
in the previous turn (never instant player control of current contribution).
Opponent's individual contributions are publicly observed after each turn.
Player invests 1 (cost 1) or waits (cost0). End +6 iff target reached.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
from collections import defaultdict
import json

T=3
TYPES=("AUTO","RECIP")
def other_units(theta,previous,reciprocity=True):
    return 1 if theta=="AUTO" else (int(previous==1) if reciprocity else 0)
def dynamic(target=3,cost=F(1),reciprocity=True):
    @lru_cache(None)
    def V(turn,progress,previous,possible):
        if turn==T:return F(6) if progress>=target else F(0)
        return max(Q(turn,progress,previous,possible,action) for action in (0,1))
    def Q(turn,progress,previous,possible,action):
        obs={}
        for theta in possible:
            units=other_units(theta,previous,reciprocity)
            obs.setdefault(units,[]).append(theta)
        return -cost*action+sum(
            F(len(states),len(possible))*V(turn+1,min(target,progress+action+units),
                                             action,tuple(states))
            for units,states in obs.items())
    root=V(0,0,0,TYPES)
    starts={a:Q(0,0,0,TYPES,a) for a in (0,1)}
    # Valid reachable optimal prefixes, with their *publicly inferable*
    # counterpart type subset; do not inspect private hidden type first.
    reachable={(0,0,TYPES):F(1)}
    witnesses=[]
    for t in range(T):
        nxt=defaultdict(F)
        for (progress,last,possible),mass in reachable.items():
            qs={a:Q(t,progress,last,possible,a) for a in (0,1)}
            best=max(qs.values())
            choices=[a for a in (0,1) if qs[a]==best]
            if len(choices)==1:
                witnesses.append({"turn":t,"progress":progress,
                                  "previous_player_contribution":last,
                                  "possible_counterpart_types":possible,
                                  "mass":mass,"Q":qs,"strict_choice":choices[0]})
            action=choices[0]
            groups={}
            for theta in possible:
                units=other_units(theta,last,reciprocity)
                groups.setdefault(units,[]).append(theta)
            for units,states in groups.items():
                nxt[(min(target,progress+action+units),action,tuple(states))]+=mass*F(len(states),len(possible))
        reachable=nxt
    assert sum(reachable.values())==1
    return root,starts,witnesses

def fixed(target=3,cost=F(1),reciprocity=True):
    vals={}
    for plan in product((0,1),repeat=T):
        total=F(0)
        for theta in TYPES:
            progress=0;prev=0;spent=F(0)
            for action in plan:
                progress+=action+other_units(theta,prev,reciprocity)
                spent+=cost*action
                prev=action
            total+=(F(6) if progress>=target else F(0))/2-spent/2
        vals["".join(map(str,plan))]=total
    best=max(vals.values())
    return best,sorted(k for k,v in vals.items() if v==best),vals

def readable(target=3,cost=F(1),reciprocity=True):
    # Plan: wait on first turn, then conditional on observed other
    # contribution: AUTO -> wait; RECIP -> contribute remaining 2 turns.
    # A real human has to discover this rule; it's a mathematical rival.
    total=F(0)
    for theta in TYPES:
        plan=(0,0,0) if theta=="AUTO" else (0,1,1)
        progress=0;last=0;spent=F(0)
        for act in plan:
            progress+=act+other_units(theta,last,reciprocity)
            last=act;spent+=cost*act
        total+=((F(6) if progress>=target else F(0))-spent)/2
    return total

def jsonify(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):jsonify(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [jsonify(v) for v in x]
    return x

def audit():
    main,starts,witnesses=dynamic()
    static,plans,allplans=fixed()
    simple=readable()
    assert (main,static,simple)==(5,4,5)
    assert starts=={0:F(5),1:F(9,2)}
    assert {w["strict_choice"] for w in witnesses}=={0,1}
    alone,_,_=dynamic(reciprocity=False)
    alonefixed,_,_=fixed(reciprocity=False)
    assert (alone,alonefixed)==(4,3)
    variants={}
    for need in (2,3,4,5):
        opt,_,_=dynamic(target=need)
        v,_,_=fixed(target=need)
        variants[str(need)]={"adaptive":opt,"fixed":v}
        assert opt>=v
    assert variants=={
      "2":{"adaptive":F(11,2),"fixed":F(5)},
      "3":{"adaptive":F(5),"fixed":F(4)},
      "4":{"adaptive":F(9,2),"fixed":F(4)},
      "5":{"adaptive":F(7,2),"fixed":F(3)}}
    return jsonify({
      "schema":"ssnoir.r350-r357.shared-progress.v1",
      "scope":"abstract partner policy model; not a self-driven NPC with independent authored agenda, nor a human playtest",
      "rules":"3 turns, shared target3, win6 if total player+counterpart units>=3, player contributes1 at personal cost1 or waits. Two private counterpart policies equally likely: AUTO adds1 every turn, RECIP adds1 only if player invested in preceding turn. Each counterpart action visible AFTER player decision and added to common progress; no current-turn controlled extra die.",
      "R350_main_partial_information":main,
      "R351_start_actions":starts,
      "R352_complete_eight_fixed_schedules":{"best":static,"examples":plans,"all":allplans},
      "R353_public_if_other_auto_wait_else_seed_reciprocity":simple,
      "R354_actual_reachable_strict_choices":witnesses,
      "R355_disable_lagged_reciprocity":{"adaptive":alone,"fixed":alonefixed},
      "R356_four_target_thresholds":variants,
      "R357_judgement":"Waiting to observe the other contributor's independent policy can have value (5 versus 4 precommitted), with both strict wait/invest choices on reachable paths. Yet one simple two-branch rule attains full optimum, and the agent is a fixed response policy not an authored independent life. This is a public-goods / complementarity + timing information composition, not a new certified gameplay mode."
    })
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
