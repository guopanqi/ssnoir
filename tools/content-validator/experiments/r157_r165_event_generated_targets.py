#!/usr/bin/env python3
"""R157-R165: NPC autonomous event alters what targets exist, not score only.

A four-action exact state graph; does not purport to be SSNoir's current
native Content, nor a human playtest. Independent enumerators cross-check.
"""
from collections import defaultdict
from functools import lru_cache
from itertools import product
import json

ACTIONS=("wait","case","contact","witness","march","generic")
N=4
# S = (case_progress, early_contact, witness_progress, march_progress,
#      generic_progress, revealed_branch). Event happens automatically
# immediately after its chosen turn. "social" contact only possible t0,t1.
INITIAL=(0,0,0,0,0,"")

def move(s,act,t,mode="responsive",event_after=1,weak_witness=False):
    c,f,w,m,g,b=s
    if act=="wait":pass
    elif act=="case" and c<3:c+=1
    elif act=="contact" and t<=1 and not f:f=1
    elif act=="witness" and b in ("witness","both") and t>event_after:
        if w<(1 if weak_witness else 2):w+=1
        else:return None
    elif act=="march" and b in ("march","both") and t>event_after and not m:m=1
    elif act=="generic" and b=="generic" and t>event_after and g<2:g+=1
    else:return None
    if t==event_after:
        b={"responsive":"witness" if f else "march",
           "none":"none",
           "forced_march":"march",
           "all_open":"both",
           "generic":"generic"}[mode]
    return (c,f,w,m,g,b)

def completed(s,weak_witness=False):
    c,f,w,m,g,b=s
    return (int(c==3),int(w==(1 if weak_witness else 2)),int(m==1),int(g==2))

def brute_paths(mode="responsive",event_after=1,weak_witness=False):
    outcomes=defaultdict(list)
    observed_event_prefixes=set()
    successes=[]
    for plan in product(ACTIONS,repeat=N):
        s=INITIAL
        for t,act in enumerate(plan):
            s=move(s,act,t,mode,event_after,weak_witness)
            if s is None:break
            if t==event_after:observed_event_prefixes.add((plan[:t+1],s[-1]))
        if s is None:continue
        result=completed(s,weak_witness)
        outcomes[result].append(" / ".join(plan))
        successes.append((plan,result))
    events=defaultdict(int)
    for prefix,branch in observed_event_prefixes:events[branch]+=1
    return outcomes,dict(events),successes

def independent_dynamic_reachability(mode="responsive",event_after=1,weak_witness=False):
    @lru_cache(None)
    def solve(t,state):
        if t==N:return frozenset((completed(state,weak_witness),))
        union=set()
        for act in ACTIONS:
            new=move(state,act,t,mode,event_after,weak_witness)
            if new is not None:union.update(solve(t+1,new))
        return frozenset(union)
    return solve(0,INITIAL)

def pareto(results):
    return sorted(v for v in results if not any(
        w!=v and all(x>=y for x,y in zip(w,v)) for w in results))

def weighted_opt(results,weights):
    scores={v:sum(x*w for x,w in zip(v,weights)) for v in results}
    best=max(scores.values())
    return best,sorted(x for x,v in scores.items() if v==best)

def intermediate_turn2(mode="responsive",event_after=1):
    s0=move(INITIAL,"case",0,mode,event_after)
    contact=move(s0,"contact",1,mode,event_after)
    neglect=move(s0,"case",1,mode,event_after)
    return {"after_contact":contact,"after_neglect":neglect}

def audit():
    variants={
        "responsive":dict(mode="responsive"),
        "none":dict(mode="none"),
        "forced_march":dict(mode="forced_march"),
        "all_open":dict(mode="all_open"),
        "generic":dict(mode="generic"),
        "late_event":dict(mode="responsive",event_after=2),
        "weak_witness":dict(mode="responsive",weak_witness=True),
    }
    report={}
    for name,kw in variants.items():
        observed,events,plans=brute_paths(**kw)
        independent=independent_dynamic_reachability(**kw)
        assert set(observed)==set(independent),(name,observed,independent)
        report[name]=dict(outcomes=len(observed),paths=sum(map(len,observed.values())),
                          at_event=events,pareto=pareto(observed),
                          work_examples={str(k):v[0] for k,v in observed.items()},
                          reachable_goals=sorted([i for i in range(4)
                                    if any(v[i] for v in observed)]))
    resp=report["responsive"]
    assert resp["outcomes"]==5
    assert resp["paths"]==67
    assert resp["pareto"]==[(0,1,0,0),(1,0,1,0)]
    assert report["none"]["pareto"]==[(1,0,0,0)]
    assert report["forced_march"]["pareto"]==[(1,0,1,0)]
    assert report["all_open"]["pareto"]==resp["pareto"]
    assert report["late_event"]["reachable_goals"]==[0,2]
    assert report["weak_witness"]["reachable_goals"]==[0,1,2]
    lead=intermediate_turn2()
    assert lead["after_contact"][-1]=="witness"
    assert lead["after_neglect"][-1]=="march"
    assert resp["at_event"]=={"witness":4,"march":4}
    assert set(report["generic"]["reachable_goals"])=={0,3}
    # Distinguish an actual structural choice from merely renamed branches.
    # Witness progress W2 and march M1 have different required action budgets.
    assert any(v==(0,1,0,0) for v in resp["pareto"])
    assert any(v==(1,0,1,0) for v in resp["pareto"])
    values={}
    for weights in ((3,4,2,0),(5,2,2,0),(3,3,3,0)):
        best,winners=weighted_opt(independent_dynamic_reachability(),weights)
        values["-".join(map(str,weights))]=dict(value=best,ends=winners)
    assert values["3-4-2-0"]["ends"]==[(1,0,1,0)]
    assert values["5-2-2-0"]["ends"]==[(1,0,1,0)]
    assert values["3-3-3-0"]["ends"]==[(1,0,1,0)]
    # Witness can become optimal if its story value is greater than case+march.
    witness_favored=weighted_opt(independent_dynamic_reachability(),(3,6,2,0))
    assert witness_favored==(6,[(0,1,0,0)])
    return {
        "schema":"ssnoir.r157-r165.target-topology.v1",
        "scope":"finite exact paper scenario; no native Content, no human data",
        "rules":{
          "player_actions":N,
          "early_window":"contact t0-t1, event automatically after t1",
          "case":"requires 3 player steps, persists",
          "early_contact":"consumes one player step; causes later witness 2-step story to exist",
          "early_no_contact":"NPC marches; later 1-step public action exists instead",
          "event_time":"after t1; subsequent goals are not retrospectively actionable",
          "outputs":"4D completed(case,witness,march,generic); do not collapse to a single plot-quality score"
        },
        "variants":report,
        "R157_R158_early_behavior_reversal":lead,
        "R159_independent_graph_enumerator":"all 6^4 action strings filtered by legality; compare memoized set-DP exact reachability",
        "R160_narrative_pareto":resp["pareto"],
        "R161_event_missing":report["none"]["pareto"],
        "R162_forced_agenda":report["forced_march"]["pareto"],
        "R163_everything_open":report["all_open"]["pareto"],
        "R164_late_event":report["late_event"]["pareto"],
        "R165_renamed_vs_changed":dict(generic=report["generic"]["pareto"],
                                       weak_witness=report["weak_witness"]["pareto"]),
        "taste_sensitivity":dict(sample_weights=values,witness_favored=witness_favored),
        "conclusion":"Branch-specific new activities exist but remain an ordinary authored goal graph until players demonstrate different attention/character experience."
    }
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
