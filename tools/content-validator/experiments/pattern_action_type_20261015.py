"""ACTION-MODE01: type-changing next-action output versus ordinary output.
Four actions A/B/S; A increments A, B after A yields C instead of B;
otherwise B increments B. All terminal rewards kept distinct.
"""
from itertools import product
from fractions import Fraction
ACTIONS='ABS'
def score(path,target,mode):
    a=b=c=side=0
    for i,x in enumerate(path):
        if x=='A': a+=1
        elif x=='S': side+=1
        elif mode and i and path[i-1]=='A': c+=1
        else: b+=1
    return (6 if a>=2 else 0)+(8+6*(target=='B') if b>=2 else 0)+(10+6*(target=='C') if c>=2 else 0)+side
def audit(mode):
    paths=[''.join(p) for p in product(ACTIONS,repeat=4)]
    fixed=max((Fraction(score(p,'B',mode)+score(p,'C',mode),2),p) for p in paths)
    rows=[]
    for first in (''.join(p) for p in product(ACTIONS,repeat=2)):
        branch_b=max((score(p,'B',mode),p) for p in paths if p.startswith(first))
        branch_c=max((score(p,'C',mode),p) for p in paths if p.startswith(first))
        rows.append((Fraction(branch_b[0]+branch_c[0],2),first,branch_b,branch_c))
    return fixed,max(rows)
if __name__=='__main__':
    assert audit(False)[0][0]==17 and audit(False)[1][0]==17
    assert audit(True)[0][0]==19 and audit(True)[1][0]==19
    assert (score('ABAB','B',True),score('ABAB','C',True))==(16,22)
    print('plain',audit(False))
    print('type-changing',audit(True))
    print('PASS 81 plans and 9 two-step prefixes per configuration')
