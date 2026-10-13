#!/usr/bin/env python3
"""R245-R252: information-gathering action changes what it observes.

Unknown fixed guard A/B. Attacks reveal guard by hit/miss but carry reward
(+2 unblocked, -3 blocked). Probe spends a turn and reveals OLD guard;
then it flips with probability f. Wait is legal. Exact public-belief DP,
plus independent forward simulator of a three-condition player rule.
"""
from fractions import Fraction as F
from collections import defaultdict
from functools import lru_cache
import json
T=3

def belief_dp(f):
    @lru_cache(None)
    def value(t,p_guardA):
        if t==T:return F(0)
        return max(Q(t,p_guardA,a) for a in "ABPW")
    def Q(t,p,a):
        if a=="A":
            return p*(-3+value(t+1,F(1)))+(1-p)*(2+value(t+1,F(0)))
        if a=="B":
            return (1-p)*(-3+value(t+1,F(0)))+p*(2+value(t+1,F(1)))
        if a=="P":
            # The probe's observation is of the previous guard, NOT
            # of the guard AFTER the probe-induced disturbance.
            return p*value(t+1,1-f)+(1-p)*value(t+1,f)
        if a=="W":return value(t+1,p)
        raise ValueError(a)
    root=value(0,F(1,2))
    initial_q={a:Q(0,F(1,2),a) for a in "ABPW"}
    return root,initial_q

def simulate_rule(f):
    """No calls to Bellman/Q. Policy can see its *belief*, not true guard."""
    mass={(0,F(1,2)):F(1,2),(1,F(1,2)):F(1,2)}
    total=F(0)
    public_trace=[]
    for t in range(T):
        nd=defaultdict(F)
        trace={}
        for (true_guard,p),pr in mass.items():
            if t==0 and min(f,1-f)<F(1,10):action="P"
            else:action="B" if p>=F(1,2) else "A"
            trace.setdefault(str(p),action)
            if action=="P":
                for switched,w in ((0,1-f),(1,f)):
                    if w:
                        old=true_guard
                        newguard=old ^ switched
                        nextbelief=(1-f) if old==0 else f
                        nd[(newguard,nextbelief)]+=pr*w
            else:
                hit=int((action=="A")!= (true_guard==0))
                total+=pr*(2 if hit else -3)
                nd[(true_guard,F(1) if true_guard==0 else F(0))]+=pr
        assert sum(nd.values())==1
        public_trace.append(trace)
        mass=nd
    return total,public_trace

def stringify(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):stringify(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [stringify(y) for y in x]
    return x

def audit():
    cases={}
    for f in (F(0),F(1,20),F(1,10),F(1,4),F(1,2),F(1)):
        optimal,initialq=belief_dp(f)
        readable,trace=simulate_rule(f)
        assert optimal==readable,(f,optimal,readable)
        cases[str(f)]={"optimal":optimal,"initial_Q":initialq,
                       "readable_forward":readable,"public_trace":trace}
    assert cases["0"]["optimal"]==4
    assert cases["1/20"]["optimal"]==F(15,4)
    assert cases["1/10"]["optimal"]==F(7,2)
    assert cases["1/4"]["optimal"]==F(7,2)
    assert cases["1/2"]["optimal"]==F(7,2)
    assert cases["1"]["optimal"]==4
    for label,c in cases.items():
        assert c["initial_Q"]["A"]==F(7,2)
        assert c["initial_Q"]["B"]==F(7,2)
        assert c["optimal"]>=c["initial_Q"]["W"]
    return stringify({
      "schema":"ssnoir.abstract.r245-r252.probe-changes-object.v1",
      "evidence":"Fraction belief-DP plus independent forward reachable policy simulation",
      "rules":{"T":T,"hidden_guard":"A or B static, prior 1/2",
               "attack":"safe +2, blocked -3, publicly reveals old/current guard",
               "P":"spend action, observe guard, then guard may switch with f",
               "W":"legal wait/skip, 0 immediate; guard unchanged",
               "no_leak":"next guard after P is not publicly revealed until subsequent attack/probe"},
      "R245_probe_does_not_move_target":cases["0"],
      "R246_rare_post_observation_flip":cases["1/20"],
      "R247_boundary_equal_attack":cases["1/10"],
      "R248_quarter_flip":cases["1/4"],
      "R249_uninformative_probe":cases["1/2"],
      "R250_certain_flip_after_probe":cases["1"],
      "R251_readable_rule":"Probe first only if min(f,1-f)<1/10, otherwise attack A. Thereafter attack the side currently likelier unguarded. Independent forward rollout matches complete DP at all cases.",
      "R252_novelty_boundary":"Endogenous disturbance of the observed target makes a useful information threshold, but it is a short P08 paid information / P09 risk avoidance composition, not a certified game mode."
    })
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
