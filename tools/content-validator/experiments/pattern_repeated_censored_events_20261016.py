"""CENSOR-REPEAT: two endogenous evidence opportunities.

Hypotheses from SSNoir INV/PART/GROUP, not a new psychology finding.
True hidden R/B remains fixed; events on each of two cycles occur independently
with public q; a shield prevents that source's event and its trace. An exposed
occurrence leaves a truthful source trace and causes harm with probability 1/2.
Final source classification pays 12 if correct; each harm event costs 6.

All numbers are exact Fractions. No independent player / human trial.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
from collections import defaultdict

ACTIONS = ('FULL', 'PART_R', 'PART_B', 'WATCH')
Q = F(1, 2)
HARM = F(6)
REWARD = F(12)


def protected(action, src):
    assert action in ACTIONS and src in ('R', 'B')
    return action == 'FULL' or action == 'PART_' + src


def emissions(src, action, q=Q):
    """Conditional signal,damage distribution; src remains hidden."""
    assert F() <= q <= 1
    if protected(action, src):
        return (('NONE', 0, F(1)),)
    return (('NONE', 0, 1-q), (src, 0, q/2), (src, 1, q/2))


def observation(p, action, q=Q):
    """Return (signal, harmed, probability, posterior of R)."""
    joint = defaultdict(lambda: [F(), F()])
    for src, prior in (('R', p), ('B', 1-p)):
        for signal, damaged, likelihood in emissions(src, action, q):
            if not likelihood: continue
            row = joint[(signal, damaged)]
            row[0] += prior * likelihood
            if src == 'R': row[1] += prior * likelihood
    return tuple((s, d, prob, rprob/prob)
                 for (s, d), (prob, rprob) in sorted(joint.items()) if prob)


def solver(q=Q, harm=HARM, reward=REWARD):
    @lru_cache(None)
    def dp(left, p):
        if not left:
            return reward * max(p, 1-p), ('STOP_R' if p >= F(1,2) else 'STOP_B',), {}
        values = {}
        for action in ACTIONS:
            values[action] = sum((pr*(dp(left-1, updated)[0]-d*harm)
                                  for _s, d, pr, updated in observation(p, action, q)), F())
        best = max(values.values())
        return best, tuple(a for a in ACTIONS if values[a] == best), values
    return dp


def fixed_plan(sequence, q=Q, harm=HARM, reward=REWARD):
    """Independent forward hidden-world enumeration, not DP's belief transitions.

    Two fixed shield choices can be followed by a Bayesian optimal final STOP
    on their true public trace history. This is a STRONG fixed-work baseline.
    """
    states = {('R', (), 0): F(1,2), ('B', (), 0): F(1,2)}
    for action in sequence:
        new = defaultdict(F)
        for (src, hist, damages), mass in states.items():
            for signal, injured, p in emissions(src, action, q):
                new[(src, hist+(signal,), damages+injured)] += mass*p
        states = dict(new)
    result = F()
    histories = {history for _,history,_ in states}
    for hist in histories:
        mass_r = sum((v for (src, h, _), v in states.items()
                      if h==hist and src=='R'), F())
        mass_b = sum((v for (src, h, _), v in states.items()
                      if h==hist and src=='B'), F())
        hurts = sum((inj*v for (_src, h, inj), v in states.items()
                     if h==hist), F())
        result += reward*max(mass_r,mass_b)-harm*hurts
    return result


def forward_adaptive(q=Q, harm=HARM, reward=REWARD):
    """A separate forward enumerator on true source, posterior and chosen policy."""
    dp = solver(q,harm,reward)
    mass = {('R',F(1,2),0):F(1,2), ('B',F(1,2),0):F(1,2)}
    history = []
    for step in (2,1):
        new = defaultdict(F)
        for (src,p,loss_count),prob in mass.items():
            action = dp(step,p)[1][0]
            post={(s,d):pp for s,d,pr,pp in observation(p,action,q)}
            for s,d,conditional in emissions(src,action,q):
                if conditional:
                    new[(src,post[s,d],loss_count+d)] += prob*conditional
                    history.append((step,src,p,action,s,d,prob*conditional))
        mass=dict(new)
    total=F()
    classify_correct=F()
    expected_harm=F()
    for (src,p,d),prob in mass.items():
        predicted='R' if p>=F(1,2) else 'B'
        classify_correct+=prob*(predicted==src)
        expected_harm+=prob*d
        total+=prob*((reward if predicted==src else 0)-harm*d)
    assert sum(mass.values(),F())==1
    return total,classify_correct,expected_harm,history


def regression():
    base=solver()
    assert base(2,F(1,2))[0]==F(75,8)
    assert base(2,F(1,2))[1]==('PART_R','PART_B')
    assert base(1,F(2,3))[1]==('PART_R',)
    assert observation(F(1,2),'PART_R') == (
        ('B',0,F(1,8),F()), ('B',1,F(1,8),F()), ('NONE',0,F(3,4),F(2,3)))
    fixed={seq:fixed_plan(seq) for seq in product(ACTIONS, repeat=2)}
    assert len(fixed)==16
    assert max(fixed.values())==F(9)
    assert {s for s,v in fixed.items() if v==F(9)}=={
        ('PART_R','PART_R'),('PART_B','PART_B')}
    assert fixed[('WATCH','WATCH')]==F(15,2)
    total,correct,hurts,history=forward_adaptive()
    assert total==base(2,F(1,2))[0]
    assert F()==sum((p for step,src,prior,act,s,d,p in history
                      if step==2 and s=='R'),F())
    assert correct>=F(1,2) and hurts>=0
    assert total-max(fixed.values()) == F(3,8)
    # Explicit strong extra option: costless complete true-source knowledge
    # makes full protection followed by correct identification yield 12.
    assert REWARD > total
    print('CENSOR-REPEAT PASS: dynamic=', total,'strong fixed=',max(fixed.values()),
          'correct=',correct,'expected harm=',hurts)
    print('At q=1/2, first PART_R & silence -> P(R)=2/3; '
          'future action PART_R still optimal. No evidence of repeated novel planning.')
    print('LIMIT: source model Fraction, no player or independent agent trials')

if __name__=='__main__': regression()
