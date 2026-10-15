"""INTERRUPT01: exhaustive 4-action interrupted-work policies."""
from itertools import product
from fractions import Fraction
def score(plan,shock,fragile):
    a=b=s=0
    for i,act in enumerate(plan):
        if i==2 and shock and fragile:a=0
        if act=="A":a+=1
        elif act=="B":b+=1
        elif act=="S":s+=1
    return 12*(a>=3)+8*(b>=2)+s
def solve(fragile):
    seq=[''.join(x) for x in product('ABS',repeat=4)]
    fixed=max((Fraction(score(p,0,fragile)+score(p,1,fragile),2),p) for p in seq)
    adaptive=[]
    for pre in map(''.join,product('ABS',repeat=2)):
        tails=[max((score(pre+tail,sh,fragile),tail) for tail in map(''.join,product('ABS',repeat=2))) for sh in (0,1)]
        adaptive.append((Fraction(tails[0][0]+tails[1][0],2),pre,tails))
    return fixed,max(adaptive)
def score_protect(plan,shock):
    a=b=s=0;protect=False
    for i,act in enumerate(plan):
        if i==2 and shock and not protect:a=0
        if act=="A":a+=1
        elif act=="B":b+=1
        elif act=="S":s+=1
        elif act=="P":protect=True
    return 12*(a>=3)+8*(b>=2)+s
def protect():
    seq=list(map(''.join,product('ABSP',repeat=4)))
    fixed=max((Fraction(score_protect(p,0)+score_protect(p,1),2),p) for p in seq)
    adaptive=[]
    for pre in map(''.join,product('ABSP',repeat=2)):
        tails=[max((score_protect(pre+tail,sh),tail) for tail in map(''.join,product('ABSP',repeat=2))) for sh in (0,1)]
        adaptive.append((Fraction(tails[0][0]+tails[1][0],2),pre,tails))
    return fixed,max(adaptive)
if __name__=="__main__":
    assert solve(False)[1][0]==13
    assert solve(True)[0][0]==10 and solve(True)[1][0]==11
    assert protect()[0][0]==12 and protect()[1][0]==12
    for name,result in [("resumable",solve(False)),("fragile",solve(True)),("protected",protect())]:
        print(name,result)
    print("PASS complete action enumeration")
