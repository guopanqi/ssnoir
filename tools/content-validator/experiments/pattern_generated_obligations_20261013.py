#!/usr/bin/env python3
"""GOAL-01: endogenous offer / preset conditional menu / costly obligation.

A finite 4-action toy with exact Fraction backward induction. Do not claim
novel gameplay, unique emotional meaning, or independent player feedback.
"""
from fractions import Fraction as Q
from functools import lru_cache

A_REWARD, B_REWARD, SIDE = Q(6), Q(9), Q(2)
HIGH_R, LOW_R, DEFAULT_PENALTY = Q(14), Q(4), Q(4)

@lru_cache(None)
def tail(steps, a, b, r, offer, done_a, done_b, done_r, liability):
    """No hidden signal is accessed: offer is revealed before tail begins."""
    if not steps:
        return ((-DEFAULT_PENALTY if liability and offer and not done_r else Q(0)), ())
    legal = ["SIDE"]
    if not done_a: legal.append("A")
    if not done_b: legal.append("B")
    if offer and not done_r: legal.append("R")
    result = []
    for act in legal:
        aa,bb,rr,da,db,dr = a,b,r,done_a,done_b,done_r
        pay = Q(0)
        if act == "SIDE": pay = SIDE
        elif act == "A":
            aa += 1
            if aa == 2: pay = A_REWARD; da = True
        elif act == "B":
            bb += 1
            if bb == 2: pay = B_REWARD; db = True
        elif act == "R":
            rr += 1
            if rr == 2: pay = HIGH_R if offer == "H" else LOW_R; dr = True
        v,p = tail(steps-1,aa,bb,rr,offer,da,db,dr,liability)
        result.append((v+pay,(act,)+p))
    return max(result,key=lambda item:item[0])

def first(p,liability=False):
    """Four first actions are legal from the SAME origin, A=1/2, B=0/2."""
    assert 0 <= p <= 1
    out = {}
    for action in ("ASK","SIDE","A","B"):
        if action == "ASK":
            hi = tail(3,1,0,0,"H",False,False,False,liability)
            lo = tail(3,1,0,0,"L",False,False,False,liability)
            out[action] = (p*hi[0]+(1-p)*lo[0],{"H":hi,"L":lo})
        else:
            aa,bb,gain,da = {"SIDE":(1,0,SIDE,False),
                             "A":(2,0,A_REWARD,True),
                             "B":(1,1,Q(0),False)}[action]
            value,seq = tail(3,aa,bb,0,"",da,False,False,liability)
            out[action] = (value+gain,{"none":(value+gain,(action,)+seq)})
    best_value = max(data[0] for data in out.values())
    return best_value,[a for a,data in out.items() if data[0]==best_value],out

def readable(p,liability=False):
    """A short preannounced policy fully matches the exact decision oracle."""
    ask = p*Q(20)+(1-p)*(Q(11) if liability else Q(15))
    return max(Q(17),ask),("ASK; high=>R,R,A; low=>A,B,B" if ask>=17
                           else "no ASK: complete A and B, take SIDE")

def prelisted_conditional_menu(p,liability=False):
    """Complete prior menu with a differently named first action.

    Includes EVERY continuation and cost. This is game-tree isomorphism, not
    artificial comparison against a weak or blind opponent.
    """
    _,_,options = first(p,liability)
    return {("CLAIM_CONDITIONAL_MENU" if name=="ASK" else name):value
            for name,value in options.items()}

def free_automatic(p,liability=False):
    """Nature grants/reveals H/L at t=0, free. A DIFFERENT protocol.
    For a compulsory adverse offer it need NOT dominate optional discovery.
    """
    hi = tail(4,1,0,0,"H",False,False,False,liability)
    lo = tail(4,1,0,0,"L",False,False,False,liability)
    return p*hi[0]+(1-p)*lo[0],{"H":hi,"L":lo}

def run():
    for p in (Q(0),Q(1,4),Q(2,5),Q(1,2),Q(2,3),Q(3,4),Q(1)):
        for liability in (False,True):
            optimal,best,actions = first(p,liability)
            expected,rule = readable(p,liability)
            assert optimal == expected,(p,liability,optimal,expected)
            assert {name:v[0] for name,v in prelisted_conditional_menu(p,liability).items()} == {
                ("CLAIM_CONDITIONAL_MENU" if name=="ASK" else name):v[0]
                for name,v in actions.items()}
            assert actions["SIDE"][0] == actions["A"][0] == actions["B"][0] == Q(17)
            assert actions["ASK"][0] == (Q(11)+9*p if liability else Q(15)+5*p)
            if not liability:assert free_automatic(p,liability)[0]>=optimal
            print("GOAL-01",{"p":str(p),"obligation":liability,"optimal":str(optimal),
                             "best":best,"ASK":str(actions["ASK"][0]),"rule":rule,
                             "free_reveal":str(free_automatic(p,liability)[0])})
    optimal,best,actions=first(Q(1,2))
    assert (optimal,best)==(Q(35,2),["ASK"])
    assert actions["ASK"][1]["H"][0]==Q(20)
    assert actions["ASK"][1]["L"][0]==Q(15)
    assert first(Q(1,2),True)[0]==Q(17)
    assert first(Q(3,4),True)[0]==Q(71,4)
    assert first(Q(0),True)[0]>free_automatic(Q(0),True)[0]
    print("PASS GOAL-01: exact policy, typed obligations, same-tree prelisted menu, and nonfree duty controls")

if __name__=="__main__":run()
