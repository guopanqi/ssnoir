#!/usr/bin/env python3
"""OPS01/CLAIM01-03: two independent abstract decision-relation audits.
Exact Fraction DP, exhaustive fixed plans, online forward histories,
same-cost full-menu counterfactuals. Does not simulate human play.
"""
from functools import lru_cache
from fractions import Fraction as Q
from itertools import product

H=6
FLIP=Q(1,5)
ACTS=('RUN','MANUAL0','MANUAL1','EDIT')

def actions(kind):
    if kind=='macro':return ('RUN','MANUAL0','MANUAL1','SET0','SET1')
    if kind=='static':return ('RUN','MANUAL0','MANUAL1')
    return ACTS

def step(mode,phase,act,kind):
    if act=='EDIT':return 1-mode,Q(0)
    if act.startswith('SET'):return int(act[-1]),Q(0)
    if act=='RUN':return mode,Q(1,2) if kind=='no_late' else Q(mode==phase)
    assert act.startswith('MANUAL')
    return mode,Q(1,2)

def operator_dp(kind,flip=FLIP):
    @lru_cache(None)
    def dp(t,m,p):
        if t==H:return Q(0)
        options=[]
        for a in actions(kind):
            nm,gain=step(m,p,a,kind)
            options.append(gain+(1-flip)*dp(t+1,nm,p)+flip*dp(t+1,nm,1-p))
        return max(options)
    return dp

def fixed_schedule(flip=FLIP):
    @lru_cache(None)
    def go(plan,t,m,p):
        if t==H:return Q(0)
        nm,g=step(m,p,plan[t],'toggle')
        return g+(1-flip)*go(plan,t+1,nm,p)+flip*go(plan,t+1,nm,1-p)
    return max(go(plan,0,0,1) for plan in product(ACTS,repeat=H))

def one_step_from_no_edit(flip=FLIP):
    @lru_cache(None)
    def base(t,m,p):
        if t==H:return Q(0)
        a='RUN' if m==p else 'MANUAL0'
        nm,g=step(m,p,a,'toggle')
        return g+(1-flip)*base(t+1,nm,p)+flip*base(t+1,nm,1-p)
    @lru_cache(None)
    def improved(t,m,p):
        if t==H:return Q(0)
        q={}
        for a in ACTS:
            nm,g=step(m,p,a,'toggle')
            q[a]=g+(1-flip)*base(t+1,nm,p)+flip*base(t+1,nm,1-p)
        nm,g=step(m,p,max(q,key=q.get),'toggle')
        return g+(1-flip)*improved(t+1,nm,p)+flip*improved(t+1,nm,1-p)
    return improved(0,0,1)

def operator_outcome(flip=FLIP):
    dp=operator_dp('toggle',flip)
    mass={(0,0,1):Q(1)}
    vector=[Q(0),Q(0),Q(0)] # phase0 results, phase1 results, edits
    for t in range(H):
        next_mass={}
        for (_,m,p),prob in mass.items():
            q={}
            for a in ACTS:
                nm,g=step(m,p,a,'toggle')
                q[a]=g+(1-flip)*dp(t+1,nm,p)+flip*dp(t+1,nm,1-p)
            a=max(q,key=q.get)
            nm,g=step(m,p,a,'toggle')
            vector[p]+=prob*g
            vector[2]+=prob*(a=='EDIT')
            for nextp,weight in ((p,1-flip),(1-p,flip)):
                key=(t+1,nm,nextp)
                next_mass[key]=next_mass.get(key,Q(0))+prob*weight
        mass=next_mass
    assert sum(mass.values())==1
    assert vector[0]+vector[1]==dp(0,0,1)
    return tuple(vector)

def forward_online(flip=FLIP):
    """2^5 public phase paths x 2^6 unseen actual inputs; no future peeking."""
    dp=operator_dp('toggle',flip)
    result=Q(0)
    plans=set()
    for changes in product((0,1),repeat=H-1):
        phases=[1]
        prob=Q(1)
        for change in changes:
            prob*=flip if change else 1-flip
            phases.append(phases[-1]^change)
        for inputs in product((0,1),repeat=H):
            mode=0
            successes=0
            plan=[]
            for t,p in enumerate(phases):
                q={}
                for a in ACTS:
                    nm,g=step(mode,p,a,'toggle')
                    q[a]=g+(1-flip)*dp(t+1,nm,p)+flip*dp(t+1,nm,1-p)
                a=max(q,key=q.get)
                plan.append(a)
                if a=='EDIT':mode=1-mode
                else:
                    actual=(inputs[t]^mode) if a=='RUN' else int(a[-1])
                    successes+=int(actual==(inputs[t]^p))
            plans.add(tuple(plan))
            result+=prob*Q(successes,2**H)
    assert result==dp(0,0,1)
    return len(plans),result

CH=4
def claim_actions(t,claim,variant):
    out=['SIDE']
    if claim=='none':
        if t<2 or variant=='late_menu':out.append('CLAIM_A')
        out.append('CLAIM_B')
    elif claim=='A' and variant=='soft':out.append('REVOKE')
    return tuple(out)

def claim_step(claim,action):
    if action=='REVOKE':return 'none'
    if action.startswith('CLAIM_'):return action[-1]
    return claim

def terminal_claim(claim,high,bonus):
    if claim=='none':return Q(0)
    if claim=='A':return Q(10+4*bonus if high else 4)
    return Q(4 if high else 10)

def claim_dp(variant,prior=Q(1,2),bonus=True):
    assert variant in ('late_menu','hard','soft')
    @lru_cache(None)
    def dp(t,claim,signal):
        if t==2 and signal=='?':
            return prior*dp(t,claim,'A')+(1-prior)*dp(t,claim,'B')
        if t==CH:return terminal_claim(claim,signal=='A',bonus)
        return max(Q(a=='SIDE')+dp(t+1,claim_step(claim,a),signal)
                   for a in claim_actions(t,claim,variant))
    return dp

def fixed_claim(variant,prior=Q(1,2),bonus=True):
    values=[]
    def visit(t,claim,side):
        if t==CH:
            values.append(side+prior*terminal_claim(claim,True,bonus)+
                          (1-prior)*terminal_claim(claim,False,bonus))
            return
        for a in claim_actions(t,claim,variant):
            visit(t+1,claim_step(claim,a),side+int(a=='SIDE'))
    visit(0,'none',0)
    return max(values),len(values)

def main():
    for flip in (Q(0),Q(1,10),Q(1,5),Q(1,2),Q(4,5),Q(1)):
        dp=operator_dp('toggle',flip)
        macro=operator_dp('macro',flip)
        assert all(dp(t,m,p)==macro(t,m,p) for t in range(H+1)
                   for m in (0,1) for p in (0,1))
        assert operator_dp('no_late',flip)(0,0,1)==3
        assert one_step_from_no_edit(flip)==dp(0,0,1)
        print('OPS',flip,'DP',dp(0,0,1),'fixed',fixed_schedule(flip),
              'without_edits',operator_dp('static',flip)(0,0,1))
    assert operator_dp('toggle')(0,0,1)==Q(518,125)
    assert fixed_schedule()==Q(9974,3125)
    assert operator_outcome()==(Q(3557,3125),Q(9393,3125),Q(7,5))
    assert forward_online()==(32,Q(518,125))
    dp=operator_dp('toggle')
    def qvalue(t,m,p,a):
        nm,g=step(m,p,a,'toggle')
        return g+(1-FLIP)*dp(t+1,nm,p)+FLIP*dp(t+1,nm,1-p)
    assert qvalue(1,1,1,'RUN')>qvalue(1,1,1,'EDIT')
    assert qvalue(1,1,0,'EDIT')>qvalue(1,1,0,'MANUAL0')
    print('OPS vector',operator_outcome(),'2048 independent histories verified')
    for prior in (Q(0),Q(1,10),Q(1,4),Q(1,2),Q(3,4),Q(1)):
        # The full late menu is the NO early premium counterfactual.
        result={v:claim_dp(v,prior,False if v=='late_menu' else True)(0,'none','?')
                for v in ('late_menu','hard','soft')}
        no_bonus=claim_dp('soft',prior,False)(0,'none','?')
        assert result['late_menu']==13
        assert result['soft']>=result['hard']
        print('CLAIM',prior,result,'soft_without_premium',no_bonus,
              'best_fixed_soft',fixed_claim('soft',prior,True))
    assert claim_dp('soft',Q(1,4))(0,'none','?')==Q(25,2)
    assert claim_dp('hard',Q(1,4))(0,'none','?')==Q(23,2)
    assert claim_dp('soft',Q(1,4),False)(0,'none','?')==Q(23,2)
    soft=claim_dp('soft',Q(1,2))
    assert soft(3,'none','B')>Q(1)+soft(3,'A','B')
    print('PASS two distinct families: exact only; no human/player claim')
if __name__=='__main__':main()
