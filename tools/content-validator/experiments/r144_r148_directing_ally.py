#!/usr/bin/env python3
"""R144-R148: does spending an action to direct an autonomous ally matter?

New paper-only actions, NOT part of SSNoir C#+Scheme or existing actor rules.
Exactly 3 player actions, 3 ally actions, goals A3/B2, reward 2 each.
"""
from fractions import Fraction as F
from functools import lru_cache
from collections import defaultdict
from itertools import product
import json

def step(a,b,player,helper):
    if player=="A":a=min(3,a+1)
    if player=="B":b=min(2,b+1)
    if helper=="A":a=min(3,a+1)
    if helper=="B":b=min(2,b+1)
    return a,b

def payoff(a,b):
    return F(2*(a>=3)+2*(b>=2))

def options(t,command,allowed):
    result=["A","B"]
    if command=="-" and t in allowed:
        result+=["directA","directB"]
    return result

def weighted_helper(command,reliability):
    if command=="-":return [("A",F(1,2)),("B",F(1,2))]
    return [(command,reliability),("B" if command=="A" else "A",1-reliability)]

def collapse(rows):
    grouped=defaultdict(F)
    for pr,a,b,cmd,h in rows:grouped[(a,b,cmd,h)]+=pr
    return tuple(sorted((pr,a,b,cmd,h) for (a,b,cmd,h),pr in grouped.items() if pr))

def optimal(visibility="none",reliability=F(1),allowed=(0,1)):
    r=F(reliability)
    assert visibility in ("none","full") and 0<=r<=1
    @lru_cache(None)
    def V(t,belief):
        if t==3:return sum(pr*payoff(a,b) for pr,a,b,_,_ in belief)
        commands={cmd for _,_,_,cmd,_ in belief}
        assert len(commands)==1,"player's declared commitment is public"
        command=next(iter(commands))
        vals=[]
        for action in options(t,command,allowed):
            buckets=defaultdict(list)
            for pr,a,b,old,hist in belief:
                new=action[-1] if action.startswith("direct") else old
                for h,p in weighted_helper(new,r):
                    if not p:continue
                    aa,bb=step(a,b,action,h)
                    history=hist+h
                    public=history if visibility=="full" else ""
                    buckets[public].append((pr*p,aa,bb,new,history))
            v=F(0)
            for rows in buckets.values():
                prob=sum(z[0] for z in rows)
                conditioned=collapse((pr/prob,a,b,c,h) for pr,a,b,c,h in rows)
                v+=prob*V(t+1,conditioned)
            vals.append(v)
        return max(vals)
    return V(0,((F(1),0,0,"-",""),))

def score_plan(plan,reliability=F(1),allowed=(0,1)):
    """Independent brute-force forward probability evaluation; no V recursion."""
    dist={(0,0,"-"):F(1)}
    for t,action in enumerate(plan):
        next_dist=defaultdict(F)
        for (a,b,cmd),mass in dist.items():
            assert action in options(t,cmd,allowed),(plan,t,action)
            new=action[-1] if action.startswith("direct") else cmd
            for h,pr in weighted_helper(new,reliability):
                aa,bb=step(a,b,action,h)
                next_dist[(aa,bb,new)]+=mass*pr
        dist=dict(next_dist)
    return sum(pr*payoff(a,b) for (a,b,_),pr in dist.items())

def brute_all_plans(reliability=F(1),allowed=(0,1)):
    """Enumerate only action sequences legal given whether ally is directed."""
    def walk(t,cmd,plan):
        if t==3:
            yield tuple(plan)
            return
        for action in options(t,cmd,allowed):
            future=action[-1] if action.startswith("direct") else cmd
            yield from walk(t+1,future,plan+[action])
    vals=[(score_plan(plan,reliability,allowed),plan) for plan in walk(0,"-",[])]
    best=max(v for v,_ in vals)
    return best,[list(x) for v,x in vals if v==best]

def pack(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {k:pack(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [pack(v) for v in x]
    return x

def audit():
    rows={}
    for reliability in (F(9,10),F(19,20),F(1)):
        per={}
        for view in ("none","full"):
            variants={
                "freely_time_command":(0,1),
                "never_command":(),
                "early_only":(0,),
                "late_only":(1,)
            }
            values={}
            for name,legal_turns in variants.items():
                value=optimal(view,reliability,legal_turns)
                values[name]=value
                if view=="none":
                    independent,_=brute_all_plans(reliability,legal_turns)
                    assert value==independent,(reliability,name,value,independent)
            per[view]=values
        rows[str(reliability)]=per
    assert rows["1"]["none"]=={
        "freely_time_command":F(4),
        "never_command":F(7,2),
        "early_only":F(4),
        "late_only":F(7,2)}
    assert all(values==F(4) for values in rows["1"]["full"].values())
    assert rows["9/10"]["none"]["freely_time_command"]==F(7,2)
    assert rows["19/20"]["none"]["freely_time_command"]==F(14859,4000)
    # Under an early "direct A", own turn 0 is spent coordinating;
    # ally delivers 3 A steps iff it actually follows A all three times,
    # and own turns 1+2 reliably deliver B2. This gives 2+2*r^3.
    # For R=0.9 this is BELOW strongest no-command script 3.5.
    for r in (F(9,10),F(19,20),F(1)):
        directed=F(2)+2*r**3
        if r==F(9,10):assert directed<F(7,2)
        else: assert directed>=F(7,2)
        assert rows[str(r)]["none"]["early_only"]==max(F(7,2),directed)
    return pack({
      "schema":"ssnoir.r144-r148.paper/v1",
      "scope":"exact paper only; character promise/relationship not modeled",
      "rules":{"turns":3,"helper_steps":3,"goals":[3,2],"completed_rewards":[2,2],
               "command_consumes_player_action":True,
               "command_persists":True,
               "command_sets_target_immediately":True,
               "post_command_each_work_succeeds_on_target_with_probability":"reliability"},
      "values":rows,
      "R144_visible_vs_hidden":dict(hidden_gain=F(1,2),visible_gain=F(0)),
      "R145_delayed_command":dict(hidden_gain=F(0),baseline=F(7,2)),
      "R146_reliability":dict(
         r9of10="dominated by no command",r19of20="direct A early yields 14859/4000",
         exact_threshold="2+2*r^3 > 7/2 iff r^3 > 3/4"),
      "R147_true_strategy_compression":"In no-report condition, one precommitted 3-action plan matches the full belief-state optimum in every tested reliability.",
      "R148_novelty":"Instructing a helper changes their future policy, but is currently a one-off reliable-work purchase (P13/R45 adjacency). No new standalone curated pattern."
    })
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
