"""FAIL-PIVOT: deliberately leave A unfinished to claim alternative B.
Two early action slots, then a public world signal and three late slots.
Exact complete action-sequence enumeration; known branching-outcome motif.
"""
from fractions import Fraction as F
from itertools import product

EARLY=('A','SIDE')
LATE=('A','B','SIDE')
A_REWARD=(15,3)
B_REWARD=(10,16)

def value(early,late,world,gate=True):
    assert len(early)==2 and len(late)==3
    a=early.count('A')+late.count('A')
    b=late.count('B')
    safe=early.count('SIDE')+late.count('SIDE')
    alt_possible=early.count('A')>=1 if gate else True
    if a>=5:return F(A_REWARD[world]+safe),'A'
    if b>=2 and alt_possible:return F(B_REWARD[world]+safe),'B'
    return F(safe),'NONE'

def optimal(gate=True,prior=F(1,2)):
    records=[]
    for early in product(EARLY,repeat=2):
        late_worlds=[]
        for w in (0,1):
            paths=[(value(early,late,w,gate)[0],late)
                   for late in product(LATE,repeat=3)]
            best=max(x[0] for x in paths)
            late_worlds.append((best,tuple(seq for v,seq in paths if v==best)))
        ev=prior*late_worlds[0][0]+(1-prior)*late_worlds[1][0]
        records.append((ev,early,tuple(late_worlds)))
    best=max(x[0] for x in records)
    return best,tuple(row for row in records if row[0]==best),records

def static(gate=True,prior=F(1,2)):
    plans=[]
    for early in product(EARLY,repeat=2):
        for late in product(LATE,repeat=3):
            ev=prior*value(early,late,0,gate)[0]+(1-prior)*value(early,late,1,gate)[0]
            plans.append((ev,early,late))
    best=max(x[0] for x in plans)
    return best,tuple(row for row in plans if row[0]==best)

def no_alt(prior=F(1,2)):
    plans=[]
    for early in product(EARLY,repeat=2):
        for late in product(LATE,repeat=3):
            a=early.count('A')+late.count('A')
            safe=early.count('SIDE')+late.count('SIDE')
            ev=safe+(prior*A_REWARD[0]+(1-prior)*A_REWARD[1] if a>=5 else 0)
            plans.append((ev,early,late))
    return max(plans)

def regression():
    dp,policies,_=optimal()
    assert dp==16
    assert policies[0][1]==('A','A')
    assert policies[0][2][0][0]==15
    assert policies[0][2][1][0]==17
    assert any(value(('A','A'),late,1)[1]=='B'
               for late in policies[0][2][1][1])
    fixed,_=static()
    assert fixed==15
    assert no_alt()[0]==9
    ungated=optimal(False)[0]
    assert ungated==16
    assert any(row[1]==('SIDE','SIDE') for row in optimal(False)[1])
    print('dynamic',dp,'static',fixed,'no B route',no_alt()[0],
          'B without early A gate',ungated)
    for p in (F(1,10),F(1,5),F(1,4),F(1,2),F(3,4),F(9,10)):
        dynamic,choices,_=optimal(True,p)
        fixed,_=static(True,p)
        assert dynamic>=fixed
        print('prior P(A world)',p,'dynamic',dynamic,'fixed',fixed,
              'early',sorted({row[1] for row in choices}))
    print('FAIL-PIVOT PASS')

if __name__=='__main__':regression()
