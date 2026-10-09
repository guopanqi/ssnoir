#!/usr/bin/env python3
"""R79–R81 independent continuation of SSNoir autonomous research.

Exact rational four-die FateStrip decision processes. Each experiment uses
its own explicit utility and objective; never compare raw expected reward
across models. Strong outside options and direct mechanic ablations are
mandatory. No Unity/C# or human-play evidence is claimed.
"""
from functools import lru_cache
from fractions import Fraction as F
from itertools import combinations_with_replacement
from r56_r65_autonomous_search import odds,hand_weight
import json,sys
HANDS=tuple(combinations_with_replacement(range(1,7),4))

def combine(expected):
    return round(float(sum(hand_weight(h)*expected[h] for h in HANDS)),9)

def r79():
    """Wait for scheduled clue, compare irreversible early commit and rescind.
    Waiting consumes one die plus public opportunity cost on the outcome.
    Reversing costs one die and forfeits progress; no invisible knowledge.
    """
    def build(wait_cost):
        @lru_cache(None)
        def value(a,b,commit,p,hand,revocable,wait_ok,waited):
            if not hand:
                reward=F(3)*(p*F(a>=2)+(1-p)*F(b>=2))
                return max(F(0),reward-wait_cost*int(waited))
            turn=4-len(hand)
            valid=[0,1] if commit<0 else [commit]
            if commit<0 and turn==0 and wait_ok: valid.append(3)
            if commit>=0 and revocable and turn>=1:valid.append(2)
            return max(action(a,b,commit,p,hand,g,i,revocable,wait_ok,waited,turn)
                       for g in valid for i in range(len(hand)))
        def action(a,b,commit,p,hand,g,i,revocable,wait_ok,waited,turn):
            rem=hand[:i]+hand[i+1:]
            if g==2:return value(0,0,1-commit,p,rem,False,wait_ok,waited)
            if g==3:
                return F(1,2)*value(a,b,-1,F(4,5),rem,revocable,wait_ok,True)+F(1,2)*value(a,b,-1,F(1,5),rem,revocable,wait_ok,True)
            def after(na,nb):
                if turn==0:
                    return F(1,2)*value(na,nb,g,F(4,5),rem,revocable,wait_ok,waited)+F(1,2)*value(na,nb,g,F(1,5),rem,revocable,wait_ok,waited)
                return value(na,nb,g,p,rem,revocable,wait_ok,waited)
            return sum(pr*after(min(2,a+k) if g==0 else a,min(2,b+k) if g==1 else b)
                       for k,pr in enumerate(odds(hand[i])) if pr)
        data={}
        for rev,wait in ((False,False),(True,False),(False,True),(True,True)):
            data[(rev,wait)]={h:value(0,0,-1,F(1,2),h,rev,wait,False) for h in HANDS}
        fixed=data[(False,False)]
        rev_force=data[(True,False)]
        wait_only=data[(False,True)]
        both=data[(True,True)]
        assert all(both[h]>=wait_only[h] and both[h]>=rev_force[h] for h in HANDS)
        assert all(wait_only[h]>=fixed[h] and rev_force[h]>=fixed[h] for h in HANDS)
        return {"wait_cost":float(wait_cost),
                "early_irreversible":combine(fixed),
                "early_with_rescind":combine(rev_force),
                "allow_wait_without_rescind":combine(wait_only),
                "allow_wait_and_rescind":combine(both),
                "rescind_gain_after_allowing_wait":combine({h:both[h]-wait_only[h] for h in HANDS}),
                "wait_gain_over_early_irreversible":combine({h:wait_only[h]-fixed[h] for h in HANDS}),
                "strict_roots_rescind_even_with_wait":sum(both[h]>wait_only[h] for h in HANDS),
                "strict_roots_wait_over_early":sum(wait_only[h]>fixed[h] for h in HANDS)}
    return {"id":"R79","relation":"wait for evidence at a real opportunity cost, with independently purchasable rescind",
            "ablations":[build(F(0)),build(F(1,2)),build(F(1))],
            "counterexample":"Allow waiting as a real alternative before crediting rescind. A cost on waiting cannot be confused with a new dynamic choice."}

def signal_branches(p,accuracy):
    chance=p*accuracy+(1-p)*(1-accuracy)
    result=[]
    if chance:result.append((chance,p*accuracy/chance))
    if chance<1:result.append((1-chance,p*(1-accuracy)/(1-chance)))
    return result

def r80():
    """Action itself yields a noisy public clue about true valued target.
    Test whether paid extra inquiry still has option value, separately from
    the value of obtaining ANY information.
    """
    @lru_cache(None)
    def val(a,b,p,hand,can_inquire,free_clue):
        if not hand or (a>=3 and b>=3):
            return F(3)*(p*F(a>=3)+(1-p)*F(b>=3))
        moves=[0,1]+([2] if can_inquire else [])
        return max(q(a,b,p,hand,g,i,can_inquire,free_clue)
                   for g in moves for i in range(len(hand)))
    def q(a,b,p,hand,g,i,can_inquire,free_clue):
        rem=hand[:i]+hand[i+1:]
        if g==2:
            def qry(k):
                if k==0:return val(a,b,p,rem,False,free_clue)
                accuracy=F(3,4) if k==1 else F(1)
                return sum(ch*val(a,b,post,rem,False,free_clue) for ch,post in signal_branches(p,accuracy))
            return sum(pr*qry(k) for k,pr in enumerate(odds(hand[i])) if pr)
        def next_one(k):
            na=min(3,a+k) if g==0 else a
            nb=min(3,b+k) if g==1 else b
            if free_clue:
                return sum(ch*val(na,nb,post,rem,can_inquire,False) for ch,post in signal_branches(p,F(3,4)))
            return val(na,nb,p,rem,can_inquire,False)
        return sum(pr*next_one(k) for k,pr in enumerate(odds(hand[i])) if pr)
    res={}
    for has_auto,paid in ((False,False),(False,True),(True,False),(True,True)):
        res[(has_auto,paid)]={h:val(0,0,F(1,2),h,paid,has_auto) for h in HANDS}
    assert all(res[(True,True)][h]>=res[(True,False)][h] for h in HANDS)
    assert all(res[(False,True)][h]>=res[(False,False)][h] for h in HANDS)
    auto,paid,base,both=res[(True,False)],res[(False,True)],res[(False,False)],res[(True,True)]
    return {"id":"R80","relation":"information arrives as a byproduct of ordinary action",
        "none":combine(base),"paid_query_without_auto_clue":combine(paid),
        "auto_clue_without_paid_query":combine(auto),"auto_clue_and_optional_paid_query":combine(both),
        "value_of_paid_query_without_auto_clue":combine({h:paid[h]-base[h] for h in HANDS}),
        "incremental_paid_query_with_auto_clue":combine({h:both[h]-auto[h] for h in HANDS}),
        "strict_roots_query_on_top_of_auto_clue":sum(both[h]>auto[h] for h in HANDS),
        "counterexample":"Do not compare query-enabled against query-disabled while failing to represent information naturally revealed during normal work."}

def r81():
    """Opponent pursues the previously occupied position at t=2 and t=4.
    Model no cheating, no map-wide visibility, explicit location cost.
    Mode=no_tracking tests whether costly moving makes *any* difference;
    fixed_enemy tests the incremental reactive pursuit contribution.
    """
    def build(reactive=True,can_move=True):
        # lane A=0, lane B=1; both identical safe progress amounts but lane B
        # incurs permanent evidence loss. Enemy watches the lane observed last
        # at the end of every two actions. Going through watched lane builds
        # heat; 3 heat is an irreversible escape failure.
        def step(s,act,gain,turn):
            lane,watch,prog,heat,evidence=s
            if act==2:lane=1-lane;heat=max(0,heat-1)
            elif act==3:heat=max(0,heat-2)
            else:
                assert act==lane
                prog=min(5,prog+gain+(int(lane==1 and gain>0)))
                if lane==1:evidence=0
                if gain==0 and lane==watch:heat+=1
                elif gain>0 and lane==watch:heat+=1
            if (turn+1)%2==0 and reactive:watch=lane
            return lane,watch,prog,heat,evidence
        def score(s):
            lane,watch,prog,heat,evidence=s
            return F(0) if heat>=3 or prog<5 else F(1+evidence)
        @lru_cache(None)
        def v(s,hand):
            if not hand or s[2]>=5 or s[3]>=3:return score(s)
            turn=4-len(hand)
            acts=[s[0]]
            if can_move:acts.append(2)
            if s[3]>0:acts.append(3)
            return max(q(s,hand,act,i,turn) for act in acts for i in range(len(hand)))
        def q(s,hand,act,i,turn):
            rem=hand[:i]+hand[i+1:]
            if act>=2:return v(step(s,act,0,turn),rem)
            return sum(pr*v(step(s,act,k,turn),rem) for k,pr in enumerate(odds(hand[i])) if pr)
        results={h:v((0,0,0,0,1),h) for h in HANDS}
        return results
    reactive_move=build(True,True)
    reactive_static=build(True,False)
    fixed_move=build(False,True)
    fixed_static=build(False,False)
    assert all(reactive_move[h]>=reactive_static[h] for h in HANDS)
    assert all(fixed_move[h]>=fixed_static[h] for h in HANDS)
    option_gain=combine({h:reactive_move[h]-reactive_static[h] for h in HANDS})
    option_gain_fixed=combine({h:fixed_move[h]-fixed_static[h] for h in HANDS})
    return {"id":"R81","relation":"opponent tracks last route, costly player relocation changes next exposure",
        "tracking_move":combine(reactive_move),"tracking_no_move":combine(reactive_static),
        "stationary_enemy_move":combine(fixed_move),"stationary_enemy_no_move":combine(fixed_static),
        "movement_option_under_tracking":option_gain,
        "movement_option_under_stationary_enemy":option_gain_fixed,
        "tracking_specific_movement_effect":round(option_gain-option_gain_fixed,9),
        "strict_root_hands_move_with_tracking":sum(reactive_move[h]>reactive_static[h] for h in HANDS),
        "counterexample":"A relocation button need not gain value from enemy tracking; first check that tracking changes the movement option's causal value."}

def main():
    rows=[r79(),r80(),r81()]
    result={"schema":"ssnoir.autonomous-mechanic-search/v1",
        "scope":"Three isolated 4-die exact Fraction paper abstractions with direct feature ablations; not human play or C# runtime",
        "cases":rows}
    content=json.dumps(result,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(content,encoding="utf-8")
    else:print(content)
if __name__=="__main__":main()
