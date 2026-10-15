"""RELAY: Crisis from work A may also produce progress toward B.
Exact finite-horizon Fraction DP, independent path replay, exhaustive fixed
scripts and short conditional-policy audit. No human-play claim.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
from collections import defaultdict

ACTIONS=('PUSH','STEADY','WORK_B','RECOVER')

class Rules:
    def __init__(self,n=4,productive=True):
        self.n=n;self.productive=productive
    def moves(self,s,action):
        t,a,b,pending,leaks=s
        if not t or action not in ACTIONS or (action=='RECOVER' and not pending):
            return ()
        losses=leaks+int(pending and action!='RECOVER')
        if action=='PUSH':
            return ((F(1,2),(t-1,min(5,a+2),b,1,losses)),
                    (F(1,2),(t-1,min(5,a+2),b,0,losses)))
        if action=='STEADY':return ((F(1),(t-1,min(5,a+1),b,0,losses)),)
        if action=='WORK_B':return ((F(1),(t-1,a,1,0,losses)),)
        return ((F(1),(t-1,a,int(b or self.productive),0,losses)),)
    def end(self,s):
        _,a,b,pending,leaks=s
        return F(10*(a==5)+3*(b==1)-(leaks+pending))

def dp_for(r):
    @lru_cache(None)
    def dp(s):
        if s[0]==0:return r.end(s),()
        options={action:sum((p*dp(next_s)[0] for p,next_s in r.moves(s,action)),F())
                 for action in ACTIONS if r.moves(s,action)}
        best=max(options.values())
        return best,tuple(a for a,v in options.items() if v==best)
    return dp

def replay(r,choice):
    """Enumerate true crisis coin outcomes without consulting DP values."""
    output=defaultdict(F)
    layers=[(F(1),(r.n,0,0,0,0),())]
    for _ in range(r.n):
        more=[]
        for p,s,h in layers:
            move=choice(s)
            assert r.moves(s,move), (s,move)
            for p2,next_s in r.moves(s,move):
                if p2:more.append((p*p2,next_s,h+((s,move,next_s),)))
        layers=more
    for p,s,h in layers:
        output['value']+=p*r.end(s)
        output['A']+=p*(s[1]==5)
        output['B']+=p*(s[2]==1)
        output['both']+=p*(s[1]==5 and s[2]==1)
        output['unresolved']+=p*(s[3]+s[4])
        output['mass']+=p
    assert output['mass']==1
    return output,layers

def best_fixed(r):
    """Enumerate 4^n scripts; an absent RECOVER wastes an action."""
    def value(s,seq):
        if not seq:return r.end(s)
        next_s=r.moves(s,seq[0])
        if not next_s:
            t,a,b,_,l=s
            next_s=((F(1),(t-1,a,b,0,l)),)
        return sum((p*value(s2,seq[1:]) for p,s2 in next_s),F())
    return max(((value((r.n,0,0,0,0),seq),seq)
                for seq in product(ACTIONS,repeat=r.n)),key=lambda z:z[0])

def always_recover(s):
    t,a,b,pending,leaks=s
    if pending:return 'RECOVER'
    if a<5:return 'PUSH'
    if not b:return 'WORK_B'
    return 'STEADY'

def short_rule(s):
    t,a,b,pending,leaks=s
    if t==1 and a==4:return 'STEADY'
    if pending:return 'RECOVER'
    if a<5:return 'PUSH'
    if not b:return 'WORK_B'
    return 'STEADY'

def regression():
    expected={3:(F(9),F(9)),4:(F(12),F(51,4)),
              5:(F(51,4),F(13)),6:(F(13),F(13)),7:(F(13),F(13))}
    for n in range(3,8):
        for productive in (False,True):
            r=Rules(n,productive);dp=dp_for(r)
            root=dp((n,0,0,0,0))[0]
            assert root==expected[n][int(productive)]
            result,_=replay(r,lambda s:dp(s)[1][0])
            assert result['value']==root
            fixed,plan=best_fixed(r)
            assert root>=fixed
            if n==4 and productive:
                assert fixed==F(12)
                assert replay(r,always_recover)[0]['value']==F(41,4)
                assert replay(r,short_rule)[0]['value']==root
                assert result['A']==result['B']==result['both']==1
                assert result['unresolved']==F(1,4)
                assert dp((3,2,0,1,0))[1]==('RECOVER',)
                assert dp((1,4,1,1,0))[1]==('STEADY',)
            print(n,productive,'DP',root,'fixed',fixed,'fixed_plan',plan)
    print('RELAY PASS: finite planning, open-loop, outcome vector, simple rule')

if __name__=='__main__':regression()
