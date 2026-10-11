#!/usr/bin/env python3
"""R83–R85 independent exact four-die design audits. NO native or human claims."""
import json,sys
from functools import lru_cache
from fractions import Fraction as Q
from itertools import combinations_with_replacement
from r56_r65_autonomous_search import odds,hand_weight
H=tuple(combinations_with_replacement(range(1,7),4))
def avg(d):return round(float(sum(hand_weight(h)*d[h] for h in H)),9)
def signals(p,accuracy):
    yes=p*accuracy+(1-p)*(1-accuracy)
    out=[]
    if yes:out.append((yes,p*accuracy/yes))
    if yes<1:out.append((1-yes,p*(1-accuracy)/(1-yes)))
    return out

def r83():
    """An easy courier disappears after the first action. A noisy clue arrives
    after any first action, including productive early work or costly wait.
    One may pay another action to rescind an earlier commitment."""
    def model(revoke,wait,expires):
        @lru_cache(None)
        def dp(a,b,p,committed,hand):
            t=4-len(hand)
            if not hand:return 3*(p*(committed==0 and a>=3)+(1-p)*(committed==1 and b>=3))
            choices=[]
            if t==0 or not expires:choices.append(Q(6,5)) # immediate safe result
            for i,die in enumerate(hand):
                rest=hand[:i]+hand[i+1:]
                if committed<0:
                    if t==0 and wait:
                        choices.append(sum(ch*dp(a,b,post,-1,rest)
                                       for ch,post in signals(p,Q(3,4))))
                    targets=(0,1)
                else:
                    targets=(committed,)
                    if revoke:
                        other=1-committed
                        choices.append(dp(0 if committed==0 else a,
                                          0 if committed==1 else b,p,other,rest))
                for goal in targets:
                    value=Q(0)
                    for gain,prob in enumerate(odds(die)):
                        if not prob:continue
                        aa=min(3,a+gain) if goal==0 else a
                        bb=min(3,b+gain) if goal==1 else b
                        if t==0:
                            continuation=sum(ch*dp(aa,bb,post,goal,rest)
                                       for ch,post in signals(p,Q(3,4)))
                        else:continuation=dp(aa,bb,p,goal,rest)
                        value+=prob*continuation
                    choices.append(value)
            return max(choices)
        return {h:dp(0,0,Q(1,2),-1,h) for h in H}
    rows=[]
    for expires in (False,True):
        no_rev=model(False,True,expires)
        with_rev=model(True,True,expires)
        no_wait=model(True,False,expires)
        assert all(with_rev[h]>=no_rev[h] and with_rev[h]>=no_wait[h] for h in H)
        rows.append({"courier_closes_after_first_action":expires,
          "wait_without_revoke":avg(no_rev),
          "wait_with_revoke":avg(with_rev),
          "revoke_feature_value_given_wait":avg({h:with_rev[h]-no_rev[h] for h in H}),
          "strict_roots_revoke_matters":sum(with_rev[h]>no_rev[h] for h in H),
          "no_wait_with_revoke":avg(no_wait),
          "wait_option_value":avg({h:with_rev[h]-no_wait[h] for h in H})})
    return {"id":"R83","name":"An actual disappearing opportunity instead of artificial wait penalties",
            "rules":"First-action safe courier pays 1.2; hidden high-value lane pays 3 if completed; one 75%-accurate clue after first action; wait and revoke each consume a die.",
            "ablations":rows}

def r84():
    """Natural work generates a noisy clue once, paid search gives perfect
    information but consumes one die. Correct public Bayesian posterior."""
    def model(acc,paid):
        @lru_cache(None)
        def dp(a,b,p,hand,query,clue):
            if not hand or (a>=3 and b>=3):
                return 3*(p*(a>=3)+(1-p)*(b>=3))
            choices=[]
            for i,die in enumerate(hand):
                rest=hand[:i]+hand[i+1:]
                if query:
                    choices.append(sum(ch*dp(a,b,post,rest,False,clue)
                                       for ch,post in signals(p,Q(1))))
                for goal in (0,1):
                    value=Q(0)
                    for gain,prob in enumerate(odds(die)):
                        if not prob:continue
                        na=min(3,a+gain) if goal==0 else a
                        nb=min(3,b+gain) if goal==1 else b
                        if clue:
                            nxt=sum(ch*dp(na,nb,post,rest,query,False)
                                   for ch,post in signals(p,acc))
                        else:nxt=dp(na,nb,p,rest,query,False)
                        value+=prob*nxt
                    choices.append(value)
            return max(choices)
        return {h:dp(0,0,Q(1,2),h,paid,True) for h in H}
    rows=[]
    for acc in (Q(1,2),Q(3,4),Q(1)):
        base=model(acc,False);full=model(acc,True)
        assert all(full[h]>=base[h] for h in H)
        rows.append({"free_clue_accuracy":float(acc),"normal_work_only":avg(base),
         "with_extra_paid_perfect_inquiry":avg(full),
         "paid_option_gain":avg({h:full[h]-base[h] for h in H}),
         "roots_paid_option_strictly_helps":sum(full[h]>base[h] for h in H)})
    assert rows[-1]["paid_option_gain"]==0
    return {"id":"R84","name":"Does a paid query survive normal activity that supplies information?",
            "rules":"Work naturally reveals an accuracy-dependent clue after its first action; an optional separate inquiry costs a die and reveals truth.","ablations":rows}

def r85():
    """P18-inspired paper map with health consequences. The target watched
    by a gunman updates to the player's last chosen route after action 2.
    Holding a photo doubles success value. Pier moves faster but destroys it.
    """
    def model(track,pier_allowed):
        @lru_cache(None)
        def dp(state,hand):
            prog,hp,aim,last,fire,photo=state
            if hp<=0:return Q(0)
            if prog>=5:return Q(1+photo)
            if not hand:return Q(0)
            t=4-len(hand);choices=[]
            for i,die in enumerate(hand):
                rest=hand[:i]+hand[i+1:]
                for move in ((0,1,2) if pier_allowed else (0,2)):
                    val=Q(0)
                    for gain,prob in enumerate(odds(die)):
                        if not prob:continue
                        if move==2:
                            new=(prog,hp-int(gain==0),aim,last,max(0,fire-gain),photo)
                        else:
                            steps=(0,2,3)[gain] if move==0 else (0,3,4)[gain]
                            damage=(fire if aim==move else 0)+int(gain==0)
                            new=(min(5,prog+steps),hp-damage,aim,move,fire,photo if move==0 else 0)
                        if t==1 and new[0]<5 and new[1]>0:
                            pp,hh,aa,ll,ff,dd=new
                            new=(pp,hh,ll if track else 0,ll,min(3,ff+1),dd)
                        val+=prob*dp(new,rest)
                    choices.append(val)
            return max(choices)
        return {h:dp((0,5,0,0,2,1),h) for h in H}
    t=model(True,True);s=model(False,True)
    nt=model(True,False);ns=model(False,False)
    return {"id":"R85","name":"P18-inspired route tracking with real public health and evidence costs",
      "rules":"Two rounds of two dice, gunfire damage when moving on aimed route, suppression lowers fire, pier sacrifices photographic proof, exit progress5.",
      "tracking":avg(t),"stationary":avg(s),
      "incremental_tracking_effect":avg({h:t[h]-s[h] for h in H}),
      "roots_tracking_changes_value":sum(t[h]!=s[h] for h in H),
      "pier_value_with_tracking":avg({h:t[h]-nt[h] for h in H}),
      "pier_value_without_tracking":avg({h:s[h]-ns[h] for h in H}),
      "limitation":"Standalone exact abstract proxy, NOT P18 native actor-composure and two-round Scheme replay."}
def main():
    rows=[r83(),r84(),r85()]
    out={"schema":"ssnoir.autonomous-2026-r83-r85/v1","rounds":3,
         "scope":"Exact Fraction, 126 weighted initial four-die hands; no native or human judgment",
         "results":rows}
    content=json.dumps(out,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(content,encoding="utf-8")
    else:print(content)
if __name__=="__main__":main()
