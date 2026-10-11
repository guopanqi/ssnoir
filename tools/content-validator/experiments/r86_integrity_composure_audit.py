#!/usr/bin/env python3
"""R86: P17 contamination/cleaning under explicit composure failure.

Tied to the actual P17 actions, but paper approximation: four FateStrip d6,
four actions, composure1/2/3, zero composure means immediate collapse,
free submission when testimony >=4 AND falsehood0. Scheme's full actor
injury, time, and UI state are NOT simulated. Exact rational policy, all
126 weighted initial hands. Include removal of forced questioning as the
feature ablation and a powerful fixed action-order family with adaptive dice.
"""
import json
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement,product
from r56_r65_autonomous_search import odds,hand_weight

H=tuple(combinations_with_replacement(range(1,7),4))
PLANS=tuple(product((0,1,2),repeat=4)) # listen / demand / clean

def evaluate(health,forced):
    @lru_cache(None)
    def dp(progress,falsehood,composure,hand):
        if composure<=0:return F(0)
        if progress>=4 and falsehood==0:return F(1)
        if not hand:return F(0)
        options=[a for a in ((0,1,2) if forced else (0,2)) if a!=2 or falsehood>0]
        return max(expected(progress,falsehood,composure,hand,a,i,dp)
                   for a in options for i in range(len(hand)))
    def expected(p,f,h,hand,a,index,continuation):
        remaining=hand[:index]+hand[index+1:]
        if a==1:
            return continuation(min(8,p+2),min(4,f+1),h,remaining)
        if a==2:
            return continuation(max(0,p-1),max(0,f-1),h,remaining)
        return sum(prob*continuation(min(8,p+gain),f,h-int(gain==0),remaining)
                   for gain,prob in enumerate(odds(hand[index])) if prob)
    @lru_cache(None)
    def fixed(p,f,h,hand,plan):
        if h<=0:return F(0)
        if p>=4 and f==0:return F(1)
        if not hand:return F(0)
        a=plan[0]
        if a==2 and f==0:a=0
        if a==1 and not forced:a=0
        return max(expected(p,f,h,hand,a,i,lambda pp,ff,hh,rem:fixed(pp,ff,hh,rem,plan[1:]))
                   for i in range(len(hand)))
    avg=F(0);root_tab={}
    for hand in H:
        ans=dp(0,0,health,hand)
        root_tab[hand]=ans
        avg+=hand_weight(hand)*ans
    fixed_total=F(0);strict_fixed=0
    if forced:
        for hand in H:
            baseline=max(fixed(0,0,health,hand,plan) for plan in PLANS)
            assert root_tab[hand]>=baseline
            fixed_total+=hand_weight(hand)*baseline
            strict_fixed+=root_tab[hand]>baseline

    def q_values(p,f,h,hand):
        options=[a for a in ((0,1,2) if forced else (0,2)) if a!=2 or f>0]
        return {(a,i):expected(p,f,h,hand,a,i,dp) for a in options for i in range(len(hand))}
    @lru_cache(None)
    def forward(p,f,h,hand):
        # [forced count, cleanup count, early clean count,
        #  first strict early clean, successful completion probability]
        if h<=0:return (F(0),)*5
        if p>=4 and f==0:return (F(0),F(0),F(0),F(0),F(1))
        if not hand:return (F(0),)*5
        choice=q_values(p,f,h,hand)
        best=max(choice.values())
        # Prefer listen on ties, then forced question, then cleanup.
        a,i=next((a,i) for a,i in choice if choice[(a,i)]==best)
        strict_clean=int(a==2 and len(hand)>1 and
                         best>max((v for (aa,j),v in choice.items() if aa!=2),default=F(-1)))
        masses=[F(a==1),F(a==2),F(a==2 and len(hand)>1),F(strict_clean),F(0)]
        rem=hand[:i]+hand[i+1:]
        branches=[]
        if a==1:branches=[(F(1),min(8,p+2),min(4,f+1),h)]
        elif a==2:branches=[(F(1),max(0,p-1),max(0,f-1),h)]
        else:branches=[(prob,min(8,p+gain),f,h-int(gain==0))
                       for gain,prob in enumerate(odds(hand[i])) if prob]
        for prob,pp,ff,hh in branches:
            row=forward(pp,ff,hh,rem)
            for k in range(5):masses[k]+=prob*row[k]
        return tuple(masses)

    path=[F(0)]*5
    for hand in H:
        w=hand_weight(hand)
        row=forward(0,0,health,hand)
        for j in range(5):path[j]+=w*row[j]
    assert abs(float(path[4]-avg))<1e-9
    return {
       "health":health,"allow_force":forced,
       "expected_credible_completion":round(float(avg),9),
       "strong_fixed_order_with_adaptive_die":round(float(fixed_total),9) if forced else None,
       "adaptive_over_fixed_order":round(float(avg-fixed_total),9) if forced else None,
       "strict_root_hands_over_fixed_order":strict_fixed if forced else None,
       "policy_expected_forced_count":round(float(path[0]),9),
       "policy_expected_cleanup_count":round(float(path[1]),9),
       "policy_expected_early_cleanup_count":round(float(path[2]),9),
       "policy_strictly_useful_early_cleanup_count":round(float(path[3]),9),
       "_per_root":root_tab
    }

def main():
    rows=[]
    for hp in (1,2,3):
        full=evaluate(hp,True)
        limited=evaluate(hp,False)
        assert all(full["_per_root"][h]>=limited["_per_root"][h] for h in H)
        witnesses=sum(full["_per_root"][h]>limited["_per_root"][h] for h in H)
        option=full["expected_credible_completion"]-limited["expected_credible_completion"]
        full.pop("_per_root");limited.pop("_per_root")
        rows.append({"composure":hp,"with_forced_questioning":full,
                     "without_forced_questioning":limited,
                     "option_gain":round(option,9),
                     "roots_where_forced_questioning_strictly_improves":witnesses})
    out={"id":"R86","focus":"P17: is asking contaminating questions and removing falsehood genuinely valuable under composure failure?",
        "scope":"Abstract approximation of native P17 including composure threshold, not exact Unity injury / hospital rules",
        "results":rows}
    import sys
    text=json.dumps(out,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(text,encoding="utf-8")
    else:print(text)
if __name__=="__main__":main()
