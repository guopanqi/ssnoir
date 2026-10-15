"""INFO-PROD01: exact DP on productive investigation, deadline and disclosure.
After action 3 true urgent target is revealed if not already known.
P=work A + perfect reveal; Q=work B + perfect reveal; I=perfect reveal only.
"""
from fractions import Fraction
from functools import lru_cache

def solve(menu="", reveal_after=3):
    @lru_cache(None)
    def known(t,a,b,side,qual_a,qual_b,T):
        if t==4:
            return (8*qual_a+8*(b>=2)+side if T=="A"
                    else 8*qual_b+8*(a>=2)+side), ""
        options=[]
        for m in "ABS":
            aa,bb,ss=a+(m=="A"),b+(m=="B"),side+(m=="S")
            qa,qb=(int(aa>=2),int(bb>=2)) if t+1==3 else (qual_a,qual_b)
            v,_=known(t+1,aa,bb,ss,qa,qb,T)
            options.append((v,m))
        return max(options)

    @lru_cache(None)
    def unknown(t,a,b,side,qual_a,qual_b):
        if t==4:
            return Fraction(8*qual_a+8*(b>=2)+side+
                            8*qual_b+8*(a>=2)+side,2), ""
        choices=[]
        for m in "ABS"+menu:
            aa,bb,ss=a+(m in ("A","P")),b+(m in ("B","Q")),side+(m=="S")
            qa,qb=(int(aa>=2),int(bb>=2)) if t+1==3 else (qual_a,qual_b)
            if m in "IPQ" or t+1==reveal_after:
                v=Fraction(sum(known(t+1,aa,bb,ss,qa,qb,T)[0] for T in "AB"),2)
            else:
                v=unknown(t+1,aa,bb,ss,qa,qb)[0]
            choices.append((v,m))
        return max(choices)
    return unknown(0,0,0,0,0,0)

if __name__=="__main__":
    expected=[("",Fraction(25,2)),("I",Fraction(25,2)),
              ("P",Fraction(16)),("PQ",Fraction(16)),
              ("IPQ",Fraction(16))]
    for menu,value in expected:
        actual=solve(menu)
        assert actual[0]==value,(menu,actual)
        print(menu or "none",actual)
    assert solve("",reveal_after=2)[0]==16
    print("PASS: 5 exact menu tests and free early-reveal upper control")
