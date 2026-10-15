"""ROUTE-MIX: strongest randomized announced policy for AUDIENCE.

Ex-ante enforced commitments to route randomization are not self-enforcing
signaling equilibria. A partner observes the route, updates by Bayes, and then
chooses to join using exactly the same incentives as AUDIENCE.
"""
from fractions import Fraction as F
from pattern_information_audience_20261016 import participant_joins, PLAYER, SOLO


def value(p_g_x, p_b_x):
    """p_g_x=P(X | G), p_b_x=P(X | B). Both types have prior 1/2."""
    assert F() <= p_g_x <= 1 and F() <= p_b_x <= 1
    result = F()
    observed = {}
    for route, q_g, q_b in (('X',p_g_x,p_b_x), ('Y',1-p_g_x,1-p_b_x)):
        if not q_g+q_b:
            continue
        p_g = q_g/(q_g+q_b)
        joins = participant_joins(p_g)
        observed[route] = (p_g, joins)
        result += ((q_g*PLAYER['G',route]+q_b*PLAYER['B',route])
                   if joins else ((q_g+q_b)*SOLO))/2
    return result, observed


def continuous_upper_bound():
    """Proof by four join/exit regions (a=P(X|G), b=P(X|B)).

    X JOIN iff b<=2a; Y JOIN iff b>=2a-1 (unused routes ignored).
    both join: 7+2a-3b/2 <=8 by b>=max(0,2a-1).
    X only: 4+3a+b/2 <=7.5 when b<2a-1<=1.
    Y only: 7-a-2b <=7.
    neither: 4.
    """
    a,b=F(1,2),F()
    payoff, observations = value(a,b)
    assert payoff==F(8)
    assert observations=={'X':(F(1),True),'Y':(F(1,3),True)}
    return payoff


def regression():
    bound=continuous_upper_bound()
    vals={}
    for i in range(41):
        for j in range(41):
            p,q=F(i,40),F(j,40)
            v, _ = value(p,q)
            vals[(i,j)]=v
            assert v<=bound
    assert max(vals.values())==bound
    assert [k for k,v in vals.items() if v==bound]==[(20,0)]
    near, obs=value(F(49,100),F())
    assert near==F(399,50) and near<bound
    assert obs['Y'][0]>F(1,3)
    # Knowing G, player would deviate from Y6 to X10 if both joined.
    # Hence committed randomization is not automatically a free-action equilibrium.
    assert PLAYER['G','X']>PLAYER['G','Y']
    print('ROUTE-MIX PASS: best committed pure=15/2; committed mixed=8; private invisible=9')
    print('Witness P(X|G)=1/2, P(X|B)=0; X posterior G=1, Y posterior G=1/3')
    print('Without tie JOIN, 399/50=7.98 is a strict-partner-join nearby option')
    print('Examined 1681 exact rational policies; analytic four-region bound=8')
    print('LIMIT: credible ex-ante randomization required; no general equilibrium or player trials')

if __name__=='__main__': regression()
