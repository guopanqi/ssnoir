"""CHAIN01-11: four identities under one-to-one matching constraints.
Exact exhaustive finite policies and multiple strong ablations.
The model is an abstract deduction grammar, not a game recreation.
"""
from fractions import Fraction as Q
from itertools import product, permutations, combinations
from functools import lru_cache

CANDIDATES=((0,1,2),(0,1,3),(0,2,3),(1,2,3))
ROSTER=range(4)

def build_worlds(unique=True):
    universe=permutations(ROSTER) if unique else product(ROSTER,repeat=4)
    return tuple(w for w in universe if all(w[i] in CANDIDATES[i] for i in ROSTER))

def resolved(worlds):
    return sum(len({w[i] for w in worlds})==1 for i in ROSTER)

def partition(worlds,who):
    return tuple(tuple(w for w in worlds if w[who]==v) for v in ROSTER
                 if any(w[who]==v for w in worlds))

@lru_cache(None)
def optimal(worlds,budget):
    if not budget:return Q(resolved(worlds)),()
    outcomes={}
    for target in ROSTER:
        parts=partition(worlds,target)
        outcomes[target]=sum(Q(len(sub),len(worlds))*optimal(sub,budget-1)[0]
                             for sub in parts)
    best=max(outcomes.values())
    return best,tuple(t for t,x in outcomes.items() if x==best)

def fixed(worlds,first,second):
    return sum(Q(len(g2),len(worlds))*resolved(g2)
               for g1 in partition(worlds,first) for g2 in partition(g1,second))

def greedy_diversity(worlds,budget):
    """No posterior DP: investigate person with most candidate identities."""
    if not budget:return Q(resolved(worlds))
    diversity=[len(partition(worlds,i)) for i in ROSTER]
    target=diversity.index(max(diversity))
    return sum(Q(len(g),len(worlds))*greedy_diversity(g,budget-1)
               for g in partition(worlds,target))

def complete_probability(worlds,policy,budget):
    if budget==0:return Q(resolved(worlds)==len(ROSTER))
    target=policy(worlds,budget)
    return sum(Q(len(g),len(worlds))*complete_probability(g,policy,budget-1)
               for g in partition(worlds,target))

def regression():
    worlds=build_worlds(True)
    independent=build_worlds(False)
    assert len(worlds)==9 and len(independent)==81
    assert resolved(worlds)==0
    one,_=optimal(worlds,1)
    two,targets=optimal(worlds,2)
    assert one==1 and two==4 and 0 in targets
    best_fixed=max((fixed(worlds,a,b),(a,b)) for a,b in combinations(ROSTER,2))
    assert best_fixed[0]==Q(28,9)
    assert optimal(independent,2)[0]==2
    assert max(fixed(independent,a,b) for a,b in combinations(ROSTER,2))==2
    print('CHAIN01/02 initial',len(worlds),'one inspect',one,
          'adaptive two',two,'fixed',best_fixed,'unconstrained independent',2)

    responses={g[0][0]:g for g in partition(worlds,0)}
    assert {x:len(g) for x,g in responses.items()}=={0:3,1:3,2:3}
    expected={0:3,1:2,2:1}
    for got,group in responses.items():
        target=expected[got]
        assert optimal(group,1)[0]==4 and fixed(group,target,target)==4
        print('CHAIN03 inspect object0 ->',got,'then object',target,'all resolved')
    adaptive_rule=lambda w,n:0 if n==2 else expected[w[0][0]]
    fixed_rule=lambda w,n:0 if n==2 else 1
    assert complete_probability(worlds,adaptive_rule,2)==1
    assert complete_probability(worlds,fixed_rule,2)==Q(5,9)
    assert Q(4)==two  # hypothetical one-action full ledger is a strong bypass
    print('CHAIN04 adaptive P(all)=1, fixed inspect0/1 P(all)=5/9')

    # A toy YES iff >=3 correct positions for a complete permutation:
    # wrong full assignments never match in exactly 3 places.
    for correct in worlds:
        for guess in worlds:
            shared=sum(x==y for x,y in zip(correct,guess))
            assert (shared>=3)==(correct==guess)
    expected_guesses=Q(sum(range(1,len(worlds)))+len(worlds)-1,len(worlds))
    assert expected_guesses==Q(44,9)
    print('CHAIN05 batch-3 complete-assignment oracle mean guess-only',expected_guesses)

    # Ablation: keep one-to-one matching, hide 0..4 candidate restrictions.
    all_set=tuple(ROSTER)
    expected_levels={0:(Q(2),Q(2)),1:(Q(8,3),Q(8,3)),
                     2:(Q(24,7),Q(24,7)),
                     3:(Q(4),Q(36,11)),4:(Q(4),Q(28,9))}
    for count in range(5):
        for selected in combinations(ROSTER,count):
            masks=[CANDIDATES[i] if i in selected else all_set for i in ROSTER]
            ws=tuple(w for w in permutations(ROSTER)
                     if all(w[i] in masks[i] for i in ROSTER))
            adaptive=optimal(ws,2)[0]
            best_pre=max(fixed(ws,a,b) for a,b in combinations(ROSTER,2))
            assert (adaptive,best_pre)==expected_levels[count]
            assert greedy_diversity(ws,2)==adaptive
            print('CHAIN06-11 public constraints',selected,
                  'worlds',len(ws),'dynamic',adaptive,'fixed',best_pre)
    # Three public constraints instead of four creates a uniquely valuable
    # FIRST investigation: the unrestricted object's identity propagates
    # through the others' constrained identity sets.
    partial=tuple(w for w in permutations(ROSTER)
                  if all(w[i] in CANDIDATES[i] for i in range(3)))
    assert len(partial)==11
    assert optimal(partial,2)==(Q(4),(3,))
    assert greedy_diversity(partial,2)==Q(4)
    assert all(sum(Q(len(g),len(partial))*optimal(g,1)[0]
                   for g in partition(partial,i))==Q(36,11)
               for i in (0,1,2))
    assert max(fixed(partial,a,b) for a,b in combinations(ROSTER,2))==Q(36,11)
    expected_followups={0:(0,1,2),1:(2,),2:(1,),3:(0,)}
    for group in partition(partial,3):
        first_answer=group[0][3]
        assert optimal(group,1)==(Q(4),expected_followups[first_answer])
    print('CHAIN12-14 first object3 uniquely optimal, all second choices conditional')
    print('CHAIN01-16 PASS (greedy max-candidate rule also optimal in all tested variants)')

if __name__=='__main__':regression()
