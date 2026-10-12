#!/usr/bin/env python3
"""R334-R341: moving before information vs observing then reaching action.
Three periods, center and two wings; private moving target theta in {L,R}.
A center SCOUT reveals theta0 but consumes t0; go to a wing at t0
reveals nothing and allows early action at t1. Intervene only at wing.
Moving center->wing costs t1, leaving t2 for later action. No free
teleport or hidden theta access before real observation.
"""
from fractions import Fraction as F
from itertools import product
import json

WINGS=("L","R")
def opposite(w):return "R" if w=="L" else "L"
def hidden_paths(p):
    for theta0 in WINGS:
        for flip1,flip2 in product((False,True),repeat=2):
            theta1=opposite(theta0) if flip1 else theta0
            theta2=opposite(theta1) if flip2 else theta1
            mass=F(1,2)*(p if flip1 else 1-p)*(p if flip2 else 1-p)
            yield (theta0,theta1,theta2),mass

def play(p,initial,policy=None,teleport=False,scout_information=True):
    """Independent evaluation using the actual three hidden target paths.

    SCOUT's t1 wing selection may depend only on observed theta0.
    A wing pre-position does not observe theta0. First attempt at t1
    publicly reveals its success/failure, only after the action.
    """
    total=F(0)
    for (theta0,theta1,theta2),mass in hidden_paths(p):
        if not mass:continue
        if initial=="S":
            selected=policy[theta0] if scout_information else policy["?"]
            value=3*int(selected==theta2)
        elif initial in WINGS:
            if initial==theta1:value=4
            else:
                # After failed early action theta1 must be the other wing;
                # in the free-relocation ablation choose the higher-probability
                # of target moving or staying before final action.
                selected=initial
                if teleport and p<F(1,2):selected=opposite(initial)
                value=3*int(selected==theta2)
        else:raise ValueError(initial)
        total+=mass*value
    return total

def all_policies(p,teleport=False):
    adaptive_scout={}
    blind_scout={}
    for x,y in product(WINGS,repeat=2):
        policy={"L":x,"R":y}
        adaptive_scout[x+y]=play(p,"S",policy,teleport)
    for choice in WINGS:
        blind_scout[choice]=play(p,"S",{"?":choice},teleport,scout_information=False)
    direct={wing:play(p,wing,teleport=teleport) for wing in WINGS}
    full=max([*adaptive_scout.values(),*direct.values()])
    precommitted=max([*blind_scout.values(),*direct.values()])
    return {"best":full,"best_open_loop":precommitted,
            "all_scout_response_rules":adaptive_scout,
            "fixed_scout_routes":blind_scout,
            "direct_wing_strategies":direct,
            "optimal_initial_classes":[key for key,v in
              {"S":max(adaptive_scout.values()),**direct}.items() if v==full]}

def symbolic(p):
    scout=3*((1-p)**2+p**2)
    wing=2+F(3,2)*p
    teleport_wing=2+F(3,2)*max(p,1-p)
    return {"scout_follow_observation":scout,
            "preposition_and_retry_same_wing":wing,
            "teleport_after_failed_attempt":teleport_wing}

def serialize(v):
    if isinstance(v,F):return str(v)
    if isinstance(v,dict):return {str(k):serialize(x) for k,x in v.items()}
    if isinstance(v,(tuple,list)):return [serialize(x) for x in v]
    return v

def audit():
    out={}
    for p in (F(0),F(1,10),F(1,5),F(1,4),F(1,2),F(1)):
        data=all_policies(p)
        free=all_policies(p,teleport=True)
        formula=symbolic(p)
        assert data["best"]==max(formula["scout_follow_observation"],
                                 formula["preposition_and_retry_same_wing"])
        assert free["best"]==max(formula["scout_follow_observation"],
                                 formula["teleport_after_failed_attempt"])
        assert free["best"]>=data["best"]>=data["best_open_loop"]
        assert len(data["all_scout_response_rules"])==4
        assert len(data["fixed_scout_routes"])==2
        out[str(p)]={**data,"teleport_after_failure":free["best"],
                     "no_scout_information":data["best_open_loop"],"independent_formula":formula}
    assert out["0"]["best"]==3 and out["0"]["best_open_loop"]==2
    assert out["1/10"]["best"]==F(123,50)
    assert out["1/10"]["best_open_loop"]==F(43,20)
    assert out["1/5"]["best"]==F(23,10)
    assert out["1/4"]["best"]==F(19,8)
    assert out["1/4"]["teleport_after_failure"]==F(25,8)
    assert out["0"]["teleport_after_failure"]==F(7,2)
    assert out["1/2"]["best"]==F(11,4)
    assert out["1"]["best"]==F(7,2)
    return serialize({
      "schema":"ssnoir.r334-r341.position-opportunity.v1",
      "scope":"exact three-period paper; not a real rendered map or player test",
      "contract":"Center C or wing L/R, no wing-to-wing move within remaining time. t0 choose SCOUT at C (reveals theta0), OR travel to wing L/R (no observation). Target independently flips wing with probability p between periods. SCOUT can respond by choosing one wing at t1 and ACT at t2 for +3 if target there; wing strategy ACT at t1 for +4 if target there, otherwise observed failure and ACT same wing at t2 for +3 if target flipped. Teleport ablation allows late final act at either wing after failure.",
      "R334_stable_target":out["0"],
      "R335_slight_movement":out["1/10"],
      "R336_switch_to_early_action":out["1/5"],
      "R337_nonlocal_quarter":out["1/4"],
      "R338_random_target":out["1/2"],
      "R339_certain_flip":out["1"],
      "R340_information_and_mobility_ablations":{k:{"full":v["best"],"without_actionable_scout":v["no_scout_information"],"free_final_reposition":v["teleport_after_failure"]} for k,v in out.items()},
      "R341_judgement":"Information from SCOUT matters only while there is enough physical time to reposition AND target persistence makes the observation useful. At p=0 scout+react beats fixed pre-position (3 vs2), while teleport-after-failure flips the preference (3.5). At p=1/4 direct preposition (19/8) beats scout. A simple public threshold explains the studied cases, so this is spatial mobility + observation-timing, not a newly certified mode."
    })
if __name__=="__main__":print(json.dumps(audit(),indent=2,ensure_ascii=False))
