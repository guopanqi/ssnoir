#!/usr/bin/env python3
"""R326-R333: acting early destroys access to future evidence.
Exact partial-information state enumeration, all 4^3 predetermined
action schedules, explicit counterfactual 'evidence remains open'.
No SSNoir native scenes, AI opponents, or human outcome data.
"""
from functools import lru_cache
from itertools import product
from fractions import Fraction as F
import json
T=3
H=(0,1,2,3)
REQUIRED=(1,2,0,2)
TESTS={"A":frozenset((0,1)),"B":frozenset((1,2))}
def solve(wrong=3,close=True,allow_tests=True,early_bonus=2):
    @lru_cache(None)
    def V(turn,possible,locked):
        if turn==T or not possible:return F(0)
        return max([F(0)]+list(Q(turn,possible,locked).values()))
    def Q(turn,possible,locked):
        values={}
        for kind in (0,1,2):
            correct=sum(REQUIRED[h]==kind for h in possible)
            rest=tuple(h for h in possible if REQUIRED[h]!=kind)
            values["act"+str(kind)]=(
                F(correct,len(possible))*(3+early_bonus*(T-turn-1))
                +F(len(rest),len(possible))*(-wrong+V(turn+1,rest,locked or close)))
        if allow_tests and not locked:
            for name,subset in TESTS.items():
                pieces=[tuple(h for h in possible if (h in subset)==answer)
                        for answer in (False,True)]
                values["test"+name]=sum(F(len(part),len(possible))*V(turn+1,part,False)
                                       for part in pieces if part)
        return values
    return V(0,H,False),Q(0,H,False),Q(1,(0,2),True),Q(1,(0,2),False)

def fixed_action_schedules(wrong=3,early_bonus=2):
    # Independent payoff via exact hidden types: preannounce a sequence
    # of interventions; successes terminate, failures incur penalty.
    # W (legitimate skip) is included in all 4^3 schedules.
    vals={}
    for plan in product((0,1,2,"W"),repeat=T):
        total=F(0)
        for h in H:
            for turn,action in enumerate(plan):
                if action=="W":continue
                if REQUIRED[h]==action:
                    total+=F(3+early_bonus*(T-turn-1),len(H))
                    break
                total-=F(wrong,len(H))
        vals["/".join(map(str,plan))]=total
    best=max(vals.values())
    return best,sorted(k for k,v in vals.items() if v==best),len(vals)

def independent_after_failed_early_action(wrong):
    # After trying type2 and failing, only states {0,2} remain; a
    # mid-stage query distinguishes them and last-stage correct action
    # pays 3 with no additional failed intervention.
    query=F(3)
    # With no query, choose one remaining type t1 (reward5 if right,
    # -wrong and other correct t2 reward3 if wrong).
    guess=F(5,2)+F(3-wrong,2)
    return query,guess

def serial(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):serial(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [serial(z) for z in x]
    return x

def main():
    rows={}
    for penalty in (0,1,2,3,4):
        shut,root,locked_branch,open_branch=solve(penalty,close=True)
        open_value,_,_,_=solve(penalty,close=False)
        disable_tests,_,_,_=solve(penalty,close=True,allow_tests=False)
        fixed,plans,count=fixed_action_schedules(penalty)
        assert open_value>=shut>=disable_tests
        assert shut>=fixed
        assert count==64
        rows[str(penalty)]={"closed":shut,"remain_open":open_value,
                            "no_tests":disable_tests,"best_precommitted_actions":fixed,
                            "fixed_examples":plans[:4],
                            "opening_Q":root}
    x=rows["3"]
    assert (x["closed"],x["remain_open"],x["no_tests"],x["best_precommitted_actions"])==(
        F(13,4),F(7,2),F(13,4),F(13,4))
    assert rows["0"]["closed"]==rows["0"]["remain_open"]==F(11,2)
    assert rows["2"]["closed"]==rows["2"]["remain_open"]==F(4)
    assert rows["4"]["closed"]==F(3) and rows["4"]["no_tests"]==F(5,2)
    assert x["opening_Q"]["act2"]==F(13,4)
    query,guess=independent_after_failed_early_action(3)
    assert (query,guess)==(3,F(5,2))
    _,_,locked_branch,open_branch=solve(3)
    assert locked_branch["act0"]==guess
    assert open_branch["testA"]==query
    return serial({
      "schema":"ssnoir.r326-r333.action-seals-evidence.v1",
      "scope":"exact abstract paper; no actual evidence or human responses",
      "rules":"4 equal hidden types (0..3) require actions (1,2,0,2); 3 turns. Intervene 0/1/2: correct ends with reward 3+2*(remaining turns), wrong costs penalty and rules out that action; any first intervention permanently closes TEST A={0,1},B={1,2} in main model. Tests cost a turn but split hidden states. Legal skip (0) at any turn.",
      "R326_main_sealed_penalty3":x,
      "R327_unsealed_counterfactual":x["remain_open"],
      "R328_early_action_and_reachable_failure":{"opening_act2":x["opening_Q"]["act2"],"possible_after_failure":[0,2],"probability":F(1,2),"closed_next_best":guess,"open_next_test":query},
      "R329_independent_fixed_64_action_plans":{p:{"best":r["best_precommitted_actions"],"examples":r["fixed_examples"]} for p,r in rows.items()},
      "R330_low_failure_cost_ablation":{k:rows[k] for k in ("0","1","2")},
      "R331_large_failure_cost_test_can_help":rows["4"],
      "R332_locked_vs_open_observed_Q":{"locked":locked_branch,"open":open_branch},
      "R333_novelty_judgement":"Permanent evidence closure is causally costly at wrong-penalty3 (3.25 versus3.5) but the best closed model is already explained by a complete precommitted 2/0/1 action ladder. This is an information/commitment opportunity-cost composition, NOT certified independently novel gameplay."
    })
if __name__=="__main__":print(json.dumps(main(),indent=2,ensure_ascii=False))
