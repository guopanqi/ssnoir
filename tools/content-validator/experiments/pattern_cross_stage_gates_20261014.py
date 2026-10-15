"""Cross-stage obligations: first-scene results gate one-use Q/R in second scene.
Full pre-reveal 5-action enumeration and 2-action follow-up actions.
"""
from itertools import product
from fractions import Fraction
ACTIONS='ABCS'
PATHS=[''.join(x) for x in product(ACTIONS,repeat=5)]
def earned(s,convenient):
    a=s[:2].count('A')>=2
    b=s[:4].count('B')>=2
    c=s.count('C')>=(1 if convenient else 2)
    return a,b,c,s.count('S')
def follow(a,b,always=False):
    menu=['X','S']+(['Q'] if a or always else [])+(['R'] if b or always else [])
    payoff={'X':3,'S':1,'Q':9,'R':5}
    return max((sum(payoff[k] for k in acts),''.join(acts)) for acts in product(menu,repeat=2)
               if acts.count('Q')<=1 and acts.count('R')<=1)
def outcome(s,convenient,always=False):
    a,b,c,side=earned(s,convenient)
    return 8*a+9*b+10*c+side+follow(a,b,always)[0]
def optimum(always=False):
    rows=[]
    for first in ACTIONS:
        branches=[max((outcome(s,k,always),s) for s in PATHS if s[0]==first) for k in (0,1)]
        rows.append((Fraction(branches[0][0]+branches[1][0],2),first,branches))
    return max(rows),rows
if __name__=='__main__':
    gated,rows=optimum(False)
    always,_=optimum(True)
    assert gated[0]==Fraction(73,2),gated
    assert always[0]==37,always
    assert gated[1]=='A'
    assert outcome('AABBS',0,False)==32
    assert outcome('AABBC',1,False)==41
    assert outcome('ACBBC',0,True)==33
    print('gated',gated)
    print('always-legal',always)
    print('PASS full conditional prefix enumeration; difference 1/2')
