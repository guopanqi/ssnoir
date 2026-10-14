"""CROSS-SCALE04: exact conditional and fixed policies, calendar / travel ablations."""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

ACTIONS = ('TEST','WORK_A','WORK_B','GO_A','GO_B','SIDE')

def solve(calendar=True, travel=True):
    def step(state, action):
        t, loc, a, b, known, typ = state
        if action == 'TEST':
            if t != 0 or loc != 'A': return None
            return (t+1, loc, a+1, b, typ, typ)
        if action == 'WORK_A':
            if travel and loc != 'A' or calendar and t >= 2: return None
            return (t+1, loc, a+1, b, known, typ)
        if action == 'WORK_B':
            if travel and loc != 'B' or calendar and t < 2: return None
            return (t+1, loc, a, b+1, known, typ)
        if action in ('GO_A','GO_B'):
            if not travel or loc == action[-1]: return None
            return (t+1, action[-1], a, b, known, typ)
        if action == 'SIDE':
            return (t+1, loc, a, b, known, typ)
        return None

    def payoff(state, side=0):
        _,_,a,b,_,typ=state
        return (10 if typ=='H' else 4)*(a>=2)+8*(b>=2)+side

    @lru_cache(None)
    def dp(t,loc,a,b,known):
        if t == 4: return F(0), ()
        values=[]
        for act in ACTIONS:
            nextvalues=[]
            for typ in (('H','L') if known=='?' else (known,)):
                ns=step((t,loc,a,b,known,typ),act)
                if ns is None: break
                _,l,aa,bb,k,_=ns
                v=F(payoff(ns)) if t==3 else dp(t+1,l,aa,bb,k)[0]
                nextvalues.append(v+(act=='SIDE'))
            if nextvalues:
                values.append((sum(nextvalues,F(0))/len(nextvalues),act))
        best=max(v for v,_ in values)
        return best,tuple(act for v,act in values if v==best)

    fixed=[]
    for seq in product(ACTIONS, repeat=4):
        vals=[]
        for typ in ('H','L'):
            s=(0,'A',0,0,'?',typ); side=0
            for act in seq:
                nxt=step(s,act)
                if nxt is None: break
                s=nxt;side+=act=='SIDE'
            else:
                vals.append(payoff(s,side))
                continue
            break
        if len(vals)==2:fixed.append((F(sum(vals),2),seq))
    fixedbest=max(v for v,_ in fixed)
    return dp(0,'A',0,0,'?')[0],dp(0,'A',0,0,'?')[1],fixedbest,[seq for v,seq in fixed if v==fixedbest],len(fixed)

if __name__=='__main__':
    expected={(True,True):(F(10),F(9),67),(False,True):(F(10),F(9),108),(True,False):(F(15),F(15),24),(False,False):(F(15),F(15),108)}
    for key,(d,f,n) in expected.items():
        actual,first,fixed,seqs,count=solve(*key)
        assert (actual,fixed,count)==(d,f,n),(key,actual,fixed,count)
        print('calendar, travel:',key,'conditional:',actual,'initial:',first,'fixed:',fixed,'best_fixed:',seqs[0],'legal:',count)
