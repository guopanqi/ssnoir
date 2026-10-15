"""SOURCE07-09: a player's own PUBLICIZE action turns subsequent reports
into echoes instead of independent evidence. Exact complete-world enumeration.
This is a deliberately short negative boundary: optimal action can be fixed
in advance for the frozen one-period protocol.
"""
from fractions import Fraction as F
from itertools import product
ACC=F(3,4);SAFE=F(3);RIGHT=F(10);WRONG=F(-28)

def value(publicize=False,publicity=F(),third=False,cost_third=F()):
    world={}
    for t,s1,s2,s3 in product((0,1),repeat=4):
        v=F(1,2)*(ACC if s1==t else 1-ACC)
        v*=(F(1) if s2==s1 else F()) if publicize else (ACC if s2==t else 1-ACC)
        v*=ACC if s3==t else 1-ACC
        if v:world[t,s1,s2,s3]=v
    assert sum(world.values(),F())==1
    out=F()
    for s1,s2 in product((0,1),repeat=2):
        choices=[(s1,s2,k) for k in ((0,1) if third else (None,))]
        for _,_,s3 in choices:
            p0=sum((w for (t,a,b,c),w in world.items()
                    if t==0 and a==s1 and b==s2 and (s3 is None or c==s3)),F())
            p1=sum((w for (t,a,b,c),w in world.items()
                    if t==1 and a==s1 and b==s2 and (s3 is None or c==s3)),F())
            mass=p0+p1
            if not mass:continue
            out+=max(mass*SAFE,p0*RIGHT+p1*WRONG,p1*RIGHT+p0*WRONG)
    return out+(publicity if publicize else 0)-(cost_third if third else 0)

def regression():
    for reward in (0,1,2,3):
        hold=value(False,F(reward))
        share=value(True,F(reward))
        assert hold==5
        assert share==3+reward
        assert (share>hold)==(reward>2)
        print('publicity',reward,'HOLD independent report',hold,
              'PUBLICIZE echo',share)
    # New independent report compensates for an echoed second report,
    # but it costs an action; it does not magically create proof for free.
    assert value(True,F(1),third=True,cost_third=F(1))==5
    assert value(False,F(1),third=True,cost_third=F(1))==F(143,32)
    assert value(False,F(1),third=True,cost_third=F())==F(175,32)
    assert value(True,F(3),third=True,cost_third=F(1))==7
    print('SOURCE07-09 PASS: endogenous echo but short fixed decision')

if __name__=='__main__':regression()
