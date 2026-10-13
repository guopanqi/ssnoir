#!/usr/bin/env python3
"""STAKE01-03: Pareto / protected-goal audit of unchanged CUST01.

Equal aggregate expected reward does NOT imply equal attainable outcome
vectors. Imports the frozen four-action CUST transition functions directly.
Expected scores only measure this toy contract, not players' preferences.
"""
from collections import deque
from fractions import Fraction as F
from itertools import product
from pattern_evidence_custody_20261013 import ACTS, INIT, legal, advance

MODES=('original','copy','matched_preserve','reusable')

def play(mode,plan):
    state=INIT
    for a in plan:
        if not legal(mode,state,a,True):return None
        state=advance(mode,state,a)
    return state

def v4(st):
    a,b,side,orig,ever,cp,shown=st
    return (F(a==2 and orig),F(b==2),F(shown),F(side))

def score(high_vec,low_vec):
    a,b,c,s=high_vec
    h=8*a+9*b+12*c+s
    a,b,c,s=low_vec
    l=8*a+9*b+s
    return (h+l)/2

def all_policies(mode):
    for first in ACTS:
        if play(mode,(first,)) is None:continue
        cont=[]
        for tail in product(ACTS,repeat=3):
            st=play(mode,(first,)+tail)
            if st is not None:cont.append((tail,v4(st)))
        for h,l in product(cont,repeat=2):
            yield ((first,h[0],l[0]),h[1],l[1])

def pareto(vectors):
    vectors=set(vectors)
    return frozenset(v for v in vectors if not any(
        w!=v and all(x>=y for x,y in zip(w,v)) for w in vectors))

def summarize(mode):
    found=list(all_policies(mode))
    full=[(tuple((x+y)/2 for x,y in zip(h,l)),sc,h,l) for sc,h,l in found]
    frontier=pareto(item[0] for item in full)
    native=max(score(h,l) for _,h,l in found)
    fixed=max(score(h,l) for ((first,p,q),h,l) in found if p==q)
    guarded=[(score(h,l),sc,h,l) for sc,h,l in found if h[0]==l[0]==1]
    guar=max(x[0] for x in guarded)
    protected=max((x for x in guarded if x[0]==guar),key=lambda x:str(x[1]))
    return {'mode':mode,'count':len(found),
      'unique':len(set(x[0] for x in full)), 'pareto':frontier,
      'root':native,'fixed':fixed,'protected':guar,'witness':protected}

def min_actions(mode,goals,maxsteps=6):
    q=deque([(INIT,())]);seen={INIT}
    while q:
        st,plan=q.popleft()
        vec=v4(st)
        if all(vec['ABC'.index(g)] for g in goals):return plan
        if len(plan)==maxsteps:continue
        for a in ACTS:
            if not legal(mode,st,a,True):continue
            ns=advance(mode,st,a)
            if ns in seen:continue
            seen.add(ns);q.append((ns,plan+(a,)))
    return None

def main():
    stats={k:summarize(k) for k in MODES}
    for mode,x in stats.items():
        print(mode,'programs',x['count'],'unique_outcomes',x['unique'],
          'Pareto',len(x['pareto']),'scalar-optimum',x['root'],
          'fixed',x['fixed'],'guarantee-A-then-maximize',x['protected'],
          'witness',x['witness'])
    assert all(x['root']==19 and x['fixed']==17 for x in stats.values())
    assert stats['original']['protected']==17
    assert stats['copy']['protected']==stats['matched_preserve']['protected']==F(37,2)
    assert stats['reusable']['protected']==19
    assert len(stats['original']['pareto'])==18
    assert len(stats['copy']['pareto'])==len(stats['reusable']['pareto'])==23
    assert stats['copy']['pareto']==stats['matched_preserve']['pareto']
    keepA=(F(1),F(1,2),F(1,2),F(0))
    replaceA=(F(1,2),F(1),F(1,2),F(0))
    assert keepA in stats['copy']['pareto'] and keepA not in stats['original']['pareto']
    assert replaceA in stats['original']['pareto'] and replaceA in stats['copy']['pareto']
    assert not all(x>=y for x,y in zip(keepA,replaceA))
    assert not all(x>=y for x,y in zip(replaceA,keepA))
    expected={
      'original':{'A':2,'B':2,'C':2,'AB':4,'AC':None,'BC':4,'ABC':None},
      'copy':{'A':2,'B':2,'C':2,'AB':4,'AC':4,'BC':4,'ABC':6},
      'reusable':{'A':2,'B':2,'C':2,'AB':4,'AC':3,'BC':4,'ABC':5}}
    for mode,checks in expected.items():
        for goal,want in checks.items():
            witness=min_actions(mode,goal)
            assert (len(witness) if witness is not None else None)==want
        print('minimum actions',mode,checks)
    assert all(min_actions('copy',g)==min_actions('matched_preserve',g)
               for g in expected['copy'])
    print('PASS equal scalar optimum hides expanded Pareto options and guaranteed-A feasibility')

if __name__=='__main__':main()
