"""TACTIC01-03: exact simultaneous zero-sum actions with a one-use ESCAPE.
States public after each contest. No claim of human play or new game primitive.
"""
from fractions import Fraction as Q
from functools import lru_cache
from itertools import product
PAY=((Q(3),Q(-3)),(Q(-2),Q(2))) # rows PRESS, VERIFY; cols HIDE, ESCAPE

def matrix(a,b,c,d):
    denom=a-b-c+d
    ps={Q(0),Q(1)}
    if denom:
        x=(d-c)/denom
        if 0<=x<=1:ps.add(x)
    worst=lambda p:min(p*a+(1-p)*c,p*b+(1-p)*d)
    p=max(sorted(ps),key=lambda k:(worst(k),-k))
    val=worst(p)
    q=(d-b)/denom if denom else None
    if q is not None and 0<=q<=1:
        assert max(q*a+(1-q)*b,q*c+(1-q)*d)==val
    return val,p,q

@lru_cache(None)
def value(n,escape=True,scan=False):
    if n==0:return Q(0),('END',)
    if not escape:return Q(3*n),('PRESS',)
    h=value(n-1,True,scan)[0];r=value(n-1,False,scan)[0]
    mat=(PAY[0][0]+h,PAY[0][1]+r,PAY[1][0]+h,PAY[1][1]+r)
    v,p,q=matrix(*mat)
    if scan and n>=2:
        # Spend this turn to force defender to publicly commit H/R next
        # contest. They cannot spend ESCAPE on the scan-only turn.
        observed=min(Q(3)+value(n-2,True,scan)[0],
                     Q(2)+value(n-2,False,scan)[0])
        if observed>v:return observed,('SCAN',)
    return v,('MIX',p,q)

def best_fixed(n):
    best=None
    for seq in product('PV',repeat=n):
        base=sum((Q(3) if x=='P' else Q(-2) for x in seq),Q(0))
        outcomes=[base]+[base+(Q(-6) if x=='P' else Q(4)) for x in seq]
        worst=min(outcomes)
        if best is None or worst>best[0]:best=(worst,seq)
    return best

def precommitted_mixed(n):
    # Both actors precommit without history-dependent actions. By symmetric
    # averaging, all PRESS marginals can be taken equal at maximin.
    f=lambda p:n*(5*p-2)+min(Q(0),4-10*p)
    return max((f(p),p) for p in (Q(0),Q(2,5),Q(1)))

def regression():
    for n in range(1,9):
        normal,action=value(n)
        with_scan,sa=value(n,scan=True)
        assert normal==Q(3*n-6)+Q(3,2**(n-1))
        assert action==('MIX',Q(1)-Q(3,5*2**(n-1)),Q(1,2))
        assert best_fixed(n)[0]==(Q(-2) if n==1 else Q(3*n-6))
        assert precommitted_mixed(n)[0]==(Q(0) if n==1 else Q(3*n-6))
        if n==2:assert sa==('SCAN',) and with_scan==2
        else:assert sa[0]=='MIX'
        assert with_scan==(Q(0) if n==1 else Q(3*n-6)+Q(2,2**(n-2)))
        print(n, 'closed_loop',normal,'fixed',best_fixed(n)[0],
              'sealed',precommitted_mixed(n)[0],
              'SCAN_available',with_scan,'press_probability',action[1])
    assert matrix(*PAY[0],*PAY[1])==(Q(0),Q(2,5),Q(1,2))
    print('TACTIC01-03 PASS')

if __name__=='__main__':regression()
