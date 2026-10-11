#!/usr/bin/env python3
"""R83: does a genuinely expiring public offer create conditional decisions?

Four visible d6, each prepared roll yields 0/1/2 preparation. Four steps:
at the beginning of step 3 an early offer (2 units => reward 1) expires,
and preparation loses one unit; late offer at end (4 units => reward 3).
At early window (after first two actions), agent can bank early immediately
or decline it to seek late. All chance rolls are fully known distributions.
No hidden information, no human or native evidence.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial
import json
from r56_r65_autonomous_search import odds
def wt(h):
    v=F(factorial(4),6**4)
    for d in set(h):v/=factorial(h.count(d))
    return v
@lru_cache(None)
def future(progress,hand):
    if not hand:return F(3 if progress>=4 else 0)
    return max(sum(pr*future(min(6,progress+gain),hand[:i]+hand[i+1:])
                   for gain,pr in enumerate(odds(hand[i])) if pr)
               for i in range(len(hand)))
@lru_cache(None)
def first(progress,hand,policy,expiry):
    used=4-len(hand)
    if used==2:
        after=max(0,progress-(1 if expiry else 0))
        later=future(after,hand)
        can=progress>=2
        if not can:return later
        if policy=="take":return max(F(1),F(0)) # always take whenever possible
        if policy=="wait":return later
        return max(F(1),later)
    return max(sum(pr*first(min(6,progress+gain),hand[:i]+hand[i+1:],policy,expiry)
                   for gain,pr in enumerate(odds(hand[i])) if pr)
               for i in range(len(hand)))
def run(expiry):
    hands=list(combinations_with_replacement(range(1,7),4))
    vals={}
    for p in ("free","take","wait"):
        vals[p]=sum(wt(h)*first(0,h,p,expiry) for h in hands)
    strict=sum(first(0,h,"free",expiry)>max(first(0,h,"take",expiry),first(0,h,"wait",expiry))
               for h in hands)
    return {"expiring":expiry,**{k:round(float(v),9) for k,v in vals.items()},
            "free_over_best_prior_fixed":round(float(vals["free"]-max(vals["take"],vals["wait"])),9),
            "root_hands_free_strictly_better_than_both":strict}
def main():
    a=run(True);b=run(False)
    assert a["free"]>=a["take"] and a["free"]>=a["wait"]
    assert b["free"]>=b["take"] and b["free"]>=b["wait"]
    print(json.dumps({"id":"R83","hypothesis":"the short public offer disappears and preparation decays if ignored",
      "expiring_offer":a,"no_decay_control":b,
      "limitations":"No native C#/human evidence; early claim exits whole interaction; only four d6; optimistic perfect knowledge of public probabilities."},ensure_ascii=False,indent=2))
if __name__=="__main__":main()
