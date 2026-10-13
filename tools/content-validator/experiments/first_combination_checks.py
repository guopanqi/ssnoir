#!/usr/bin/env python3
"""Two minimal decision-structure checks. Exact fractions; not a fun evaluator."""
import argparse
import json
import random
from fractions import Fraction as F
from functools import lru_cache

HALF = F(1, 2)
FIX = F(3, 4)

@lru_cache(None)
def crisis(t, p, fixed, detour, goal=5):
    if p >= goal:
        return F(1)
    if not t:
        return F(0)
    if fixed:
        choices = [crisis(t-1,p+2,True,detour,goal)]
    else:
        choices = [FIX*crisis(t-1,p,True,detour,goal)+(1-FIX)*crisis(t-1,p,False,detour,goal)]
    if detour:
        choices.append(crisis(t-1,p+1,fixed,detour,goal))
    return max(choices)

@lru_cache(None)
def information(t,a,b,known,can_research=True,work_reveals=False):
    if a>=2 or b>=2:
        winner = 'A' if a>=2 else 'B'
        return F(4) if known=='?' else F(6 if known==winner else 2)
    if t==0:
        return F(0)
    if work_reveals and known=='?':
        first=HALF*information(t-1,a+1,b,'A',can_research,work_reveals)+HALF*information(t-1,a+1,b,'B',can_research,work_reveals)
    else:
        first=information(t-1,a+1,b,known,can_research,work_reveals)
    choices=[first,information(t-1,a,b+1,known,can_research,work_reveals)]
    if can_research and known=='?':
        choices.append(HALF*information(t-1,a,b,'A',can_research,work_reveals)+HALF*information(t-1,a,b,'B',can_research,work_reveals))
    return max(choices)

def check():
    cases=[]
    for p in (1,2):
        rep=FIX*crisis(2,p,True,True)+(1-FIX)*crisis(2,p,False,True)
        alt=crisis(2,p+1,False,True)
        cases.append({'progress':p,'repair':str(rep),'detour':str(alt),'preferred':'repair' if rep>alt else 'detour' if alt>rep else 'tie'})
    base=HALF*crisis(3,1,False,False)+HALF*crisis(3,2,False,False)
    changed=HALF*crisis(3,1,False,True)+HALF*crisis(3,2,False,True)
    assert [x['preferred'] for x in cases]==['repair','detour']
    assert (base,changed)==(F(3,4),F(7,8))
    info=[]
    for t in (2,3):
        info.append({'turns':t,'no_research':str(information(t,0,0,'?',False)),
            'paid_research':str(information(t,0,0,'?',True)),
            'productive_signal':str(information(t,0,0,'?',True,True))})
    assert info==[
        {'turns':2,'no_research':'4','paid_research':'4','productive_signal':'4'},
        {'turns':3,'no_research':'4','paid_research':'6','productive_signal':'6'}
    ],info
    print(json.dumps({'crisis':{'after_first_step':cases,'no_detour':str(base),'with_detour':str(changed)},'information':info,
        'scope':'Exact toy model only; no official game or human experiment'},ensure_ascii=False,indent=2))

def play(mode,seed):
    rng=random.Random(seed)
    if mode=='crisis':
        p=rng.choice((1,2)); t=3; fixed=False
        print('First action complete; main route now blocked.')
        while t and p<5:
            options=('advance','detour') if fixed else ('repair','detour')
            print(f'public: progress={p}/5, turns={t}, main_open={fixed}; actions={options}')
            try: move=input('> ').strip().lower()
            except EOFError: break
            if move not in options:
                print('Illegal action');continue
            t-=1
            if move=='advance': p+=2
            elif move=='detour': p+=1
            else:
                fixed=rng.randrange(4)!=0
                print('Repair success' if fixed else 'Repair failed')
        print('completed' if p>=5 else 'unfinished',p)
    else:
        actual=rng.choice(('A','B')); known=None; a=b=0; t=3
        while t and a<2 and b<2:
            opts=['work-a','work-b']+(['research'] if known is None else [])
            print(f'public: A={a}/2, B={b}/2, turns={t}, favorable={known or "?"}; actions={opts}')
            try: move=input('> ').strip().lower()
            except EOFError: break
            if move not in opts:
                print('Illegal action');continue
            t-=1
            if move=='work-a': a+=1
            elif move=='work-b': b+=1
            else: known=actual
        winner='A' if a>=2 else 'B' if b>=2 else None
        print('winner',winner,'hidden_type',actual,'payoff',6 if winner==actual else 2 if winner else 0)

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--play',choices=['crisis','info'])
    parser.add_argument('--seed',type=int,default=19)
    args=parser.parse_args()
    play(args.play,args.seed) if args.play else check()
