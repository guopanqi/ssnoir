"""CASCADE01-08: faulty upstream premises invalidate shared downstream claims.
All probabilities Fraction; exact dynamic DP and independently replayed
hidden-world paths. No claim of new gameplay primitive or human validation.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

PREMISES=tuple(range(4))
DEPENDENCIES=((0,1),(1,2),(2,3))
RIGHT,WRONG,SAFE=F(9),F(-12),F(1)

def real_claim(fault,claim,repaired=False):
    return RIGHT if repaired or fault not in DEPENDENCIES[claim] else WRONG

@lru_cache(None)
def dp(worlds,time,claimed=0,tested=0,repaired=False,
       allow_repair=False,universal_repair=False,oracle=False,
       infer_repair=False):
    if time==0:return F(0),('END',)
    continuation=lambda ws,mask=claimed,known=tested,fixed=repaired:dp(
        ws,time-1,mask,known,fixed,allow_repair,universal_repair,
        oracle,infer_repair)[0]
    actions={'SAFE':SAFE+continuation(worlds)}
    for claim in range(3):
        if not claimed&(1<<claim):
            gain=sum((real_claim(fault,claim,repaired)
                      for fault in worlds),F())/len(worlds)
            actions[f'CLAIM{claim}']=gain+continuation(
                worlds,mask=claimed|(1<<claim))
    if not repaired:
        for premise in PREMISES:
            if tested&(1<<premise):continue
            val=F()
            for sub in (tuple(f for f in worlds if f==premise),
                        tuple(f for f in worlds if f!=premise)):
                if sub:val+=F(len(sub),len(worlds))*continuation(
                    sub,known=tested|(1<<premise))
            actions[f'TEST{premise}']=val
        if allow_repair and len(worlds)==1 and (
            infer_repair or tested&(1<<worlds[0])
        ):
            actions[f'REPAIR{worlds[0]}']=continuation(worlds,fixed=True)
        if universal_repair:
            actions['REPAIR_ALL']=continuation(worlds,fixed=True)
        if oracle and len(worlds)>1:
            actions['ORACLE']=sum((
                continuation((f,),known=tested|(1<<f)) for f in worlds
            ),F())/len(worlds)
    best=max(actions.values())
    return best,tuple(a for a,v in actions.items() if v==best)

def replay(time,**flags):
    result={}
    for fault in PREMISES:
        worlds=PREMISES
        left,claimed,tested,repaired=time,0,0,False
        score,actions=F(),[]
        while left:
            act=dp(worlds,left,claimed,tested,repaired,**flags)[1][0]
            actions.append(act)
            if act=='SAFE':
                score+=SAFE
            elif act.startswith('CLAIM'):
                c=int(act[-1])
                assert not claimed&(1<<c)
                score+=real_claim(fault,c,repaired)
                claimed|=1<<c
            elif act.startswith('TEST'):
                i=int(act[-1])
                assert not tested&(1<<i)
                tested|=1<<i
                worlds=tuple(f for f in worlds if (f==i)==(fault==i))
            elif act.startswith('REPAIR'):
                assert act=='REPAIR_ALL' or worlds==(fault,)
                repaired=True
            elif act=='ORACLE':
                worlds=(fault,)
                tested|=1<<fault
            else:raise AssertionError(act)
            assert fault in worlds
            left-=1
        result[fault]=(score,tuple(actions),repaired)
    avg=sum((x[0] for x in result.values()),F())/len(PREMISES)
    assert avg==dp(PREMISES,time,**flags)[0],(avg,result)
    return avg,result

def fixed_open_loop(n):
    best=(F(-10**9),())
    alphabet=('SAFE','CLAIM0','CLAIM1','CLAIM2',
              'TEST0','TEST1','TEST2','TEST3')
    for seq in product(alphabet,repeat=n):
        if len(set(a for a in seq if a!='SAFE'))!=sum(
            a!='SAFE' for a in seq
        ):continue
        value=sum((
            sum((real_claim(f,int(a[-1])) for f in PREMISES),F())/4
            if a.startswith('CLAIM') else
            SAFE if a=='SAFE' else F()
            for a in seq
        ),F())
        if value>best[0]:best=(value,seq)
    return best

def score_first_test(n,target,**flags):
    return sum((
        F(len(sub),4)*dp(sub,n-1,tested=1<<target,**flags)[0]
        for sub in ((target,),
                    tuple(w for w in PREMISES if w!=target))
    ),F())

def regression():
    assert [dp(PREMISES,n)[0] for n in range(1,7)]==[
        F(1),F(15,4),F(45,4),F(55,4),F(59,4),F(63,4)]
    assert [dp(PREMISES,n,allow_repair=True)[0]
            for n in range(1,7)]==[
        F(1),F(15,4),F(45,4),F(14),F(81,4),F(93,4)]
    for n in (3,4,5):
        base=replay(n)
        repair=replay(n,allow_repair=True)
        assert fixed_open_loop(n)[0]==n
        assert base[0]<=repair[0]
        print('n',n,'detect_only',base[0],
              'diagnose_and_repair',repair[0],
              'fixed_schedule',n,'per_fault',repair[1])
    assert dp(PREMISES,4)[1]==('TEST0','TEST3')
    assert dp(PREMISES,4,allow_repair=True)[1]==('TEST1','TEST2')
    assert [score_first_test(4,i) for i in PREMISES]==[
        F(55,4),F(49,4),F(49,4),F(55,4)]
    assert [score_first_test(4,i,allow_repair=True) for i in PREMISES]==[
        F(55,4),F(14),F(14),F(55,4)]
    assert dp(PREMISES,4,allow_repair=True,oracle=True)[0]==F(37,2)
    assert dp(PREMISES,5,allow_repair=True,oracle=True)[0]==F(27)
    assert dp(PREMISES,4,allow_repair=True,universal_repair=True)[0]==F(27)
    assert dp(PREMISES,5,allow_repair=True,universal_repair=True)[0]==F(28)
    for n in range(2,7):
        assert dp(PREMISES,n,allow_repair=True)[0]==dp(
            PREMISES,n,allow_repair=True,infer_repair=True)[0]
    print('CASCADE01-08 PASS')

if __name__=='__main__':
    regression()
