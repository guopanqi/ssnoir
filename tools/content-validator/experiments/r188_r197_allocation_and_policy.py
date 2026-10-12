#!/usr/bin/env python3
"""SSNoir R188-R197: durable-vs-reset allocation and policy-writing.

Pure abstract rule-space. Exact probability and no fabricated human
evidence. R188-193 allocation, R194-197 self-modifying verbs.
"""
from collections import defaultdict
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json

def show(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):show(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [show(v) for v in x]
    return x

def allocate(expire=True,T=4):
    """2 actions per cycle. Current quality 1/2 is visible before assigning.
    A progress is durable; B pays 2 at cycle end upon reaching 2.
    B's unsuccessful partial progress expires if expire=True.
    End-of-horizon A>=4 pays 4. Both use the same limited action pool.
    """
    def transition(t,a,b,q,choice):
        a2=min(4,a+(q if choice=="A" else 0))
        b2=min(2,b+(q if choice=="B" else 0))
        reward=0
        if t%2:
            reward=2*int(b2>=2)
            if b2>=2 or expire:b2=0
        return reward,a2,b2
    @lru_cache(None)
    def opt(t,a,b):
        if t==T:return F(4*int(a>=4))
        return sum(F(1,2)*max(qvalue(t,a,b,q,kind) for kind in "AB") for q in (1,2))
    def qvalue(t,a,b,q,act):
        r,a2,b2=transition(t,a,b,q,act)
        return r+opt(t+1,a2,b2)
    def evaluate_rule(rule):
        total=F(0)
        for quality in product((1,2),repeat=T):
            a=b=0
            result=0
            for t,q in enumerate(quality):
                act=rule(t,a,b,q)
                reward,a,b=transition(t,a,b,q,act)
                result+=reward
            total+=F(result+4*int(a>=4),2**T)
        return total
    fixed={''.join(seq):evaluate_rule(lambda t,a,b,q,seq=seq:seq[t])
           for seq in product("AB",repeat=T)}
    quality_policies={''.join(seq):evaluate_rule(
        lambda t,a,b,q,seq=seq:seq[t*2+q-1]) for seq in product("AB",repeat=2*T)}
    baseline_policies={
       "always_durable":lambda t,a,b,q:"A",
       "always_ephemeral":lambda t,a,b,q:"B",
       "finish_window_at_second_action":lambda t,a,b,q:"B" if t%2 and b<2 else "A",
       "quality_2_durable_else_ephemeral":lambda t,a,b,q:"A" if q==2 else "B",
       "complete_durable_then_window":lambda t,a,b,q:"A" if a<4 else "B",
    }
    heuristics={name:evaluate_rule(rule) for name,rule in baseline_policies.items()}
    observed={}
    mass={(0,0):F(1)}
    witnesses=[]
    for t in range(T):
        forward=defaultdict(F)
        for (a,b),prob in mass.items():
            local={}
            for q in (1,2):
                qs={act:qvalue(t,a,b,q,act) for act in "AB"}
                local[q]=qs
                if qs["A"]!=qs["B"]:
                    witnesses.append(dict(t=t,state=(a,b),observed_quality=q,
                                          probability=prob/F(2),qvalues=qs,
                                          best=max(qs,key=qs.get)))
                choice=max("AB",key=lambda act:qs[act])
                _,a2,b2=transition(t,a,b,q,choice)
                forward[(a2,b2)]+=prob/F(2)
            if local[1]["A"]>local[1]["B"] and local[2]["A"]<local[2]["B"]:
                observed[(t,a,b)]="A-then-B"
            elif local[1]["A"]<local[1]["B"] and local[2]["A"]>local[2]["B"]:
                observed[(t,a,b)]="B-then-A"
        mass=forward
    assert sum(mass.values())==1
    return dict(value=opt(0,0,0),best_fixed=max(fixed.values()),
                best_public_quality_only=max(quality_policies.values()),
                fixed_examples=sorted(k for k,v in fixed.items() if v==max(fixed.values()))[:6],
                quality_only_examples=sorted(k for k,v in quality_policies.items()
                                             if v==max(quality_policies.values()))[:4],
                simple_rules=heuristics,reach_strict=witnesses,
                quality_swaps_on_same_prestate=observed,
                finished_state_probability=sum(mass.values()))

POLICIES=("A","B","Q","T")
def tick(a,b,policy,quality,need_a,need_b):
    if policy=="A":choice="A"
    elif policy=="B":choice="B"
    elif policy=="Q":choice="A" if quality==2 else "B"
    else:choice="A" if a<need_a else "B"
    if choice=="A":a=min(need_a,a+quality)
    else:b=min(need_b,b+quality)
    return a,b

def programmable(need_a=3,need_b=3):
    """Player writes a rule once, then it runs on four publicly observable
    quality ticks. At halfway may rewrite policy, but editing costs one tick.
    A/B are fixed operations, Q tests quality, T checks remaining A target.
    """
    def final(a,b):
        return F(4*int(a>=need_a)+4*int(b>=need_b))+F(a+b,10)
    def end_of(quality,policy):
        a=b=0
        for q in quality:
            a,b=tick(a,b,policy,q,need_a,need_b)
        return final(a,b)
    static={
        policy:sum(end_of(qualities,policy)/16 for qualities in product((1,2),repeat=4))
        for policy in POLICIES
    }
    def conditional_after_two(policy):
        total=F(0)
        rewrite_strict=[]
        for q0,q1 in product((1,2),repeat=2):
            a,b=tick(0,0,policy,q0,need_a,need_b)
            a,b=tick(a,b,policy,q1,need_a,need_b)
            keep=sum(final(*tick(*tick(a,b,policy,q2,need_a,need_b),
                                 policy,q3,need_a,need_b))/4 for q2,q3 in product((1,2),repeat=2))
            edits={new:sum(final(*tick(a,b,new,q3,need_a,need_b))/2
                           for q3 in (1,2)) for new in POLICIES}
            changed=max(edits.values())
            if changed>keep:rewrite_strict.append((q0,q1,keep,changed))
            total+=max(keep,changed)/4
        return total,rewrite_strict
    adaptive={k:conditional_after_two(k)[0] for k in POLICIES}
    return dict(static_values=static,highest_static=max(static.values()),
                policies_achieving_static=sorted(k for k,v in static.items() if v==max(static.values())),
                reprogram_if_revealed_values=adaptive,
                best_adaptive=max(adaptive.values()),
                adaptive_gap=max(adaptive.values())-max(static.values()),
                strict_rewrite_by_policy={k:conditional_after_two(k)[1] for k in POLICIES})

def audit():
    a=allocate()
    carry=allocate(expire=False)
    assert a["value"]==F(11,2)
    assert a["best_fixed"]==F(9,2)
    assert a["best_public_quality_only"]==F(19,4)
    assert carry["value"]==a["value"] # surprising zero marginal at this horizon
    assert a["quality_swaps_on_same_prestate"] # public quality reverses choice
    assert max(a["simple_rules"].values())<=a["value"]
    grammar=programmable(need_a=3,need_b=3)
    assert grammar["highest_static"]==F(1087,160)
    assert grammar["best_adaptive"]==grammar["highest_static"]
    assert grammar["policies_achieving_static"]==["T"]
    grammar_hard=programmable(need_a=4,need_b=4)
    assert grammar_hard["highest_static"]==F(771,160)
    assert grammar_hard["adaptive_gap"]==0
    return show({
      "schema":"ssnoir.r188-r197.abstract.v1",
      "evidence":"exact public-quality DP plus forward reachable state mass; exhaustive fixed and 2^8 public quality-only rules; independently all 2^4 input tapes for programmed policies",
      "R188_2cycle_persistent_and_volatile":a,
      "R189_best_static_plan":a["best_fixed"],
      "R190_quality_only_strong_policy":a["best_public_quality_only"],
      "R191_same_prestate_quality_reversal":a["quality_swaps_on_same_prestate"],
      "R192_allow_partial_B_to_survive_boundary":carry,
      "R193_relation_assessment":"value arises from P02/P04 capacity, quality revelation, and P05 expiry; carrying B did not change root optimum, so do not declare expiry essential",
      "R194_policy_as_action":grammar,
      "R195_midstream_rule_rewrite_tax":grammar["adaptive_gap"],
      "R196_second_target_condition":grammar_hard,
      "R197_relation_assessment":"rule writing is distinct user-facing activity from allocating dice, but tested model's single short T rule fully explains best value, rewrites have zero root benefit. Novelty not validated."
    })
if __name__=="__main__":print(json.dumps(audit(),indent=2,ensure_ascii=False))
