"""CASCADE09-15: one false premise versus independent faults, under
different prerequisite graphs. Preserves each marginal P(premise fails)=1/4,
but changes joint correlation. Exact Fraction DP and real-world replay.
"""
from fractions import Fraction as F
from functools import lru_cache
from pattern_belief_revision_cascade_20261015 import dp as base_dp

GRAPHS={
    'chain':((0,1),(1,2),(2,3)),
    'star':((0,1),(0,2),(0,3)),
    'triangle_isolated':((0,1),(0,2),(1,2)),
    'shifted_star':((0,1),(1,2),(1,3)),
}
PEOPLE=tuple(range(4))

@lru_cache(None)
def exclusive(graph,worlds,n,claimed=0,checked=0,repaired=False,repair=True):
    if n==0:return F(0),()
    later=lambda ws,claimed=claimed,checked=checked,repaired=repaired:exclusive(
        graph,ws,n-1,claimed,checked,repaired,repair)[0]
    options={'SAFE':F(1)+later(worlds)}
    for j,deps in enumerate(graph):
        if claimed>>j&1:continue
        reward=sum((F(9 if repaired or w not in deps else -12)
                    for w in worlds),F())/len(worlds)
        options['CLAIM'+str(j)]=reward+later(worlds,claimed=claimed|(1<<j))
    if not repaired:
        for i in PEOPLE:
            if checked>>i&1:continue
            options['TEST'+str(i)]=sum((
                F(len(g),len(worlds))*later(g,checked=checked|(1<<i))
                for g in (tuple(w for w in worlds if w==i),
                          tuple(w for w in worlds if w!=i)) if g
            ),F())
        if repair and len(worlds)==1 and checked>>worlds[0]&1:
            options['REPAIR']=later(worlds,repaired=True)
    best=max(options.values())
    return best,tuple(a for a,v in options.items() if v==best)

MASKS=tuple(range(16))
WEIGHTS={w:F(1,4)**w.bit_count()*F(3,4)**(4-w.bit_count()) for w in MASKS}
assert sum(WEIGHTS.values(),F())==1

def mass(states):return sum((WEIGHTS[w] for w in states),F())

@lru_cache(None)
def independent(worlds,n,claimed=0,checked=0,repaired=0,repair=True):
    if n==0:return F(0),()
    later=lambda ws,claimed=claimed,checked=checked,repaired=repaired:independent(
        ws,n-1,claimed,checked,repaired,repair)[0]
    total=mass(worlds)
    options={'SAFE':F(1)+later(worlds)}
    for j,deps in enumerate(GRAPHS['chain']):
        if claimed>>j&1:continue
        gain=sum((
            WEIGHTS[w]*F(9 if not any(
                (w>>i&1) and not(repaired>>i&1) for i in deps
            ) else -12) for w in worlds
        ),F())/total
        options['CLAIM'+str(j)]=gain+later(worlds,claimed=claimed|(1<<j))
    for i in PEOPLE:
        if checked>>i&1:continue
        options['TEST'+str(i)]=sum((
            mass(g)/total*later(g,checked=checked|(1<<i))
            for g in (tuple(w for w in worlds if w>>i&1),
                      tuple(w for w in worlds if not(w>>i&1))) if g
        ),F())
    if repair:
        for i in PEOPLE:
            if (checked>>i&1) and not(repaired>>i&1) and all(w>>i&1 for w in worlds):
                options['REPAIR'+str(i)]=later(worlds,repaired=repaired|(1<<i))
    best=max(options.values())
    return best,tuple(a for a,v in options.items() if v==best)

def replay_independent(n,repair=True):
    outcomes={}
    for truth in MASKS:
        states=MASKS;time=n;claimed=checked=repaired=0
        score=F();history=[]
        while time:
            a=independent(states,time,claimed,checked,repaired,repair)[1][0]
            history.append(a)
            if a=='SAFE':score+=1
            elif a.startswith('CLAIM'):
                c=int(a[-1]);assert not claimed>>c&1
                score+=F(9 if not any(
                    (truth>>i&1) and not(repaired>>i&1)
                    for i in GRAPHS['chain'][c]
                ) else -12)
                claimed|=1<<c
            elif a.startswith('TEST'):
                i=int(a[-1]);assert not checked>>i&1
                checked|=1<<i
                states=tuple(w for w in states
                    if bool(w>>i&1)==bool(truth>>i&1))
            elif a.startswith('REPAIR'):
                i=int(a[-1]);assert truth>>i&1 and checked>>i&1
                repaired|=1<<i
            else:raise ValueError(a)
            assert truth in states
            time-=1
        outcomes[truth]=(score,tuple(history))
    expected=sum((WEIGHTS[w]*v[0] for w,v in outcomes.items()),F())
    assert expected==independent(MASKS,n,repair=repair)[0]
    return expected,outcomes

def regression():
    assert exclusive(GRAPHS['chain'],PEOPLE,4,repair=False)[0]==base_dp(PEOPLE,4)[0]
    assert exclusive(GRAPHS['chain'],PEOPLE,5)[0]==base_dp(PEOPLE,5,allow_repair=True)[0]
    expected={
        'chain':((F(55,4),('TEST0','TEST3')),(F(14),('TEST1','TEST2'))),
        'star':((F(47,4),('TEST1','TEST2','TEST3')),(F(14),('TEST0',))),
        'triangle_isolated':((F(55,4),('TEST3',)),(F(55,4),('TEST3',))),
        'shifted_star':((F(47,4),('TEST0','TEST2','TEST3')),(F(14),('TEST1',))),
    }
    for name,graph in GRAPHS.items():
        off=exclusive(graph,PEOPLE,4,repair=False)
        on=exclusive(graph,PEOPLE,4)
        assert (off,on)==expected[name]
        degrees=tuple(sum(i in deps for deps in graph) for i in PEOPLE)
        top=max(degrees)
        high_degree={f'TEST{i}' for i in PEOPLE if degrees[i]==top}
        if name in ('chain','star','triangle_isolated'):
            assert not(set(off[1])&high_degree)
        if name=='triangle_isolated':
            assert degrees[3]==0 and off[1]==('TEST3',)
        print('graph',name,'degrees',degrees,'no_repair',off,'repair',on)
    assert [independent(MASKS,n,repair=False)[0] for n in (2,3,4,5)]==[
        F(49,16),F(209,32),F(635,64),F(3219,256)]
    assert [independent(MASKS,n,repair=True)[0] for n in (2,3,4,5)]==[
        F(49,16),F(429,64),F(345,32),F(3771,256)]
    assert independent(MASKS,4,repair=False)[1]==('TEST1','TEST2')
    assert exclusive(GRAPHS['chain'],PEOPLE,4,repair=False)[1]==('TEST0','TEST3')
    for i in PEOPLE:
        assert sum((WEIGHTS[w] for w in MASKS if w>>i&1),F())==F(1,4)
    assert sum((WEIGHTS[w] for w in MASKS if w.bit_count()==1),F())==F(27,64)
    assert replay_independent(4,repair=False)[0]==F(635,64)
    assert replay_independent(5,repair=True)[0]==F(3771,256)
    print('CASCADE09-15 PASS')

if __name__=='__main__':regression()
