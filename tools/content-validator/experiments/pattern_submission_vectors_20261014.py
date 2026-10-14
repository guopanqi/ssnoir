"""Enumerate SUBMIT01 physical-submission outcome vectors without assigning player preferences."""
from functools import lru_cache
from itertools import product
from fractions import Fraction

@lru_cache(None)
def continuations(t,a,b,side,used,mask,slots):
    if not t:
        return frozenset({(mask,side)})
    out=set()
    for x in "ABS":
        if x=="A" and a==3: continue
        if x=="B" and b==3: continue
        out.update(continuations(t-1,a+(x=="A"),b+(x=="B"),side+(x=="S"),used,mask,slots))
    if used<slots and a==3 and not(mask&1):
        out.update(continuations(t-1,a,b,side,used+1,mask|1,slots))
    if used<slots and b==3 and not(mask&2):
        out.update(continuations(t-1,a,b,side,used+1,mask|2,slots))
    return frozenset(out)

def outcomes(slots):
    out=set()
    for pre in product("ABS",repeat=6):
        a,b,s=map(pre.count,"ABS")
        if a>3 or b>3:continue
        end=continuations(3,a,b,s,0,0,slots)
        for ma,sa in end:
            for mb,sb in end:
                p_a=Fraction(bool(ma&1)+bool(mb&1),2)
                p_b=Fraction(bool(ma&2)+bool(mb&2),2)
                side=Fraction(sa+sb,2)
                val=side+Fraction((12 if ma&1 else 0)+(3 if ma&2 else 0)+(3 if mb&1 else 0)+(10 if mb&2 else 0),2)
                out.add((p_a,p_b,side,val))
    return out

def pareto(rows):
    # Project to outcome vectors before checking dominance.
    vectors={r[:3] for r in rows}
    return {x for x in vectors if not any(y!=x and all(y[i]>=x[i] for i in range(3)) for y in vectors)}

if __name__=="__main__":
    for slots,expected_count,expected_front in ((1,73,9),(2,86,14)):
        rs=outcomes(slots)
        assert len(rs)==expected_count
        assert len(pareto(rs))==expected_front
        assert max(x[3] for x in rs)==15
        assert ((Fraction(1,2),Fraction(1,2),Fraction(4),Fraction(15)) in rs)
        if slots==2:
            assert (Fraction(1),Fraction(1),Fraction(1),Fraction(15)) in rs
        else:
            assert not any(x[:3]==(1,1,1) for x in rs)
        print("PASS",slots,len(rs),len(pareto(rs)),"scalar_max=15")
