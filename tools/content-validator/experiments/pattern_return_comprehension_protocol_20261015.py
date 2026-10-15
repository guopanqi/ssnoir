"""RETURN comprehension study's OFFLINE scenario integrity checks.

No participants and no 'agent played' claims. These frozen cards support
equivalent staged and macro-plan presentations. This validates signal
reveal boundaries and the true-world ledger without assessing UX.
"""
from fractions import Fraction as F
from itertools import product
from pattern_return_world_local_loop_20261015 import (
    ready_after, world_final, support_p, STARTS
)

CARDS=(
    # code, start, hazard, forecast, support, eventual need, local result coin
    ("no-hazard-good-forecast", "OPEN", 0, 0, 1, 0, 0),
    ("no-hazard-weak-support", "OPEN", 0, 1, 0, 1, 1),
    ("no-hazard-strong-support", "OPEN", 0, 1, 1, 1, 0),
    ("hazard-protect-A", "OPEN", 1, 0, 0, 1, 1),
    ("hazard-predict-B", "OPEN", 1, 1, 1, 0, 0),
    ("cautious-opening", "GUARD", 0, 1, 0, 0, 1),
    ("cautious-strong-support", "GUARD", 0, 0, 1, 1, 0),
)
# Only 3/7 cards have correct forecast; cards are deliberately
# scenario coverage, NOT a draw from the 75%-accurate forecast distribution.
# Do not use their unweighted mean as an unbiased theoretical payoff sample.

WORLD_ACTIONS=("ACCEPT", "DECLINE")
ACCEPT_ACTIONS=("LINK", "CUT", "ABANDON")
DECLINE_ACTIONS=("MEND", "KEEP")

def visible(card,stage,accept=False):
    """Reveal schedule is unchanged between UI conditions."""
    name,start,h,y,k,w,coin=card
    if stage=="L1":return {"start":start}
    if stage=="W1":return {"start":start,"hazard":h,"forecast":y}
    if stage=="L2":
        out={"start":start,"hazard":h,"forecast":y,"accepted":accept}
        if accept:out["support"]=k
        return out
    if stage=="W2":
        return {"start":start,"hazard":h,"forecast":y,
                "accepted":accept,"true_demand":w}
    raise ValueError(stage)

def legal(choice,action):
    return action in (ACCEPT_ACTIONS if choice=="ACCEPT" else DECLINE_ACTIONS)

def outcome(card,choice,action):
    _,start,h,y,k,w,coin=card
    assert choice in WORLD_ACTIONS and legal(choice,action)
    transitions=ready_after(start,h,choice,action,k)
    assert sum((row[0] for row in transitions),F())==1
    selected=transitions[coin % len(transitions)]
    probability,a,b,breach,feedback=selected
    assert probability>0
    gain,final=world_final(a,b,w)
    return {"p":probability,"ready_A":a,"ready_B":b,
            "breach":breach,"feedback":feedback,
            "final_choice":final[0],"net":gain-breach}

def staged_and_macro_same(card,choice,macro):
    """Macro is a K-indexed return action committed before seeing K."""
    k=card[4]
    action=macro[k] if choice=="ACCEPT" else macro[0]
    assert legal(choice,action)
    return outcome(card,choice,action)

def regression():
    assert len(CARDS)==len({c[0] for c in CARDS})==7
    seen={"LINK_SUCCESS":0,"LINK_FAIL":0}
    tested=0
    for card in CARDS:
        name,start,h,y,k,w,coin=card
        assert start in STARTS
        assert h in (0,1) and (start!="GUARD" or h==0)
        assert all(v in (0,1) for v in (y,k,w,coin))
        before=visible(card,"W1")
        assert "support" not in before and "true_demand" not in before
        assert "support" not in visible(card,"L2",False)
        assert "support" in visible(card,"L2",True)
        assert "true_demand" not in visible(card,"L2",True)
        assert visible(card,"W2",True)["true_demand"]==w
        for choice in WORLD_ACTIONS:
            moves=ACCEPT_ACTIONS if choice=="ACCEPT" else DECLINE_ACTIONS
            for a in moves:
                actual=outcome(card,choice,a)
                if a=="LINK":seen[actual["feedback"]]+=1
                assert actual["net"]<=14
                if choice=="DECLINE":
                    assert actual["ready_B"] is False and actual["breach"]==0
                if a=="CUT":
                    assert actual["ready_A"] is False and actual["ready_B"] is True
                if a=="ABANDON":assert actual["breach"]==2
                if a=="MEND":assert actual["ready_A"] and not actual["ready_B"]
                macro=(a,a)
                assert staged_and_macro_same(card,choice,macro)==actual
                tested+=1
        # Compare every one of eleven condition plans for both K outcomes.
        macros=[("DECLINE",(a,a)) for a in DECLINE_ACTIONS]
        macros += [("ACCEPT",(low,high))
                   for low,high in product(ACCEPT_ACTIONS,repeat=2)]
        assert len(macros)==11
        for choice,macro in macros:
            for support in (0,1):
                counterpart=(name,start,h,y,support,w,coin)
                action=macro[support if choice=="ACCEPT" else 0]
                assert staged_and_macro_same(counterpart,choice,macro)==outcome(
                    counterpart,choice,action)
    assert seen["LINK_SUCCESS"] and seen["LINK_FAIL"]
    assert tested==35
    print("PASS: 7 deterministic scenarios, 35 legal settled transitions")
    print("PASS: early reveals hide support and demand; 11 macro patterns valid")
    print("PASS: both LINK outcomes reachable across sampled cards")
    print("NOTE: no human or blind-agent sessions occurred")

if __name__=="__main__":
    regression()
