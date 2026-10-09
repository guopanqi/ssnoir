#!/usr/bin/env python3
"""R76–R78: exploratory *non-identical* relations, exact four-die paper models.

R76: location change consumes a die and affects travel speed/document quality.
R77: imperfect signal purchase changes posterior, not hidden truth visibility.
R78: public evidence arrives after first commitment; a one-time costly switch
     can reclaim an otherwise bad commitment.

Outputs exact rational expectation (rounded to 9 decimals), actual option-value
ablation and root-hand differences. Does not assert human fun or C# parity.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement
from r56_r65_autonomous_search import odds,hand_weight
import json

N=4
HANDS=tuple(combinations_with_replacement(range(1,7),N))
def expectations(fn):
    acc=F(0);witness=0
    for hand in HANDS:
        x,y=fn(hand)
        assert x>=y,(hand,x,y)
        acc+=hand_weight(hand)*(x-y)
        witness+=x>y
    return round(float(acc),9),witness

def r76():
    # s=(lane, progress, alert, documentary_integrity).
    # Lane 0 safe+slow; lane1 fast, permanently sacrifices evidence and
    # alerts guards. Changing lane costs one action die but clears alert.
    def update(s,act,gain):
        lane,progress,alert,document=s
        if act=="move":return (1-lane,progress,0,document)
        if act=="hide":return (lane,progress,max(0,alert-2),document)
        inc=gain if lane==0 else (0 if gain==0 else gain+1)
        return lane,min(5,progress+inc),alert+(1 if lane==1 else 0),document if lane==0 else 0
    def points(s):
        lane,progress,alert,document=s
        return F(1+document) if progress>=5 else F(0)
    def done(s):return s[1]>=5 or s[2]>=3
    @lru_cache(None)
    def value(s,hand,allow_move):
        if not hand or done(s):return points(s)
        valid=["advance"]
        if allow_move:valid.append("move")
        if s[2]>0:valid.append("hide")
        return max(choice(s,hand,act,i,allow_move)
                   for act in valid for i in range(len(hand)))
    def choice(s,hand,act,i,allow_move):
        rest=hand[:i]+hand[i+1:]
        if act!="advance":return value(update(s,act,0),rest,allow_move)
        return sum(p*value(update(s,act,k),rest,allow_move)
                   for k,p in enumerate(odds(hand[i])) if p)
    optimal=baseline=F(0);diffs=0
    for hand in HANDS:
        v=value((0,0,0,1),hand,True)
        w=value((0,0,0,1),hand,False)
        weight=hand_weight(hand)
        optimal+=weight*v;baseline+=weight*w
        diffs+=v>w
    return {"id":"R76","relation":"costly spatial lane switch vs fixed starting route",
            "expected_with_change":round(float(optimal),9),
            "expected_without_change":round(float(baseline),9),
            "direct_option_gain":round(float(optimal-baseline),9),
            "strict_starting_hands":diffs,
            "boundary":"Simplified two-lane spatial abstraction; not a full map or engine encounter"}

def signal_branches(p,accuracy):
    # Belief update: truthful signal has accuracy, hidden type A has prior p.
    chance_a=p*accuracy+(1-p)*(1-accuracy)
    chance_b=1-chance_a
    result=[]
    if chance_a:
        result.append((chance_a,p*accuracy/chance_a))
    if chance_b:
        result.append((chance_b,p*(1-accuracy)/chance_b))
    return result

def r77():
    # progress A/B must each reach 3; only one hidden truth pays reward3.
    @lru_cache(None)
    def val(a,b,p,hand,look):
        if not hand or (a==3 and b==3):
            return 3*(p*F(a==3)+(1-p)*F(b==3))
        acts=[0,1]+([2] if look else [])
        return max(q(a,b,p,hand,g,i,look) for g in acts for i in range(len(hand)))
    def q(a,b,p,hand,g,i,look):
        remaining=hand[:i]+hand[i+1:]
        if g==2:
            def obs(k):
                if k==0:return val(a,b,p,remaining,False)
                accuracy=F(3,4) if k==1 else F(1)
                return sum(pr*val(a,b,post,remaining,False) for pr,post in signal_branches(p,accuracy))
            return sum(pr*obs(k) for k,pr in enumerate(odds(hand[i])) if pr)
        return sum(pr*val(min(3,a+k) if g==0 else a,
                          min(3,b+k) if g==1 else b,p,remaining,look)
                   for k,pr in enumerate(odds(hand[i])) if pr)
    total_yes=total_no=F(0);strict=0
    for hand in HANDS:
        y=val(0,0,F(1,2),hand,True)
        n=val(0,0,F(1,2),hand,False)
        assert y>=n
        total_yes+=hand_weight(hand)*y
        total_no+=hand_weight(hand)*n
        strict+=y>n
    return {"id":"R77","relation":"paid imperfect information with correctly updated posterior belief",
        "expected_with_inquiry":round(float(total_yes),9),
        "expected_without_inquiry":round(float(total_no),9),
        "direct_option_gain":round(float(total_yes-total_no),9),
        "strict_starting_hands":strict,
        "boundary":"Inspection outcome is public, truth remains hidden; independent completion rewards only match hidden type"}

def r78():
    # The public clue arrives after the first action; before the first action
    # the player can commit OR consume a die waiting for the clue. Without this
    # wait option, the value of "revoke" is likely exaggerated by a forced
    # early commitment. Audit both conditions independently.
    @lru_cache(None)
    def value(a,b,commit,p,hand,can_revoke,allow_wait):
        if not hand:
            return 3*(p*F(a>=2)+(1-p)*F(b>=2))
        t=N-len(hand)
        acts=[0,1] if commit<0 else [commit]
        if commit<0 and t==0 and allow_wait:acts.append(3)
        if commit>=0 and can_revoke and t>=1:acts.append(2)
        return max(q(a,b,commit,p,hand,g,i,can_revoke,allow_wait,t)
                   for g in acts for i in range(len(hand)))
    def q(a,b,commit,p,hand,g,i,can_revoke,allow_wait,t):
        rem=hand[:i]+hand[i+1:]
        if g==2:
            return value(0,0,1-commit,p,rem,False,allow_wait)
        if g==3:
            return F(1,2)*value(a,b,-1,F(4,5),rem,can_revoke,allow_wait)+F(1,2)*value(a,b,-1,F(1,5),rem,can_revoke,allow_wait)
        def rec(new_a,new_b):
            if t==0:
                return (F(1,2)*value(new_a,new_b,g,F(4,5),rem,can_revoke,allow_wait)
                        +F(1,2)*value(new_a,new_b,g,F(1,5),rem,can_revoke,allow_wait))
            return value(new_a,new_b,g,p,rem,can_revoke,allow_wait)
        return sum(pr*rec(min(2,a+k) if g==0 else a,
                          min(2,b+k) if g==1 else b)
                   for k,pr in enumerate(odds(hand[i])) if pr)
    totals=[F(0)]*4; strict=0
    for hand in HANDS:
        # 0 forced early+revocation, 1 forced early+no revoke,
        # 2 opt to wait+revocation, 3 opt to wait+no revoke.
        outcomes=[value(0,0,-1,F(1,2),hand,r,w)
                  for r,w in ((True,False),(False,False),(True,True),(False,True))]
        assert outcomes[0]>=outcomes[1] and outcomes[2]>=outcomes[3]
        assert outcomes[2]>=outcomes[0]
        strict+=outcomes[2]>outcomes[3]
        for i,v in enumerate(outcomes):totals[i]+=hand_weight(hand)*v
    return {"id":"R78","relation":"one-time costly rescind option vs delaying commitment until public evidence",
            "expected_reversible_forced_early":round(float(totals[0]),9),
            "expected_irreversible_forced_early":round(float(totals[1]),9),
            "expected_reversible_with_wait":round(float(totals[2]),9),
            "expected_irreversible_with_wait":round(float(totals[3]),9),
            "forced_early_option_gain":round(float(totals[0]-totals[1]),9),
            "direct_option_gain":round(float(totals[2]-totals[3]),9),
            "strict_starting_hands":strict,
            "boundary":"Wait consumes an action and can avoid premature commitment; same reveal and remaining dice, no native or human evidence"}

def main():
    results=[r76(),r77(),r78()]
    out={"schema":"ssnoir.autonomous-diverse-relations/v1",
         "note":"All three use exact Fraction, skill1 four d6, 126 weighted root hands; different reward definitions are not cross-comparable.",
         "results":results}
    from sys import argv
    text=json.dumps(out,ensure_ascii=False,indent=2)+"\n"
    if len(argv)>1:
        from pathlib import Path
        Path(argv[1]).write_text(text,encoding="utf8")
    else:print(text)
if __name__=="__main__":main()
