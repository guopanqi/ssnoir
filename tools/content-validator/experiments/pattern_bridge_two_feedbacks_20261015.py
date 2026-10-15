"""BRIDGE01-12: local crisis -> outcome permission -> next-contract goal switch.
Exact Fractions, public forecast Y, late demand W, and separate true-world
forward policy replay. Classic conditional access/option-value combination,
NOT a novel primitive, original Citizen Sleeper 2 implementation, or playtest.
"""
from fractions import Fraction as F
from itertools import product

E,S=0,1

def world_distribution(crisis=F(1,2), accuracy=F(3,4)):
    for h,y,w in product((0,1),repeat=3):
        yield h,y,w,(crisis if h else 1-crisis)*F(1,2)*(accuracy if y==w else 1-accuracy)

def stage2(has_e,has_s,w,alt_value=8):
    """Exactly two actions; E/S are different deliverables, ALT requires both."""
    plans=[]
    first=['SIDE','ALT1']+(['E'] if has_e else [])+(['S'] if has_s else [])
    second=['SIDE','ALT2']+(['E'] if has_e else [])+(['S'] if has_s else [])
    for a1,a2 in product(first,second):
        if (a1=='ALT1') != (a2=='ALT2'):continue
        if a1 in ('E','S') and a1==a2:continue
        if a1=='ALT1':
            pay=F(alt_value);delivered=('ALT',)
        else:
            delivered=tuple(a for a in (a1,a2) if a in ('E','S'))
            pay=sum(F(12 if int(a=='S')==w else 2)
                    for a in delivered)+sum(a=='SIDE' for a in (a1,a2))
        plans.append((pay,(a1,a2),delivered))
    best=max(p[0] for p in plans)
    return best,tuple(x for x in plans if x[0]==best)

def late_best(e,s,belief,alt_value=8,late_info=True):
    """Best value conditioned on public Y; W either revealed or still unknown."""
    if late_info:
        values=[stage2(e,s,w,alt_value)[0] for w in (E,S)]
        return (1-belief)*values[0]+belief*values[1]
    plans=[]
    first=['SIDE','ALT1']+(['E'] if e else [])+(['S'] if s else [])
    second=['SIDE','ALT2']+(['E'] if e else [])+(['S'] if s else [])
    for a1,a2 in product(first,second):
        if (a1=='ALT1') != (a2=='ALT2'):continue
        if a1 in ('E','S') and a1==a2:continue
        if a1=='ALT1':value=F(alt_value)
        else:
            value=sum((1-belief)*F(12 if a=='E' else 2)+
                      belief*F(12 if a=='S' else 2)
                      for a in (a1,a2) if a in ('E','S'))
            value+=sum(a=='SIDE' for a in (a1,a2))
        plans.append(value)
    return max(plans)

def post_first(h,y,q=F(3,4),late_info=True,alt=8):
    """RUSH result: hazard observed, Y predicts stage2 demand."""
    belief=F(q) if y==S else 1-F(q)
    options={'PRESS':(True,not h),'AID':(False,True)}
    scores={a:late_best(*flags,belief,alt,late_info)
            for a,flags in options.items()}
    best=max(scores.values())
    return best,tuple(a for a,v in scores.items() if v==best)

def solution(crisis=F(1,2),q=F(3,4),early_info=True,late_info=True,
             wait_bonus=1,alt=8,free_both=False):
    """Phase1 action RUSH vs WAIT; following action sees hazard, optionally Y.
    WAIT gets SIDE bonus and one guaranteed permission. Strong bypass flags
    are explicitly more powerful alternatives, not equal-cost same-effect.
    """
    crisis,q=F(crisis),F(q)
    if early_info:
        rush=sum(p*post_first(h,y,q,late_info,alt)[0]
                 for h,y,w,p in world_distribution(crisis,q))
        wait=F(wait_bonus)+sum(F(1,2)*max(
            late_best(True,False,q if y==S else 1-q,alt,late_info),
            late_best(False,True,q if y==S else 1-q,alt,late_info))
            for y in (E,S))
    else:
        rush=F()
        for h in (0,1):
            actions=((True,not h),(False,True))
            rush+=(crisis if h else 1-crisis)*max(
                late_best(e,s,F(1,2),alt,late_info) for e,s in actions)
        wait=F(wait_bonus)+max(late_best(e,s,F(1,2),alt,late_info)
                                for e,s in ((True,False),(False,True)))
    if free_both:
        wait=F(wait_bonus)+late_best(True,True,F(1,2),alt,late_info)
    out={'RUSH':rush,'WAIT':wait}
    return max(out.values()),out

def forward_policy():
    """Independently replay real H,Y,W and a SHORT human-readable policy."""
    expected=F()
    fields={k:F() for k in ('E','S','ALT','BOTH','SIDE','hazard','late_pivot')}
    for h,y,w,p in world_distribution():
        # Stage1: rush; no hazard gets both; hazard choose predicted demand.
        act='PRESS' if not h or y==E else 'AID'
        e,s=(True,not h) if act=='PRESS' else (False,True)
        score,plans=stage2(e,s,w)
        plan=plans[0]
        expected+=p*score
        if h:fields['hazard']+=p
        for k in ('E','S','ALT'):
            if k in plan[2]:fields[k]+=p
        if 'E' in plan[2] and 'S' in plan[2]:fields['BOTH']+=p
        if 'SIDE' in plan[1]:fields['SIDE']+=p
        if h and y!=w and 'ALT' in plan[2]:fields['late_pivot']+=p
    assert expected==F(103,8)
    assert fields=={'E':F(11,16),'S':F(11,16),'ALT':F(1,8),
                    'BOTH':F(1,2),'SIDE':F(3,8),'hazard':F(1,2),
                    'late_pivot':F(1,8)}
    return expected,fields

def regression():
    assert [stage2(True,True,w)[0] for w in (E,S)]==[14,14]
    assert stage2(True,False,E)[0]==13
    assert stage2(True,False,S)[0]==8
    assert post_first(1,E)[1]==('PRESS',)
    assert post_first(1,S)[1]==('AID',)
    assert post_first(0,E)[1]==post_first(0,S)[1]==('PRESS',)
    ablations={(early,late):solution(early_info=early,late_info=late)[0]
               for early,late in product((False,True),repeat=2)}
    assert ablations=={(False,False):F(11),
                       (True,False):F(49,4),
                       (False,True):F(49,4),
                       (True,True):F(103,8)}
    assert solution()[1]=={'RUSH':F(103,8),'WAIT':F(51,4)}
    assert solution(crisis=F(0))[0]==14
    assert solution(crisis=F(1))[0]==F(51,4)
    assert solution(crisis=F(1,4))[0]==F(215,16)
    assert solution(crisis=F(3,4))[0]==F(51,4)
    assert solution(wait_bonus=2)[0]==F(55,4)
    assert solution(free_both=True)[0]==15
    assert solution(alt=15)[0]==16
    for q in (F(1,2),F(2,3),F(3,4),F(4,5),F(9,10),F(1)):
        value,branch=solution(q=q)
        assert branch['RUSH']==11+F(5,2)*q
        assert branch['WAIT']==9+5*q
        assert (branch['WAIT']>=branch['RUSH'])==(q>=F(4,5))
        assert value==max(branch.values())
    for p in (F(0),F(1,4),F(1,2),F(5,9),F(3,4),F(1)):
        value,branch=solution(crisis=p)
        assert branch['RUSH']==14-F(9,4)*p
        assert branch['WAIT']==F(51,4)
        assert ('RUSH' in (a for a,v in branch.items() if v==value))==(p<=F(5,9))
    # Early and late signals have diminishing returns, not synergy.
    assert F(103,8)+11-2*F(49,4)==-F(5,8)
    print('BRIDGE ablations:',ablations)
    print('BRIDGE result identities:',forward_policy())
    print('BRIDGE01-12 PASS')

if __name__=='__main__':regression()
