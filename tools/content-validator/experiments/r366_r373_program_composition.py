#!/usr/bin/env python3
"""R366-R373: author an executable Boolean operation, not switch presets.

The rule is an actual composed function g(x,y)=(a*x) XOR (b*y) XOR c.
Player starts g=0 and may EDIT-X, EDIT-Y or EDIT-INVERT, each toggling
one clause at the cost of the entire current turn. RUN executes g on
the public current input; correct +1 and public feedback reveals the
previously hidden required rule XOR vs XNOR. Six turns, uniform inputs.
Exact Fraction dynamic programming with independent fixed schedules and
complete hidden-target/input tape rollouts of the public optimal policy.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
from collections import defaultdict
import json
OPS={"X":0b1100,"Y":0b1010,"I":0b1111}
CORRECT=(0b0110,0b1001) # XOR and XNOR, complementary output truth tables
ACTIONS=("R","X","Y","I")
T=6
def output(mask,inp):return (mask>>inp)&1
def truth(k,inp):return output(CORRECT[k],inp)

@lru_cache(None)
def value(t,mask,known,edits,cap):
    if t==T:return F(0)
    available=("R",)+tuple(k for k in OPS if edits<cap)
    return sum(F(1,4)*max(q(t,mask,known,edits,cap,inp,action)
                          for action in available) for inp in range(4))

def q(t,mask,known,edits,cap,inp,action):
    if action!="R":
        assert edits<cap
        return value(t+1,mask^OPS[action],known,edits+1,cap)
    if known!=-1:
        return int(output(mask,inp)==truth(known,inp))+value(t+1,mask,known,edits,cap)
    return sum(F(1,2)*(int(output(mask,inp)==truth(k,inp))
                       +value(t+1,mask,k,edits,cap)) for k in (0,1))

def fixed_actions():
    # Precommit all 4^6 schedules, no condition on public inputs or
    # observed run correctness. Independent exact average of input
    # sequences and two equally likely complementary targets.
    schedules={}
    for seq in product(ACTIONS,repeat=T):
        mask=0;total=F(0)
        for action in seq:
            if action=="R":
                total+=sum(F(4-(mask^target).bit_count(),8) for target in CORRECT)
            else:mask^=OPS[action]
        schedules["".join(seq)]=total
    best=max(schedules.values())
    return best,sorted(k for k,v in schedules.items() if v==best)[:8],len(schedules)

def independent_rule_build_early():
    # A popular one-line approach: RUN once to identify XOR vs XNOR,
    # then blindly build exactly that rule (2 or 3 edits), then RUN.
    total=F(0)
    for k in (0,1):
        mask=0;planned=("R","X","Y")+ (("I",) if k==1 else ())
        planned=planned+("R",)*(T-len(planned))
        for t,act in enumerate(planned):
            if act=="R":
                total+=sum(F(int(output(mask,i)==truth(k,i)),2*4) for i in range(4))
            else:mask^=OPS[act]
    return total

def independent_trajectory_rollout():
    # Fully forward complete 2*4^6 actual hidden truth/input tapes.
    # Queries optimal PUBLIC state only; private type passed to policy
    # solely after the first RUN feedback makes it knowable.
    reward=F(0);mass_multiedit=F(0);distinct_paths=set()
    for actual_k in (0,1):
        for inputs in product(range(4),repeat=T):
            mask=0;known=-1;edits=0;history=[];r=0
            for t,inp in enumerate(inputs):
                legal=("R",)+tuple(a for a in OPS if edits<T)
                options={act:q(t,mask,known,edits,T,inp,act) for act in legal}
                best=max(options.values())
                chosen=next(act for act in ACTIONS if act in options and options[act]==best)
                history.append(chosen)
                if chosen=="R":
                    r+=int(output(mask,inp)==truth(actual_k,inp))
                    known=actual_k # exact feedback: XOR/XNOR always opposite
                else:mask^=OPS[chosen];edits+=1
            reward+=F(r,2*4**T)
            if len(set(history)&set(OPS))>=2:
                mass_multiedit+=F(1,2*4**T)
                distinct_paths.add("".join(history))
    return reward,mass_multiedit,sorted(distinct_paths)[:8]

def encode(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):encode(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [encode(v) for v in x]
    return x

def audit():
    root=value(0,0,-1,0,T)
    vals={str(limit):value(0,0,-1,0,limit) for limit in (0,1,2,3,T)}
    fixed,examples,count=fixed_actions()
    early_build=independent_rule_build_early()
    oracle=sum(F(1,2)*value(0,0,k,0,T) for k in (0,1))
    independent,prob_multiedit,examples_multiedit=independent_trajectory_rollout()
    root_Q={str(inp):{a:q(0,0,-1,0,T,inp,a) for a in ACTIONS} for inp in range(4)}
    assert root==independent==F(111,32)
    assert vals=={"0":F(3),"1":F(3),"2":F(215,64),"3":F(111,32),"6":F(111,32)}
    assert fixed==early_build==3 and count==4**T
    assert oracle==F(243,64)
    assert prob_multiedit>0
    assert all(v["R"]>max(v[a] for a in OPS) for v in root_Q.values())
    return encode({
      "schema":"ssnoir.r366-r373.composed-executable-rule.v1",
      "scope":"exact abstract author/execute operation, not executable in Unity; no actual player evidence",
      "rules":"At each of 6 turns public input (x,y) uniform among 00/01/10/11. Hidden target truth table XOR or XNOR equally likely, stable. Program initially g=0. Edit X toggles x clause, edit Y toggles y clause, edit I toggles global inversion, each consumes a whole turn. RUN gives +1 if current program output matches hidden target at current public input, otherwise +0; public correctness result identifies the target type on first RUN. Player may edit several clauses across turns and can inspect public input before each decision.",
      "R366_full_program_DP":root,
      "R367_best_4096_precommitted_programs":{"best":fixed,"examples":examples,"count":count},
      "R368_independent_forward_all_actual_inputs":independent,
      "R369_edit_budget_ablation":vals,
      "R370_build_entire_rule_after_first_diagnosis":early_build,
      "R371_public_initial_Q_per_input":root_Q,
      "R372_hidden_rule_oracle":oracle,
      "R373_multiclause_actual_optimal_paths_and_novelty":{
         "positive_mass_with_two_different_edits":prob_multiedit,
         "witnesses":examples_multiedit,
         "verdict":"For six trials, composition of two or three editable Boolean clauses has genuine positive option value that is absent with only one allowed edit. A naive 'RUN first, then blindly finish target program' ties always RUN at 3, whereas responding to public current inputs while editing reaches 111/32. This is real rule authoring with feedback, beyond simple R198 switch modes, but finite four-input/XOR-XNOR setup is small and classic program synthesis/online adaptation; no certification of independent gameplay or fun."
      }
    })
if __name__=="__main__":print(json.dumps(audit(),indent=2,ensure_ascii=False))
