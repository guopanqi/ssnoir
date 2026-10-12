#!/usr/bin/env python3
"""S3-III independent source-aware time-pressure counterfactual (paper only).

Conditioned on observing Eddie being attacked. A real arrival triggers a
same-day local window, not a globally visible quest deadline. Full model
matches the frozen S3-II toy, while control lets Eddie remain available
after that night; no other rule/probability is changed.
"""
from fractions import Fraction as F
from functools import lru_cache
import json,sys
from pathlib import Path

ROOT=(0,0,0,0,8)
ROLL=((0,F(1,3)),(1,F(1,3)),(2,F(1,3)))
def make_solver(expires):
    def available(s):
        d,o,l,e,n=s
        return ([("D")] if d<2 and n>0 else [])+(
            [("O")] if o<2 and n>4 else [])+(
            [("L")] if n>0 and n<=4 and o>=2 and l<2 else [])+(
            [("E")] if n>0 and e<2 and (n>4 or not expires) else [])
    def moves(s,act):
        d,o,l,e,n=s
        return [(p,(min(2,d+g) if act=="D" else d,
                    min(2,o+g) if act=="O" else o,
                    min(2,l+g) if act=="L" else l,
                    min(2,e+g) if act=="E" else e,n-1)) for g,p in ROLL]
    def reward(s):
        d,o,l,e,n=s
        return F(2)*F(d==2)+F(2)*F(l==2)+F(2)*F(d==2 and l==2)+F(3,2)*F(e==2)
    @lru_cache(None)
    def V(s):
        actions=available(s)
        return max((sum(p*V(t) for p,t in moves(s,x)) for x in actions),
                   default=reward(s))
    def Q(s):
        return {x:sum(p*V(t) for p,t in moves(s,x)) for x in available(s)}
    return V,Q,moves,available

def main():
    with_due,full_Q,moves,available=make_solver(True)
    without_due,no_Q,_,_=make_solver(False)
    assert without_due(ROOT)>=with_due(ROOT)
    expected_seen=F(0)
    strict_E_to_D=F(0)
    strict_D_to_E=F(0)
    strict_change_global=F(0)
    sample=[]
    seen=set()
    def forward(state,mass):
        nonlocal expected_seen,strict_E_to_D,strict_D_to_E,strict_change_global
        qs=full_Q(state)
        if not qs:return
        active=next(x for x in ("E","O","D","L") if x in qs and qs[x]==max(qs.values()))
        qs_no=no_Q(state)
        alt=next(x for x in ("E","O","D","L") if x in qs_no and qs_no[x]==max(qs_no.values()))
        if "E" in qs and "D" in qs:
            expected_seen+=mass
            if qs["E"]>qs["D"] and qs_no["E"]<qs_no["D"]:
                strict_E_to_D+=mass
            if qs["D"]>qs["E"] and qs_no["D"]<qs_no["E"]:
                strict_D_to_E+=mass
            if active!=alt and qs[active]>qs[alt] and qs_no[alt]>qs_no[active]:
                strict_change_global+=mass
                if len(sample)<6:
                    sample.append({"state":list(state),"one_history_mass":str(mass),
                        "full_action":active,"no_deadline_action":alt,
                        "full_Q":{k:str(v) for k,v in qs.items()},
                        "no_deadline_Q":{k:str(v) for k,v in qs_no.items()}})
        for p,next_state in moves(state,active):
            forward(next_state,mass*p)
    forward(ROOT,F(1))
    report={"study":"S3-III public arrival window deadline versus forever Eddie",
        "identity":"Exact Fraction, conditional after player enters bar; not official C# Scheme, not human",
        "full_optimal":str(with_due(ROOT)),
        "remove_only_seen_eddie_deadline":str(without_due(ROOT)),
        "opportunity_cost_of_deadline":str(without_due(ROOT)-with_due(ROOT)),
        "both_eddie_dock_legal_expected_visits":str(expected_seen),
        "strict_due_E_vs_no_due_D_visits":str(strict_E_to_D),
        "strict_due_D_vs_no_due_E_visits":str(strict_D_to_E),
        "strict_global_action_reversal_expected_visits":str(strict_change_global),
        "normal_path_witnesses":sample,
        "source_limits":["Eddie side story current runtime entry side-story-open? #f",
                         "waiting for player arrival before timed response",
                         "no public urgency prior to discovery",
                         "2-progress Eddie is research cost proxy, not actual fight outcome"]}
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf8")
    else:print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=="__main__":main()
