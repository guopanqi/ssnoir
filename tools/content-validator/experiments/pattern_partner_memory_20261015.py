"""MEMORY01/02: partner history and the value of active inspection.
Exact Fraction belief DP; independent forward pass over actual hidden types.
This is a fixed hidden trait, not a strategic/learning opponent.
"""
from fractions import Fraction as F
from functools import lru_cache

HIGH, LOW = F(4,5), F(1,5)
PROBE_H, PROBE_L = F(3,4), F(1,4)

def update(p, like_h, like_l):
    prob=p*like_h+(1-p)*like_l
    return prob, p*like_h/prob

@lru_cache(None)
def dp(turns,p,persistent=True,inspection=True):
    if turns==0:return F(0),()
    choices=[(F(1)+dp(turns-1,p,persistent,inspection)[0],'SAFE')]
    s,good=update(p,HIGH,LOW)
    f,bad=update(p,1-HIGH,1-LOW)
    if not persistent:good=bad=F(1,2)
    choices.append((s*(7+dp(turns-1,good,persistent,inspection)[0])+
                    f*(-5+dp(turns-1,bad,persistent,inspection)[0]),'TRY'))
    if inspection:
        yes,a=update(p,PROBE_H,PROBE_L)
        no,b=update(p,1-PROBE_H,1-PROBE_L)
        if not persistent:a=b=F(1,2)
        choices.append((yes*dp(turns-1,a,persistent,inspection)[0]+
                        no*dp(turns-1,b,persistent,inspection)[0],'PROBE'))
    best=max(val for val,_ in choices)
    return best,tuple(a for val,a in choices if val==best)

def replay(turns,rule):
    """Forward world enumeration, not a reverse-DP evaluation."""
    records=[]
    for true_type in ('H','L'):
        def walk(n,p,weight,gain,succ,fail,steps):
            if n==0:
                records.append((weight,gain,steps));return
            act=rule(n,p,succ,fail)
            if act=='SAFE':
                walk(n-1,p,weight,gain+1,succ,fail,steps+('SAFE',))
                return
            if act=='TRY':
                real=HIGH if true_type=='H' else LOW
                for mark,chance,lh,ll,reward in (
                    ('+',real,HIGH,LOW,7),
                    ('-',1-real,1-HIGH,1-LOW,-5)):
                    _,pp=update(p,lh,ll)
                    walk(n-1,pp,weight*chance,gain+reward,
                         succ+(mark=='+'),fail+(mark=='-'),steps+('TRY'+mark,))
                return
            if act=='PROBE':
                real=PROBE_H if true_type=='H' else PROBE_L
                for mark,chance,lh,ll in (
                    ('+',real,PROBE_H,PROBE_L),
                    ('-',1-real,1-PROBE_H,1-PROBE_L)):
                    _,pp=update(p,lh,ll)
                    walk(n-1,pp,weight*chance,gain,succ,fail,steps+('PROBE'+mark,))
                return
            raise ValueError(act)
        walk(turns,F(1,2),F(1,2),F(0),0,0,())
    assert sum((w for w,_,_ in records),F(0))==1
    return (sum((w*g for w,g,_ in records),F(0)),
            sum((w*sum(x.startswith('PROBE') for x in s) for w,_,s in records),F(0)))

def best_rule(n,p,succ,fail):
    return dp(n,p)[1][0]

def balance_rule(n,p,succ,fail):
    return 'TRY' if succ>=fail else 'SAFE'

def first_failure_rule(n,p,succ,fail):
    return 'TRY' if fail==0 else 'SAFE'

def regression():
    roots={1:F(1),2:F(77,25),3:F(129,25),4:F(4633,625),
           5:F(6041,625),6:F(187089,15625),7:F(179341,12500)}
    for n,root in roots.items():
        val,_=dp(n,F(1,2))
        assert val==root
        forward,probe_count=replay(n,best_rule)
        assert forward==val
        balance,_=replay(n,balance_rule)
        assert (balance==val)==(n<=6)
        passive=dp(n,F(1,2),inspection=False)[0]
        if n<=6:assert passive==val and probe_count==0
        assert dp(n,F(1,2),persistent=False)[0]==n
        print(n,'adaptive',val,'fixed',n,'balance',balance,
              'no_probe',passive,'probes_per_run',probe_count)
    assert dp(7,F(1,2))[0]-dp(7,F(1,2),inspection=False)[0]==F(4093,62500)
    assert dp(6,F(1,5))[1]==('PROBE',)
    assert dp(4,F(1,2))[1]==('TRY',)
    assert dp(3,F(1,5))[1]==('SAFE',)
    assert replay(4,first_failure_rule)[0]==F(4417,625)
    print('MEMORY01/02 PASS')

if __name__=='__main__':regression()
