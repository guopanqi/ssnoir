#!/usr/bin/env python3
"""SEM01-03: purpose retargeting vs production bonus vs full menu.

Four action turns, exact Fraction DP. Signal HIGH/LOW is observed before
the action indexed by 'reveal'. A/B each pay 8 on reaching two progress;
C pays 12 in HIGH, 1 in LOW. SIDE pays 1 immediately. With last=A,
B_TO_C is a NEW OUTPUT action instead of an increased B yield.
"""
from functools import lru_cache
from fractions import Fraction as Q

T = 4
KINDS = ('retarget', 'buff', 'always', 'plain')
INITIAL = (0, 0, 0, '-')

def legal(kind, last):
    assert kind in KINDS
    actions = ['A', 'B', 'SIDE']
    if kind == 'always' or (kind == 'retarget' and last == 'A'):
        actions.append('B_TO_C')
    return tuple(actions)

def step(kind, state, action):
    a, b, c, last = state
    assert action in legal(kind, last), (kind, state, action)
    if action == 'A':
        return min(a + 1, 2), b, c, 'A'
    if action == 'B':
        return a, min(b + 1 + int(kind == 'buff' and last == 'A'), 2), c, 'B'
    if action == 'B_TO_C':
        return a, b, 1, 'B'
    return a, b, c, 'SIDE'

def terminal(state, high):
    a, b, c, _ = state
    return Q(8 * (a == 2) + 8 * (b == 2) + (12 if high else 1) * (c == 1))

def solver(kind, reveal=1, p=Q(1, 2)):
    assert 0 <= reveal < T and 0 <= p <= 1
    @lru_cache(None)
    def dp(turn, state, signal):
        if signal == '?' and turn == reveal:
            return p * dp(turn, state, 'high') + (1-p) * dp(turn, state, 'low')
        if turn == T:
            assert signal != '?'
            return terminal(state, signal == 'high')
        return max(Q(act == 'SIDE') + dp(turn+1, step(kind,state,act),signal)
                   for act in legal(kind, state[-1]))
    if reveal == 0:
        return p*dp(0,INITIAL,'high') + (1-p)*dp(0,INITIAL,'low')
    return dp(0,INITIAL,'?')

def fixed(kind, p=Q(1,2)):
    """Exhaustively enumerate all legal non-adaptive scripts."""
    scores = []
    def rec(turn,state,actions,side):
        if turn == T:
            scores.append((side+p*terminal(state,True)+(1-p)*terminal(state,False),
                           tuple(actions)))
            return
        for action in legal(kind,state[-1]):
            rec(turn+1,step(kind,state,action),actions+[action],
                side+int(action=='SIDE'))
    rec(0,INITIAL,[],0)
    best = max(v for v,_ in scores)
    return best,tuple(plan for v,plan in scores if v==best),len(scores)

def run_plan(kind,plan,high):
    state=INITIAL
    side=0
    for action in plan:
        side+=int(action=='SIDE')
        state=step(kind,state,action)
    return Q(side)+terminal(state,high),state

def main():
    values={k:solver(k) for k in KINDS}
    assert values=={'retarget':Q(37,2),'buff':Q(17),
                    'always':Q(37,2),'plain':Q(16)}
    assert fixed('retarget')[0]==16
    for kind in KINDS:
        print('SEM01',kind,'adaptive',values[k],'fixed',fixed(kind))
    # A short observable rule, not a sophisticated search, attains exact DP.
    plans={'high':('A','A','B_TO_C','SIDE'),
           'low':('A','A','B','B')}
    high,sh=run_plan('retarget',plans['high'],True)
    low,sl=run_plan('retarget',plans['low'],False)
    assert (high,low)==(Q(21),Q(16))
    assert (high+low)/2==values['retarget']
    print('SEM01 public conditional plan',plans,'result',(high,low),
          'end_states',(sh,sl))
    # Only the terminal C result has been converted; no generic B+1 can
    # represent this outcome dimension without adding a different action.
    # But the matched always-available menu CAN do everything at same cost.
    for reveal in range(T):
        row={k:solver(k,reveal=reveal) for k in KINDS}
        assert row['always']>=row['retarget']
        print('SEM02 reveal_turn',reveal,row)
    for p in (Q(0),Q(1,4),Q(1,2),Q(3,4),Q(1)):
        r=solver('retarget',p=p)
        u=solver('always',p=p)
        f=fixed('retarget',p=p)[0]
        assert f<=r<=u
        print('SEM03 P(high)',p,'retarget',r,'universal',u,'fixed',f)
    print('PASS exact public-signal audit, strong fixed schedules, fair menus')

if __name__=='__main__':
    main()
