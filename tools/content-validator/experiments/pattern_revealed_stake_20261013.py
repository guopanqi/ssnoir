#!/usr/bin/env python3
"""STAKE04: protected A/B requirement revealed before or after first hand.

Uses unchanged CUST transitions and rewards. Demand high/low also becomes
public after first hand. Optimize reward subject to named goal completion
in EVERY branch; this is a constraint, not a higher numerical A/B prize.
"""
from fractions import Fraction as F
from itertools import product
from pattern_evidence_custody_20261013 import ACTS,INIT,legal,advance

MODES=('original','copy','matched_preserve','reusable')
REQUIREMENTS=('A','B')

def next_state(mode,st,a):
    return advance(mode,st,a) if legal(mode,st,a,True) else None

def vec(st):
    a,b,s,orig,ever,cp,shown=st
    return F(a==2 and orig),F(b==2),F(shown),F(s)

def reward(v,high):
    a,b,c,s=v
    return 8*a+9*b+12*int(high)*c+s

def best_with_first(mode,first,required,high):
    start=next_state(mode,INIT,first)
    if start is None:return None
    candidates=[(start,(first,))]
    for _ in range(3):
        candidates=[(ns,p+(a,)) for st,p in candidates
                    for a in ACTS if (ns:=next_state(mode,st,a)) is not None]
    kept=[(reward(vec(st),high),path,vec(st))
          for st,path in candidates if vec(st)['AB'.index(required)]==1]
    return max(kept,default=None,key=lambda x:(x[0],str(x[1])))

def solve(mode,early=False):
    initial={}
    for first in ('A','B','SIDE'):
        initial[first]={(required,high):best_with_first(mode,first,required,high)
             for required,high in product(REQUIREMENTS,(False,True))}
    if not early:
        options=[(sum(v[0] for v in branches.values())/4,first,branches)
             for first,branches in initial.items() if all(v is not None for v in branches.values())]
        return max(options,key=lambda x:x[0]),initial
    options=[]
    for required in REQUIREMENTS:
        choices=[]
        for first,branches in initial.items():
            if any(branches[(required,high)] is None for high in (False,True)):continue
            score=sum(branches[(required,high)][0] for high in (False,True))/2
            choices.append((score,first))
        options.append(max(choices))
    return sum(x[0] for x in options)/2,options

def main():
    results={}
    for mode in MODES:
        late,whole=solve(mode,False)
        early,early_first=solve(mode,True)
        results[mode]=(late[0],early)
        print(mode,'late_requirement',late[0],'first',late[1],
              'early_requirement',early,'best_first_by_requirement',early_first)
        for (req,high),item in late[2].items():
            print(' ',req,'HIGH?',high,'score/plan/outcomes',item)
        assert early>=late[0]
    assert results=={
       'original':(F(18),F(18)),
       'copy':(F(75,4),F(75,4)),
       'matched_preserve':(F(75,4),F(75,4)),
       'reusable':(F(19),F(19))
    },results
    print('PASS protected identity matters, but early knowledge has zero value with robust first A')

if __name__=='__main__':main()
