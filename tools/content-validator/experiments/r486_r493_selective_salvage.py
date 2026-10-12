#!/usr/bin/env python3
from functools import lru_cache
from fractions import Fraction as F
import json

def value(bonus=3,wx=3,wy=2,audit=True,patch=True,known=False):
    @lru_cache(None)
    def v(t,x,y,px,py,k):
        if t==4:
            def s(th):
                a=x if th=='A' or px else 0
                b=y if th=='B' or py else 0
                return a+b+bonus*bool(a and b)
            return F(s(k)) if k else F(s('A')+s('B'),2)
        options=[v(t+1,x+(wx if t==0 else 1),y,px,py,k),
                 v(t+1,x,y+(wy if t==0 else 1),px,py,k)]
        if audit and not k:
            options.append((v(t+1,x,y,px,py,'A')+v(t+1,x,y,px,py,'B'))/2)
        if patch and k=='B' and not px:
            options.append(v(t+1,x,y,True,py,k))
        if patch and k=='A' and not py:
            options.append(v(t+1,x,y,px,True,k))
        return max(options)
    if known:return (v(0,0,0,False,False,'A')+v(0,0,0,False,False,'B'))/2
    return v(0,0,0,False,False,None)

def run():
    r={'R486_terminal_dependency':{'theta_A':3,'theta_B':1},
       'R487_full':value(),'R488_fixed_recipe':{'A':F(7),'B':F(7)},
       'R489_no_audit':value(audit=False),
       'R490_no_patch':value(patch=False),
       'R491_no_complement_bonus':value(bonus=0),
       'R492_no_window':value(wx=1,wy=1),
       'R493_known_start':value(known=True)}
    assert [r[k] for k in ['R487_full','R489_no_audit','R490_no_patch','R491_no_complement_bonus','R492_no_window','R493_known_start']]==[7,3,F(7,2),F(9,2),5,8]
    return {k:({z:str(w) for z,w in v.items()} if isinstance(v,dict) else str(v)) for k,v in r.items()}
if __name__=='__main__':print(json.dumps(run(),ensure_ascii=False,indent=2))
