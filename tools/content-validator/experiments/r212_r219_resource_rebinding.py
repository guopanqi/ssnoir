#!/usr/bin/env python3
"""R212–R219: rebinding the LEGAL target of future resource types.

A type-q resource (quality 1 or 2) is visible before use.  U consumes it
on its currently bound goal. E spends this resource to flip its type's
future binding. Exact Fraction DP plus independent enumeration of all
input tapes and fixed/quality-dependent action plans.
"""
import json
from collections import defaultdict
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

T=6; NEED=(3,4); START=(0,0,0,1,0)

def move(s,quality,action,budget):
    a,b,map1,map2,changed=s
    bound=map1 if quality==1 else map2
    if action=="U":
        if bound==0:a=min(NEED[0],a+quality)
        else:b=min(NEED[1],b+quality)
    elif action=="E" and changed<budget:
        changed+=1
        if quality==1:map1=1-map1
        else:map2=1-map2
    else:
        return None
    return a,b,map1,map2,changed

def score(s):
    return F(4*int(s[0]>=NEED[0])+4*int(s[1]>=NEED[1]))

def solve(budget=T):
    @lru_cache(None)
    def value(turn,state):
        if turn==T:return score(state)
        return sum(max(value(turn+1,next_s) for action in "UE"
                    if (next_s:=move(state,q,action,budget)) is not None)/2
                   for q in (1,2))
    def qval(turn,state,q,action):
        ns=move(state,q,action,budget)
        if ns is None:return None
        return value(turn+1,ns)
    reaches={START:F(1)}
    strict=[]
    for turn in range(T):
        after=defaultdict(F)
        for state,mass in reaches.items():
            for quality in (1,2):
                vals={a:qval(turn,state,quality,a) for a in "UE"
                      if qval(turn,state,quality,a) is not None}
                best=max(vals.values())
                winners=[a for a in "UE" if a in vals and vals[a]==best]
                if len(winners)==1:
                    strict.append({"turn":turn,"state":state,"observed_quality":quality,
                        "forward_mass":mass/2,"Q":vals,"best":winners[0]})
                new=move(state,quality,winners[0],budget)
                after[new]+=mass/2
        reaches=after
    assert sum(reaches.values())==1
    return value(0,START),strict

def plan_value(strategy,budget):
    total=F(0)
    for inputs in product((1,2),repeat=T):
        state=START
        for turn,quality in enumerate(inputs):
            action=strategy(turn,state,quality)
            new=move(state,quality,action,budget)
            state=new if new is not None else move(state,quality,"U",budget)
        total+=score(state)/2**T
    return total

def known_type_only():
    # Allow a planned rule to depend on today's visible quality and day
    # but NOT the goal progress, stored binding or previous inputs.
    by_policy=defaultdict(list)
    for policy in product("UE",repeat=2*T):
        value=plan_value(lambda t,s,q,policy=policy:policy[2*t+q-1],T)
        by_policy[value].append("".join(policy))
    top=max(by_policy)
    return {"best":top,"examples":sorted(by_policy[top])[:3],
            "tested_condition_plans":4**T}

def fixed_plans():
    results={}
    for policy in product("UE",repeat=T):
        key="".join(policy)
        results[key]=plan_value(lambda t,s,q,policy=policy:policy[t],T)
    best=max(results.values())
    return {"best":best,"examples":sorted(p for p,v in results.items() if v==best)[:6],
            "tested_schedules":2**T}

def readable():
    # Treat resource type as a legal-right that can be remapped once its
    # currently assigned target is already completed.
    return plan_value(lambda t,s,q:
        "E" if s[(s[2] if q==1 else s[3])]==NEED[(s[2] if q==1 else s[3])] else "U",T)

def freely_reallocate():
    # Remove legal binding altogether: each incoming q is freely assignable
    # on the spot to whichever of the two goals has best marginal value.
    @lru_cache(None)
    def solve_free(t,a,b):
        if t==T:return F(4*int(a==NEED[0])+4*int(b==NEED[1]))
        return sum(max(solve_free(t+1,min(NEED[0],a+q),b),
                       solve_free(t+1,a,min(NEED[1],b+q)))/2 for q in (1,2))
    return solve_free(0,0,0)

def j(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):j(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [j(v) for v in x]
    return x

def audit():
    normal,witnesses=solve(T)
    disabled,_=solve(0)
    single,_=solve(1)
    fixed=fixed_plans()
    conditional=known_type_only()
    simple=readable()
    unrestricted=freely_reallocate()
    assert normal==F(121,16)
    assert disabled==F(99,16)
    assert single==normal
    assert fixed["best"]==disabled
    assert conditional["best"]==F(25,4)
    assert simple==normal
    assert unrestricted==F(127,16)
    assert {x["best"] for x in witnesses}=={"U","E"}
    return j({
      "schema":"ssnoir.r212-r219.resource-rebinding.v1",
      "scope":"abstract exact paper only; no official SSNoir actor/resources",
      "rules":{"T":T,"goals":NEED,"initial_resource_type_bindings":"quality1→A, quality2→B",
               "U":"use observed resource on its currently bound goal",
               "E":"forfeit observed resource and flip that resource type's permanent binding",
               "quality":"1 or2 equiprobable, visibly revealed before U/E",
               "score":"4 per goal completed"},
      "R212_full_feedback_best":normal,
      "R213_forbid_rebinding":disabled,
      "R214_at_most_one_rebinding":single,
      "R215_strong_preplanned_schedules":fixed,
      "R216_best_quality_by_turn_without_state":conditional,
      "R217_normal_strict_paths":witnesses,
      "R218_simple_if_bound_goal_full_rule":simple,
      "R219_abolish_binding_and_choose_target_freely":unrestricted,
      "assessment":"Rebinding has a large mechanical effect, but ONE edit and a simple complete-goal rule achieve full value. P04 specialization + P13 conversion, not an independent repeated decision pattern."
    })
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
