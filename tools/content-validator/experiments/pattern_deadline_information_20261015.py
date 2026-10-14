"""INFO-WINDOW01: urgency disclosure, free noisy signal, paid perfect inspection."""
from itertools import product
from fractions import Fraction
ACT="ABS"
def score(s,target,urgency=True):
    a=s[:3 if urgency and target=="A" else 4].count("A")>=2
    b=s[:3 if urgency and target=="B" else 4].count("B")>=2
    return 8*a+8*b+s.count("S")
def reveal(n):
    bests=[]
    for prefix in map("".join,product(ACT,repeat=n)):
        branches=[]
        for target in "AB":
            branches.append(max((score(prefix+"".join(tail),target),"".join(tail))
                                for tail in product(ACT,repeat=4-n)))
        bests.append((Fraction(branches[0][0]+branches[1][0],2),prefix,branches))
    return max(bests)
def noisy(q):
    root=[]
    for first in ACT:
        branches=[]
        for signal in "AB":
            options=[]
            for middle in map("".join,product(ACT,repeat=2)):
                conditioned=[max((score(first+middle+end,t),end) for end in ACT)
                             for t in "AB"]
                expected=sum((q if t==signal else 1-q)*conditioned[i][0]
                             for i,t in enumerate("AB"))
                options.append((expected,middle,conditioned))
            branches.append(max(options))
        root.append((sum(x[0] for x in branches)/2,first,branches))
    return max(root)
def paid_inspect():
    root=[]
    for first in ACT:
        # The second action is INSPECT (not SIDE); the actual target
        # is learned at the end of that action.
        branches=[]
        for t in "AB":
            branches.append(max((score(first+"S"+a+b,t)-1,a+b)
                                for a,b in product(ACT,repeat=2)))
        root.append((Fraction(branches[0][0]+branches[1][0],2),first,branches))
    return max(root)
if __name__=="__main__":
    expected={0:16,1:16,2:16,3:Fraction(25,2),4:12}
    for n,v in expected.items():
        answer=reveal(n)
        assert answer[0]==v,(n,answer)
        print("reveal after",n,"actions:",v)
    for q,v in [(Fraction(1,2),Fraction(25,2)),
                (Fraction(3,4),Fraction(57,4)),(Fraction(1),16)]:
        answer=noisy(q)
        assert answer[0]==v,(q,answer)
        print("early noisy signal",q,":",v)
    assert paid_inspect()[0]==9
    assert max(Fraction(score(s,"A")+score(s,"B"),2)
               for s in map("".join,product(ACT,repeat=4)))==12
    print("PASS: full fixed paths, reveal windows, noisy and paid inspection")
