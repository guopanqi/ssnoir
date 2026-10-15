"""TACTIC04: self-interested opponent, not zero-sum. Finite simultaneous
PRESS/VERIFY x HIDE/one-use ESCAPE; exact subgame mixed equilibrium.
Opponent payoff differs from negative player payoff; player terminal rewards
and the admissible actions are the same as TACTIC01-03.
"""
from fractions import Fraction as Q
from functools import lru_cache
from pattern_adversarial_tempo_20261015 import value as zero_sum
PLAYER=((Q(3),Q(-3)),(Q(-2),Q(2)))
OTHER=((Q(-2),Q(-1)),(Q(0),Q(-4)))

@lru_cache(None)
def game(n,escape=True):
    if n==0:return Q(0),Q(0),None
    if not escape:return Q(3*n),Q(-2*n),('PRESS','HIDE')
    ph,oh,_=game(n-1,True)
    pr,orr,_=game(n-1,False)
    a,b=PLAYER[0][0]+ph,PLAYER[0][1]+pr
    c,d=PLAYER[1][0]+ph,PLAYER[1][1]+pr
    x,y=OTHER[0][0]+oh,OTHER[0][1]+orr
    z,w=OTHER[1][0]+oh,OTHER[1][1]+orr
    q=(d-b)/(a-b-c+d)  # opponent HIDE probability
    p=(w-z)/(x-y-z+w)  # player PRESS probability
    assert 0<p<1 and 0<q<1
    vp=q*a+(1-q)*b
    vo=p*x+(1-p)*z
    assert q*c+(1-q)*d==vp
    assert p*y+(1-p)*w==vo
    return vp,vo,('MIX',p,q)

def flight_dominated(n):
    # Counterfactual: opponent places such a large personal cost on ESCAPE
    # that HIDE strictly dominates it, no matter which player action.
    opponent=((Q(0),Q(-3)),(Q(0),Q(-4)))
    assert all(row[0]>row[1] for row in opponent)
    return Q(3*n),Q(0),('PRESS','HIDE')

def regression():
    for n in range(1,9):
        assert flight_dominated(n)==(Q(3*n),Q(0),('PRESS','HIDE'))
        vp,vo,policy=game(n)
        robust,rm=zero_sum(n)
        assert vp==robust
        assert vo==Q(1-2*n)-Q(3**n,5**n)
        assert policy==('MIX',Q(1)-Q(3**(n-1),5**n),Q(1,2))
        assert policy[1]!=rm[1]
        print(n,'zero_sum_press',rm[1],'self_interested_press',policy[1],
              'player_value_both',vp,'other_value',vo)
    print('TACTIC04 PASS')

if __name__=='__main__':regression()
