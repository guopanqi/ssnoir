#!/usr/bin/env python3
"""X03: hard-forbidden action and costly defiance can share optimal policies.

This deliberately does NOT infer narrative meaning or actual player preferences
from an expected-reward solver. It tests payoff vs action-affordance separation.
"""
from fractions import Fraction as F
from functools import lru_cache

A_GOAL=2
B_GOAL=2
A_REWARD=F(10)
B_REWARD=F(6)
SIDE=F(1)
SALVAGE=F(2)
PENALTY=F(20)

def late_model(start_a,start_b,authorized,mode,penalty=PENALTY):
    assert mode in ("HARD","SOFT")
    @lru_cache(None)
    def solve(t,a,b,closed,a_done,b_done):
        if t==2:return F(0),()
        actions=["SIDE"]
        if not b_done:actions.append("B")
        if not a_done and not closed and (authorized or mode=="SOFT"):
            actions.append("A")
        if not authorized and a>=1 and not closed and not a_done:
            actions.append("SALVAGE")
        options=[]
        for act in actions:
            na,nb,nclosed,nda,ndb=a,b,closed,a_done,b_done
            gain=F(0)
            if act=="SIDE":gain=SIDE
            if act=="A":
                na+=1
                if na>=A_GOAL:
                    gain=A_REWARD-(F(0) if authorized else penalty)
                    nda=True
            if act=="B":
                nb+=1
                if nb>=B_GOAL:gain=B_REWARD;ndb=True
            if act=="SALVAGE":
                gain=SALVAGE
                na=0
                nclosed=True
            value,path=solve(t+1,na,nb,nclosed,nda,ndb)
            options.append((gain+value,(act,)+path))
        return max(options,key=lambda it:it[0])
    result=solve(0,start_a,start_b,False,False,False)
    return result

def ex_ante(q,mode,penalty=PENALTY):
    chance=F(q)
    options={}
    for pre in ("A","B"):
        a,b=(1,0) if pre=="A" else (0,1)
        allowed=late_model(a,b,True,mode,penalty)
        forbidden=late_model(a,b,False,mode,penalty)
        options[pre]=(
            chance*allowed[0]+(1-chance)*forbidden[0],
            {"authorized":allowed,"forbidden":forbidden}
        )
    optimal=max(options,key=lambda k:options[k][0])
    return optimal,options

def action_menu(a,b,authorized,mode):
    result=["SIDE","B"]
    if authorized or mode=="SOFT":
        result.append("A")
    if not authorized and a>=1:
        result.append("SALVAGE")
    return result

def force_A_after_revocation(mode,penalty=PENALTY):
    # PREP A already executed, new information removes A authority.
    # User deliberately insists on A anyway, then does SIDE once.
    if mode=="HARD":return None
    return A_REWARD-penalty+SIDE

def run():
    for q,choice,value in [(F(1,4),"B",F(31,4)),
                           (F(3,4),"A",F(39,4)),
                           (F(1,2),"A",F(17,2))]:
        hard,h=ex_ante(q,"HARD")
        soft,s=ex_ante(q,"SOFT")
        assert hard==soft==choice,(q,hard,soft)
        assert h["A"][0]==s["A"][0]==F(6)+F(5)*q,(q,h,s)
        assert h["B"][0]==s["B"][0]==F(7)+F(3)*q,(q,h,s)
        assert h[choice][0]==value,(q,h,value)
        print("X03",str(q),"choice",choice,"value",str(value),
              "A",h["A"],"B",h["B"])
    assert "A" not in action_menu(1,0,False,"HARD")
    assert "A" in action_menu(1,0,False,"SOFT")
    assert force_A_after_revocation("HARD") is None
    assert force_A_after_revocation("SOFT")==F(-9)
    # For every naturally reachable post-preparation state, rational maximal
    # values agree. Yet the counterfactual deliberately refused action differs.
    for pre in ("A","B"):
        a,b=(1,0) if pre=="A" else (0,1)
        for permitted in (False,True):
            hard=late_model(a,b,permitted,"HARD")
            soft=late_model(a,b,permitted,"SOFT")
            assert hard==soft,(pre,permitted,hard,soft)
    # A sufficiently small soft penalty can change rational actions, proving
    # equivalence is conditional on the penalty, not universal semantics.
    cheap=late_model(1,0,False,"SOFT",penalty=F(0))
    hard=late_model(1,0,False,"HARD")
    assert cheap[0]>hard[0]
    print("COUNTERFACTUAL disabled A hard, present-but-penalized A soft",
          "hard defiance",force_A_after_revocation("HARD"),
          "soft defiance",force_A_after_revocation("SOFT"),
          "cheap penalty changes rational solution",cheap)
    print("PASS X03 rational-policy equivalence despite divergent player action menus")


if __name__=="__main__":run()
