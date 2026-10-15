"""ACTION-COUPLE01: changing actions speeds production and preserves options."""
from itertools import product
from fractions import Fraction
ACTIONS="AB"
PATHS=["".join(s) for s in product(ACTIONS,repeat=4)]
def work(s,change_bonus=2):
    a=b=0
    for i,action in enumerate(s):
        amount=change_bonus if i>0 and action!=s[i-1] else 1
        if action=="A":a+=amount
        else:b+=amount
    return a,b
def score(s,priority,change_bonus=2):
    a,b=work(s,change_bonus)
    return (10 if priority=="A" else 4)*int(a>=4)+(10 if priority=="B" else 4)*int(b>=4)
def audit(change_bonus=2):
    fixed=max((Fraction(score(s,"A",change_bonus)+score(s,"B",change_bonus),2),s) for s in PATHS)
    conditional=[]
    for prefix in ("AA","AB","BA","BB"):
        a=max((score(s,"A",change_bonus),s) for s in PATHS if s.startswith(prefix))
        b=max((score(s,"B",change_bonus),s) for s in PATHS if s.startswith(prefix))
        conditional.append((Fraction(a[0]+b[0],2),prefix,a,b))
    return fixed,max(conditional),conditional
if __name__=="__main__":
    fixed,adaptive,rows=audit()
    assert fixed[0]==7 and adaptive[0]==10
    assert adaptive[1] in ("AB","BA")
    assert score("ABAA","A")==10 and score("ABBB","B")==10
    assert all(x[0]<=7 for x in rows if x[1] in ("AA","BB"))
    baseline,flex,_=audit(1)
    assert baseline[0]==7 and flex[0]==7
    print("coupled fixed",fixed,"adaptive",adaptive)
    print("without switching boost fixed",baseline,"adaptive",flex)
    print("PASS 16 fixed action paths and 4 contingent prefixes")
