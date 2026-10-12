#!/usr/bin/env python3
"""R358-R365. Decision authority, private independent goals and learning.

At each turn two agents have independent payoff targets theta and phi:
the player's reward is matching theta, partner's reward is matching phi.
Partner sees phi privately and ALWAYS chooses phi when delegated, not an
action ordered by the player. Alignment propensity is a hidden fixed type
(3/4 vs 1/4), learnable only from delegated outcomes after the turn.
All exact posterior calculations use public histories, no oracle leaks.
"""
from functools import lru_cache
from itertools import product
from fractions import Fraction as F
import json

Q=(F(1,2),F(3,4),F(1,2)) # P(player's desired event A), publicly known
ALIGNED=(F(3,4),F(1,4)) # P(phi==theta) for the two latent relationship types
BONUS=F(1,4) # freed attention value when delegating rather than self-control
ACTS=("A","B","D")

@lru_cache(None)
def dp(t,a,b):
    if t==len(Q):return F(0)
    return max(qval(t,a,b,x) for x in ACTS)

def qval(t,a,b,x):
    if x in ("A","B"):
        success=Q[t] if x=="A" else 1-Q[t]
        return success+dp(t+1,a,b)
    probability=(a*ALIGNED[0]+b*ALIGNED[1])/(a+b)
    return (BONUS+probability
            +probability*dp(t+1,a*ALIGNED[0],b*ALIGNED[1])
            +(1-probability)*dp(t+1,a*(1-ALIGNED[0]),b*(1-ALIGNED[1])))

def exhaustive_fixed():
    # Fully independent event-tree forward value, not Bellman recursion.
    # Two hidden relationship types, three theta and phi draws, and
    # every one of 3^3 non-contingent decisions.
    out={}
    for plan in product(ACTS,repeat=3):
        total=F(0)
        for latent in (0,1):
            for theta in product(("A","B"),repeat=3):
                for phi in product(("A","B"),repeat=3):
                    likelihood=F(1,2)
                    for t in range(3):
                        likelihood*=Q[t] if theta[t]=="A" else 1-Q[t]
                        likelihood*=ALIGNED[latent] if theta[t]==phi[t] else 1-ALIGNED[latent]
                    gain=sum((int((phi[t] if act=="D" else act)==theta[t])
                              +(BONUS if act=="D" else 0)) for t,act in enumerate(plan))
                    total+=likelihood*gain
        out["".join(plan)]=total
    top=max(out.values())
    return top,sorted(k for k,v in out.items() if v==top),out

def independent_public_rule():
    # Start delegate; observe its actual phi and true theta AFTER the
    # turn, then in the middle task delegate if aligned, self A if
    # misaligned. On last task delegate again. A human-readable rival.
    total=F(0)
    for latent in (0,1):
        for theta in product(("A","B"),repeat=3):
            for phi in product(("A","B"),repeat=3):
                prob=F(1,2)
                for t in range(3):
                    prob*=Q[t] if theta[t]=="A" else 1-Q[t]
                    prob*=ALIGNED[latent] if phi[t]==theta[t] else 1-ALIGNED[latent]
                plan=("D","D" if theta[0]==phi[0] else "A","D")
                gain=sum((int((phi[t] if act=="D" else act)==theta[t])
                         +(BONUS if act=="D" else 0)) for t,act in enumerate(plan))
                total+=prob*gain
    return total

def oracle_before_first():
    # The relationship type is publicly known at game start. This is
    # a strict information advantage ablation, not legitimate play.
    return sum(F(1,2)*sum(max(Q[t],1-Q[t],BONUS+ALIGNED[k])
                          for t in range(3)) for k in (0,1))

def redraw_each_turn():
    # Reset latent type independently per task: no persistent relationship
    # to learn. Environment changes, but public task priors stay frozen.
    return sum(max(Q[t],1-Q[t],BONUS+sum(ALIGNED)/2) for t in range(3))

def encode(v):
    if isinstance(v,F):return str(v)
    if isinstance(v,dict):return {str(k):encode(x) for k,x in v.items()}
    if isinstance(v,(list,tuple)):return [encode(x) for x in v]
    return v

def main():
    root=dp(0,F(1),F(1))
    initial={x:qval(0,F(1),F(1),x) for x in ACTS}
    agree={x:qval(1,ALIGNED[0],ALIGNED[1],x) for x in ACTS}
    disagree={x:qval(1,1-ALIGNED[0],1-ALIGNED[1],x) for x in ACTS}
    fixed,plans,allfixed=exhaustive_fixed()
    readable=independent_public_rule()
    oracle=oracle_before_first()
    reset=redraw_each_turn()
    assert (root,fixed,readable,oracle,reset)==(F(37,16),F(9,4),F(37,16),F(19,8),F(9,4))
    assert initial["D"]>initial["A"]==initial["B"]
    assert agree["D"]>agree["A"]
    assert disagree["A"]>disagree["D"]
    assert sum(1 for k in allfixed)==27
    return encode({
      "schema":"ssnoir.r358-r365.delegated-private-goals.v1",
      "scope":"two distinct payoff functions and private observations, exact paper model only; not a self-sustaining NPC",
      "contract":"Three tasks. Public P(theta=A)=[1/2,3/4,1/2]. Partner privately observes its independent target phi and when delegated chooses phi, maximizing its own +1 payoff, not the player's theta target. Latent relationship alignment rate is 3/4 or 1/4, stable across tasks, equiprobable but hidden. Delegate gives player 1 for matching theta plus 1/4 freed-attention value. Self A/B gives player 1 if matches theta, no bonus. theta and partner's actual phi revealed AFTER delegation, so relationship type can be inferred only from past delegated results.",
      "R358_belief_DP":root,
      "R359_root_Q":initial,
      "R360_reachable_divergence_same_public_task":{"agreement":agree,"disagreement":disagree,
        "each_probability":F(1,2)},
      "R361_all_27_nonadaptive_schedules":{"best":fixed,"examples":plans},
      "R362_independent_public_rule":readable,
      "R363_oracle_hidden_relationship":oracle,
      "R364_partner_relation_resets_every_turn":reset,
      "R365_verdict":"Delegation transfers authorship of an action to another goal-driven actor; observing whether its chosen phi agreed with theta changes later rational authority choices. Yet a short 'delegate first; middle task delegate if agreement else self; delegate last' public rule fully matches optimal DP, and the other's own objective is one-step not enduring. Evidence of delegation/learning combination, not certified new persistent social gameplay."
    })
if __name__=="__main__":print(json.dumps(main(),indent=2,ensure_ascii=False))
