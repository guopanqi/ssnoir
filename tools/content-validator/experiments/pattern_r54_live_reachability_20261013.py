#!/usr/bin/env python3
"""R54-RV03: abstract exact reachability of actual R54 Scheme step/attack order.
This is NOT dice-weighted: every gain 0/1/2 is treated as a possible label.
Encounter goals A3 or B5, stops at FIRST completed goal; 4 moves maximum;
after moves 2 or 4, if not already completed, pursuer subtracts 2 progress
from the leading goal, ties A. Enumerate all live action/outcome prefixes;
do not mistake branch-count proportions for real player probabilities.
"""
from collections import defaultdict
from functools import lru_cache
from itertools import product
T=4;A=3;B=5
MOVES=tuple(product(('A','B'),(0,1,2)))

def step(t,a,b,move):
    name,gain=move
    if name=='A':a=min(A,a+gain)
    else:b=min(B,b+gain)
    t+=1
    if a>=A:return t,a,b,'A'
    if b>=B:return t,a,b,'B'
    if t in (2,4):
        if a>=b:a=max(0,a-2)
        else:b=max(0,b-2)
    return t,a,b,'timeout' if t==4 else None

@lru_cache(None)
def possible_wins(t,a,b):
    if t==T:return frozenset()
    result=set()
    for move in MOVES:
        nt,na,nb,term=step(t,a,b,move)
        if term in ('A','B'):result.add(term)
        elif term is None:result.update(possible_wins(nt,na,nb))
    return frozenset(result)

def main():
    active={(0,0,0):1}
    ended={'A':0,'B':0,'timeout':0}
    expected=[
        (1,1,1,0,0,0),
        (6,5,4,2,0,0),
        (33,4,9,24,0,0),
        (196,11,0,72,12,112),
        (0,0,0,0,0,0),
    ]
    for turn in range(T+1):
        counts={'both':0,'A_only':0,'B_only':0,'neither':0}
        for (_,a,b),count in active.items():
            wins=possible_wins(turn,a,b)
            key='both' if len(wins)==2 else 'A_only' if wins=={'A'} else 'B_only' if wins=={'B'} else 'neither'
            counts[key]+=count
        record=(sum(active.values()),len({(a,b) for _,a,b in active}),
                counts['both'],counts['A_only'],counts['B_only'],counts['neither'])
        assert record==expected[turn],(turn,record)
        print('after_actions',turn,'active_histories,unique_states,both,A,B,neither',record,
              'terminal_prefixes',ended)
        if turn==T:break
        next_active=defaultdict(int)
        for (t,a,b),count in active.items():
            for move in MOVES:
                ns=step(t,a,b,move)
                if ns[3] is None:next_active[ns[:3]]+=count
                else:ended[ns[3]]+=count
        active=dict(next_active)
    assert possible_wins(0,0,0)==frozenset({'A','B'})
    assert possible_wins(2,0,0)==frozenset({'A'})
    assert possible_wins(2,0,2)==frozenset({'A','B'})
    assert ended=={'A':110,'B':15,'timeout':1056}
    print('PASS R54 live-goal feasibility, attack-after-uncompleted-move, no dice odds')

if __name__=='__main__':main()
