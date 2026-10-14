from functools import lru_cache
from fractions import Fraction
from itertools import product

REWARD={'A':12,'B':10}

def solve(early=6,late=2,slots=1,nonmatch=3):
    @lru_cache(None)
    def after(t,a,b,s,used,submitted,truth):
        value=s+sum((REWARD[x] if x==truth else nonmatch) for x in submitted)
        if not t: return (value, ())
        options=[]
        for act in 'ABS':
            aa,bb,ss=a+(act=='A'),b+(act=='B'),s+(act=='S')
            if (act=='A' and a>=3) or (act=='B' and b>=3): continue
            q=after(t-1,aa,bb,ss,used,submitted,truth)
            options.append((q[0],(act,)+q[1]))
        for act,progress in [('A',a),('B',b)]:
            if progress>=3 and act not in submitted and used<slots:
                q=after(t-1,a,b,s,used+1,submitted+(act,),truth)
                options.append((q[0],('SUB_'+act,)+q[1]))
        return max(options,key=lambda x:x[0])
    best=None
    for seq in product('ABS',repeat=early):
        a=seq.count('A');b=seq.count('B');s=seq.count('S')
        if a>3 or b>3: continue
        outcomes={x:after(late,a,b,s,0,(),x) for x in 'AB'}
        v=sum((Fraction(outcomes[x][0],2) for x in 'AB'),Fraction(0))
        if best is None or v>best[0]: best=(v,''.join(seq),outcomes)
    return best

if __name__=='__main__':
    expected={(4,2,1):11,(4,2,2):11,(5,2,1):12,(5,2,2):12,
              (6,2,1):13,(6,2,2):14,(6,3,1):15,(6,3,2):15}
    for params,v in expected.items():
        got=solve(*params)
        assert got[0]==v,(params,got,v)
        print(*params,got)
    print("PASS 8 exact Fraction exhaustive baselines")
