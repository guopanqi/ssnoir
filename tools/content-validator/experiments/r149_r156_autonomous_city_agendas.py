#!/usr/bin/env python3
"""R149-R156: independent NPC agendas vs generic deadlines. Exact paper-only."""
import json
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

ACTIONS=("investigate","protect","hold","publish")
INITIAL=(0,0,0,0)  # evidence 0..2, dock-protected, press-held, verified-publication
N=3

def move(state,action,t,dock_last=1,allow_hold=True):
    evidence,dock,held,published=state
    if action=="investigate" and evidence<2:return (evidence+1,dock,held,published)
    if action=="protect" and not dock and t<=dock_last:return (evidence,1,held,published)
    if action=="hold" and allow_hold and not held:return (evidence,dock,1,published)
    if action=="publish" and evidence==2 and not published:return (evidence,dock,held,1)
    return None

def outcome(state,high,active_press=True,active_dock=True,weights=(2,3,2,3)):
    """Independent processes are terminal acts, not the player's own work."""
    evidence,dock,held,published=state
    evidence_weight,true_weight,dock_weight,false_weight=weights
    private=evidence_weight*int(evidence==2)
    accurate=true_weight*int(published)
    neighborhood=(dock_weight if dock else -dock_weight)*int(active_dock)
    if not active_dock:neighborhood=dock_weight*int(dock)
    premature=-false_weight*int(bool(high and active_press and not (held or published)))
    return (private,accurate,neighborhood,premature)

def score(out):return F(sum(out))

def branch_scripts(first,high,dock_last=1,allow_hold=True,
                   active_press=True,active_dock=True,weights=(2,3,2,3)):
    after=move(INITIAL,first,0,dock_last,allow_hold)
    if after is None:return []
    rows=[]
    for a,b in product(ACTIONS,repeat=2):
        mid=move(after,a,1,dock_last,allow_hold)
        if mid is None:continue
        end=move(mid,b,2,dock_last,allow_hold)
        if end is None:continue
        output=outcome(end,high,active_press,active_dock,weights)
        rows.append((score(output),(first,a,b),output))
    return rows

def fixed_plans(active_press=True,active_dock=True,dock_last=1,
                allow_hold=True,weights=(2,3,2,3)):
    all_rows=[]
    for a,b,c in product(ACTIONS,repeat=3):
        state=INITIAL
        for t,act in enumerate((a,b,c)):
            state=move(state,act,t,dock_last,allow_hold)
            if state is None:break
        if state is None:continue
        low=outcome(state,0,active_press,active_dock,weights)
        high=outcome(state,1,active_press,active_dock,weights)
        all_rows.append(((score(low)+score(high))/2,(a,b,c),low,high))
    return all_rows

def exact_dp(active_press=True,active_dock=True,signal_turn=1,dock_last=1,
             allow_hold=True,weights=(2,3,2,3)):
    """Player cannot know high/low before signal_turn; hidden truth held in belief."""
    @lru_cache(None)
    def V(t,state,known):
        if t==N:
            if known is not None:
                return score(outcome(state,known,active_press,active_dock,weights))
            return (score(outcome(state,0,active_press,active_dock,weights))+
                    score(outcome(state,1,active_press,active_dock,weights)))/2
        choices=[Q(t,state,known,a) for a in ACTIONS if
                 move(state,a,t,dock_last,allow_hold) is not None]
        return max(choices)
    def Q(t,state,known,act):
        after=move(state,act,t,dock_last,allow_hold)
        if after is None:raise ValueError("illegal action")
        if known is not None:return V(t+1,after,known)
        if t+1>=signal_turn:
            return (V(t+1,after,0)+V(t+1,after,1))/2
        return V(t+1,after,None)
    if signal_turn==0:
        root=(V(0,INITIAL,0)+V(0,INITIAL,1))/2
    else:root=V(0,INITIAL,None)
    return root,Q,V

def independent_policy_enumeration(active_press=True,active_dock=True,
             signal_turn=1,dock_last=1,allow_hold=True,weights=(2,3,2,3)):
    """Standalone enumerator: no recursive Q/DP used for verification."""
    if signal_turn in (0,1):
        if signal_turn==0:
            # For each public type, independently choose the strongest complete path.
            totals=[]
            for high in (0,1):
                rows=[r for first in ACTIONS for r in branch_scripts(
                    first,high,dock_last,allow_hold,active_press,active_dock,weights)]
                totals.append(max(v for v,_,_ in rows))
            return (sum(totals,F(0))/2,None)
        best=F(-999)
        top_first=[]
        for first in ACTIONS:
            low=branch_scripts(first,0,dock_last,allow_hold,active_press,active_dock,weights)
            high=branch_scripts(first,1,dock_last,allow_hold,active_press,active_dock,weights)
            if not low or not high:continue
            # All low-path/high-path pairs with shared t0 first action are legal
            # contingent policies. No other shared information is assumed.
            val=max((lv+hv)/2 for lv,_,_ in low for hv,_,_ in high)
            if val>best:best,top_first=val,[first]
            elif val==best:top_first.append(first)
        return best,top_first
    if signal_turn==3:
        rows=fixed_plans(active_press,active_dock,dock_last,allow_hold,weights)
        return max(v for v,*_ in rows),None
    raise ValueError("unmodeled signal schedule")

def reachable_q():
    root,Q,V=exact_dp()
    rootq={a:Q(0,INITIAL,None,a) for a in ACTIONS
           if move(INITIAL,a,0) is not None}
    assert rootq["investigate"]>rootq["protect"]>rootq["hold"]
    after=move(INITIAL,"investigate",0)
    nodes={}
    for high in (0,1):
        valuations={a:Q(1,after,high,a) for a in ACTIONS if move(after,a,1)}
        chosen=max(valuations,key=valuations.get)
        assert all(valuations[chosen]>v for a,v in valuations.items() if a!=chosen)
        nodes[["low","high"][high]]=dict(values=valuations,chosen=chosen,mass=F(1,2),
                                           best=valuations[chosen])
    assert nodes["low"]["chosen"]=="protect" and nodes["high"]["chosen"]=="investigate"
    assert nodes["low"]["best"]==4 and nodes["high"]["best"]==3
    assert root==F(7,2)
    return rootq,nodes

def end_vectors():
    """Narrative outcomes, not just scalar optimality."""
    by_high={}
    for high in (0,1):
        rows=[entry for first in ACTIONS for entry in branch_scripts(first,high)]
        unique=set(tuple(v) for _,_,v in rows)
        # Non-dominated 4D vector under componentwise greater-is-better.
        frontier=sorted(v for v in unique if not any(w!=v and all(x>=y for x,y in zip(w,v))
                                                     for w in unique))
        by_high[("low","high")[high]]=dict(unique_count=len(unique),pareto=frontier)
    return by_high

def as_json(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {k:as_json(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [as_json(v) for v in x]
    return x

def audit():
    baseline,Q,V=exact_dp()
    enumeration,first=independent_policy_enumeration()
    assert baseline==enumeration==F(7,2) and first==["investigate"]
    rootq,nodes=reachable_q()
    fixed=max(r[0] for r in fixed_plans())
    assert fixed==3
    signal_delayed,_q,_v=exact_dp(signal_turn=3)
    independent_delayed,_=independent_policy_enumeration(signal_turn=3)
    assert signal_delayed==independent_delayed==fixed
    press_off,_q,_v=exact_dp(active_press=False)
    dock_off,_q,_v=exact_dp(active_dock=False)
    for opts,val in [({"active_press":False},press_off),
                     ({"active_dock":False},dock_off)]:
        assert independent_policy_enumeration(**opts)[0]==val
    assert press_off==4 and dock_off==5
    hold_removed,_q,_v=exact_dp(allow_hold=False)
    assert independent_policy_enumeration(allow_hold=False)[0]==hold_removed==baseline
    dock_till_end,_q,_v=exact_dp(dock_last=2)
    assert independent_policy_enumeration(dock_last=2)[0]==dock_till_end==baseline
    early,_q,_v=exact_dp(signal_turn=0)
    assert independent_policy_enumeration(signal_turn=0)[0]==early==baseline
    vectors=end_vectors()
    assert any(tuple(z)==(2,3,-2,0) for z in vectors["high"]["pareto"])
    assert any(tuple(z)==(2,0,2,-3) for z in vectors["high"]["pareto"])
    return as_json({
      "schema":"ssnoir.r149-r156.exact.v1",
      "scope":"paper only; hypothetical autonomous city actors, not actual SSNoir",
      "rules":{"player_turns":3,"signals":"low/high uniform, publicly after t0",
               "action_names":ACTIONS,
               "investigation_evidence_steps":2,
               "dock_intervention_last_turn":1,
               "publication_at":"end of t2",
               "values":"private2 + true_report3 + protected2 / spontaneous_dock_action -2; high uncorrected press rumor -3"},
      "R149_R150":dict(root=baseline,first_action_Q=rootq,reachable_branch_Q=nodes,
                       independently_enumerated_root=enumeration),
      "R151_reveal_delayed":dict(value=signal_delayed,policy_best_fixed=fixed,
                                 timely_information_gain=baseline-fixed),
      "R152_journalist_passive":dict(value=press_off,baseline=baseline),
      "R153_dock_passive":dict(value=dock_off,baseline=baseline),
      "R154_unproductive_venue":dict(hold_removed=hold_removed,
                                    remove_dock_deadline_but_keep_social_consequence=dock_till_end),
      "R155_information_early":dict(value=early,early_vs_after_first_gain=early-baseline),
      "R156_narrative_outcomes":vectors,
      "novelty":"Strict branch requires timing and independent NPC consequences; scalar objective is an explicit illustrative taste; cannot claim new seventh standalone pattern."
    })
if __name__=="__main__":
    print(json.dumps(audit(),indent=2,ensure_ascii=False))
