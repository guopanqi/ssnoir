#!/usr/bin/env python3
"""R89: Does a PUBLIC intermediate integrity inspection cause voluntary early cleanup?

P17 inspired, but NEW HYPOTHETICAL checkpoint is NOT in the original
'实验·证词掺水.scm' scene. No native engine or human evidence.

4 observed dice; exact Fraction optimal policy, health2; listen bad costs 1
composure, good adds 2 progress, medium adds 1. Forced questioning adds
2 progress+1 falsehood; cleanup costs a die and removes one progress and
one falsehood. Free successful submission at progress>=4 and falsehood==0.
One inspection after action #2 or #3, with 0%, 50% or 100% probability
of immediate failure *if* falsehood is visible; no secret future info.
"""
from functools import lru_cache
from itertools import combinations_with_replacement
from fractions import Fraction as F
import json, sys
from r56_r65_autonomous_search import odds,hand_weight
H=tuple(combinations_with_replacement(range(1,7),4))

def screen(checkpoint:int,inspection_chance:F):
    assert checkpoint in (2,3)
    @lru_cache(None)
    def q(p,f,h,hand,act,index):
        rem=hand[:index]+hand[index+1:]
        vals=( ((0,F(1)),) if act in (1,2) else tuple(enumerate(odds(hand[index]))) )
        expected=F(0)
        for gain,pr in vals:
            if act==0: pp,ff,hh=min(8,p+gain),f,h-int(gain==0)
            elif act==1:pp,ff,hh=min(8,p+2),min(4,f+1),h
            else:pp,ff,hh=max(0,p-1),f-1,h
            # Checkpoint triggers immediately after Nth action. Failed
            # inspection ends the attempt, successful inspection continues.
            if len(hand)==5-checkpoint and ff>0:
                pr*=1-inspection_chance
            expected+=pr*best(pp,ff,hh,rem)
        return expected
    @lru_cache(None)
    def best(p,f,h,hand):
        if h<=0:return F(0)
        if p>=4 and not f:return F(1)
        if not hand:return F(0)
        return max(q(p,f,h,hand,a,i) for a in (0,1,2)
                   if a!=2 or f>0 for i in range(len(hand)))
    @lru_cache(None)
    def forward(p,f,h,hand):
        if h<=0 or (p>=4 and f==0) or not hand:
            return F(0),F(0),F(0)
        choices=[(q(p,f,h,hand,a,i),a,i) for a in (0,1,2)
                 if a!=2 or f>0 for i in range(len(hand))]
        score=max(x[0] for x in choices)
        # Tie break: listen, force, clean; smaller-die index.
        _,act,i=next(x for x in choices if x[0]==score)
        strict=act==2 and len(hand)>1 and score>max(
            (x[0] for x in choices if x[1]!=2),default=F(-1))
        row=[F(act==2 and len(hand)>1),F(strict),F(act==1)]
        vals=( ((0,F(1)),) if act in (1,2) else tuple(enumerate(odds(hand[i]))) )
        for gain,prob in vals:
            if act==0: pp,ff,hh=min(8,p+gain),f,h-int(gain==0)
            elif act==1: pp,ff,hh=min(8,p+2),min(4,f+1),h
            else:pp,ff,hh=max(0,p-1),f-1,h
            if len(hand)==5-checkpoint and ff>0:
                prob*=1-inspection_chance
            child=forward(pp,ff,hh,hand[:i]+hand[i+1:])
            for k in range(3):row[k]+=prob*child[k]
        return tuple(row)
    win=sum(hand_weight(h)*best(0,0,2,h) for h in H)
    path=[sum(hand_weight(h)*forward(0,0,2,h)[k] for h in H)
          for k in range(3)]
    return dict(inspection_after_action=checkpoint,
        chance_if_contaminated=float(inspection_chance),
        credible_completion=round(float(win),9),
        expected_early_cleanup=round(float(path[0]),9),
        strict_early_cleanup=round(float(path[1]),9),
        expected_forced_questioning=round(float(path[2]),9))
def main():
    cases=[screen(t,p) for t in (2,3) for p in (F(0),F(1,2),F(1))]
    assert cases[0]["credible_completion"]==cases[1]["credible_completion"]
    assert cases[1]["strict_early_cleanup"]==0
    assert cases[4]["strict_early_cleanup"]>0.25
    assert cases[4]["credible_completion"]<cases[3]["credible_completion"]
    assert cases[4]["credible_completion"]==cases[5]["credible_completion"]
    result={"id":"R89","scope":"P17-inspired counterfactual paper model ONLY; checkpoint does not exist in original native scene",
      "finding":"Inspection timing #3, unlike #2, makes STRICTLY beneficial early cleanup occur along selected optimal paths. This does not prove storytelling or human enjoyment.",
      "cases":cases,"provenance":"126 exactly weighted d6 hands, Fraction DP; skill1; composure 2; public action-count checkpoint"}
    txt=json.dumps(result,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(txt,encoding="utf8")
    else:print(txt)
if __name__=="__main__":main()
