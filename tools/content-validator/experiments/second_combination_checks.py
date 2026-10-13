#!/usr/bin/env python3
"""PL-C03: minimal exact model of preparation before probabilistic route crisis."""
from functools import lru_cache
from fractions import Fraction as F
import json

HALF=F(1,2)
REPAIR=F(3,4)

def make(goal=5, q=HALF, can_prepare=True):
    @lru_cache(None)
    def value(t,p,blocked,prepared):
        if p>=goal: return F(1)
        if not t: return F(0)
        return max(dict(actions(t,p,blocked,prepared)).values())
    def actions(t,p,blocked,prepared):
        if not t or p>=goal: return []
        if blocked:
            return [('detour',value(t-1,p+(2 if prepared else 1),True,prepared)),
                    ('repair',REPAIR*value(t-1,p,False,prepared)+(1-REPAIR)*value(t-1,p,True,prepared))]
        advance=sum((HALF*((1-q)*value(t-1,p+dp,False,prepared)+q*value(t-1,p+dp,True,prepared)) for dp in (1,2)),F(0))
        ans=[('advance',advance)]
        if can_prepare and not prepared: ans.append(('prepare',value(t-1,p,False,True)))
        return ans
    return value,actions

def main():
    rows=[]
    for q in [F(0),F(1,4),F(1,2),F(1)]:
        v,a=make(q=q)
        r=dict(a(4,0,False,False))
        no,_=make(q=q,can_prepare=False)
        rows.append({'q':str(q),'advance':str(r['advance']),'prepare':str(r['prepare']),
                     'best':max(r,key=r.get),'no_prepare':str(no(4,0,False,False))})
    assert [(r['q'],r['advance'],r['prepare'],r['best']) for r in rows]==[
        ('0','15/16','1/2','advance'),('1/4','821/1024','43/64','advance'),
        ('1/2','11/16','13/16','prepare'),('1','1/2','1','prepare')]
    _,a=make(q=HALF)
    later=dict(a(3,1,False,False))
    assert later=={'advance':F(21,32),'prepare':F(3,8)}
    print(json.dumps({'rows':rows,'after_progress1_no_crisis':{k:str(v) for k,v in later.items()},
        'limits':'Exact frozen abstract rules; not human, Unity, or commercial game validation'},ensure_ascii=False,indent=2))

if __name__=='__main__': main()
