#!/usr/bin/env python3
"""R228-R235: stagewise rearrangement of identical tool inventory.

Both the target and the stochastic multiplier are distinct information:
target is public at t0; multiplier is uniform {2,3} and becomes public
only BETWEEN moves 1 and 2. The multiplier is fixed before the run but
cannot be observed at the first action. All legal 3! orders are enumerated.
"""
from itertools import permutations
from fractions import Fraction as F
import json

MODE=(2,3)
TARGETS=(4,5)
TOOLS=("-1","+1","M")

def run(start,sequence,multiplier):
    value=start
    for op in sequence:
        value=value*multiplier if op=="M" else value+int(op)
    return value

def policy(start,tools,goal):
    # First action cannot depend on multiplier; last two may be reordered
    # AFTER multiplier is observed. The independent per-mode branches form
    # a complete two-step deterministic contingent plan enumeration.
    choices=[]
    for first in tools:
        rest=tuple(x for x in tools if x!=first)
        plans=tuple(permutations(rest))
        for branch2 in plans:
            for branch3 in plans:
                scores=(int(run(start,(first,)+branch2,2)==goal),
                        int(run(start,(first,)+branch3,3)==goal))
                val=F(sum(scores),2)
                choices.append((val,(first,branch2,branch3),scores))
    return max(x[0] for x in choices),[z for z in choices if z[0]==max(x[0] for x in choices)]

def baseline(start,tools,goal):
    vals={seq:sum(F(int(run(start,seq,n)==goal),2) for n in MODE)
          for seq in permutations(tools)}
    return max(vals.values()),[list(z) for z in vals if vals[z]==max(vals.values())]

def full_early(start,tools,goal):
    return sum(F(int(any(run(start,order,n)==goal for order in permutations(tools))),2)
               for n in MODE)

def late_goal(start,tools):
    # First tool must be committed before both target and multiplier become known.
    best=F(0)
    for first in tools:
        possible={}
        for n in MODE:
            possible[n]={
                run(start,(first,)+remaining,n)
                for remaining in permutations([x for x in tools if x!=first])}
        val=sum(F(int(target in possible[n]),len(TARGETS)*len(MODE))
                for n in MODE for target in TARGETS)
        best=max(best,val)
    return best

def check_transfer(start,tools,goal):
    # Exactly the same branch logic with a different start and arithmetic
    # inventory. Counts as transfer of a general composition principle, not
    # evidence that an additional game is "interesting".
    val,winners=policy(start,tools,goal)
    fixed,_=baseline(start,tools,goal)
    assert val>=fixed
    return {"initial":start,"tools":tools,"goal":goal,
            "feedback":val,"fixed":fixed,"witness":winners[0]}

def serialized(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):serialized(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [serialized(z) for z in x]
    return x

def main():
    branches={goal:{"feedback":policy(2,TOOLS,goal)[0],
                    "fixed":baseline(2,TOOLS,goal)[0],
                    "firsts":[b[1][0] for b in policy(2,TOOLS,goal)[1]]}
              for goal in TARGETS}
    assert branches[4]["feedback"]==1 and branches[4]["fixed"]==F(1,2)
    assert branches[5]["feedback"]==F(1,2) and branches[5]["fixed"]==F(1,2)
    assert "M" not in branches[4]["firsts"]
    expected=sum(v["feedback"] for v in branches.values())/len(TARGETS)
    fixed_target_aware=sum(v["fixed"] for v in branches.values())/len(TARGETS)
    assert expected==F(3,4)
    assert fixed_target_aware==F(1,2)
    early=sum(full_early(2,TOOLS,target) for target in TARGETS)/2
    assert early==expected
    late=late_goal(2,TOOLS)
    assert late==F(1,2)
    commute=("+1","+2","-1")
    c={t:policy(2,commute,t)[0] for t in TARGETS}
    assert c=={4:F(1),5:F(0)}
    transfer=check_transfer(3,("-1","+2","M"),8)
    assert transfer["feedback"]==1 and transfer["fixed"]==F(1,2)
    sequences={str(n):{" ".join(x):run(2,x,n) for x in permutations(TOOLS)}
               for n in MODE}
    return serialized({
      "schema":"ssnoir.abstract.r228-r235.dynamic-order.v1",
      "scope":"exact paper model, not SSNoir native or player responses",
      "contract":{"initial":2,"tools":TOOLS,"target_visible":"before first",
                  "hidden_mode":"x2 or x3, equal; revealed between first and second",
                  "no_reuse":"each of the three tools must be used exactly once"},
      "R228_target_branch_contingent_policy":branches,
      "R229_timed_multiplier_feedback":expected,
      "R230_target_aware_but_multiplier_blind":fixed_target_aware,
      "R231_mode_public_before_first":early,
      "R232_target_delayed_until_after_first":late,
      "R233_commuting_inventory_control":c,
      "R234_independent_transfer":transfer,
      "R235_truth_table_all_permutations":sequences,
      "interpretation":"Information between the first and second actions has a +1/4 expected success advantage over blind full order. The best initial tool depends on requested output. Still short conditional order rules, not proven sustained novelty."
    })
if __name__=="__main__":print(json.dumps(main(),indent=2,ensure_ascii=False))
