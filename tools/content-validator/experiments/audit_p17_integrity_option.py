#!/usr/bin/env python3
"""P17 evidence contamination: does the 'force a name' button actually help?

Exact four-die FateStrip toy abstraction of Scheme 实验·证词掺水.scm:
Listen 0/1/2 progress, Force +2 progress/+1 falsehood, Verify -1 progress/
-1 falsehood. Police arrive when dice run out; credible report requires
progress >=4 and falsehood==0. Reporting early once credible ends the round.

Excludes composure and hospitalization; a deliberately marked limited paper
ablation, NOT native Session evidence or a claim that human players enjoy it.
"""
from itertools import combinations_with_replacement,product
from functools import lru_cache
from fractions import Fraction as F
import json
from r56_r65_autonomous_search import odds,hand_weight

HANDS=tuple(combinations_with_replacement(range(1,7),4))
def go(force_allowed=True):
    @lru_cache(None)
    def optimal(progress,lies,hand):
        if progress>=4 and lies==0:return F(1)
        if not hand:return F(0)
        acts=[0]+([1] if force_allowed else [])+([2] if lies else [])
        return max(branch(progress,lies,hand,a,i,optimal)
                   for a in acts for i in range(len(hand)))
    def branch(p,l,hand,a,i,cont):
        rest=hand[:i]+hand[i+1:]
        if a==1:return cont(min(8,p+2),min(4,l+1),rest)
        if a==2:return cont(max(0,p-1),l-1,rest)
        return sum(prob*cont(min(8,p+gain),l,rest)
                   for gain,prob in enumerate(odds(hand[i])) if prob)
    @lru_cache(None)
    def fixed(p,l,hand,script):
        if p>=4 and l==0:return F(1)
        if not hand:return F(0)
        action=script[0]
        if action==1 and not force_allowed:action=0
        if action==2 and not l:action=0
        return max(branch(p,l,hand,action,i,lambda pp,ll,rr:fixed(pp,ll,rr,script[1:]))
                   for i in range(len(hand)))
    values={h:optimal(0,0,h) for h in HANDS}
    scripts=tuple(product((0,1,2),repeat=4))
    strong={h:max(fixed(0,0,h,s) for s in scripts) for h in HANDS}
    assert all(values[h]>=strong[h] for h in HANDS)
    def avg(a):return round(float(sum(hand_weight(h)*a[h] for h in HANDS)),9)
    return {"optimal":avg(values),"strong_script":avg(strong),
      "strict_hands_over_strong_script":sum(values[h]>strong[h] for h in HANDS)},values

def main():
    yes,y=go(True);no,n=go(False)
    assert all(y[h]>=n[h] for h in HANDS)
    def avg_diff():return round(float(sum(hand_weight(h)*(y[h]-n[h]) for h in HANDS)),9)
    diff=avg_diff()
    result={"id":"P17-causal-option-audit",
        "with_force_button":yes,"without_force_button":no,
        "direct_option_gain":diff,
        "strict_root_hands_force_helpful":sum(y[h]>n[h] for h in HANDS),
        "model_limit":"Composure, persistent injury and interactive submission cadence excluded; full Scheme reward/health may differ.",
        "question":"Does introducing then cleaning misinformation ever beat simply listening without forced pollution in the normal four-die capacity?"}
    print(json.dumps(result,ensure_ascii=False,indent=2))
if __name__=="__main__":main()
