#!/usr/bin/env python3
"""ATN01/02: independent goals vs public time-window interference.
State is progress A/B, t in 0..4; at t2 block A or B for final 2 hands
with 1/4 chance each, or no block with 1/2. Two units complete each goal.
Exact Fraction probabilities, exhaustive 3^4 unconditional plans, all
3^2 preparation plans and optimal public contingent continuations.
Evidence is feasibility/strategy, NOT human experience.
"""
from itertools import product
from fractions import Fraction as Q
from functools import lru_cache
from collections import defaultdict
ACT=('A','B','SIDE')
MODES=('none','blockA','blockB')
PROBS=(Q(1,2),Q(1,4),Q(1,4))
T=4

def transition(state,action):
    a,b,s=state
    return min(2,a+(action=='A')),min(2,b+(action=='B')),s+(action=='SIDE')

def legal(action,mode):
    return not (action=='A' and mode=='blockA' or action=='B' and mode=='blockB')

def terminal(state):
    a,b,s=state
    return Q(a==2),Q(b==2),Q(s)

def eval_fixed(plan,blocked=True):
    total=[Q(0)]*3
    for m,p in zip(MODES,PROBS):
        state=(0,0,0)
        for t,act in enumerate(plan):
            # fixed prescription with basic legal fallback, not future-aware
            if blocked and t>=2 and not legal(act,m):act='SIDE'
            state=transition(state,act)
        for i,v in enumerate(terminal(state)):total[i]+=p*v
    return tuple(total)

def pareto(vectors):
    distinct=sorted(set(vectors))
    return [v for v in distinct if not any(u!=v and all(x>=y for x,y in zip(u,v)) for u in distinct)]

def dp(wA,wB,wS=Q(0),blocked=True):
    @lru_cache(None)
    def continue_at(t,a,b,s,mode):
        if t==4:return wA*(a==2)+wB*(b==2)+wS*s,()
        choices=[]
        for act in ACT:
            if blocked and t>=2 and not legal(act,mode):continue
            state=transition((a,b,s),act)
            v,_=continue_at(t+1,*state,mode)
            choices.append((v,act))
        v,act=max(choices,key=lambda x:(x[0],-ACT.index(x[1])))
        return v,(act,)
    @lru_cache(None)
    def solve_pre(t,a,b,s):
        if t==2:
            return sum(p*continue_at(2,a,b,s,m)[0] for m,p in zip(MODES,PROBS)),()
        choices=[]
        for act in ACT:
            st=transition((a,b,s),act)
            v,_=solve_pre(t+1,*st)
            choices.append((v,act))
        return max(choices,key=lambda x:(x[0],-ACT.index(x[1])))
    return solve_pre(0,0,0,0)[0]

def full_policy(pre,mode):
    state=(0,0,0)
    for action in pre:state=transition(state,action)
    choices=[]
    for acts in product(ACT,repeat=2):
        if not all(legal(x,mode) for x in acts):continue
        ss=state
        for a in acts:ss=transition(ss,a)
        choices.append((acts,terminal(ss)))
    return choices

def all_contingent_outcomes():
    vectors=[]; assignments=0
    for pre in product(ACT,repeat=2):
        continuations=[full_policy(pre,m) for m in MODES]
        for choices in product(*continuations):
            assignments+=1
            vec=tuple(sum(p*entry[1][i] for entry,p in zip(choices,PROBS)) for i in range(3))
            vectors.append(vec)
    return vectors,assignments

def policy_outcomes_for(pre,selection):
    return tuple(sum(p*selection[m][i] for m,p in zip(MODES,PROBS)) for i in range(3))

def main():
    groups=defaultdict(set)
    for plan in product(ACT,repeat=4):
        groups[tuple(plan.count(x) for x in ACT)].add(eval_fixed(plan,blocked=False))
    assert len(groups)==15 and all(len(v)==1 for v in groups.values())
    baseline=pareto(v.pop() for v in groups.values())
    assert (Q(1),Q(1),Q(0)) in baseline and len(baseline)==4
    vectors,count=all_contingent_outcomes()
    front=pareto(vectors)
    assert count==1296 and len(set(vectors))==157 and len(front)==20
    print('ATN01 no-interference action-count-classes',len(groups),'frontier',baseline)
    print('ATN02 contingent_policy_count',count,'unique_vectors',len(set(vectors)),'pareto_count',len(front))
    print('ATN02 full-frontier',front)
    for weights in [(1,1,0),(10,8,1),(8,10,1),(10,8,0)]:
        val=dp(*(Q(x) for x in weights))
        best_fixed=max(sum(Q(w)*v for w,v in zip(weights,eval_fixed(p))) for p in product(ACT,repeat=4))
        optimal=max(sum(Q(w)*v for w,v in zip(weights,vv)) for vv in vectors)
        assert val==optimal and val==best_fixed
        print('ATN02 weight',weights,'DP',val,'best_fixed',best_fixed)
    for pre in ('AA','AB','BA','BB'):
        cur=[]
        for picks in product(*(full_policy(tuple(pre),m) for m in MODES)):
            cur.append(tuple(sum(p*v[1][i] for p,v in zip(PROBS,picks)) for i in range(3)))
        print('ATN02 first-two',pre,'pareto_count',len(pareto(cur)))
    va=policy_outcomes_for(('A','A'),{'none':(Q(1),Q(1),Q(0)), 'blockA':(Q(1),Q(1),Q(0)), 'blockB':(Q(1),Q(0),Q(2))})
    vb=policy_outcomes_for(('B','B'),{'none':(Q(1),Q(1),Q(0)), 'blockA':(Q(0),Q(1),Q(2)), 'blockB':(Q(1),Q(1),Q(0))})
    assert va==(Q(1),Q(3,4),Q(1,2)) and vb==(Q(3,4),Q(1),Q(1,2))
    print('PASS exact contingent outcomes, no-event commutativity and dominance controls')

if __name__=='__main__':main()
