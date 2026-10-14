#!/usr/bin/env python3
"""Four-action investigation: independent exact Fraction DP and forward audit.

Hidden A/B equal prior; completing 3 WORK for the correct type pays 10/8;
SIDE grants 1. TRACK_X does one work for X plus a noisy (conditionally
independent) binary report. INSPECT obtains perfect report at action cost.
Observation arrives after its action; policies respond on later actions.
No human, independent blind play, or game-engine verification.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

TYPES=("A","B")
ACTIONS=("WORK_A","WORK_B","TRACK_A","TRACK_B","INSPECT","SIDE")

def signals(prior,act,accuracy):
    if act=="INSPECT":
        if prior:yield prior,F(1),"A"
        if 1-prior:yield 1-prior,F(0),"B"
    elif act.startswith("TRACK"):
        for name in TYPES:
            la=accuracy if name=="A" else 1-accuracy
            lb=1-accuracy if name=="A" else accuracy
            prob=prior*la+(1-prior)*lb
            if prob:yield prob,prior*la/prob,name
    else:yield F(1),prior,"-"

def solve(track=True,inspect=True,accuracy=F(3,4),inspect_time=1,track_window=4):
    assert 0<=accuracy<=1 and 1<=inspect_time<=4
    @lru_cache(None)
    def walk(t,a,b,side,prior):
        if t==4:return prior*10*(a>=3)+(1-prior)*8*(b>=3)+side,()
        candidates=[]
        for op in ACTIONS:
            if op.startswith("TRACK") and (not track or t>=track_window):continue
            if op=="INSPECT" and (not inspect or t+inspect_time>4):continue
            aa=a+int(op in ("WORK_A","TRACK_A"))
            bb=b+int(op in ("WORK_B","TRACK_B"))
            ss=side+int(op=="SIDE")
            next_t=t+(inspect_time if op=="INSPECT" else 1)
            branches={}
            val=F(0)
            for prob,p,signal in signals(prior,op,accuracy):
                v,policy=walk(next_t,aa,bb,ss,p)
                val+=prob*v
                branches[signal]=(str(p),policy)
            candidates.append((val,(op,branches)))
        return max(candidates,key=lambda x:x[0])
    return walk(0,0,0,0,F(1,2))

def replay(policy,real_type,accuracy=F(3,4),inspect_time=1):
    """Separately forward-enumerate every latent type and noisy observation."""
    def branch(t,a,b,side,policy):
        if t==4:
            return F(10*(a>=3) if real_type=="A" else 8*(b>=3))+side
        op,signals_map=policy
        aa=a+int(op in ("WORK_A","TRACK_A"))
        bb=b+int(op in ("WORK_B","TRACK_B"))
        ss=side+int(op=="SIDE")
        nt=t+(inspect_time if op=="INSPECT" else 1)
        if op=="INSPECT":
            return branch(nt,aa,bb,ss,signals_map[real_type][1])
        if op.startswith("TRACK"):
            return sum((prob*branch(nt,aa,bb,ss,signals_map[signal][1])
                for signal,prob in (("A",accuracy if real_type=="A" else 1-accuracy),
                                     ("B",1-accuracy if real_type=="A" else accuracy))
                if prob),F(0))
        return branch(nt,aa,bb,ss,signals_map["-"][1])
    return branch(0,0,0,0,policy)

def run():
    cases={
      "blind":dict(track=False,inspect=False),
      "inspect_one":dict(track=False),
      "track_only":dict(inspect=False),
      "both":dict(),
      "inspect_two":dict(inspect_time=2),
      "perfect_track":dict(inspect=False,accuracy=F(1)),
      "useless_track":dict(inspect=False,accuracy=F(1,2)),
      "only_first_track":dict(inspect=False,track_window=1)
    }
    expected={"blind":F(6),"inspect_one":F(9),"track_only":F(119,16),
              "both":F(9),"inspect_two":F(119,16),
              "perfect_track":F(19,2),"useless_track":F(6),
              "only_first_track":F(29,4)}
    for name,args in cases.items():
        value,policy=solve(**args)
        assert value==expected[name],(name,value)
        forward=(replay(policy,"A",args.get("accuracy",F(3,4)),
                         args.get("inspect_time",1))+
                 replay(policy,"B",args.get("accuracy",F(3,4)),
                         args.get("inspect_time",1)))/2
        assert value==forward,(name,value,forward)
        print(name, str(value), "first",policy[0])
    assert solve()[1][0]=="INSPECT"
    assert solve(inspect_time=2)[1][0].startswith("TRACK")
    assert solve(inspect=False,accuracy=F(1,2))[1][0]=="WORK_A"
    print("PASS: Bayesian posteriors, full legal menus, accurate/weak signal,")
    print("complete forward trajectories, work-window/cost ablations")
if __name__=="__main__":run()
