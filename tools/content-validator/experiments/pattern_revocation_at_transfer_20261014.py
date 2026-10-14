from fractions import Fraction
from functools import lru_cache

def solve(revoke=True,recover=True,independent_b=True):
    @lru_cache(None)
    def V(t,owner,permit,claimed,b,side,ever_transferred):
        if t==0:
            return Fraction(side+(7 if claimed==1 else 12 if claimed==2 else 0)+(10 if b==2 else 0)),()
        choices=[]
        def put(label,s):
            if label=="TRANSFER" and revoke:
                on=V(t-1,s[0],0,*s[2:])[0]
                off=V(t-1,*s)[0]
                v=(on+off)/2
            else:v=V(t-1,*s)[0]
            choices.append((v,label))
        put("SIDE",(owner,permit,claimed,b,side+1,ever_transferred))
        if independent_b and b<2:put("B",(owner,permit,claimed,b+1,side,ever_transferred))
        if owner==0 and not ever_transferred and not claimed:put("TRANSFER",(1,1,claimed,b,side,1))
        if owner==0 and not claimed:put("REDEEM",(0,permit,1,b,side,ever_transferred))
        if owner==1 and permit and not claimed:put("ASK",(1,permit,2,b,side,ever_transferred))
        if owner==1 and not claimed and recover:put("RECOVER",(0,permit,claimed,b,side,ever_transferred))
        best=max(v for v,_ in choices)
        return best,tuple(label for v,label in choices if v==best)
    return V(4,0,0,0,0,0,0)

if __name__=="__main__":
    for settings,expected in [
      ((True,True,True),18),
      ((True,False,True),18),
      ((False,True,True),22),
      ((True,True,False),11),
      ((False,False,True),22)]:
        got=solve(*settings)
        assert got[0]==expected,(settings,got)
        print(settings,got)
    print("5 immediate-revocation baselines PASS")
