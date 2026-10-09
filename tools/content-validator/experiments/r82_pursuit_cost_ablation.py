#!/usr/bin/env python3
"""R82 follow-up to negative R81: enemy response must actually cause loss.

Two rounds of two actions each. Pursuer attacks the lane it WATCHED at the
end of each round and then updates watch to the player's present lane.
A dedicated Hide action protects from the next attack; Move switches lane,
costs a die and permanently sacrifices the document. Progress action uses
FateStrip outcome 0/1/2, with an extra pace on the exposed B lane.
Goal >=3 by the end of round 2; caught earlier -> zero. Reward A evidence2,
otherwise1. Values and opponent timing are identical between arms except
the *tracking update*. No free clairvoyance of unrevealed rolls.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement,product
from r56_r65_autonomous_search import odds,hand_weight
import json
HANDS=tuple(combinations_with_replacement(range(1,7),4))
N=4
def model(track,move_ok):
    @lru_cache(None)
    def v(s,hand):
        pos,watch,progress,cover,evidence,alive=s
        if not alive:return F(0)
        if not hand:return F((1+evidence) if progress>=3 else 0)
        turn=N-len(hand)
        actions=[0,2] + ([1] if move_ok else [])  # 0 advance, 1 move, 2 hide
        return max(q(s,hand,act,i,turn) for act in actions for i in range(len(hand)))
    def update(s,act,gain,turn):
        pos,watch,progress,cover,evidence,alive=s
        if act==0:
            progress=min(3,progress+gain+int(pos==1 and gain>0))
            if pos==1:evidence=0
        elif act==1:
            pos=1-pos
        else:cover=1
        if (turn+1)%2==0:
            if watch==pos and not cover:alive=0
            cover=0
            if track:watch=pos
        return (pos,watch,progress,cover,evidence,alive)
    def q(s,hand,act,i,turn):
        rest=hand[:i]+hand[i+1:]
        if act!=0:return v(update(s,act,0,turn),rest)
        return sum(p*v(update(s,act,k,turn),rest) for k,p in enumerate(odds(hand[i])) if p)
    return {h:v((0,0,0,0,1,1),h) for h in HANDS}
def avg(tab):
    return round(float(sum(hand_weight(h)*tab[h] for h in HANDS)),9)
def main():
    tracked=model(True,True);tracked_nomove=model(True,False)
    stationary=model(False,True);stationary_nomove=model(False,False)
    assert all(tracked[h]>=tracked_nomove[h] and stationary[h]>=stationary_nomove[h] for h in HANDS)
    option_track={h:tracked[h]-tracked_nomove[h] for h in HANDS}
    option_fixed={h:stationary[h]-stationary_nomove[h] for h in HANDS}
    different={h:tracked[h]-stationary[h] for h in HANDS}
    result={"id":"R82","relation":"visible opponent follows position, next-round exposure has an enforceable lethal cost",
        "full_tracking":avg(tracked),"no_move_tracking":avg(tracked_nomove),
        "full_stationary":avg(stationary),"no_move_stationary":avg(stationary_nomove),
        "move_option_value_with_tracking":avg(option_track),
        "move_option_value_without_tracking":avg(option_fixed),
        "opponent_reactivity_effect":avg(different),
        "strict_root_hands_where_tracking_changes_value":sum(tracked[h]!=stationary[h] for h in HANDS),
        "strict_root_hands_where_move_matters_with_tracking":sum(option_track[h]>0 for h in HANDS),
        "counterexample":"Making pursuit lethal does not by itself establish an interesting decision; confirm tracking versus a stationary pursuer actually changes expected outcome before discussing human play.",
        "scope":"Exact four dice paper model, two rounds, public aim before each round; not a Unity simulation or human trial"}
    print(json.dumps(result,ensure_ascii=False,indent=2))
if __name__=="__main__":main()
