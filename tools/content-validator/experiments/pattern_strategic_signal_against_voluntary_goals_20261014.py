#!/usr/bin/env python3
"""Two independent audits: strategic repeated opponent and volunteered outcomes.
Frozen author models; no human, Unity-native or independent blind-agent evidence.
"""
from fractions import Fraction as F
from itertools import product
import pattern_mutable_goals_crosscontract_20261014 as cross

TYPES=("A","B")
FAVORITE,OTHER,PROTECTION = 8,1,5

def enemy_second(kind,protect):
    values={t:F(FAVORITE if t==kind else OTHER)-(PROTECTION if t==protect else 0)
            for t in TYPES}
    values["EXIT"]=F(0)
    return max(values,key=values.get),values

def opponent_payoff(kind,first,protect_signal_chance,first_true_reward):
    # Symmetric defender: protect the observed signal with probability q.
    # In phase2 attacking the favorite is strict for either protection.
    p=(protect_signal_chance if kind==first else 1-protect_signal_chance)
    return (first_true_reward if kind==first else F(0))+F(FAVORITE)-PROTECTION*p

def signal_witness(reward=F(1)):
    # Symmetric fully mixed sequential equilibrium; no uniqueness claimed.
    r=F(reward);q=(r+PROTECTION)/(2*PROTECTION)
    assert 0<q<1
    for kind in TYPES:
        different="B" if kind=="A" else "A"
        assert opponent_payoff(kind,kind,q,r)==opponent_payoff(kind,different,q,r)
        for shield in TYPES:
            choice,payoffs=enemy_second(kind,shield)
            assert choice==kind and payoffs[kind]>max(v for k,v in payoffs.items() if k!=kind)
    # Each type plays A/B equally, so Bayes posterior after any signal is 1/2.
    for signal in TYPES:
        likelihood_a=F(1,2);likelihood_b=F(1,2);prior=F(1,2)
        posterior=likelihood_a*prior/(likelihood_a*prior+likelihood_b*(1-prior))
        assert posterior==F(1,2)
        # Defender's second-round harm is the opponent's earned phase2 payoff.
        loss=(enemy_second("A",signal)[1]["A"]+enemy_second("B",signal)[1]["B"])/2
        assert loss==F(11,2)
    return q,loss

def separating(reward):
    # Honest first move, defender shields reported type; full on-path Bayes.
    # The attacker can deviate to a dishonest first move.
    r=F(reward)
    for kind in TYPES:
        other="B" if kind=="A" else "A"
        truth=opponent_payoff(kind,kind,F(1),r)
        bluff=opponent_payoff(kind,other,F(1),r)
        assert truth-bluff==r-5
    return r>=5

def information_controls():
    q,loss=signal_witness(F(1))
    assert q==F(3,5) and loss==F(11,2)
    assert separating(F(1)) is False and separating(F(4)) is False
    assert separating(F(6)) is True # separate payoff protocol, high early stakes
    for kind in TYPES:
        other="B" if kind=="A" else "A"
        # Blindly trust the first reported target with certainty, not rational:
        assert opponent_payoff(kind,other,F(1),F(1)) > opponent_payoff(kind,kind,F(1),F(1))
        assert enemy_second(kind,other)[1][kind]==8
        assert enemy_second(kind,kind)[1][kind]==3
    perfect=F(3);report_cost=F(1);universal_cost=F(3)
    assert loss-perfect==F(5,2)
    assert perfect+report_cost==F(4) < loss
    assert loss+report_cost==F(13,2) # after shield, report cannot change action
    assert universal_cost < perfect+report_cost # strong ordinary counterfactual
    return q,loss

def goal_tree(hard,safe=False):
    # Same actions/state/transitions as prior CROSS; only the declared terminal
    # objective changes. No new secret extra reward for an announced pledge.
    assert hard in ("FLEX","A","B","BOTH","FULL_C")
    rows=[]
    for early in product(cross.P1,repeat=2):
        state=cross.START
        for act in early:state=cross.action(state,act,None,1)
        branches={}
        for need in TYPES:
            candidates=[]
            late_actions=cross.P2+(("SAFE",) if safe else ())
            for late in product(late_actions,repeat=2):
                s=state
                for act in late:s=cross.action(s,act,need,2,safe=safe) if s is not None else None
                if s is None:continue
                utility,(a,b,c,full,side)=cross.result(s)
                permissible={"FLEX":True,"A":a>0,"B":b>0,"BOTH":a>0 and b>0,"FULL_C":full>0}[hard]
                if permissible:candidates.append((utility,late,(a,b,c,full,side)))
            if candidates:branches[need]=max(candidates,key=lambda x:x[0])
        if len(branches)==2:
            value=(branches["A"][0]+branches["B"][0])/2
            rows.append((value,early,branches))
    assert rows
    return max(rows,key=lambda x:x[0])

def check():
    q,loss=information_controls()
    print("SIGNAL", {"first_true_reward":"1","P_first_A_given_any_type":"1/2",
          "P_shield_first_target":str(q),"equilibrium_player_harm":str(loss),
          "naive_trust_harm":"8","truthful_when_early_reward_is_6_harm":"3",
          "verified_timely_report_cost_1_harm":"4",
          "same_report_late_cost_1_harm":"13/2",
          "universal_shield_cost_3_harm":"3"})
    old={g:goal_tree(g) for g in ("FLEX","A","B","BOTH","FULL_C")}
    safe={g:goal_tree(g,True) for g in old}
    assert {g:old[g][0] for g in old} == {
        "FLEX":F(31,2),"A":F(29,2),"B":F(27,2),"BOTH":F(11),"FULL_C":F(31,2)}
    assert {g:safe[g][0] for g in safe} == {
        "FLEX":F(17),"A":F(17),"B":F(16),"BOTH":F(11),"FULL_C":F(17)}
    for complete in (old,safe):
        assert all(complete[g][0]<=complete["FLEX"][0] for g in complete)
        for g,(value,first,branches) in complete.items():
            assert value == sum((branches[n][0] for n in TYPES),F())/2
            for n in TYPES:
                state=cross.START
                for act in first:state=cross.action(state,act,None,1)
                for act in branches[n][1]:
                    state=cross.action(state,act,n,2,safe=complete is safe)
                assert cross.result(state)[0]==branches[n][0]
    print("VOLUNTARY_STAKES", {
        "baseline":{k:(str(v[0]),v[1]) for k,v in old.items()},
        "with_safe":{k:(str(v[0]),v[1]) for k,v in safe.items()}})
    print("PASS exact two-type opponent equilibrium witness, deviation tests, complete cross-contract policies")

if __name__=="__main__":check()
