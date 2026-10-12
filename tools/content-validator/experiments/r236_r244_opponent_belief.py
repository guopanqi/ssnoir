#!/usr/bin/env python3
"""R236-R244: belief-safe adaptive opponent, explicit probes and simple policy.

The opponent currently blocks side (last_player_choice XOR hidden_type).
Type transitions only AFTER player action and the block result is observed.
A/B attacks reveal hidden type from hit/miss, as does cost-only Probe.
No policy can read the current hidden type before acting. Exact Fraction.
"""
from fractions import Fraction as F
from itertools import product
from collections import defaultdict
from functools import lru_cache
import json

T=4
def qdata(p_hit,p_miss,probe=True):
    actions=(0,1,"P") if probe else (0,1)
    @lru_cache(None)
    def value(t,last,prob0):
        if t==T:return F(0)
        return max(Q(t,last,prob0,a) for a in actions)
    def Q(t,last,prob0,action):
        total=F(0)
        for theta,m in ((0,prob0),(1,1-prob0)):
            if not m:continue
            blocked=last ^ theta
            hit=int(action!="P" and action!=blocked)
            newlast=last if action=="P" else action
            flip=p_hit if hit else p_miss
            nextp=(1-flip) if theta==0 else flip
            total+=m*(hit+value(t+1,newlast,nextp))
        return total
    root=value(0,0,F(1,2))
    qroot={str(a):Q(0,0,F(1,2),a) for a in actions}
    # Follow a fixed, publicly deterministic tie-break optimal policy.
    dist={(0,F(1,2)):F(1)}
    witness=[]
    for t in range(T):
        nd=defaultdict(F)
        for (last,prob0),mass in dist.items():
            qs={str(a):Q(t,last,prob0,a) for a in actions}
            best=max(qs.values())
            winners=[a for a in actions if qs[str(a)]==best]
            if len(winners)==1:
                witness.append({"turn":t,"last":last,"belief_type0":prob0,
                                "probability":mass,"values":qs,"strict_best":str(winners[0])})
            action=winners[0]
            for theta,pm in ((0,prob0),(1,1-prob0)):
                if not pm:continue
                blocked=last ^ theta
                hit=int(action!="P" and action!=blocked)
                newlast=last if action=="P" else action
                flip=p_hit if hit else p_miss
                nextp=1-flip if theta==0 else flip
                nd[(newlast,nextp)]+=mass*pm
        dist=nd
    assert sum(dist.values())==1
    return root,qroot,witness

def simulate_public_rule(p_hit,p_miss,rule="one_step"):
    # An INDEPENDENT forward simulator for a human-readable policy.
    # State stores true hidden theta but decision function only accesses
    # previous publicly inferred theta and previous win/loss.
    # True theta is used ONLY after decision to generate the reward/next state.
    mass={(0,theta,None,None):F(1,2) for theta in (0,1)}
    total=F(0)
    for t in range(T):
        new=defaultdict(F)
        for (last,theta,observed_theta,prev_hit),m in mass.items():
            if rule=="always_zero":action=0
            elif rule=="alternate":action=t%2
            elif rule=="one_step":
                if observed_theta is None:action=0
                else:
                    flip=p_hit if prev_hit else p_miss
                    predicted=observed_theta if flip<=F(1,2) else 1-observed_theta
                    action=1-(last ^ predicted)
            else:raise ValueError(rule)
            blocked=last ^ theta
            hit=int(action!=blocked)
            total+=m*hit
            flip=p_hit if hit else p_miss
            for changed,pr in ((0,1-flip),(1,flip)):
                if pr:new[(action,theta ^ changed,theta,hit)]+=m*pr
        mass=new
    assert sum(mass.values())==1
    return total

def fixed_all(p_hit,p_miss):
    results={}
    for plan in product((0,1),repeat=T):
        # Fixed plan cannot depend on observations, but state simulation
        # still updates the opponent after every real hit/failure.
        mass={(0,theta):F(1,2) for theta in (0,1)}
        value=F(0)
        for action in plan:
            after=defaultdict(F)
            for (last,theta),m in mass.items():
                blocked=last ^ theta
                hit=int(action!=blocked)
                value+=m*hit
                flip=p_hit if hit else p_miss
                after[(action,theta)]+=m*(1-flip)
                after[(action,theta ^ 1)]+=m*flip
            mass=after
        results["".join(str(x) for x in plan)]=value
    return max(results.values()),sorted(k for k,v in results.items() if v==max(results.values()))[:6]

def serialize(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):serialize(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [serialize(z) for z in x]
    return x

def audit():
    variants={
      "never_switch":(F(0),F(0)),
      "random_independent_switch":(F(1,4),F(1,4)),
      "respond_to_success":(F(3,4),F(1,4)),
      "memory_erased":(F(1,2),F(1,2))
    }
    out={}
    for name,(p_hit,p_miss) in variants.items():
        root,qroot,wit=qdata(p_hit,p_miss)
        without_probe,_,_=qdata(p_hit,p_miss,False)
        fixed,plans=fixed_all(p_hit,p_miss)
        simple=simulate_public_rule(p_hit,p_miss)
        assert root==without_probe
        assert root==simple, (name,root,simple)
        assert root>=fixed
        assert qroot["P"]<root, (name,qroot,root)
        out[name]={
            "after_hit_switch":p_hit,"after_miss_switch":p_miss,
            "full_belief_optimum":root,"forbid_probe":without_probe,
            "best_fixed_A_B":fixed,"fixed_examples":plans,
            "simple_one_step_rule":simple,"probe_root_Q":qroot["P"],
            "normal_strict_witnesses":wit
        }
    assert out["never_switch"]["full_belief_optimum"]==F(7,2)
    assert out["random_independent_switch"]["full_belief_optimum"]==F(11,4)
    assert out["respond_to_success"]["full_belief_optimum"]==F(11,4)
    assert out["memory_erased"]["full_belief_optimum"]==F(2)
    assert out["memory_erased"]["best_fixed_A_B"]==2
    assert out["never_switch"]["best_fixed_A_B"]==2
    return serialize({
      "schema":"ssnoir.abstract.r236-r244.adaptive-opposition.v1",
      "scope":"exact paper; zero Unity/ human evidence; hidden type never visible before current action",
      "rules":{"T":T,"initial_last":"A","hidden_type":"track last / invert last, prior 1/2",
               "current_guard":"last player action xor current hidden type",
               "attack":"A or B; success +1 if not blocked; hit/miss reveals current guard",
               "probe":"spend one action to reveal current guard but +0; cannot attack same turn",
               "next_type":"may flip after public hit or miss, probability depends on outcome"},
      "R236_stable_hidden_rule":out["never_switch"],
      "R237_random_switch":out["random_independent_switch"],
      "R238_react_to_player_success":out["respond_to_success"],
      "R239_opponent_memory_erased":out["memory_erased"],
      "R240_probe_dominated":"Probe never raises optimal value; the attack outcome already identifies current hidden type.",
      "R241_independent_forward_readable_policy":"First attack A; next infer previous guard from last hit/miss, predict the likelier current hidden type, attack the unguarded side. Matches full belief DP in all variants.",
      "R242_fixed_unresponsive_plans":{k:v["best_fixed_A_B"] for k,v in out.items()},
      "R243_normal_reachable_Q":{k:v["normal_strict_witnesses"] for k,v in out.items()},
      "R244_novelty_boundary":"Even reactive hidden switch after hit/miss is one-step tracking / P16+P03, not a second persistent infer-or-bluff activity. Stop this minimal family; don't add elaborate enemies to save it."
    })
if __name__=="__main__":print(json.dumps(audit(),indent=2,ensure_ascii=False))
