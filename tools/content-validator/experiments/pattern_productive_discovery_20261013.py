#!/usr/bin/env python3
"""DISC-01: learning while doing vs pure research, feedback delay, route switches.

Belief-aware exact DP in a three-action encounter with an unchanging hidden
target T in {A,B}. No policy sees an unobserved type. A software oracle cannot
establish novelty, narrative meaning, or actual human player understanding.
"""
from fractions import Fraction as Q
from functools import lru_cache

REWARD, SIDE = Q(8), Q(2)

def solve(p=Q(1,2), *, reveal="early", switching_cost=False, inspection=True):
    assert reveal in ("early","late","never")
    assert 0<=p<=1
    @lru_cache(None)
    def go(remaining,a,b,completed,known,last,adapted,works):
        if not remaining:return (Q(0),())
        choices=[]
        def consider(action,branches):
            expected=sum(prob*(immediate+go(*state)[0])
                         for prob,immediate,state in branches)
            choices.append((expected,action))
        consider("SIDE",[(Q(1),SIDE,(remaining-1,a,b,completed,known,last,adapted,works))])
        if inspection and known==-1:
            consider("INSPECT",[
                (p,Q(0),(remaining-1,a,b,completed,0,last,adapted,works)),
                (1-p,Q(0),(remaining-1,a,b,completed,1,last,adapted,works))])
        if switching_cost and last!=-1 and not adapted:
            consider("ADAPT",[(Q(1),Q(0),(remaining-1,a,b,completed,known,last,True,works))])
        for which,name in ((0,"WORK_A"),(1,"WORK_B")):
            if completed & (1<<which):continue
            if switching_cost and last!=-1 and last!=which and not adapted:continue
            new_a,new_b=a+(which==0),b+(which==1)
            finished=(new_a if which==0 else new_b)==2
            if finished:
                # Closing a wrong task consumes both work actions and yields zero.
                types=((Q(1),known),) if known!=-1 else ((p,0),(1-p,1))
                branches=[(prob,REWARD if t==which else Q(0),
                           (remaining-1,new_a,new_b,completed | (1<<which),
                            t,which,False,works+1)) for prob,t in types]
            elif known==-1 and (reveal=="early" or
                                  (reveal=="late" and works+1>=2)):
                branches=[
                    (p,Q(0),(remaining-1,new_a,new_b,completed,0,which,False,works+1)),
                    (1-p,Q(0),(remaining-1,new_a,new_b,completed,1,which,False,works+1))]
            else:
                branches=[(Q(1),Q(0),(remaining-1,new_a,new_b,completed,
                                     known,which,False,works+1))]
            consider(name,branches)
        return max(choices,key=lambda x:x[0])
    return go(3,0,0,0,-1,-1,False,0),go

def clear_rules(p=Q(1,2)):
    """Human-readable preannounced scripts in the same observation protocol."""
    major=max(p,1-p)
    return {
        "static":SIDE+REWARD*major,  # choose most likely target twice then SIDE
        "inspect":REWARD,           # INSPECT then complete revealed target
        "work-probe":REWARD+SIDE*major
        # Start work on likely target: match=>complete & SIDE; otherwise switch
        # and complete opposite target in both remaining actions.
    }

def run():
    for p in (Q(1,4),Q(1,2),Q(3,4)):
        optimal,_=solve(p)
        rules=clear_rules(p)
        assert optimal[0]==max(rules.values())==rules["work-probe"]
        late,_=solve(p,reveal="late")
        taxed,_=solve(p,switching_cost=True)
        without_inspection,_=solve(p,inspection=False)
        blind,_=solve(p,reveal="never",inspection=False)
        assert late[0]==max(rules["inspect"],rules["static"])
        assert without_inspection[0]==rules["work-probe"]
        assert taxed[0]>=rules["inspect"]
        assert blind[0]==max(rules["static"],Q(6))
        print("DISC-01",{"p":str(p),"early":str(optimal[0]),"first":optimal[1],
                         "late":str(late[0]),"switch_cost":str(taxed[0]),
                         "without_inspect":str(without_inspection[0]),
                         "blind_without_inspect":str(blind[0]),
                         "scripts":{k:str(v) for k,v in rules.items()}})
    (value,first),go=solve(Q(1,2))
    assert (value,first)==(Q(9),"WORK_A")
    # SAME naturally reachable after-first-WORK_A state:
    # Under type A, completion plus SIDE yields 10 in either order;
    # under type B, first action must WORK_B for two-step completion.
    assert go(2,1,0,0,0,0,False,1)[0]==Q(10)
    assert go(2,1,0,0,1,0,False,1)==(Q(8),"WORK_B")
    assert solve(Q(1,2),reveal="late")[0]==(Q(8),"INSPECT")
    assert solve(Q(1,2),switching_cost=True)[0]==(Q(8),"INSPECT")
    assert solve(Q(1,2),reveal="never",inspection=False)[0][0]==Q(6)
    assert solve(Q(1,2),inspection=False)[0][0]==Q(9)
    print("PASS DISC-01 productive probe, timely feedback, same-cost inspection and switching-cost controls")

if __name__=="__main__":run()
