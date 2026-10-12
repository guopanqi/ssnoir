#!/usr/bin/env python3
"""R131-R143: exact information and autonomous-collaborator counterexamples.

Independent paper models only. No C#+Scheme engine or human experience claims.
All outcomes are Fractions and completed outputs cannot be undone.
"""
from collections import defaultdict
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json

def social_signal(accuracy=F(1, 2), timely=True):
    # A says yes with p=1/2: gain 3 and later B (expected 3).
    # If A refuses, at t1 choose coerce A (3) or B (tier 2/4).
    # A public high/low signal predicts B's tier with accuracy q, if timely.
    # For this frozen three-action model, waiting rather than asking A at
    # t0 is strictly dominated; initial policy does not know B's tier.
    q = F(accuracy) if timely else F(1, 2)
    assert F(1, 2) <= q <= 1
    high, low = 2 + 2*q, 4 - 2*q
    value = F(1, 2)*6 + F(1, 4)*(max(F(3), high)+max(F(3), low))
    choose = lambda v: "B" if v > 3 else "A" if v < 3 else "tie"
    return dict(value=value, low_action=choose(low), high_action=choose(high))

def step(a, b, player, helper):
    if player == "A": a = min(3, a+1)
    if player == "B": b = min(2, b+1)
    if helper == "A": a = min(3, a+1)
    if helper == "B": b = min(2, b+1)
    return a, b

def reward(a, b):
    return F(2*(a == 3)+2*(b == 2))

def aggregate(items):
    d = defaultdict(F)
    for prob,a,b,hist in items: d[(a,b,hist)] += prob
    return tuple(sorted((prob,a,b,h) for (a,b,h),prob in d.items() if prob))

def observations_optimum(p=F(1, 2), lag=0, report="target"):
    """Exact belief-state optimization: no action can read latent helper work.

    lag0 sees each completed autonomous work immediately; lag1 learns one
    turn later; lag3 never learns. "complete" exposes just completed goals,
    not partial progress. Work on already finished task is legal but wasted.
    """
    p = F(p)
    assert 0 <= p <= 1 and lag in (0,1,3)
    @lru_cache(None)
    def V(t, belief):
        if t == 3:
            return sum(w*reward(a,b) for w,a,b,h in belief)
        vals = []
        for action in ("A","B","-"):
            groups = defaultdict(list)
            for weight,a,b,hist in belief:
                for helper, q in (("A",p),("B",1-p)):
                    if not q: continue
                    aa,bb=step(a,b,action,helper)
                    hh=hist+helper
                    key = hh[:max(0,t+1-lag)] if report=="target" else (aa==3,bb==2)
                    groups[key].append((weight*q,aa,bb,hh))
            v=F(0)
            for outcomes in groups.values():
                mass=sum(z[0] for z in outcomes)
                conditional=aggregate((w/mass,a,b,h) for w,a,b,h in outcomes)
                v += mass*V(t+1,conditional)
            vals.append(v)
        return max(vals)
    return V(0,((F(1),0,0,""),))

def evaluate_schedule(actions, p=F(1,2), helper="random", helper_turns=3):
    dist={(0,0):F(1)}
    for t,action in enumerate(actions):
        after=defaultdict(F)
        for (a,b),weight in dist.items():
            choices = (("A",p),("B",1-p)) if t<helper_turns and helper=="random" else \
                      (("A",F(1)),) if t<helper_turns and helper=="A" else \
                      (("B",F(1)),) if t<helper_turns and helper=="B" else (("-",F(1)),)
            for h,q in choices:
                if q:
                    state=step(a,b,action,h)
                    after[state]+=weight*q
        dist=dict(after)
    return sum(w*reward(a,b) for (a,b),w in dist.items())

def best_schedule(p=F(1,2),helper="random",helper_turns=3):
    r=[(evaluate_schedule("".join(seq),p,helper,helper_turns),"".join(seq))
       for seq in product("AB",repeat=3)]
    v=max(x[0] for x in r)
    return v,[name for score,name in r if score==v]

def independently_enumerate_policies(p=F(1,2),lag=0,report="target"):
    """No belief DP: brute-force all deterministic observation-key strategies.

    Enumerate 2^7 full-report, 2^4 one-turn-lag, 2^3 no-report, and
    2^9 completion-only policy tables. Evaluate every 3-turn chance path.
    """
    if report=="complete":
        masks=list(product((False,True),repeat=2))
        keys=[(0,None)]+[(1,m) for m in masks]+[(2,m) for m in masks]
    else:
        keys={0:[(0,""),(1,"A"),(1,"B"),(2,"AA"),(2,"AB"),(2,"BA"),(2,"BB")],
              1:[(0,""),(1,""),(2,"A"),(2,"B")],
              3:[(0,""),(1,""),(2,"")]}[lag]
    best=F(-1)
    for choices in product("AB",repeat=len(keys)):
        policy=dict(zip(keys,choices))
        v=F(0)
        for path in product("AB",repeat=3):
            a=b=0
            probability=F(1)
            mask=None
            for t,h in enumerate(path):
                key=(t,mask) if report=="complete" else (t,"".join(path[:max(0,t-lag)]))
                a,b=step(a,b,policy[key],h)
                probability *= p if h=="A" else 1-p
                mask=(a==3,b==2)
            v+=probability*reward(a,b)
        best=max(best,v)
    return best

def simple_visible_policy(p=F(1,2)):
    """A human-readable three-branch rule, no future helper knowledge."""
    states={(0,0):F(1)}
    path=[]
    for t in range(3):
        next_states=defaultdict(F)
        for (a,b),mass in sorted(states.items()):
            act="A" if t==0 or (t==1 and a<2) or (t==2 and a<3) else "B"
            path.append(dict(turn=t,progress=[a,b],action=act,path_mass=mass))
            for h,q in (("A",p),("B",1-p)):
                if q:next_states[step(a,b,act,h)]+=mass*q
        states=dict(next_states)
    return sum(m*reward(a,b) for (a,b),m in states.items()),path

def independent_strict_branch_witnesses():
    """Separate 1/2-step finite tree, without belief-state DP or strategy cache.

    Show BOTH alternatives are legal at each visited state and how much
    switching buys, on the chosen optimal policy's actual two branches.
    These are expected path probabilities, not human play frequencies.
    """
    def update(a,b,action,helper):
        return (min(3,a+int(action=="A")+int(helper=="A")),
                min(2,b+int(action=="B")+int(helper=="B")))
    def utility(a,b): return F(2*int(a==3)+2*int(b==2))
    def last(a,b,action):
        return sum((utility(*update(a,b,action,h)) for h in ("A","B")), F(0))/2
    def middle(a,b,action):
        return sum((max(last(*update(a,b,action,h),next_action)
                        for next_action in ("A","B"))
                    for h in ("A","B")),F(0))/2
    rows={}
    for turn,a,b,q,preferred in (
        (1,2,0,middle,"B"),
        (1,1,1,middle,"A"),
        (2,2,2,last,"A"),
        (2,3,1,last,"B")):
        A,B=q(a,b,"A"),q(a,b,"B")
        assert (A>B and preferred=="A") or (B>A and preferred=="B")
        rows[f"t{turn}_A{a}_B{b}"]={
            "choose_A_value":A,"choose_B_value":B,
            "strict_choice":preferred,"path_mass":F(1,2),
            "strict_gap":abs(A-B)}
    assert rows["t1_A2_B0"]["strict_gap"]==F(1,2)
    assert rows["t1_A1_B1"]["strict_gap"]==F(1,2)
    assert rows["t2_A2_B2"]["strict_gap"]==1
    assert rows["t2_A3_B1"]["strict_gap"]==1
    return rows

def two_helper_turns(p=F(1,2)):
    """Separate DP for helper only acting during first two player turns."""
    @lru_cache(None)
    def V(t,a,b):
        if t==3:return reward(a,b)
        out=[]
        for action in "AB":
            if t<2:
                out.append(p*V(t+1,*step(a,b,action,"A"))+
                           (1-p)*V(t+1,*step(a,b,action,"B")))
            else:out.append(V(t+1,*step(a,b,action,"-")))
        return max(out)
    return V(0,0,0),best_schedule(p,helper_turns=2)

def pack(obj):
    if isinstance(obj,F):return str(obj)
    if isinstance(obj,dict):return {k:pack(v) for k,v in obj.items()}
    if isinstance(obj,(list,tuple)):return [pack(v) for v in obj]
    return obj

def audit():
    social={str(q):social_signal(q) for q in (F(1,2),F(3,4),F(1))}
    late=social_signal(F(1),timely=False)
    assert [social[q]["value"] for q in ("1/2","3/4","1")] == [F(9,2),F(37,8),F(19,4)]
    assert late["value"]==F(9,2)
    data={}
    for p in (F(1,4),F(1,2),F(3,4)):
        m=dict(full=observations_optimum(p,0),lag1=observations_optimum(p,1),
               none=observations_optimum(p,3))
        fixed,plans=best_schedule(p)
        assert m["none"]==fixed and m["full"]>=m["lag1"]>=m["none"]
        for lag,k in ((0,"full"),(1,"lag1"),(3,"none")):
            assert independently_enumerate_policies(p,lag)==m[k],(p,lag,m[k])
        data[str(p)]=dict(**m,best_fixed=fixed,fixed_plans=plans)
    assert data["1/2"]["full"]==4 and data["1/2"]["none"]==F(7,2)
    # Minimal observable policy and normal-path branch masses.
    value,path=simple_visible_policy()
    witnesses=independent_strict_branch_witnesses()
    assert value==4
    assert [x["path_mass"] for x in path if x["turn"]==1]==[F(1,2),F(1,2)]
    # Independent exhaustive verifier checks "only completed" information.
    completion_only=observations_optimum(report="complete")
    assert independently_enumerate_policies(report="complete")==completion_only==F(15,4)
    short,best_short=two_helper_turns()
    assert short==4 and best_short[0]==3
    fixed_helper={kind:best_schedule(helper=kind)[0] for kind in ("A","B","none")}
    assert fixed_helper=={"A":4,"B":4,"none":2}
    # Deterministic helper follows a public opposite-action policy: BBB
    # causes helper to do A three times, providing both completed outputs.
    assert evaluate_schedule("BBB",helper="A")==4
    return pack({
      "schema":"ssnoir.r131-r143.v1","scope":"Fraction paper-only, not engine or human",
      "R131_R133_signal":dict(values=social,full_report_after_choice=late,
                full_info_increment=F(19,4)-F(9,2)),
      "R134_R138_independent_helper":dict(
        rules=dict(turns=3,goals=[3,2],reward_each=2,
                   helper_target_choices="independent A/B", completed_persist=True),
        by_probability=data),
      "R135_independent_policy_enumeration":"2^7/2^4/2^3 exact policies; equality for all three p",
      "R136_independent_strict_reachable_witnesses":witnesses,
      "R136_public_simple_rule":dict(value=value,normal_path=path),
      "R139_ablation":fixed_helper,
      "R140_adaptive_minus_best_fixed":F(4)-F(7,2),
      "R141_completion_only_report":completion_only,
      "R142_helper_active_first_two_turns":dict(full=short,best_fixed=best_short),
      "R143_deterministic_complement":dict(value=4,strong_script="BBB",adaptive_gain=0),
      "novelty":"Two families plus response-source controls, NOT thirteen distinct good mechanisms"
    })

if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
