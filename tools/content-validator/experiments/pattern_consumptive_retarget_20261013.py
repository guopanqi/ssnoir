#!/usr/bin/env python3
"""SEM04: consumptive purpose transformation, matched material-aware menu.

A makes one material/progress; B_TO_C requires and consumes one A progress
to secure C instead of advancing B. A>=2 pays 10, B>=2 pays 8, C pays
12 in HIGH and 1 in LOW; SIDE gives 1. Three actions, signal after turn 1.
Gated conversion must immediately follow A; token_menu requires material
but not adjacency. No human/engine play implied by this exhaustive model.
"""
from functools import lru_cache
from fractions import Fraction as Q
KINDS=('gated','token_menu','buff','plain')
T=3
INITIAL=(0,0,0,'-')
def legal(kind,state):
    a,b,c,last=state
    actions=['A','B','SIDE']
    if kind=='gated' and last=='A' and a>=1:
        actions.append('B_TO_C')
    if kind=='token_menu' and a>=1:
        actions.append('B_TO_C')
    return tuple(actions)
def step(kind,state,act):
    assert act in legal(kind,state)
    a,b,c,last=state
    if act=='A':return min(a+1,3),b,c,'A'
    if act=='B':return a,min(b+1+int(kind=='buff' and last=='A'),3),c,'B'
    if act=='B_TO_C':return a-1,b,1,'B'
    return a,b,c,'SIDE'
def terminal(state,high):
    a,b,c,_=state
    return Q(10*(a>=2)+8*(b>=2)+(12 if high else 1)*c)
def solver(kind):
    @lru_cache(None)
    def dp(turn,state,high):
        if turn==T:return terminal(state,high)
        return max(Q(x=='SIDE')+dp(turn+1,step(kind,state,x),high)
                   for x in legal(kind,state))
    first={}
    for x in legal(kind,INITIAL):
        state=step(kind,INITIAL,x)
        first[x]=Q(x=='SIDE')+(dp(1,state,True)+dp(1,state,False))/2
    return max(first.values()),first,dp
def fixed(kind):
    scores=[]
    def rec(turn,state,seq,side):
        if turn==T:
            scores.append((Q(side)+(terminal(state,True)+terminal(state,False))/2,tuple(seq)))
            return
        for x in legal(kind,state):
            rec(turn+1,step(kind,state,x),seq+[x],side+int(x=='SIDE'))
    rec(0,INITIAL,[],0)
    best=max(v for v,_ in scores)
    return best,[s for v,s in scores if v==best],len(scores)
def main():
    scores={}
    for kind in KINDS:
        root,first,dp=solver(kind)
        scores[kind]=root
        print('SEM04',kind,'adaptive',root,'root_first',first,'fixed',fixed(kind))
    assert scores=={'gated':Q(12),'token_menu':Q(12),
                    'buff':Q(18),'plain':Q(11)}
    assert fixed('gated')[0]==11
    assert solver('gated')[1]['A']==solver('gated')[1]['SIDE']==12
    # Distinct end outcomes for a plan visible to the player:
    # HIGH: convert one A piece into C, then SIDE => C payoff12 + SIDE1.
    # LOW: keep A material, produce the second A, then SIDE => A10 + SIDE1.
    assert step('gated',step('gated',INITIAL,'A'),'B_TO_C')[0]==0
    assert scores['token_menu']>=scores['gated']
    print('PASS material conservation and same-cost always-convert counterfactual')
if __name__=='__main__':main()
