#!/usr/bin/env python3
"""Exact abstract tests: mutable self-interested goal vs cross-contract typed results.
Only structural/finite mathematical evidence, not independent playtesting.
"""
from fractions import Fraction as F
from itertools import product

ACTOR = ('HELP_A','HELP_B','EXIT')
PLAYER = ('A','B','SIDE')
OWN = {'N':{'A':8,'B':4},'M':{'A':4,'B':8}}

def m_end(first,helper,path,threshold):
    aa=(first,)+path
    a=sum(z=='A' for z in aa)+(helper=='HELP_A')
    b=sum(z=='B' for z in aa)+(helper=='HELP_B')
    win_a=int(a>=threshold);win_b=int(b>=threshold)
    joint=int((win_a and helper=='HELP_A') or (win_b and helper=='HELP_B'))
    side=aa.count('SIDE')
    return F(10*win_a+9*win_b+2*joint+side),(win_a,win_b,joint,side)

def m_reply(first,shock,threshold,allow_unqualified=False):
    choices=[]
    for op in ACTOR:
        if first=='SIDE' and op!='EXIT' and not allow_unqualified:
            continue # free unrelated SIDE has no shared investment; actor declines
        value,route=max(((m_end(first,op,seq,threshold),seq)
                         for seq in product(PLAYER,repeat=2)),key=lambda r:r[0][0])
        own=F(3) if op=='EXIT' else F(OWN[shock][op[-1]]*value[1][int(op=='HELP_B')])
        choices.append((own,op,value[0],value[1],route))
    # The actor foresees the player's best legal continuation and optimizes OWN.
    # On equal utility, choose EXIT; no equality of participation is silently trusted.
    return max(choices,key=lambda r:(r[0],r[1]=='EXIT',-ACTOR.index(r[1])))

def m_full(threshold,allow_unqualified=False):
    rows=[]
    for first in PLAYER:
        branches={s:m_reply(first,s,threshold,allow_unqualified) for s in ('N','M')}
        root=sum((r[2] for r in branches.values()),F())/2
        rows.append((root,first,branches))
    return max(rows,key=lambda r:(r[0],-PLAYER.index(r[1]))),rows

def m_machine(threshold):
    root,all_rows=m_full(threshold)
    for _,first,branches in all_rows:
        for shock,data in branches.items():
            if first=='SIDE':expected='EXIT'
            elif threshold==3:expected='HELP_A' if shock=='N' else 'HELP_B'
            else:expected='HELP_'+first
            assert data[1]==expected,(threshold,first,shock,data)
            assert m_end(first,data[1],data[4],threshold)[1]==data[3]
    return root

P1=('WORK_A','WORK_B','SIDE')
P2=P1+('DIRECT','SUBMIT_A','SUBMIT_B')
START=(0,0,0,0,0,False) # A progress,B progress,direct progress,C value,SIDE,submitted

def action(s,verb,need,phase,submit=True,safe=False):
    a,b,d,c,side,used=s
    if verb=='WORK_A':a+=1
    elif verb=='WORK_B':b+=1
    elif verb=='SIDE':side+=1
    elif verb=='DIRECT':
        assert phase==2
        d+=1
        if d>=2:c=max(c,6)
    elif verb=='SAFE':
        if phase!=2 or not safe:return None
        c=10
    elif verb.startswith('SUBMIT_'):
        if phase!=2 or not submit or used or verb[-1]!=need:return None
        if (a if need=='A' else b)<2:return None
        c=10;used=True
    else:raise ValueError(verb)
    return a,b,d,c,side,used

def result(s):
    a,b,d,c,side,used=s
    wa=int(a>=2);wb=int(b>=2)
    return F(6*wa+5*wb+c+side),(wa,wb,int(c>0),int(c==10),side)

def phase_two(s,need,submit=True,safe=False):
    options=[]
    for moves in product(P2+(('SAFE',) if safe else ()),repeat=2):
        now=s
        for verb in moves:
            now=action(now,verb,need,2,submit,safe) if now is not None else None
        if now is not None:options.append((result(now),moves,now))
    assert options
    score,move,terminal=max(options,key=lambda r:r[0][0])
    return score[0],move,score[1],terminal

def full(submit=True,safe=False):
    rows=[]
    for prep in product(P1,repeat=2):
        s=START
        for v in prep:s=action(s,v,None,1)
        routes={need:phase_two(s,need,submit,safe) for need in ('A','B')}
        total=(routes['A'][0]+routes['B'][0])/2
        rows.append((total,prep,routes))
    return max(rows,key=lambda r:r[0]),rows

def fixed():
    # Exhaustive four-action prewritten scripts, with illegal actions replaced
    # by legal SIDE to avoid penalizing an opponent with an inert command.
    rows=[]
    for prep in product(P1,repeat=2):
        before=START
        for v in prep:before=action(before,v,None,1)
        for moves in product(P2,repeat=2):
            total=F()
            for need in ('A','B'):
                s=before
                for v in moves:
                    nxt=action(s,v,need,2)
                    s=nxt if nxt is not None else action(s,'SIDE',need,2)
                total+=result(s)[0]/2
            rows.append((total,prep,moves))
    return max(rows,key=lambda r:r[0])

def compress(s):
    a,b,d,c,side,used=s
    return min(a,2),min(b,2),min(d,2),c,side,used

def provenance_state_test():
    """The cross-contract affordance is completely determined by typed work
    counters; there is no special memory of *how* A/B was obtained."""
    checks=0
    for prep in product(P1,repeat=2):
        s=START
        for v in prep:s=action(s,v,None,1)
        for need in ('A','B'):
            states={s}
            for _ in range(2):
                following=set()
                for state in states:
                    for move in P2:
                        left=action(state,move,need,2)
                        right=action(compress(state),move,need,2)
                        assert (left is None)==(right is None)
                        if left is None:continue
                        assert compress(left)==compress(right)
                        assert result(left)==result(compress(left))
                        checks+=1;following.add(left)
                states=following
    return checks

def verify():
    m3=m_machine(3)
    m4=m_machine(4)
    assert m3[0]==12 and m3[1]=='A'
    assert (m3[2]['N'][1],m3[2]['M'][1])==('HELP_A','HELP_B')
    assert (m3[2]['N'][2],m3[2]['M'][2])==(13,11)
    assert m4[0]==12 and m4[1]=='A'
    assert (m4[2]['N'][1],m4[2]['M'][1])==('HELP_A','HELP_A')
    # Remove qualification: a free helper may cooperate even after unrelated
    # SIDE. Then the fixed first-work prerequisite, not social cognition,
    # caused SIDE to be unattractive in the original.
    free_help,_=m_full(3,allow_unqualified=True)
    assert free_help[0]==F(25,2) and free_help[1]=='SIDE'
    assert (free_help[2]['N'][1],free_help[2]['M'][1])==('HELP_A','HELP_B')
    # Under shock M the actor values B=8>A=4, but B is unreachable after
    # WORK_A when threshold4. Helping A obtains actual own4 > EXIT3.
    assert OWN['M']['B']>OWN['M']['A']>3
    assert m_end('A','HELP_B',('B','B'),4)[1][1]==0
    print('MUTABLE',[(k,v[0],v[1],{s:(z[1],str(z[2]),z[4]) for s,z in v[2].items()}) for k,v in ((3,m3),(4,m4))])
    root,rows=full()
    assert root[0]==F(31,2) and root[1] in (('WORK_A','WORK_B'),('WORK_B','WORK_A'))
    assert root[2]['A'][0]==16 and root[2]['B'][0]==15
    assert root[2]['A'][2]==(1,0,1,1,0)
    assert root[2]['B'][2]==(0,1,1,1,0)
    first=next(row for row in rows if row[1]==('WORK_A','WORK_B'))
    assert first[0]==root[0]
    assert first[2]['A'][0]==16 and first[2]['B'][0]==15
    fixed_base=fixed()
    assert fixed_base[0]==F(25,2)
    assert full(submit=False)[0][0]==12
    safe=full(safe=True)[0]
    assert safe[0]==17 and safe[1]==('WORK_A','WORK_A')
    # A direct two-action fallback is legal in every publicly revealed need.
    for _,prep,_ in rows:
        s=START
        for v in prep:s=action(s,v,None,1)
        for need in ('A','B'):
            direct=result(action(action(s,'DIRECT',need,2),'DIRECT',need,2))[0]
            assert phase_two(s,need)[0]>=direct
    checks=provenance_state_test()
    assert checks>150
    print('CROSS',{'optimal':str(root[0]),'prep':root[1],
                   'A':(str(root[2]['A'][0]),root[2]['A'][1],root[2]['A'][2]),
                   'B':(str(root[2]['B'][0]),root[2]['B'][1],root[2]['B'][2]),
                   'fixed':str(fixed_base[0]),'no_proof':12,
                   'safe_complete_substitute':17,'typed_counter_checks':checks})
    print('PASS exact exhaustive normal starts, actor self-interest, public signal, strong menus, type-only state isomorphism')

if __name__=='__main__':verify()
