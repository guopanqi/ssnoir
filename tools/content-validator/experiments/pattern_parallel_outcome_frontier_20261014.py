"""Complete conditional outcome audit of OBLIGATION02, not a new playtest."""
from itertools import product
from fractions import Fraction
from collections import defaultdict
ACTIONS="ABCS"
PATHS=["".join(s) for s in product(ACTIONS,repeat=5)]
def result(s,convenient):
    deadline={'A':2,'B':4,'C':5}
    need={'A':2,'B':2,'C':1 if convenient else 2}
    achieved=tuple(int(s[:deadline[k]].count(k)>=need[k]) for k in 'ABC')
    side=s.count('S')
    return achieved,side
def value(o):
    achieved,side=o
    return sum(x*y for x,y in zip(achieved,(8,9,10)))+side
def project(a,b):
    return tuple(Fraction(a[0][i]+b[0][i],2) for i in range(3))+(Fraction(a[1]+b[1],2),)
groups={}
for first in ACTIONS:
    for convenient in (0,1):
        d=defaultdict(list)
        for s in PATHS:
            if s[0]==first:d[result(s,convenient)].append(s)
        groups[first,convenient]=d
pairs={}
for first in ACTIONS:
    for a,aseq in groups[first,0].items():
        for b,bseq in groups[first,1].items():
            pairs[a,b]=(first,aseq[0],bseq[0])
vectors={project(*pair) for pair in pairs}
front={v for v in vectors if not any(w!=v and all(x>=y for x,y in zip(w,v)) for w in vectors)}
best=max(Fraction(value(a)+value(b),2) for a,b in pairs)
optimal={project(a,b) for a,b in pairs if Fraction(value(a)+value(b),2)==best}
assert len(pairs)==590 and len(vectors)==144 and len(front)==26
assert best==23
assert optimal=={(Fraction(1),Fraction(1,2),Fraction(1),Fraction(1,2)),(Fraction(1,2),Fraction(1),Fraction(1),Fraction(0))}
print('PASS 590 joint outcome combinations, 144 projected vectors, 26 non-dominated; 2 optimal vectors')
