#!/usr/bin/env python3
"""A<->B decision edge research: gate, optional reopen, and feedback burden.
Exact public-signal Fraction DP, normal-initial-state witnesses and ablations.
"""
from collections import defaultdict
from fractions import Fraction as F
from functools import lru_cache

def gate(close=True,gate_b=True,early_offer=True):
    @lru_cache(None)
    def V(t,a,done,b,closed):
        if t==3:return F(0)
        signals=((0,F(1,2)),(1,F(1,2))) if t==1 else ((0,F(1)),)
        return sum((p*Q(t,a,done,b,closed,s)[0] for s,p in signals),F(0))
    @lru_cache(None)
    def Q(t,a,done,b,closed,signal):
        opts={'SIDE':1+V(t+1,a,done,b,closed)}
        if not done and not closed:
            opts['WORK_A']=F(8 if a==1 else 0)+V(t+1,a+1,a==1,b,closed)
        if not b and (a>=1 or not gate_b):
            reward=(14 if signal else 4) if t==1 and early_offer else 6
            opts['CLAIM_B']=F(reward)+V(t+1,a,done,True,closed or (close and not done))
        best=max(opts.values())
        move=next(k for k in ('CLAIM_B','WORK_A','SIDE') if k in opts and opts[k]==best)
        return best,move,opts
    return V(0,0,False,False,False),Q

def reopen(can_reopen=True,close=True,require_a=True,offer_u=True):
    @lru_cache(None)
    def V(t,a,done,b,closed):
        if t==4:return F(0)
        signals=((0,F(1,2)),(1,F(1,2))) if t==1 or t==2 and offer_u else ((0,F(1)),)
        return sum((p*Q(t,a,done,b,closed,s)[0] for s,p in signals),F(0))
    @lru_cache(None)
    def Q(t,a,done,b,closed,signal):
        opts={'SIDE':1+V(t+1,a,done,b,closed)}
        if not done and not closed:
            opts['WORK_A']=F(8 if a==1 else 0)+V(t+1,a+1,a==1,b,closed)
        if not b and (a>=1 or not require_a):
            reward=(14 if signal else 4) if t==1 else 6
            opts['CLAIM_B']=F(reward)+V(t+1,a,done,True,closed or (close and not done))
        if t==2 and offer_u and signal:
            opts['URGENT']=F(9)+V(t+1,a,done,b,closed)
        if can_reopen and b and closed:
            opts['REOPEN_A']=V(t+1,a,done,b,False)
        best=max(opts.values())
        return best,next(k for k in ('CLAIM_B','WORK_A','URGENT','REOPEN_A','SIDE')
                         if k in opts and opts[k]==best),opts
    return V(0,0,False,False,False),Q

def burden(n=6,create_debt=True,can_clear=True,end_penalty=7,cap=2):
    @lru_cache(None)
    def V(t,d):
        if t==n:return F(-end_penalty*d) if create_debt else F(0)
        return sum((p*Q(t,d,high,u)[0] for high,u,p in signals),F(0))
    @lru_cache(None)
    def Q(t,d,high,u):
        opts={'SIDE':1+V(t+1,d)}
        if u:opts['URGENT']=5+V(t+1,d)
        if not create_debt or d<cap:
            opts['B']=F(8 if high else 3)+V(t+1,d+1 if create_debt else d)
        if create_debt and can_clear and d>0:
            opts['A_CLEAR']=V(t+1,d-1)
        best=max(opts.values())
        return best,next(k for k in ('B','URGENT','A_CLEAR','SIDE')
                         if k in opts and opts[k]==best),opts
    signals=tuple((hi,u,F(1,2)*(F(1,4) if u else F(3,4)))
                  for hi in (False,True) for u in (False,True))
    return V(0,0),Q

def gate_or_reopen_reach(model='gate',**kwargs):
    if model=='gate':root,Q=gate(**kwargs);n=3
    elif model=='reopen':root,Q=reopen(**kwargs);n=4
    else:raise ValueError(model)
    frontier={(0,False,False,False):F(1)}
    outcomes=defaultdict(F);action_mass=defaultdict(F)
    for t in range(n):
        nxt=defaultdict(F)
        for (a,done,b,closed),mass in frontier.items():
            signals=((False,F(1,2)),(True,F(1,2))) if t==1 or (
                model=='reopen' and t==2 and kwargs.get('offer_u',True)) else ((False,F(1)),)
            for signal,prob in signals:
                _,move,opts=Q(t,a,done,b,closed,signal)
                assert move in opts
                w=mass*prob
                action_mass[(t,move)]+=w
                na=a+1 if move=='WORK_A' else a
                ndone=done or na==2
                nb=b or move=='CLAIM_B'
                nc=False if move=='REOPEN_A' else closed or (
                    move=='CLAIM_B' and kwargs.get('close',True) and not done)
                nxt[(na,ndone,nb,nc)]+=w
        assert sum(nxt.values())==1
        frontier=nxt
    for (a,done,b,closed),mass in frontier.items():outcomes[(done,b)]+=mass
    assert sum(outcomes.values())==1
    return root,dict(outcomes),dict(action_mass)

def burden_simple(rule,n=6):
    @lru_cache(None)
    def V(t,d):
        if t==n:return -7*F(d)
        acc=F(0)
        for hi in (False,True):
            for u in (False,True):
                if rule=='high_then_clear':
                    act='B' if hi and d<2 else 'URGENT' if u else 'A_CLEAR' if d else 'SIDE'
                elif rule=='clear_then_high':
                    act='A_CLEAR' if d and t==n-1 else 'B' if hi and d<2 else 'URGENT' if u else 'A_CLEAR' if d else 'SIDE'
                elif rule=='high_and_safe':
                    act='B' if hi and d<2 and t<n-1 else 'URGENT' if u else 'A_CLEAR' if d else 'SIDE'
                elif rule=='settle_first':
                    act='A_CLEAR' if d else 'URGENT' if u else 'B' if hi else 'SIDE'
                elif rule=='rule_short':
                    act='A_CLEAR' if d and t==n-1 else 'B' if hi and d<2 and t<n-1 else 'URGENT' if u else 'A_CLEAR' if d else 'SIDE'
                else:raise ValueError(rule)
                r=8 if act=='B' and hi else 3 if act=='B' else 5 if act=='URGENT' else 1 if act=='SIDE' else 0
                nd=d+1 if act=='B' else d-1 if act=='A_CLEAR' else d
                acc+=F(1,2)*(F(1,4) if u else F(3,4))*(F(r)+V(t+1,nd))
        return acc
    return V(0,0)

def burden_reachable_vector():
    root,Q=burden()
    frontier={0:F(1)};v=defaultdict(F);day1=[];witness=[]
    for t in range(6):
        nxt=defaultdict(F)
        for d,mass in frontier.items():
            for hi in (False,True):
                for u in (False,True):
                    prob=mass*F(1,2)*(F(1,4) if u else F(3,4))
                    _,a,opts=Q(t,d,hi,u)
                    v[a if a!='B' else 'B_HIGH' if hi else 'B_LOW']+=prob
                    if d==2:v['CAP_DAY']+=prob
                    if t==1 and prob:day1.append((d,hi,u,a))
                    if t==2 and d==2 and hi and not u and prob:
                        witness.append((t,d,hi,u,a,opts))
                    nd=d+1 if a=='B' else d-1 if a=='A_CLEAR' else d
                    nxt[nd]+=prob
        assert sum(nxt.values())==1
        frontier=nxt
    debt=sum((F(d)*p for d,p in frontier.items()),F(0))
    v['END_DEBT']=debt
    assert 8*v['B_HIGH']+3*v['B_LOW']+5*v['URGENT']+v['SIDE']-7*debt==root
    assert sum((v[k] for k in ('B_HIGH','B_LOW','URGENT','SIDE','A_CLEAR')))==6
    return dict(v),frontier,day1,witness

def check():
    assert gate()[0]==F(29,2)
    assert gate(close=False)[0]==18
    assert gate(gate_b=False)[0]==F(29,2)
    assert gate(early_offer=False)[0]==14
    _,Q=gate()
    assert Q(0,0,False,False,False,0)[1]=='WORK_A'
    assert Q(1,1,False,False,False,0)[1]=='WORK_A'
    assert Q(1,1,False,False,False,1)[1]=='CLAIM_B'
    assert gate_or_reopen_reach()==(
        F(29,2),{(True,True):F(1,2),(False,True):F(1,2)},
        {(0,'WORK_A'):F(1),(1,'WORK_A'):F(1,2),(1,'CLAIM_B'):F(1,2),
         (2,'CLAIM_B'):F(1,2),(2,'SIDE'):F(1,2)})
    assert gate_or_reopen_reach(close=False)[1]=={(True,True):F(1)}
    assert gate_or_reopen_reach(gate_b=False)==gate_or_reopen_reach()
    # Fixed A,A,B gives 14, observed conditional A/(A or B) gives 14.5.
    assert F(8+6)==14

    assert reopen()[0]==21
    assert reopen(can_reopen=False)[0]==F(39,2)
    assert reopen(close=False)[0]==23
    assert reopen(require_a=False)[0]==21
    assert reopen(offer_u=False)[0]==F(37,2)
    _,R=reopen()
    assert R(0,0,False,False,False,0)[1]=='WORK_A'
    assert R(1,1,False,False,False,0)[1]=='WORK_A'
    assert R(1,1,False,False,False,1)[1]=='CLAIM_B'
    assert R(2,1,False,True,True,0)[1]=='REOPEN_A'
    assert R(2,1,False,True,True,1)[1]=='URGENT'
    assert gate_or_reopen_reach('reopen')[1]=={
        (True,True):F(3,4),(False,True):F(1,4)}
    assert gate_or_reopen_reach('reopen',can_reopen=False)[1]=={
        (True,True):F(1,2),(False,True):F(1,2)}
    assert gate_or_reopen_reach('reopen',require_a=False)==gate_or_reopen_reach('reopen')
    # The four public high/low x U paths yield 15,23,22,24.
    assert (F(8+6+1)+F(8+9+6)+F(14+8)+F(14+9+1))/4==21

    assert burden()[0]==F(21415,1024)
    assert burden(create_debt=False)[0]==F(69,2)
    assert burden(can_clear=False)[0]==12
    assert burden(end_penalty=0)[0]==F(439527,16384)
    assert burden(cap=1)[0]==F(40795,2048)
    assert burden(cap=3)[0]==F(85885,4096)
    v,last,d1,witness=burden_reachable_vector()
    assert last=={0:F(14551,16384),1:F(1833,16384)}
    assert v['A_CLEAR']==F(4103,2048) and v['CAP_DAY']==F(83,128)
    assert (1,True,False,'B') in d1 and (1,False,False,'A_CLEAR') in d1
    assert witness and all('B' not in x[5] for x in witness)
    controls={k:burden_simple(k) for k in (
        'high_then_clear','clear_then_high','high_and_safe','settle_first','rule_short')}
    assert controls['rule_short']==F(166859,8192)
    assert max(controls.values())==controls['rule_short']<burden()[0]
    print('GATE: root 29/2; remove B-closes-A 18; remove A-gates-B 29/2; remove early B 14')
    print('REOPEN: root 21; no paid reopening 39/2; no closure 23; no U 37/2')
    print('BURDEN: root 21415/1024; no burden 69/2; no cleanup 12')
    print('BURDEN multi-outcome vector:',{k:str(x) for k,x in v.items()})
    print('BURDEN 5 simple baselines:',{k:str(x) for k,x in controls.items()})
    print('PASS Fraction/observed-information/policy/forward-reach/ablation checks')

if __name__=='__main__':check()
