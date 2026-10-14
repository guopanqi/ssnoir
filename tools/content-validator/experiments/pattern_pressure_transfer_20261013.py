#!/usr/bin/env python3
"""PT01: two ongoing goals; WORK creates self-pressure; MOVE transfers
an audit threat rather than deleting it. Fraction backward induction.
Four turns from A=B=1/2, no storyteller, no players. Strong same-cost ERASE
control and 14 transparent public policies prevent novelty inflation.
"""
from fractions import Fraction as F
from functools import lru_cache

START=(0,1,1,0,0) # turn, A progress, B progress, heat A, heat B
REWARD_A,REWARD_B=F(12),F(9)

class Game:
    def __init__(self,audit=F(1,2),redirect=True,erase=False,days=4):
        self.audit,self.redirect,self.erase,self.days=F(audit),redirect,erase,days
        assert F(0)<=self.audit<=F(1)
    def menu(self,s):
        t,a,b,ha,hb=s
        if t==self.days:return ()
        m=['SIDE','WORK_A','WORK_B']
        if self.redirect and ha:m.append('MOVE_A_TO_B')
        if self.redirect and hb:m.append('MOVE_B_TO_A')
        if self.erase and ha:m.append('ERASE_A')
        if self.erase and hb:m.append('ERASE_B')
        return tuple(m)
    def step(self,s,action):
        assert action in self.menu(s),(s,action)
        t,a,b,ha,hb=s
        gain=F(0)
        if action=='SIDE':gain=F(1)
        elif action=='WORK_A':a=min(2,a+1);ha=min(2,ha+1)
        elif action=='WORK_B':b=min(2,b+1);hb=min(2,hb+1)
        elif action=='MOVE_A_TO_B':ha-=1;hb=min(2,hb+1)
        elif action=='MOVE_B_TO_A':hb-=1;ha=min(2,ha+1)
        elif action=='ERASE_A':ha-=1
        elif action=='ERASE_B':hb-=1
        return gain,(
            (self.audit,(t+1,max(0,a-int(ha>0)),b,max(0,ha-1),hb)),
            (1-self.audit,(t+1,a,max(0,b-int(hb>0)),ha,max(0,hb-1))),
        )
    @lru_cache(None)
    def optimal(self,s=START):
        if s[0]==self.days:return F(REWARD_A*int(s[1]==2)+REWARD_B*int(s[2]==2)),None
        vals=[]
        for action in self.menu(s):
            now,trans=self.step(s,action)
            v=now+sum(prob*self.optimal(nxt)[0] for prob,nxt in trans)
            vals.append((v,action))
        return max(vals,key=lambda pair:pair[0])
    def evaluate(self,policy):
        @lru_cache(None)
        def visit(s):
            if s[0]==self.days:return REWARD_A*int(s[1]==2)+REWARD_B*int(s[2]==2)
            action=policy(s)
            now,trans=self.step(s,action)
            return now+sum(prob*visit(nxt) for prob,nxt in trans)
        return visit(START)

def short_rule(order,mode):
    def choose(s):
        _,a,b,ha,hb=s
        if mode=='A' and ha:return 'MOVE_A_TO_B'
        if mode=='B' and hb:return 'MOVE_B_TO_A'
        if mode=='any':
            if ha:return 'MOVE_A_TO_B'
            if hb:return 'MOVE_B_TO_A'
        if mode=='progress':
            if ha and a:return 'MOVE_A_TO_B'
            if hb and b:return 'MOVE_B_TO_A'
        if mode=='complete':
            if ha and a==2:return 'MOVE_A_TO_B'
            if hb and b==2:return 'MOVE_B_TO_A'
        if mode=='hotter':
            if ha>hb:return 'MOVE_A_TO_B'
            if hb>ha:return 'MOVE_B_TO_A'
        for key in order:
            if key=='A' and a<2:return 'WORK_A'
            if key=='B' and b<2:return 'WORK_B'
        return 'SIDE'
    return choose

def audit():
    expected={
        F(1,4):(F(203,16),F(3613,256),F(981,64)),
        F(1,2):(F(83,8),F(111,8),F(63,4)),
        F(3,4):(F(11),F(835,64),F(219,16)),
    }
    for chance,(empty,move,clean) in expected.items():
        none=Game(chance,False,False)
        reroute=Game(chance,True,False)
        erase=Game(chance,False,True)
        both=Game(chance,True,True)
        assert (none.optimal()[0],reroute.optimal()[0],erase.optimal()[0])==(empty,move,clean)
        assert empty<move<clean
        assert both.optimal()[0]==clean,'redirect must not beat same-cost pressure deletion'
        readable={(order,kind):reroute.evaluate(short_rule(order,kind))
            for order in ('AB','BA')
            for kind in ('never','A','B','any','progress','complete','hotter')}
        best=max(readable.items(),key=lambda item:item[1])
        assert best[1]<=move
        print('PT01',str(chance),'no redirect',str(empty),'move',str(move),
              'erase',str(clean),'best-14-script',best[0],str(best[1]),
              'oracle gap',str(move-best[1]))
        if chance==F(1,2):
            assert best[1]==F(109,8)
            assert reroute.optimal()[1]=='WORK_A'
            initial=reroute.step(START,'WORK_A')[1]
            stateA=(1,1,1,0,0)
            stateB=(1,2,1,1,0)
            assert set(nxt for _,nxt in initial)=={stateA,stateB}
            assert reroute.optimal(stateA)[1]=='WORK_A'
            assert reroute.optimal(stateB)[1]=='MOVE_A_TO_B'
            print('PT01 reachable reaction: audit A -> WORK_A, audit B -> MOVE_A_TO_B')
    print('PASS PT01 complete entry, state response, same-cost clean, 14 rule comparisons')

if __name__=='__main__':audit()
