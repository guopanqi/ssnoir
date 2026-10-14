#!/usr/bin/env python3
"""DISC-02: productive noisy evidence vs perfectly reliable dedicated inspection.

Three actions, one latent target T=A/B at prior 1/2, one noisy observation
after the FIRST productive work, exact Bayesian posterior, no leaked type.
An INSPECT action instead perfectly reveals T, but creates no task progress.
Evidence accuracy q: P(report=T | T)=q. No repeated independent work reports.
"""
from fractions import Fraction as Q
from functools import lru_cache

def solve(q):
    assert Q(1,2)<=q<=1
    @lru_cache(None)
    def go(left,pa,pb,completed,belief,reported):
        if left==0:return (Q(0),())
        options=[]
        def consider(name,branches):
            v=sum(prob*(gain+go(*st)[0]) for prob,gain,st in branches)
            options.append((v,name))
        consider("SIDE",[(Q(1),Q(2),(left-1,pa,pb,completed,belief,reported))])
        if belief not in (Q(0),Q(1)):
            consider("INSPECT",[
                (belief,Q(0),(left-1,pa,pb,completed,Q(1),reported)),
                (1-belief,Q(0),(left-1,pa,pb,completed,Q(0),reported))])
        for which,name in ((0,"WORK_A"),(1,"WORK_B")):
            if completed & (1<<which):continue
            a,b=pa+(which==0),pb+(which==1)
            if (a if which==0 else b)==2:
                branches=[
                    (belief,Q(8) if which==0 else Q(0),
                     (left-1,a,b,completed|(1<<which),Q(1),reported)),
                    (1-belief,Q(8) if which==1 else Q(0),
                     (left-1,a,b,completed|(1<<which),Q(0),reported))]
                branches=[x for x in branches if x[0]>0]
            elif not reported:
                chance_A=belief*q+(1-belief)*(1-q)
                chance_B=1-chance_A
                branches=[]
                if chance_A>0:
                    posterior_A=belief*q/chance_A
                    branches.append((chance_A,Q(0),(left-1,a,b,completed,
                                                     posterior_A,True)))
                if chance_B>0:
                    posterior_B=belief*(1-q)/chance_B
                    branches.append((chance_B,Q(0),(left-1,a,b,completed,
                                                     posterior_B,True)))
            else:
                branches=[(Q(1),Q(0),(left-1,a,b,completed,belief,reported))]
            consider(name,branches)
        return max(options,key=lambda x:x[0])
    return go(3,0,0,0,Q(1,2),False),go

def readable(q):
    """All meaningful short scripts at prior 1/2.

    WORK first / report match -> complete job+SIDE (expected posterior reward
    8q+2). Mismatch -> either switch tasks twice (8q), or ignore report and
    finish the worked job+SIDE (8(1-q)+2). INSPECT=>complete chosen target=8.
    """
    continue_wrong=8*(1-q)+2
    switch=8*q
    work=(8*q+2+max(continue_wrong,switch))/2
    return {"inspect":Q(8),"work-and-react":work,
            "work-blind":Q(6)}

def run():
    for q in (Q(1,2),Q(3,5),Q(3,4),Q(7,8),Q(9,10),Q(1)):
        (value,act),go=solve(q)
        scripts=readable(q)
        assert value==max(scripts.values()),(q,value,scripts)
        if q<Q(7,8):assert (value,act)==(Q(8),"INSPECT")
        if q>Q(7,8):assert (value,act)==(8*q+1,"WORK_A")
        if q==Q(7,8):assert value==Q(8)
        print("DISC-02",{"accuracy":str(q),"oracle":str(value),"first":act,
                         "scripts":{k:str(v) for k,v in scripts.items()}})
    # A noisy signal below 5/8 has insufficient posterior weight to justify
    # changing an already-progressing goal in the final two actions.
    assert 8*Q(3,5) < 8*(1-Q(3,5))+2
    assert 8*Q(3,4) > 8*(1-Q(3,4))+2
    # A reliable but imperfect signal (3/4) STILL loses to one exact INSPECT.
    assert solve(Q(3,4))[0][0]==Q(8)
    # q=1 reproduces DISC-01's perfect, free first-work observation.
    assert solve(Q(1))[0][0]==Q(9)
    print("PASS DISC-02 exact belief update, 5/8 reaction boundary, 7/8 work-vs-inspection boundary")

if __name__=="__main__":run()
