#!/usr/bin/env python3
"""R198-R203: cheap falsification of repeated rule-editing novelty.

World has a visible binary demand that persists with probability p.
Player has a binary operation mode; RUN pays if demand matches; EDIT
changes operation but forfeits current action. No narrative variables.
Fraction DP checked against independent forward evaluation of public rules.
"""
from collections import defaultdict
from fractions import Fraction as F
from functools import lru_cache
import json

def case(p,T=6):
    @lru_cache(None)
    def best(t,demand,mode):
        if t==T:return F(0)
        return max(q(t,demand,mode,"R"),q(t,demand,mode,"E"))
    def q(t,demand,mode,a):
        nm=1-mode if a=="E" else mode
        now=F(int(a=="R" and demand==mode))
        return now+p*best(t+1,demand,nm)+(1-p)*best(t+1,1-demand,nm)
    optimum=(best(0,0,0)+best(0,1,0))/2
    def eval_public_rule(name):
        # Distinct forward probability mass evaluator, no DP calls.
        distribution={(0,0):F(1,2),(1,0):F(1,2)}
        gains=F(0)
        for t in range(T):
            nextdist=defaultdict(F)
            for (d,m),prob in distribution.items():
                act=("R" if name=="never" else
                     "E" if name=="always_edit" and d!=m else
                     "E" if name=="only_early" and d!=m and t<T-1 else "R")
                if act=="R" and d==m:gains+=prob
                nm=1-m if act=="E" else m
                nextdist[(d,nm)]+=prob*p
                nextdist[(1-d,nm)]+=prob*(1-p)
            distribution=nextdist
        assert sum(distribution.values())==1
        return gains
    strategies={name:eval_public_rule(name) for name in ("never","always_edit","only_early")}
    reachable={}
    mass={(0,0):F(1,2),(1,0):F(1,2)}
    strict=[]
    for t in range(T):
        after=defaultdict(F)
        for (d,m),prob in mass.items():
            choices={a:q(t,d,m,a) for a in "RE"}
            win=max("RE",key=lambda a:choices[a])
            if choices["R"]!=choices["E"]:
                strict.append(dict(t=t,demand=d,mode=m,probability=prob,values=choices,best=win))
            nm=1-m if win=="E" else m
            after[(d,nm)]+=prob*p
            after[(1-d,nm)]+=prob*(1-p)
        mass=after
    return dict(p=p,optimum=optimum,public_rules=strategies,
                strict_on_optimal_path=strict,final_probability=sum(mass.values()))

def stringify(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):stringify(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [stringify(v) for v in x]
    return x

def main():
    cases={str(p):case(p) for p in (F(1,2),F(2,3),F(3,4),F(1))}
    assert cases["1/2"]["optimum"]==3
    assert cases["2/3"]["optimum"]==F(23,6)
    assert cases["3/4"]["optimum"]==F(17,4)
    assert cases["1"]["optimum"]==F(11,2)
    for p,entry in cases.items():
        assert entry["optimum"]==max(entry["public_rules"].values())
        assert entry["public_rules"]["only_early"]==entry["optimum"]
        assert entry["final_probability"]==1
    assert cases["1/2"]["public_rules"]["never"]==3
    assert cases["3/4"]["public_rules"]["never"]==3
    return stringify({
      "schema":"ssnoir.r198-r203.rule-edit.v1",
      "evidence":"exact finite public demand MDP + independent forward strategy evaluator",
      "R198_no_persistence":cases["1/2"],
      "R199_persistence_2_3":cases["2/3"],
      "R200_persistence_3_4":cases["3/4"],
      "R201_perfect_persistence":cases["1"],
      "R202_strong_public_rule_comparison":"Run on match, edit on mismatch unless last action. Matches optimal for all four environments.",
      "R203_novelty_judgement":"Even repeated editing here reduces to one memorable threshold. Ordinary switching cost / Markov persistence, not novel self-programming."
    })
if __name__=="__main__":print(json.dumps(main(),ensure_ascii=False,indent=2))
