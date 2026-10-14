"""Five actions, three distinct obligations, disclosed C workload after first action."""
from itertools import product
from fractions import Fraction
from functools import lru_cache
DEADLINES={'A':2,'B':4,'C':5}
REWARDS={'A':8,'B':9,'C':10}
ACTIONS='ABCS'

def evaluate(sequence,shock):
    requirements={'A':2,'B':2,'C':1 if shock else 2}
    achievements=tuple(int(sum(a==key for a in sequence[:DEADLINES[key]])>=requirements[key]) for key in 'ABC')
    side=sequence.count('S')
    return sum(REWARDS[k]*a for k,a in zip('ABC',achievements))+side, achievements+(side,)

def enumerate_policies():
    all_sequences=[''.join(s) for s in product(ACTIONS,repeat=5)]
    fixed=max((Fraction(evaluate(s,0)[0]+evaluate(s,1)[0],2),s) for s in all_sequences)
    choices=[]
    for first in ACTIONS:
        states=[]
        for shock in (0,1):
            states.append(max((evaluate(s,shock)[0],s) for s in all_sequences if s[0]==first))
        choices.append((Fraction(states[0][0]+states[1][0],2),first,states))
    return fixed,max(choices),choices

# Independent recursion of suffix plans, fixed initial action and revealed shock.
@lru_cache(None)
def dp(t,first,shock,prefix):
    if t==5:
        return evaluate(prefix,shock)[0]
    return max(dp(t+1,first,shock,prefix+action) for action in ACTIONS)

if __name__=='__main__':
    fixed,best,choices=enumerate_policies()
    assert fixed[0]==22,(fixed,best)
    assert best[0]==23,(fixed,best)
    assert best[1]=='A'
    assert sorted(x[0] for x in choices)==[Fraction(41,2)]*3+[Fraction(23)]
    assert (evaluate('ACBBC',0)[0],evaluate('AABBC',1)[0])==(19,27)
    assert all(max(evaluate(s,shock)[0] for s in (first+''.join(rest) for rest in product(ACTIONS,repeat=4)))==
               dp(1,first,shock,first) for first in ACTIONS for shock in (0,1))
    print('fixed',fixed,'adaptive',best)
    print('PASS 5-action complete enumeration and suffix recursion')

def constrained_best(required):
    paths=[''.join(s) for s in product(ACTIONS,repeat=5)]
    candidates=[]
    for first in ACTIONS:
        best=[]
        for shock in (0,1):
            valid=[(evaluate(s,shock)[0],s) for s in paths
                   if s[0]==first and evaluate(s,shock)[1]['ABC'.index(required)]]
            if not valid: break
            best.append(max(valid))
        if len(best)==2:
            candidates.append((Fraction(best[0][0]+best[1][0],2),first,best))
    return max(candidates)

if __name__=='__main__':
    for required in 'ABC':
        answer=constrained_best(required)
        assert answer[0]==23,(required,answer)
        print('mandatory',required,answer)
    assert evaluate('AASCC',0)==(19,(1,0,1,1))
    print('PASS: every individually mandatory outcome retains root 23')
