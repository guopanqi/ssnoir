"""PLEDGE01-09: early certified positive notice and late verifiable correction.
Exact Fraction enumeration and an independent per-world payoff ledger.
This is a planning/communication model, NOT a signalling equilibrium or playtest.
"""
from fractions import Fraction as F
from itertools import product

Q=F(3,4)
A_GOOD,A_BAD=F(4),F(-6)
B_GOOD,B_BAD=F(4),F(-6)
SIDE=F(1)
PUB_BONUS=F(2)
FALSE_REPUTATION=F(-12)
WORLDS=tuple((t,s,F(1,2)*(Q if t==s else 1-Q))
             for t,s in product((0,1),repeat=2))
assert sum((p for _,_,p in WORLDS),F())==1

def agent_response(prob_good,gain,loss):
    """An audience chooses to invest only when expected private return >0."""
    return prob_good*gain+(1-prob_good)*loss>0

assert agent_response(Q,A_GOOD,A_BAD)
assert not agent_response(1-Q,A_GOOD,A_BAD)
assert agent_response(F(1),B_GOOD,B_BAD)
assert not agent_response(F(0),B_GOOD,B_BAD)

def ledger(t,s,early,late,bonus=PUB_BONUS,false_cost=-FALSE_REPUTATION,
           allow_send=True,free_automatic_truth=False,allow_early_issue=True,
           allow_negative_retraction=True):
    """(early actor, late actor, publicity, false assurance penalty, side job).

    t=1 means safe; s=1 a certified but noisy positive signal.
    ISSUE only when s=1. It triggers irrevocable A action; B acts later.
    Late SEND contains independently verifiable T, not cheap talk.
    """
    assert t in (0,1) and s in (0,1)
    assert early in ('HOLD','ISSUE') and late in ('SIDE','SEND')
    assert early!='ISSUE' or (s and allow_early_issue)
    assert allow_send or late!='SEND'
    assert allow_negative_retraction or not (
        early=='ISSUE' and not t and late=='SEND')
    early_invest=early=='ISSUE' and agent_response(Q,A_GOOD,A_BAD)
    if free_automatic_truth:
        b_invest=bool(t)
    elif late=='SEND':
        b_invest=agent_response(F(t),B_GOOD,B_BAD)
    else:
        b_invest=early=='ISSUE' and agent_response(Q,B_GOOD,B_BAD)
    a=(A_GOOD if t else A_BAD) if early_invest else F()
    b=(B_GOOD if t else B_BAD) if b_invest else F()
    publicity=F(bonus) if early=='ISSUE' else F()
    penalty=-F(false_cost) if early=='ISSUE' and t==0 else F()
    side=SIDE if late=='SIDE' else F()
    return (a,b,publicity,penalty,side)

def payoff(v):return sum(v,F())
def posterior(s):return Q if s else 1-Q

def late_options(early,t,s,**kw):
    actions=['SIDE']
    if kw.get('allow_send',True) and (
        kw.get('allow_negative_retraction',True) or
        not (early=='ISSUE' and not t)):
        actions.append('SEND')
    return {a:ledger(t,s,early,a,**kw) for a in actions}

def best_conditional(s,early,**kw):
    p=posterior(s)
    sub=[]
    for t in (0,1):
        choices=late_options(early,t,s,**kw)
        value=max(payoff(v) for v in choices.values())
        best=tuple(a for a,v in choices.items() if payoff(v)==value)
        sub.append((p if t else 1-p,value,best))
    return sum((w*v for w,v,_ in sub),F()),tuple(x[-1] for x in sub)

def full_dynamic(**kw):
    by_s={}
    for s in (0,1):
        options={e:best_conditional(s,e,**kw)[0]
                 for e in (['HOLD','ISSUE'] if s else ['HOLD'])}
        highest=max(options.values())
        by_s[s]=(highest,tuple(e for e,v in options.items() if v==highest),
                 options)
    return sum((by_s[s][0] for s in (0,1)),F())/2,by_s

def best_open_loop(**kw):
    """Both moves prewritten by initial signal, no late contingency on T.
    Prewritten SEND still delivers verifiable T to the receiver.
    """
    plans={}
    for s in (0,1):
        possibilities=[]
        first=['HOLD','ISSUE'] if s else ['HOLD']
        second=['SIDE','SEND'] if kw.get('allow_send',True) else ['SIDE']
        for early,late in product(first,second):
            p=posterior(s)
            value=sum((w*payoff(ledger(t,s,early,late,**kw))
                       for t,w in ((0,1-p),(1,p))),F())
            possibilities.append((value,early,late))
        hi=max(v for v,_,_ in possibilities)
        plans[s]=(hi,tuple((e,l) for v,e,l in possibilities if v==hi))
    return sum((plans[s][0] for s in (0,1)),F())/2,plans

def policy_ledger(policy,**kw):
    vectors=[]
    for t,s,p in WORLDS:
        e,l=policy(s,t)
        vectors.append((t,s,p,ledger(t,s,e,l,**kw)))
    mean=tuple(sum((p*v[i] for _,_,p,v in vectors),F()) for i in range(5))
    return mean,vectors

def perfect_early_information(price=F()):
    """STRONGER alternative: true state is certified *before* ISSUE deadline.
    The fee is cash, not an action; do not call this equal-work substitute.
    """
    return (A_GOOD+B_GOOD+PUB_BONUS+SIDE+SIDE)/2-F(price)

def regression():
    full,policy=full_dynamic()
    fixed,plans=best_open_loop()
    no_send,_=full_dynamic(allow_send=False)
    no_retract,_=full_dynamic(allow_negative_retraction=False)
    automatic,_=full_dynamic(free_automatic_truth=True)
    assert (full,fixed,no_send,no_retract,automatic)==(
        F(3),F(9,4),F(2),F(5,2),F(13,4))
    assert policy[1][1]==('ISSUE',) and policy[0][1]==('HOLD',)
    assert best_conditional(1,'ISSUE')[1]==(('SEND',),('SIDE',))
    assert best_conditional(1,'HOLD')[1]==(('SIDE',),('SEND',))
    assert full-fixed==F(3,4) and full-no_retract==F(1,2)
    # An equally powerful prewritten T-contingent rule reaches the same value.
    contingent=lambda s,t:(
        ('ISSUE','SEND' if t==0 else 'SIDE') if s else
        ('HOLD','SEND' if t==1 else 'SIDE'))
    mean,worlds=policy_ledger(contingent)
    assert mean==(F(3,4),F(2),F(1),F(-3,2),F(3,4))
    assert sum(mean)==full
    assert [(t,s,payoff(v)) for t,s,_,v in worlds]==[
        (0,0,F(1)),(0,1,F(-16)),(1,0,F(4)),(1,1,F(11))]
    assert [perfect_early_information(i) for i in range(5)]==[
        F(6),F(5),F(4),F(3),F(2)]
    for reward in (0,1,2,3,4):
        val,rule=full_dynamic(bonus=F(reward))
        assert val==max(F(5,2),F(2)+F(reward,2))
        assert ('ISSUE' in rule[1][1])==(reward>=1)
    for penalty in (0,8,12,16,20):
        val,rule=full_dynamic(false_cost=F(penalty))
        assert val==max(F(5,2),F(9,2)-F(penalty,8))
        assert ('ISSUE' in rule[1][1])==(penalty<=16)
    assert Q*A_GOOD+(1-Q)*A_BAD==F(3,2)
    print('full',full,'fixed late',fixed,'no_send',no_send,
          'no false retraction',no_retract,'automatic B truth',automatic)
    print('world identity',worlds,'mean 5-axis result',mean)
    print('PLEDGE01-09 PASS')

if __name__=='__main__':regression()
