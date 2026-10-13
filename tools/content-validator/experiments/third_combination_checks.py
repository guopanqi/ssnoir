#!/usr/bin/env python3
"""PL-C05: exact comparison of early preparation and same-cost on-site enable."""
from fractions import Fraction as F
from functools import lru_cache
import json

HALF=F(1,2)
REPAIR=F(3,4)

def solve(q,early,onsite,goal=5,turns=4):
    @lru_cache(None)
    def value(t,p,blocked,ready):
        if p>=goal: return F(1)
        if not t: return F(0)
        if blocked:
            opts=[value(t-1,p+(2 if ready else 1),True,ready),
                REPAIR*value(t-1,p,False,ready)+(1-REPAIR)*value(t-1,p,True,ready)]
            if onsite and not ready: opts.append(value(t-1,p,True,True))
            return max(opts)
        push=sum((HALF*((1-q)*value(t-1,p+d,False,ready)+q*value(t-1,p+d,True,ready)) for d in (1,2)),F(0))
        opts=[push]
        if early and not ready: opts.append(value(t-1,p,False,True))
        return max(opts)
    return value(turns,0,False,False)

def main():
    rows=[]
    for q in [F(0),F(1,4),F(1,2),F(1)]:
        ans={label:solve(q,e,s) for label,e,s in
            [('none',False,False),('early_only',True,False),
             ('onsite_only',False,True),('both',True,True)]}
        assert ans['both']==ans['onsite_only'],ans
        assert ans['onsite_only']>=ans['early_only']>=ans['none'],ans
        rows.append({'q':str(q),**{k:str(v) for k,v in ans.items()}})
    assert rows[1]['onsite_only']=='931/1024'
    assert rows[2]['onsite_only']=='117/128'
    print(json.dumps({'rows':rows,'scope':'Exact toy model only; no human or engine validation'},ensure_ascii=False,indent=2))

if __name__=='__main__': main()
