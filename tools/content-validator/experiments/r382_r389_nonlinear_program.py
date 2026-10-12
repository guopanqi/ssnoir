#!/usr/bin/env python3
"""R382–R389: transfer of authored executable operations beyond XOR/XNOR.

Same six-turn online rule-editing activity as R366, new target family
AND or OR (not complementary) and a new nonlinear clause N=(x AND y).
Input is public before each action. RUN correctness gives partial feedback;
it only reveals which target type when their outputs differ at input 01/10.
The programmed function persists across turns and changes with edits.
Exact posterior DP and independent complete actual tape replay; strong
all-fixed-schedule, no-feedback, and missing-operator ablations.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json

T=6
TARGETS=(0b1000,0b1110) # AND, OR
FULL_OPS={"X":0b1100,"Y":0b1010,"N":0b1000,"I":0b1111}
OLD_OPS={"X":0b1100,"Y":0b1010,"I":0b1111}

def bit(mask,i):return (mask>>i)&1

def solver(ops=FULL_OPS,cap=6,feedback=True):
    @lru_cache(None)
    def V(t,mask,possible,used):
        if t==T:return F(0)
        return sum(F(1,4)*max(Q(t,mask,possible,used,i,a) for a in ("R",)+tuple(ops)
                              if a=="R" or used<cap) for i in range(4))
    def Q(t,mask,possible,used,i,a):
        if a!="R":
            assert used<cap
            return V(t+1,mask^ops[a],possible,used+1)
        if not feedback:
            return (sum(F(int(bit(mask,i)==bit(TARGETS[k],i)),len(possible)) for k in possible)
                    +V(t+1,mask,possible,used))
        total=F(0)
        for success in (0,1):
            matching=tuple(k for k in possible
                           if int(bit(mask,i)==bit(TARGETS[k],i))==success)
            if matching:
                mass=F(len(matching),len(possible))
                total+=mass*(success+V(t+1,mask,matching,used))
        return total
    return V(0,0,(0,1),0),V,Q

def fixed_schedules(ops=FULL_OPS):
    # Different method: simply average each RUN truth-table correctness
    # over both fixed unknown targets and over the four independent inputs.
    vals={}
    for seq in product(("R",)+tuple(ops),repeat=T):
        mask=0;reward=F(0)
        for action in seq:
            if action=="R":
                reward+=sum(F(4-(mask^target).bit_count(),8) for target in TARGETS)
            else:mask^=ops[action]
        vals["".join(seq)]=reward
    maxval=max(vals.values())
    return maxval,sorted(k for k,v in vals.items() if v==maxval)[:7],len(vals)

def independent_all_tapes(Q,ops=FULL_OPS):
    # Independently execute the actual private target and ALL 4^6
    # public input streams. Query the policy on PUBLIC states only.
    # Unlike the DP, execution updates targets by observing the actual
    # correct/incorrect signal; it never passes the hidden k to Q.
    total=F(0)
    partial_feedback=F(0)
    nonlinear_edit=F(0)
    witness=None
    for k in (0,1):
        for inputs in product(range(4),repeat=T):
            mask=0;types=(0,1);used=0;pay=0;history=[]
            for t,i in enumerate(inputs):
                actions=("R",)+tuple(ops)
                qs={a:Q(t,mask,types,used,i,a) for a in actions}
                top=max(qs.values())
                a=next(a for a in actions if qs[a]==top)
                history.append(a)
                if a=="R":
                    success=int(bit(mask,i)==bit(TARGETS[k],i))
                    pay+=success
                    types=tuple(h for h in types
                                if int(bit(mask,i)==bit(TARGETS[h],i))==success)
                    assert types
                    if len(types)==2 and t>=0:partial_feedback+=F(1,2*4**T)
                else:
                    mask^=ops[a];used+=1
            total+=F(pay,2*4**T)
            if "N" in history:
                nonlinear_edit+=F(1,2*4**T)
                if witness is None:witness={"type":k,"inputs":inputs,"acts":"".join(history)}
    return total,partial_feedback,nonlinear_edit,witness

def encode(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):encode(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [encode(v) for v in x]
    return x

def audit():
    best,V,Q=solver()
    no_feedback,_,_=solver(feedback=False)
    missing_N,_,_=solver(ops=OLD_OPS)
    fixed,plans,count=fixed_schedules()
    full_tapes,partial,nonlinear,witness=independent_all_tapes(Q)
    cap={str(k):solver(cap=k)[0] for k in (0,1,2,3)}
    oracle=sum(V(0,0,(k,),0)/2 for k in (0,1))
    # Regression against R366: AND and OR are NOT complements, so unlike
    # XOR/XNOR, many valid RUN observations DO NOT identify hidden target.
    assert bit(TARGETS[0],0)==bit(TARGETS[1],0)
    assert bit(TARGETS[0],3)==bit(TARGETS[1],3)
    assert bit(TARGETS[0],1)!=bit(TARGETS[1],1)
    assert (best,full_tapes,no_feedback,missing_N,fixed,oracle)==(
        F(16921,4096),F(16921,4096),F(15707,4096),F(16161,4096),F(15,4),F(37325,8192))
    assert cap=={"0":F(3),"1":F(16753,4096),"2":F(16753,4096),"3":F(16921,4096)}
    assert count==5**T and partial>0 and nonlinear>0
    return encode({
      "schema":"ssnoir.r382-r389.non-linear-function-transfer.v1",
      "scope":"paper abstract program authoring, no human play or formal SSNoir rules",
      "rules":"Six episodes of public Boolean input (x,y) uniformly among four pairs. Required target AND(x,y) or OR(x,y) hidden and fixed, 50/50. Current program starts 0. RUN returns reward1 on correct output and truthful right/wrong feedback; only input 01/10 distinguishes the hidden target, whereas input 00/11 does not. Four clause edits toggle X, Y, nonlinear N=(x AND y), or full inversion I; each consumes a turn. Goal maximize total RUN correctness while editing one persistent executable function.",
      "R382_full_non_linear_posterior_DP":best,
      "R383_independent_8192_actual_input_tapes":full_tapes,
      "R384_best_15625_fixed_grammars":{"best":fixed,"count":count,"examples":plans},
      "R385_fewer_edit_permissions":cap,
      "R386_remove_non_linear_clause":missing_N,
      "R387_execution_feedback_not_informative":no_feedback,
      "R388_oracle_type_and_partial_observability":{"oracle":oracle,
             "positive_mass_run_did_not_identify_type":partial},
      "R389_transfer_and_limits":{"positive_mass_optimal_paths_using_AND_clause":nonlinear,
             "witness":witness,
             "judgement":"The authored-rule loop survives changing the target from complementary XOR/XNOR to AND/OR; information is now partial and the nonlinear AND clause has independent value. But the strong static 1-edit baseline already reaches 15/4, and one edit has positive value, unlike XOR/XNOR where one edit had no value. Composition complementarity is therefore NOT universal. The small classic online program-synthesis problem is only a candidate mental activity, not evidence of originality or pleasure."
      }
    })
if __name__=="__main__":print(json.dumps(audit(),indent=2,ensure_ascii=False))
