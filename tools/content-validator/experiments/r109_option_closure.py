#!/usr/bin/env python3
"""R109: does completing the main objective close an unresolved side discovery?

Exact Fraction independent 4-action paper counterfactual. Extends R108's
ordinary-work free clue and dedicated probe. Makes main completion an
immediate terminal event, while preserving all other outcomes/probabilities.
No Scheme or human evidence. Compare directly to R108's independent outcomes,
with and without paid probe, and against legible reactive policy families.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json, sys

def study(K=4, q=F(1,4), bonus=2, actions=4):
    q=F(q)
    def available(a,k,got,can_probe):
        return (('work',) if a<3 else ()) + (('explore',) if not got else ()) + (('probe',) if can_probe and k>1 and not got else ())
    def move(a,k,got,n,action):
        if action=='work':
            choices=((q,1),(1-q,k)) if k>1 else ((F(1),1),)
            return tuple((F(1,3)*pr,min(3,a+v),kk,got,n-1) for v in range(3) for pr,kk in choices if pr)
        if action=='explore':
            return ((F(1,k),a,k,True,n-1),(F(k-1,k),a,k-1,False,n-1)) if k>1 else ((F(1),a,1,True,n-1),)
        if action=='probe':
            return ((F(1),a,1,got,n-1),)
        raise ValueError(action)
    def terminal(a,got,n,closure):
        return n==0 or (a>=3 and (closure or got))
    def reward(a,got):
        return F(a>=3)+bonus*F(got)
    @lru_cache(None)
    def optimal(a,k,got,n,closure,can_probe):
        if terminal(a,got,n,closure):return reward(a,got)
        return max(sum(pr*optimal(aa,kk,gg,nn,closure,can_probe) for pr,aa,kk,gg,nn in move(a,k,got,n,act))
                   for act in available(a,k,got,can_probe))
    def action_values(s,closure):
        a,k,got,n=s
        if terminal(a,got,n,closure):return {}
        return {act:sum(pr*optimal(aa,kk,gg,nn,closure,True)
                   for pr,aa,kk,gg,nn in move(a,k,got,n,act))
                   for act in available(a,k,got,True)}
    def make_policy(family, t, closure):
        site_limit,progress_limit,cutoff=t
        def choose(a,k,got,n):
            opts=available(a,k,got,True)
            if 'work' in opts and family=='work-first':return 'work'
            if 'work' in opts and family=='work-one' and n==actions:return 'work'
            if 'work' in opts and family=='work-until' and a<progress_limit and n>cutoff:return 'work'
            if 'work' in opts and family=='keep-open' and (got or a<progress_limit) and n>cutoff:return 'work'
            if 'explore' in opts and family=='explore-one' and k==K and n==actions:return 'explore'
            if 'explore' in opts:
                if k>site_limit and n>1 and 'probe' in opts:return 'probe'
                return 'explore'
            return 'work'
        return choose
    def evaluate(policy,closure):
        @lru_cache(None)
        def v(a,k,got,n):
            if terminal(a,got,n,closure):return reward(a,got)
            act=policy(a,k,got,n)
            return sum(p*v(aa,kk,gg,nn) for p,aa,kk,gg,nn in move(a,k,got,n,act))
        return v(0,K,False,actions)
    cases={}
    for closure in (False,True):
        all_baselines=[]
        for family in ('work-first','explore-first','explore-one','work-one','work-until','keep-open'):
            for t in product(range(1,K+1),range(1,4),range(1,actions)):
                score=evaluate(make_policy(family,t,closure),closure)
                all_baselines.append((score,family,t))
        best=max(all_baselines)
        full=optimal(0,K,False,actions,closure,True)
        without=optimal(0,K,False,actions,closure,False)
        assert full>=best[0]>=0 and full>=without
        cases['closed' if closure else 'independent']={
            'optimal':str(full),'without_probe':str(without),'independent_probe_value':str(full-without),
            'best_simple':{'score':str(best[0]),'family':best[1],'threshold':list(best[2]),'unexplained_gap':str(full-best[0])}
        }
    visits={}
    changed=F(0); opposite_strict=F(0); close_strict_optional=F(0); root_example=None
    def traverse(a,k,got,n,mass):
        nonlocal changed, opposite_strict,close_strict_optional,root_example
        if terminal(a,got,n,True):return
        s=(a,k,got,n)
        visits[s]=visits.get(s,F(0))+mass
        qclose=action_values(s,True)
        qopen=action_values(s,False)
        b=max(qclose.values());bopen=max(qopen.values())
        chosen=next(x for x in ('work','explore','probe') if x in qclose and qclose[x]==b)
        open_choice=next(x for x in ('work','explore','probe') if x in qopen and qopen[x]==bopen)
        if chosen!=open_choice:
            changed+=mass
            strictC=b>max(v for x,v in qclose.items() if x!=chosen)
            strictO=bopen>max(v for x,v in qopen.items() if x!=open_choice)
            if strictC and strictO:
                opposite_strict+=mass
                if root_example is None:root_example={
                    'state':str(s),'probability_path_mass':str(mass),
                    'closure_best':chosen,'independent_best':open_choice,
                    'closed_values':{x:str(v) for x,v in qclose.items()},
                    'independent_values':{x:str(v) for x,v in qopen.items()}
                }
        if chosen!='work' and 'work' in qclose and b>qclose['work']:
            close_strict_optional+=mass
        for pr,aa,kk,gg,nn in move(a,k,got,n,chosen):
            traverse(aa,kk,gg,nn,mass*pr)
    traverse(0,K,False,actions,F(1))
    return {'K':K,'q':str(q),'bonus':bonus,'actions':actions,
            'cases':cases,'closure_loss':str(F(cases['independent']['optimal'])-F(cases['closed']['optimal'])),
            'changed_action_visits_on_closed_optimal_paths':str(changed),
            'strict_opposite_action_visits':str(opposite_strict),
            'strict_optional_above_work_visits':str(close_strict_optional),
            'example_strict_reversal':root_example,'reachable_states_on_closed_policy':len(visits)}

def main():
    cases=[study(4,F(1,4),2),study(3,F(1,4),2),study(4,F(1,2),2),
           study(4,F(1,4),1),study(4,F(1,4),3)]
    for r in cases:
        assert F(r['closure_loss'])>=0
        for v in r['cases'].values():
            assert F(v['optimal'])>=F(v['without_probe'])
            assert F(v['best_simple']['unexplained_gap'])>=0
    print(json.dumps({'study':'R109','evidence':'Exact Fraction, independent 4-action paper closure counterfactual; no official engine or human evidence','cases':cases},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
