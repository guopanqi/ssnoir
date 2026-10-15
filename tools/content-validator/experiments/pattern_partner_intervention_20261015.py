"""MEMORY03/04: permanent ability-improvement action versus learning.
Hidden H success 4/5; hidden L success 1/5, then 2/5 or 13/20 after COACH.
This opponent does not strategically adapt. All probabilities exact Fractions.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
HIGH,LOW=F(4,5),F(1,5)

@lru_cache(None)
def dp(n,low_after,p=F(1,2),trained=False,allow=True,learn=True):
    if n==0:return F(0),()
    hi=HIGH;lo=low_after if trained else LOW
    success=p*hi+(1-p)*lo
    yes=p*hi/success if learn else p
    no=p*(1-hi)/(1-success) if learn else p
    opts=[(F(1)+dp(n-1,low_after,p,trained,allow,learn)[0],'SAFE'),
          (success*(7+dp(n-1,low_after,yes,trained,allow,learn)[0])+
           (1-success)*(-5+dp(n-1,low_after,no,trained,allow,learn)[0]),'TRY')]
    if allow and not trained:
        opts.append((dp(n-1,low_after,p,True,allow,learn)[0],'COACH'))
    best=max(v for v,_ in opts)
    return best,tuple(action for v,action in opts if v==best)

def forward(n,low_after):
    """Independently replay actual H/L worlds under the DP policy."""
    paths=[]
    for typ in ('H','L'):
        def run(left,p,trained,weight,gain,steps):
            if left==0:
                paths.append((weight,gain,steps));return
            action=dp(left,low_after,p,trained)[1][0]
            if action=='SAFE':
                run(left-1,p,trained,weight,gain+1,steps+('SAFE',))
            elif action=='COACH':
                assert not trained
                run(left-1,p,True,weight,gain,steps+('COACH',))
            else:
                hi=HIGH;lo=low_after if trained else LOW
                for tag in ('+','-'):
                    l_hi=hi if tag=='+' else 1-hi
                    l_lo=lo if tag=='+' else 1-lo
                    likelihood=l_hi if typ=='H' else l_lo
                    posterior=p*l_hi/(p*l_hi+(1-p)*l_lo)
                    run(left-1,posterior,trained,weight*likelihood,
                        gain+(7 if tag=='+' else -5),steps+('TRY'+tag,))
        run(n,F(1,2),False,F(1,2),F(0),())
    assert sum((w for w,_,_ in paths),F())==1
    val=sum((w*gain for w,gain,_ in paths),F())
    coach_rate=sum((w*('COACH' in seq) for w,_,seq in paths),F())
    return val,coach_rate

def fixed(n,low_after):
    best=(F(-100000),None)
    for actions in product(('TRY','SAFE','COACH'),repeat=n):
        if actions.count('COACH')>1:continue
        gain=F(0);trained=False
        for act in actions:
            if act=='COACH':trained=True
            elif act=='SAFE':gain+=1
            else:
                prob=(HIGH+(low_after if trained else LOW))/2
                gain+=prob*7+(1-prob)*(-5)
        best=max(best,(gain,actions))
    return best

def regression():
    cases=[(F(13,20),4,F(111,10),'COACH'),
           (F(13,20),7,F(111,5),'COACH'),
           (F(2,5),4,F(4633,625),'TRY'),
           (F(2,5),8,F(2616663,156250),'TRY')]
    for low,n,expected,first in cases:
        value,actions=dp(n,low)
        assert value==expected and first in actions
        verify,rate=forward(n,low)
        assert verify==value
        print('low_after',low,'n',n,'adaptive',value,'first',actions,
              'precommitted',fixed(n,low),'coaching_probability',rate)
    assert dp(4,F(13,20),learn=False)[0]==F(111,10)
    assert fixed(4,F(13,20))[0]==F(111,10)
    assert dp(7,F(2,5),p=F(1,5))[1]==('COACH',)
    assert dp(8,F(2,5),allow=False)[0]==F(1297813,78125)
    assert dp(8,F(2,5))[0]-dp(8,F(2,5),allow=False)[0]==F(21037,156250)
    assert dp(8,F(2,5),learn=False)[0]==F(77,5)
    print('MEMORY03/04 PASS')

if __name__=='__main__':regression()
