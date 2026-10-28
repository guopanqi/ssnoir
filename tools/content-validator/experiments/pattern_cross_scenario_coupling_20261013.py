#!/usr/bin/env python3
"""COUP01: operator-produced evidence and conditional claim actions.
Pure Python Fraction exact audit; graph coupling can be bypassed by a
simple optimal policy. No player psychology claims.
"""
from functools import lru_cache
from fractions import Fraction as Q
DAYS=7
CHANGE=Q(1,5)

def build(kind='gated',edit=True,prior=Q(1,2)):
    assert kind in ('gated','flat')
    @lru_cache(None)
    def dp(t,mode,phase,a,b,claim,need):
        if t==4 and need=='?':
            return prior*dp(t,mode,phase,a,b,claim,'A')+(1-prior)*dp(t,mode,phase,a,b,claim,'B')
        if t==DAYS:
            return Q(0) if claim=='none' else Q((14 if need=='A' else 4)
                if claim=='A' else (10 if need=='B' else 4))
        moves=['SIDE','MANUAL_A','MANUAL_B','RUN']
        if edit:moves.append('EDIT')
        if claim=='none' and a>=1 and t<4:moves.append('CLAIM_A')
        if claim=='none' and b>=2:moves.append('CLAIM_B')
        if claim=='A':moves.append('REVOKE')
        if kind=='gated' and claim=='A':
            moves=[x for x in moves if x!='MANUAL_B' and not (x=='RUN' and phase==1)]
        values={}
        for move in moves:
            nm,na,nb,nc=mode,a,b,claim
            reward=Q(move=='SIDE')
            if move=='EDIT':nm=1-mode
            elif move=='CLAIM_A':na-=1;nc='A'
            elif move=='CLAIM_B':nb-=2;nc='B'
            elif move=='REVOKE':nc='none'
            elif move=='RUN' and mode==phase:
                if phase==0:na=1
                else:nb=min(2,b+1)
            elif move=='MANUAL_A':na=1
            elif move=='MANUAL_B':nb=min(2,b+1)
            success=(1-CHANGE)*dp(t+1,nm,phase,na,nb,nc,need)+CHANGE*dp(t+1,nm,1-phase,na,nb,nc,need)
            if move.startswith('MANUAL'):
                fail=(1-CHANGE)*dp(t+1,nm,phase,a,b,nc,need)+CHANGE*dp(t+1,nm,1-phase,a,b,nc,need)
                success=(success+fail)/2
            values[move]=reward+success
        return max(values.values())
    return dp

def main():
    rows=[]
    for prior in (Q(1,4),Q(1,2),Q(3,4)):
        results={}
        for phase in (0,1):
            for kind in ('flat','gated'):
                for edit in (False,True):
                    solve=build(kind,edit,prior)
                    results[(phase,kind,edit)]=solve(0,0,phase,0,0,'none','?')
        for phase in (0,1):
            assert results[(phase,'flat',True)]>=results[(phase,'gated',True)]
            assert results[(phase,'gated',True)]>=results[(phase,'gated',False)]
        # Available from phase0: RUN -> one A proof, CLAIM_A, SIDE x5.
        # It skips all purported cross-goal reconfiguration.
        bypass=Q(9)+10*prior
        assert results[(0,'flat',True)]==bypass
        assert results[(0,'gated',True)]==bypass
        rows.append((prior,results))
        print('COUP01',prior,'simple_phase0',bypass,'phase1_gated',
              results[(1,'gated',True)],'phase1_flat',
              results[(1,'flat',True)],'phase1_no_edit',
              results[(1,'gated',False)])
    assert rows[0][1][(1,'flat',True)]-rows[0][1][(1,'gated',True)]==Q(61,5000)
    assert rows[1][1][(1,'flat',True)]==rows[1][1][(1,'gated',True)]
    assert rows[2][1][(1,'flat',True)]==rows[2][1][(1,'gated',True)]
    print('PASS full coupled-policy DP and strong simple-bypass controls')
if __name__=='__main__':main()
