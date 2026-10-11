#!/usr/bin/env python3
"""R87: P19 mutually exclusive courier address vs late buyer ledger.

Reachability/utility audit, not a perfect engine replay. Freeze existing
Scheme timing: two preparations per round, progress decays by two at each
round boundary; courier is present in round 2 and requires >=4; buyer in
round 3 requires >=6; accepting courier closes buyer forever.
An action uses skill-1 FateStrip outcome 0/1/2.
At courier moment p=4,5,6 are all reachable by ordinary earlier preparation.
Next buyer round supplies a new visible hand of four d6 and allows two
preparation actions with adaptive die choice. Address value1, ledger value
is separately varied. This does not impose a cardinal narrative preference.
"""
from functools import lru_cache
from fractions import Fraction as F
from itertools import combinations_with_replacement
from r56_r65_autonomous_search import odds,hand_weight
import json,sys
H=tuple(combinations_with_replacement(range(1,7),4))

@lru_cache(None)
def buyer_success(p,hand,actions):
    if p>=6:return F(1)
    if actions==0 or not hand:return F(0)
    return max(sum(prob*buyer_success(min(6,p+gain),hand[:i]+hand[i+1:],actions-1)
                   for gain,prob in enumerate(odds(die)) if prob)
               for i,die in enumerate(hand))

def probability(courier_progress):
    # Waiting one round expires the courier; stored preparation loses 2.
    start=max(0,courier_progress-2)
    return sum(hand_weight(h)*buyer_success(start,h,2) for h in H)

def accessible_states():
    states={0}
    for round_index in range(2):
        after={p+gain for p in states for gain in (0,1,2)}
        after={p+gain for p in after for gain in (0,1,2)}
        states={max(0,p-2) if round_index==0 else p for p in after}
    return sorted(states)

def main():
    reachable=accessible_states()
    assert set((4,5,6)).issubset(reachable)
    pr={p:probability(p) for p in (4,5,6)}
    data=[]
    for ledger_value in (F(5,4),F(3,2),F(2)):
        data.append({"ledger_reward_relative_to_address":float(ledger_value),
          "courier_states":[{"available_progress":p,
             "buyer_success_probability":round(float(pr[p]),9),
             "take_courier_reward":1,
             "wait_buyer_expected_reward":round(float(ledger_value*pr[p]),9),
             "strict_best_decision":"take courier" if ledger_value*pr[p]<1 else
               "wait buyer" if ledger_value*pr[p]>1 else "tie"}
            for p in (4,5,6)]})
    assert data[1]["courier_states"][0]["strict_best_decision"]=="take courier"
    assert data[1]["courier_states"][1]["strict_best_decision"]=="wait buyer"
    assert all(s["strict_best_decision"]=="wait buyer" for s in data[2]["courier_states"])
    result={"id":"R87","scenario":"P19 authored Scheme opportunity window reachability",
      "reachable_courier_progress_states":reachable,
      "model":"exact 126 initial unordered future four-die hands, two prep actions, -2 between courier and buyer, action result0/1/2; not full C# skill/composure",
      "comparison":data,
      "important":"Original Scheme does not assign numerical utility to warehouse address and buyer ledger. Distinct endings are not by themselves meaningful choices: relative preferences determine whether both options can be rational.",
      "verdict":"Conditional reversal exists for ledger/address1.5, but with ratio2 the address is dominated whenever available; human stakes must clarify value before promotion."}
    content=json.dumps(result,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(content,encoding="utf-8")
    else:print(content)
if __name__=="__main__":main()
