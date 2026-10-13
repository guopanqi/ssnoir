#!/usr/bin/env python3
"""R470-477: tentative outputs depend on a shared hidden upstream premise."""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json
T=4
def score(a,b,theta):return a if theta=='A' else b
def move(t,action,a,b,theta,seen,early):
    if action=='MAKE-A':return (a+(early if t==0 else 1),b)
    if action=='MAKE-B':return (a,b+(early if t==0 else 1))
    if action=='REBASE':
        assert seen and theta in 'AB'
        return (a+b,0) if theta=='A' else (0,a+b)
    raise ValueError(action)
def solve(early=3,audit=True,rebase=True):
    @lru_cache(None)
    def val(t,a,b,known):
        if t==T:return F(a if known=='A' else b) if known else F(a+b,2)
        actions=['MAKE-A','MAKE-B']
        if audit and not known:actions.append('AUDIT')
        if rebase and known:actions.append('REBASE')
        opts={}
        for act in actions:
            if act=='AUDIT':q=sum(val(t+1,a,b,theta) for theta in 'AB')/2
            else:
                aa,bb=move(t,act,a,b,known,bool(known),early)
                q=val(t+1,aa,bb,known)
            opts[act]=q
        return max(opts.values())
    return val(0,0,0,None)
def best_fixed(early=3,audit=True,rebase=True):
    paths={}
    for seq in product(('MAKE-A','MAKE-B','AUDIT','REBASE'),repeat=T):
        if any(x=='AUDIT' for x in seq) and not audit:continue
        if any(x=='REBASE' for x in seq) and not rebase:continue
        values=[]
        for theta in 'AB':
            a=b=0;seen=False
            for t,x in enumerate(seq):
                if x=='AUDIT' and seen:break
                if x=='REBASE' and not seen:break
                if x=='AUDIT':seen=True
                else:a,b=move(t,x,a,b,theta,seen,early)
            else:
                values.append(score(a,b,theta));continue
            break
        if len(values)==2:paths['/'.join(seq)]=sum(values)*F(1,2)
    maximum=max(paths.values())
    return maximum,sorted(k for k,v in paths.items() if v==maximum),len(paths)
def replay(script,theta,early=3):
    a=b=0;seen=False;trace=[]
    for t in range(T):
        action=script[t] if isinstance(script,list) else script(t,theta if seen else None)
        if action=='AUDIT':assert not seen;seen=True
        else:a,b=move(t,action,a,b,theta,seen,early)
        trace.append({'turn':t+1,'action':action,'known':seen,'provisional_A':a,'provisional_B':b})
    return score(a,b,theta),trace
def audit():
    assert score(3,1,'A')==3 and score(3,1,'B')==1  # R470
    full=solve();assert full==F(9,2)                 # R471
    no_audit=solve(audit=False);assert no_audit==3    # R472
    no_rebase=solve(rebase=False);assert no_rebase==F(7,2) # R473
    fixed=best_fixed();assert fixed[0]==4 and fixed[2]==81 # R474
    no_window=solve(early=1);assert no_window==3     # R475
    smaller_window=solve(early=2);assert smaller_window==F(7,2) # R476
    def conditional(t,known):
        if t==0:return 'MAKE-A'
        if t==1:return 'AUDIT'
        if t==2:return 'REBASE' if known=='B' else 'MAKE-A'
        return 'MAKE-B' if known=='B' else 'MAKE-A'
    witness={k:replay(conditional,k) for k in 'AB'}
    assert (witness['A'][0],witness['B'][0])==(5,4)
    assert sum(witness[k][0] for k in 'AB')/2==full  # R477
    fixed_witness={k:replay('MAKE-A/MAKE-A/AUDIT/REBASE'.split('/'),k) for k in 'AB'}
    assert fixed_witness['A'][0]==fixed_witness['B'][0]==4
    return {'schema':'ssnoir.r470-r477.shared-premise-rebase-v1',
      'protocol':'Four action turns, hidden upstream theta=A/B equiprobable. MAKE-A/B adds tentative units of that type; at turn0 MAKE adds 3, later MAKE 1. Terminal score counts only units of correct type theta. AUDIT uses entire turn, once only, reveals theta; after known, REBASE uses entire turn to switch ALL units to correct type. Without audit no ordinary progress feedback. Fixed script uses same action legality but cannot condition next action on audit result. No second worker or social-agent claim.',
      'R470_terminal_invariant':'Only units of true type count; rebase legal only after audit',
      'R471_full_adaptive':full,'R472_no_audit':no_audit,'R473_no_rebase':no_rebase,
      'R474_all_81_legal_fixed_scripts':{'best':fixed[0],'winners':fixed[1],'count':fixed[2]},
      'R475_no_early_window':no_window,'R476_smaller_early_window':smaller_window,
      'R477_independent_constructive_witness':{'conditional':witness,'best_fixed_example':fixed_witness},
      'verdict':'Adaptive 9/2 vs best of 81 fixed scripts 4. First MAKE-A, AUDIT, then REBASE if B, otherwise MAKE-A, then final MAKE yields 5 or 4. Without high-value early window, prefer AUDIT first and the root falls to 3. One transparent conditional script explains the entire difference; it is a known combination of information, early option and bulk rework, not an independently validated new gameplay loop.'}
def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [enc(v) for v in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
