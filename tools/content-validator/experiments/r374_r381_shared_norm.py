#!/usr/bin/env python3
"""R374-R381: co-authored legality with separate agents' objectives.

A persistent public rule OPEN / ONLY-A / ONLY-B dictates what action the
counterpart may perform. Player pays 1/4 to change the rule and wants a
public sequence A,B,A. Counterpart privately and consistently prefers
A or B and uses its *one* veto on the first attempt to exclude its side,
restoring OPEN. The partner's veto maximizes its immediate own reward,
but it is not claimed to be a strategic optimal life agent. Both chosen
action and veto are public AFTER rule changes. Exact finite DP versus
complete prewritten norm sequences and independent forward simulations.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json

D=("A","B","A")
RULES=("O","A","B")
C=F(1,4)
def result(theta,prev,used,chosen,turn,c=C,policy="one"):
    veto=(chosen!="O" and chosen!=theta and
          (policy=="unlimited" or (policy=="one" and not used)))
    active="O" if veto else chosen
    used=used or veto
    actual=theta if active=="O" else active
    payoff=F(int(actual==D[turn]))-c*int(chosen!=prev)
    return active,used,veto,actual,payoff

def model(cost=C,policy="one"):
    @lru_cache(None)
    def V(t,current,used,possible):
        if t==len(D):return F(0)
        return max(Q(t,current,used,possible,choice) for choice in RULES)
    def Q(t,current,used,possible,choice):
        groups={}
        for theta in possible:
            nxt,remaining,veto,actual,score=result(theta,current,used,choice,t,cost,policy)
            key=(nxt,remaining,veto,actual)
            groups.setdefault(key,[]).append((theta,score))
        return sum(F(len(rows),len(possible))*(rows[0][1]+V(t+1,key[0],key[1],tuple(x[0] for x in rows)))
                   for key,rows in groups.items())
    root=V(0,"O",False,("A","B"))
    qroot={x:Q(0,"O",False,("A","B"),x) for x in RULES}
    return root,qroot,Q,V

def fixed_plans(cost=C,policy="one"):
    out={}
    for seq in product(RULES,repeat=3):
        total=F(0)
        for theta in ("A","B"):
            current="O";used=False
            for turn,chosen in enumerate(seq):
                current,used,_,_,score=result(theta,current,used,chosen,turn,cost,policy)
                total+=score/2
        out["".join(seq)]=total
    best=max(out.values())
    return best,sorted(k for k,v in out.items() if v==best),out

def policy_rollout(rule,cost=C,policy="one"):
    total=F(0)
    witnesses={}
    for theta in ("A","B"):
        prev="O";used=False;seen=None
        trace=[]
        for t in range(3):
            chosen=rule(t,prev,seen,used)
            nxt,used,veto,action,score=result(theta,prev,used,chosen,t,cost,policy)
            total+=score/2
            trace.append({"turn":t,"chosen":chosen,"public_veto":veto,
                          "counterpart_action":action,"public_rule_after":nxt,
                          "player_reward":score})
            seen=action # OPEN output or triggered veto directly reveals private theta
            prev=nxt
        witnesses[theta]=trace
    return total,witnesses

def explainable_rule(t,current,seen,used):
    if t==0:return "O"
    if seen=="B":return "A" # t=1: deliberately elicit scarce veto; t=2: enforce A
    return "O"

def always_lock_public_target(t,current,seen,used):return D[t]
def stringify(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):stringify(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [stringify(v) for v in x]
    return x

def audit():
    root,qs,Q,V=model()
    fixed,plans,allplans=fixed_plans()
    readable,trace=policy_rollout(explainable_rule)
    naive,_=policy_rollout(always_lock_public_target)
    hidden_oracle=sum(V(0,"O",False,(theta,))/2 for theta in ("A","B"))
    no_veto,_,_,_=model(policy="none")
    endless_veto,_,_,_=model(policy="unlimited")
    expensive,_,_,_=model(cost=F(1,2))
    expensive_fixed,_,_=fixed_plans(cost=F(1,2))
    q_after_open={theta:{rule:Q(1,"O",False,(theta,),rule) for rule in RULES} for theta in ("A","B")}
    assert (root,fixed,readable,naive,hidden_oracle,no_veto,endless_veto,expensive)==(
        F(7,4),F(13,8),F(7,4),F(5,4),F(15,8),F(9,4),F(3,2),F(3,2))
    assert expensive_fixed==F(3,2)
    assert qs["O"]==qs["B"]==F(7,4)>qs["A"]
    assert q_after_open["A"]["O"]>q_after_open["A"]["A"]
    assert q_after_open["B"]["A"]>q_after_open["B"]["O"]
    assert trace["B"][1]["public_veto"] and trace["B"][1]["chosen"]=="A"
    assert len(allplans)==3**3
    return stringify({
      "schema":"ssnoir.r374-r381.coauthored-legal-rule.v1",
      "scope":"paper two-utility norm game with fixed finite partner veto policy; not an agent strategically reasoning over its entire future",
      "contract":"3 periods public player desired output D=A,B,A, shared persistent legal norm OPEN/ONLY-A/ONLY-B. Player may change public norm before each turn at cost1/4; counterpart independently prefers fixed private theta=A or B and chooses theta when legal. When forced away from theta and holding its single veto, counterpart immediately vetoes and restores OPEN, using up veto; after that a later constraint can force it to act against own objective. Partner obtains 1 when it executes theta, player obtains 1 when actual counterpart action matches D. Actual action, public norm and veto are revealed only after player edits.",
      "R374_full_public_belief_DP":root,
      "R375_all_27_nonadaptive_norm_plans":{"best":fixed,"plans":plans},
      "R376_normal_reachable_opposite_rules":q_after_open,
      "R377_deliberate_veto_expenditure_readable_rule":{"value":readable,"true_type_traces":trace},
      "R378_simple_lock_current_demand":naive,
      "R379_partner_veto_capacity_ablations":{"one":root,"no_veto":no_veto,"unlimited":endless_veto},
      "R380_hidden_type_oracle_and_higher_edit_cost":{"full_information":hidden_oracle,
               "cost_half_full":expensive,"cost_half_fixed":expensive_fixed},
      "R381_verdict":"On a reachable OPEN-first trace with partner theta=B, the player proposes ONLY-A at period2 despite publicly wanting B in that period, inducing the partner to spend its one veto and enabling ONLY-A enforcement on period3. This changes a shared legality rule and another independent goal's actual action, unlike a resource pip. Yet a simple type-conditional two-line plan achieves all 7/4, opponent's veto is a fixed immediate-preference policy (not optimal full strategic agency), and this resembles classic coercion/veto-capacity and opportunity cost. Keep as a candidate for legibility/ethics review; do not declare game innovation or endorse coercion as a social goal."
    })
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
