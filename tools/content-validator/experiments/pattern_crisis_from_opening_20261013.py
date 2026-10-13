#!/usr/bin/env python3
"""X01 full-start audit: is the alternative goal naturally reachable?

Two preparation actions from A=2/3 B=0/3, then a publicly revealed lock
(crisis) and two response actions. Preparation has safe SIDE or B investment.
Fractions, exhaustive memoized backward induction and strong public scripts.
This is deliberately an interpretable known-mode composition, not human play.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
from collections import defaultdict

A_REWARD=F(3)
B_REWARD=F(6)
SIDE=F(23,10)
PREP_STEPS=2
LATE_STEPS=2

def respond(b,side,locked,b_available=True):
    """Two final actions: A completes once if unlocked, B at progress 3,
    FIX unlocks A; SIDE yields immediate 23/10. Count rewards only once.
    """
    @lru_cache(None)
    def go(step,a,b,lock,done_a,done_b):
        if step==LATE_STEPS:return (F(0),())
        options=[]
        available=["SIDE"]
        if lock:available.append("FIX")
        elif not done_a:available.append("A")
        if b_available and not done_b:available.append("B")
        for x in available:
            na,nb,nlock,da,db=a,b,lock,done_a,done_b
            reward=F(0)
            if x=="SIDE":reward=SIDE
            elif x=="FIX":nlock=False
            elif x=="A":
                na+=1
                if na>=3:reward=A_REWARD;da=True
            elif x=="B":
                nb+=1
                if nb>=3:reward=B_REWARD;db=True
            val,tail=go(step+1,na,nb,nlock,da,db)
            options.append((reward+val,(x,)+tail))
        return max(options,key=lambda x:x[0])
    value,seq=go(0,2,b,bool(locked),False,False)
    return value+side*SIDE,seq

def full(prob_crisis,b_available=True,lock_matters=True):
    q=F(prob_crisis)
    assert 0<=q<=1
    @lru_cache(None)
    def prep(step,b,side):
        if step==PREP_STEPS:
            locked_value,locked_seq=respond(b,side,lock_matters, b_available)
            clear_value,clear_seq=respond(b,side,False,b_available)
            return (q*locked_value+(1-q)*clear_value,(),
                    (locked_value,locked_seq),(clear_value,clear_seq))
        candidates=[]
        for act in (("SIDE","B") if b_available else ("SIDE",)):
            child=prep(step+1,b+(act=="B"),side+(act=="SIDE"))
            candidates.append((child[0],(act,)+child[1],*child[2:]))
        return max(candidates,key=lambda x:x[0])
    return prep(0,0,0)

def fixed_preparation(q,sequence,lock_matters=True,b_available=True):
    assert len(sequence)==2
    assert all(x in ("B","SIDE") for x in sequence)
    b=sequence.count("B");s=sequence.count("SIDE")
    in_crisis=respond(b,s,lock_matters,b_available)
    no_crisis=respond(b,s,False,b_available)
    return q*in_crisis[0]+(1-q)*no_crisis[0],in_crisis,no_crisis

def main():
    cases=[(F(1,4),"SIDE",F(373,40)),(F(3,4),"B",F(339,40))]
    for q,first_action,expected in cases:
        best=full(q)
        assert best[0]==expected,(q,best)
        assert best[1]==((first_action,)*2)
        accessible={s:fixed_preparation(q,s) for s in product(("SIDE","B"),repeat=2)}
        assert max(v[0] for v in accessible.values())==best[0]
        print("X01 opening",str(q),"optimal",str(best[0]),
              "early",best[1],"crisis",best[2],
              "clear",best[3])
        print("X01 full 4 early paths",
              {"".join(s):str(v[0]) for s,v in accessible.items()})
        if first_action=="SIDE":
            assert best[2][1]==("FIX","A")
        else:
            assert best[2][1] in (("B","SIDE"),("SIDE","B"))
        assert best[3][1] in (("A","SIDE"),("SIDE","A"),("A","B"),("B","A"))
    # At threshold 9/16 both prep routes can be optimal; the oracle's
    # tie behavior is not evidence for two distinct strict decisions.
    bound=F(9,16)
    ss=fixed_preparation(bound,("SIDE","SIDE"))[0]
    bb=fixed_preparation(bound,("B","B"))[0]
    ab=fixed_preparation(bound,("SIDE","B"))[0]
    assert ss==bb>ab
    for q in (F(0),F(1,4),bound,F(3,4),F(1)):
        oracle=full(q)[0]
        ss=fixed_preparation(q,("SIDE","SIDE"))[0]
        bb=fixed_preparation(q,("B","B"))[0]
        ab=fixed_preparation(q,("SIDE","B"))[0]
        assert oracle==max(ss,bb)
        assert ss==F(99,10)-F(23,10)*q
        assert bb==F(9)-F(7,10)*q
        assert ab==F(83,10)
        assert full(q,lock_matters=False)[1]==("SIDE","SIDE")
        assert full(q,b_available=False)[1]==("SIDE","SIDE")
        print("X01 controls",str(q),"oracle",str(oracle),
              "SS",str(ss),"BB",str(bb),"AB",str(ab),
              "no lock",str(full(q,lock_matters=False)[0]),
              "no B",str(full(q,b_available=False)[0]))
    # Symmetric sources: old local B=2 is reached with two B prep actions
    # and old B=0 with two SIDE actions, from exactly the same start.
    assert fixed_preparation(F(3,4),("B","B"))[1][1] in (
        ("B","SIDE"),("SIDE","B"))
    print("PASS X01 full opening reachable, crisis effects, simple threshold controls")

if __name__=="__main__":main()
