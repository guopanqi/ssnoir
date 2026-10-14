#!/usr/bin/env python3
"""RV01/RV02: reachable switching window and cheap route-tracking bypass.
This is NOT a replay of R54's actual dice mechanics; it is a necessary
optimistic upper-bound (each future action may add at most 2 progress).
Four actions, goal A needs 3, B needs 5; action yields A/B 1 or 2.
Separately, P18-style pursuer watches previous route: ABABA completes
3 A and 2 B with no consecutive route repeat. No human experiment here.
"""
from itertools import product
A_TARGET=3
B_TARGET=5
N=4
MOVES=('A1','A2','B1','B2')

def state(prefix):
    a=b=0
    for m in prefix:
        if m[0]=='A':a+=int(m[1])
        else:b+=int(m[1])
    return min(a,A_TARGET),min(b,B_TARGET)

def feasible(prefix):
    a,b=state(prefix)
    left=N-len(prefix)
    return a+2*left>=A_TARGET,b+2*left>=B_TARGET

def cheap_pursuer_bypass():
    paths=[]
    for path in product('AB',repeat=5):
        hit=sum(path[i]==path[i-1] for i in range(1,5))
        paths.append((path,path.count('A')>=3,path.count('B')>=2,hit))
    zero=[x for x in paths if x[1] and x[2] and x[3]==0]
    assert len(zero)==1 and zero[0][0]==tuple('ABABA')
    # Both goals fully achieved, pursuer never catches: simultaneous
    # upper bound met by fixed sequence, so nothing adaptive to optimize.
    return zero[0]

def main():
    expected={0:(1,0,0,0),1:(4,0,0,0),2:(12,4,0,0),
              3:(18,38,8,0),4:(0,152,47,57)}
    for t in range(N+1):
        paths=list(product(MOVES,repeat=t))
        counts={'both':0,'A_only':0,'B_only':0,'neither':0}
        a_committed=0
        for path in paths:
            ok_a,ok_b=feasible(path)
            name='both' if ok_a and ok_b else 'A_only' if ok_a else 'B_only' if ok_b else 'neither'
            counts[name]+=1
            a,b=state(path)
            if a>=A_TARGET and not ok_b:a_committed+=1
        assert tuple(counts.values())==expected[t]
        print('RV01 depth',t,'counts',counts,'A-complete B-unreachable',a_committed)
    assert feasible(('A2','A2'))==(True,False)
    assert feasible(('B2','B2'))==(True,True)
    print('RV02 fixed complete safe path',cheap_pursuer_bypass())
    print('PASS viable-switch ceiling, no-repeat shortcut')

if __name__=='__main__':main()
