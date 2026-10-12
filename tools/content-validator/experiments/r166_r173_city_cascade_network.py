#!/usr/bin/env python3
"""R166-R173: a second city actor responds to the *outcome* of the first.

Six player actions; two automatic and independent city event triggers;
exact 7^6 plan enumeration and independent set-valued graph DP.
Paper research only, no SSNoir native scene modification.
"""
from collections import defaultdict
from functools import lru_cache
from itertools import product
import json

ACTIONS=("wait","case","contact","witness","march","followup","intercept","rumor")
INITIAL=(0,0,0,0,0,0,0,"","")
N=6

def move(s,action,t,second="responsive",second_after=3):
    c,f,w,m,n,x,r,first,after=s
    if action=="wait":pass
    elif action=="case" and c<4:c+=1
    elif action=="contact" and t<=1 and not f:f=1
    elif action=="witness" and t>1 and first=="witness" and w<2:w+=1
    elif action=="march" and t>1 and first=="march" and not m:m=1
    elif action=="followup" and t>second_after and after=="followup" and n<2:n+=1
    elif action=="intercept" and t>second_after and after=="intercept" and not x:x=1
    elif action=="rumor" and t>second_after and after=="rumor" and not r:r=1
    else:return None
    if t==1:first="witness" if f else "march"
    if t==second_after:
        if second=="responsive":
            after="followup" if w==2 else ("intercept" if m else "rumor")
        elif second=="static":after="rumor"
        elif second=="none":after="none"
        else:raise ValueError("bad second actor mode")
    return (c,f,w,m,n,x,r,first,after)

def terminal(s):
    c,f,w,m,n,x,r,first,after=s
    return (int(c==4),int(w==2),int(m==1),
            int(n==2),int(x==1),int(r==1))

def brute(second="responsive",second_after=3):
    by_end=defaultdict(list)
    event_prefixes={}
    for plan in product(ACTIONS,repeat=N):
        state=INITIAL
        for t,action in enumerate(plan):
            state=move(state,action,t,second,second_after)
            if state is None:break
            if t==second_after:
                event_prefixes[plan[:t+1]]=state[-1]
        if state is not None:by_end[terminal(state)].append(" > ".join(plan))
    return by_end,event_prefixes

def independent_set_dp(second="responsive",second_after=3):
    @lru_cache(None)
    def visit(t,state):
        if t==N:return frozenset((terminal(state),))
        found=set()
        for action in ACTIONS:
            nextstate=move(state,action,t,second,second_after)
            if nextstate is not None:found.update(visit(t+1,nextstate))
        return frozenset(found)
    return visit(0,INITIAL)

def pareto(vectors):
    return sorted(v for v in vectors if not any(
        w!=v and all(a>=b for a,b in zip(w,v)) for w in vectors))

def visible_comparison():
    """Both prefixes are legal through t3; actor2's response is public at t4."""
    result={}
    for key,plan in {
      "contact_witness_complete":("case","contact","witness","witness"),
      "contact_witness_partial":("case","contact","case","witness"),
      "no_contact_march_seen":("case","case","march","case"),
      "no_contact_march_unseen":("case","case","case","case")
    }.items():
        state=INITIAL
        for t,action in enumerate(plan):
            state=move(state,action,t)
            assert state is not None
        result[key]=dict(after_first=state[-2],after_second=state[-1],
                         partial=(state[0],state[2],state[3]))
    assert result["contact_witness_complete"]["after_second"]=="followup"
    assert result["contact_witness_partial"]["after_second"]=="rumor"
    assert result["no_contact_march_seen"]["after_second"]=="intercept"
    assert result["no_contact_march_unseen"]["after_second"]=="rumor"
    return result

def audit():
    variants={
      "responsive":dict(second="responsive"),
      "static_rumor":dict(second="static"),
      "no_second_actor":dict(second="none"),
      "late_second_actor":dict(second="responsive",second_after=4)
    }
    results={}
    for name,variant in variants.items():
        ends,prefixes=brute(**variant)
        from_dp=independent_set_dp(**variant)
        assert set(ends)==set(from_dp),(name,ends,from_dp)
        response=defaultdict(int)
        for x in prefixes.values():response[x]+=1
        results[name]=dict(
            unique_endings=len(ends),legal_sequences=sum(len(x) for x in ends.values()),
            pareto=pareto(ends),responses=dict(response),
            example_for_pareto={str(end):ends[end][0] for end in pareto(ends)}
        )
    assert results["responsive"]["unique_endings"]==13
    assert results["responsive"]["legal_sequences"]==837
    assert results["responsive"]["pareto"]==[
        (0,1,0,0,0,1),
        (0,1,0,1,0,0),
        (1,0,1,0,0,1),
        (1,0,1,0,1,0)]
    assert results["static_rumor"]["unique_endings"]==10
    assert results["static_rumor"]["pareto"]==[
        (0,1,0,0,0,1),
        (1,0,1,0,0,1)]
    assert results["no_second_actor"]["unique_endings"]==5
    assert results["no_second_actor"]["pareto"]==[
        (0,1,0,0,0,0),
        (1,0,1,0,0,0)]
    assert results["late_second_actor"]["unique_endings"]==9
    assert results["late_second_actor"]["pareto"]==[
        (0,1,0,0,0,0),
        (1,0,0,0,0,1),
        (1,0,1,0,1,0)]
    intermediate=visible_comparison()
    assert all(len(x)==6 for x in results["responsive"]["pareto"])
    return {
      "schema":"ssnoir.r166-r173.city-cascades.v1",
      "scope":"two hypothetical autonomous actors, exact abstract paper only",
      "rules":{
        "player_turns":6,"main_case_requires":4,
        "actorA_event":"after t1, contact opens witness(2), otherwise march(1)",
        "actorB_event":"after t3; witness complete→followup(2), march joined→intercept(1), otherwise rumor(1)",
        "observable_after":"actor B response visible before t4 player action",
        "endings_vector":"case, witness, march, followup, intercept, rumor",
        "no_foreknowledge":"player t2 cannot spend future target actions before event triggers"
      },
      "R166_two_actor_cascade":results["responsive"],
      "R167_independent_two_solvers":"all 8^6 possible plans filtered against memoized complete state set; exact equality for 4 variants",
      "R168_no_second_actor":results["no_second_actor"],
      "R169_static_second_actor":results["static_rumor"],
      "R170_late_reaction":results["late_second_actor"],
      "R171_public_outcome_dependency":intermediate,
      "R172_multiobjective_frontier":results["responsive"]["pareto"],
      "R173_novelty_boundary":"Every event is an authored publicly describable finite branch, not proven independent new game mechanics. No seventh curated type.",
    }
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
