#!/usr/bin/env python3
"""R294-R301: sequential offers / inference vs best complete price ladder.

Three hidden acceptance thresholds {1,2,3}, equal prior. At each of three
turns the player may make price offer 1/2/3, wait, or spend a turn to learn
the threshold. Offer acceptance exits; rejection only reveals theta>price.
Exact belief DP compared with independent full fixed path enumeration.
"""
import json
from functools import lru_cache
from fractions import Fraction as F
from itertools import product

T=3
TYPES=(1,2,3)
ACTS=(1,2,3,"W","P")
def model(discount):
 @lru_cache(None)
 def dp(t,possible):
  if t==T:return F(0)
  return max(q(t,possible,a) for a in ACTS)
 def q(t,possible,action):
  if action=="W":return dp(t+1,possible)
  if action=="P":return sum(dp(t+1,(theta,)) for theta in possible)/len(possible)
  accepted=[theta for theta in possible if theta<=action]
  rejected=tuple(theta for theta in possible if theta>action)
  now=F(len(accepted),len(possible))*(4-action-discount*t)
  later=F(len(rejected),len(possible))*dp(t+1,rejected) if rejected else 0
  return now+later
 root=dp(0,TYPES)
 initial={str(a):q(0,TYPES,a) for a in ACTS}
 # Independent forward evaluator of precommitted legal plans. It does NOT
 # observe/condition on rejection except that acceptance terminates early.
 def fixed_payoff(plan):
  total=F(0)
  for theta in TYPES:
   for t,act in enumerate(plan):
    if act=="W":continue
    assert act in (1,2,3)
    if act>=theta:
     # Rational agent can always skip a negative offer; enumeration includes W.
     total+=F(4-act)-discount*t
     break
  return total/3
 fixed={str(plan):fixed_payoff(plan) for plan in product((1,2,3,"W"),repeat=T)}
 val=max(fixed.values())
 wins=[key for key,v in fixed.items() if v==val]
 # Several readable strategies, fully independent of DP.
 ascending=fixed_payoff((1,2,3))
 generous=fixed_payoff((2,3,"W"))
 known_type_probe=sum(max(F(0),F(4-theta)-discount) for theta in TYPES)/3
 return {"discount":discount,"full_DP":root,"best_open_loop_offer_plan":val,
         "best_offer_examples":wins[:8],"best_plan_count":len(wins),
         "root_Q_including_probe_wait":initial,"ascending_123":ascending,
         "generous_23W":generous,"probe_first_then_personalize":known_type_probe,
         "complete_fixed_plans":4**T}
def ser(v):
 if isinstance(v,F):return str(v)
 if isinstance(v,dict):return {str(k):ser(x) for k,x in v.items()}
 if isinstance(v,(tuple,list)):return [ser(x) for x in v]
 return v
def audit():
 d={str(delta):model(delta) for delta in (F(0),F(1,4),F(1,2),F(1))}
 exact={"0":F(2),"1/4":F(7,4),"1/2":F(3,2),"1":F(4,3)}
 for k,v in d.items():
  assert v["full_DP"]==v["best_open_loop_offer_plan"]==exact[k]
  assert v["full_DP"]>=v["probe_first_then_personalize"]
  assert v["complete_fixed_plans"]==64
 assert d["1/4"]["ascending_123"]==F(7,4)
 assert d["1/2"]["generous_23W"]==F(3,2)
 assert d["1"]["root_Q_including_probe_wait"]["P"]==1
 assert d["1"]["root_Q_including_probe_wait"]["W"]==F(2,3)
 return ser({
  "schema":"ssnoir.r294-r301.offer-and-rejection.v1",
  "scope":"complete-information rule and independent responder threshold, no real counterpart/human/native engine",
  "rules":"Three private thresholds 1,2,3 equally likely; player offers price 1/2/3; responder accepts iff price>=threshold, pays player 4-price-time*discount and ends; rejection publicly means threshold>price. Player may wait W or spend one turn P to learn exact threshold, then can offer; 3 action periods total.",
  "R294_no_delay":d["0"],
  "R295_quarter_delay":d["1/4"],
  "R296_half_delay":d["1/2"],
  "R297_high_delay":d["1"],
  "R298_probe_included_and_never_strictly_better":{k:{"probe_value":v["root_Q_including_probe_wait"]["P"],"root":v["full_DP"]} for k,v in d.items()},
  "R299_wait_included_no_forced_negative_offer":{k:v["root_Q_including_probe_wait"]["W"] for k,v in d.items()},
  "R300_complete_64_precommitted_ladders":{k:v["best_open_loop_offer_plan"] for k,v in d.items()},
  "R301_interpretation":"One-bit rejection on a terminated success path cannot create a branching continuation: only rejection reaches the next decision, so a complete offer ladder already implements every informed response. Probe can reveal types but is not strictly useful at root in these models. This is classic price screening and timing, NOT a novel independent bargaining game."
 })
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
