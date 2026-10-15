"""AUDIENCE: a useful private fact can become destructive when it leaks via action.

This exact two-type participation model is a mature signalling/information-timing
combination (GRC, PS, SIG families), NOT an invented independent social primitive.

All types, player payoff routes, participant incentives and action timing are
fully specified; no human, independent-agent or strategic-mixed-equilibrium tests.
"""
from fractions import Fraction as F
from itertools import product

TYPES = ('G', 'B')
ROUTES = ('X', 'Y')
PRIOR = {'G': F(1,2), 'B': F(1,2)}
PARTNER = {'G': F(4), 'B': F(-2)}
PLAYER = {('G','X'):F(10),('G','Y'):F(6),
          ('B','X'):F(5),('B','Y'):F(8)}
SOLO = F(4)


def participant_joins(prob_good, outside=F()):
    """Join only if expected participation payoff meets outside value;
    equality joins by explicit tie rule. No exogenous participation chance."""
    return prob_good * PARTNER['G'] + (1-prob_good)*PARTNER['B'] >= outside


def best_player_route(type_posterior, joins):
    """Expected player payoff on the menu the participant actually permits."""
    if not joins:
        return SOLO, ('SOLO',)
    scores={a:sum((type_posterior.get(t,F())*PLAYER[t,a] for t in TYPES), F()) for a in ROUTES}
    best=max(scores.values())
    return best, tuple(a for a,v in scores.items() if v==best)


def no_test(outside=F()):
    joins=participant_joins(PRIOR['G'],outside)
    return best_player_route(PRIOR,joins)


def private_before_commit(outside=F()):
    """Partner joins first on prior. It CANNOT observe report or later route
    until joining becomes irrevocable."""
    joins=participant_joins(PRIOR['G'],outside)
    if not joins:return SOLO, {'G':'SOLO','B':'SOLO'}
    choices={t:best_player_route({t:F(1)},True)[1][0] for t in TYPES}
    return sum((PRIOR[t]*PLAYER[t,choices[t]] for t in TYPES),F()),choices


def public_before_commit(outside=F(),compensation=False):
    """Truthful report is broadcast before partner chooses join or exit.
    Optional player-paid 2-unit compensation restores B partner's payoff
    from -2 to 0 (only with outside=0).
    """
    score=F();moves={}
    for t in TYPES:
        join=participant_joins(F(t=='G'),outside)
        if join:
            payoff,route=best_player_route({t:F(1)},True)
            moves[t]=('JOIN',route[0])
        else:
            payoff=SOLO
            moves[t]=('EXIT','SOLO')
            if compensation and t=='B' and outside==0:
                supported=max(PLAYER[t,r] for r in ROUTES)-2
                if supported>payoff:
                    payoff=supported
                    moves[t]=('PAID_TO_JOIN','Y')
        score+=PRIOR[t]*payoff
    return score,moves


def deterministic_route_signal(mapping,outside=F()):
    """Player privately knows type and follows a PRE-COMMITTED pure map.
    Partner sees the route BEFORE joining, and updates on used routes.
    Commitment-policy comparison; NOT an exhaustive signaling equilibrium.
    """
    score=F();out={}
    for route in ROUTES:
        used=[t for t in TYPES if mapping[t]==route]
        if not used:continue
        mass=sum((PRIOR[t] for t in used),F())
        belief=sum((PRIOR[t] for t in used if t=='G'),F())/mass
        joins=participant_joins(belief,outside)
        out[route]={'types':tuple(used),'P(G)':belief,'join':joins}
        for t in used:
            score+=PRIOR[t]*(PLAYER[t,route] if joins else SOLO)
    return score,out


def regression():
    assert participant_joins(F(1,2)) and not participant_joins(F())
    assert no_test() == (F(15,2),('X',))
    assert private_before_commit()==(F(9),{'G':'X','B':'Y'})
    assert public_before_commit()==(F(7),{'G':('JOIN','X'),'B':('EXIT','SOLO')})
    assert private_before_commit()[0]>no_test()[0]>public_before_commit()[0]
    tables={}
    for first,second in product(ROUTES,repeat=2):
        mapping={'G':first,'B':second}
        value, response=deterministic_route_signal(mapping)
        tables[first+second]=(value,response)
    assert {k:v[0] for k,v in tables.items()}=={
        'XX':F(15,2),'XY':F(7),'YX':F(5),'YY':F(7)}
    assert max(v for v,_ in tables.values())==no_test()[0]
    assert tables['XY'][1]['Y']=={'types':('B',),'P(G)':F(),'join':False}
    assert public_before_commit(compensation=True)[0] == F(8)
    outside=F(3,2)
    assert no_test(outside)[0]==SOLO
    assert private_before_commit(outside)[0]==SOLO
    assert public_before_commit(outside)[0]==F(7)
    print('AUDIENCE PASS: none=',no_test()[0],
          'private-unseen=',private_before_commit()[0],
          'public-before=',public_before_commit()[0],
          'public-with-compensation=',public_before_commit(compensation=True)[0])
    print('VISIBLE-ROUTE committed maps:',{k:str(v[0]) for k,v in tables.items()},
          'best',max(v for v,_ in tables.values()))
    print('Outside=3/2: no/secret=4, public=7; higher outside option reverses ranking.')
    print('LIMIT: exact type incentives and pure committed route maps, no humans, agents, or full signaling equilibria')

if __name__=='__main__': regression()
