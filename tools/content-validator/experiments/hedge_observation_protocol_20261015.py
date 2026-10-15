"""HEDGE-OBSERVE02: Independent paper-trial rule executor and validity tests."""
from fractions import Fraction as F
from itertools import product
class Session:
 def __init__(self,early,parallel,event):
  self.early,self.parallel,self.event=early,parallel,event
  self.t=0;self.live={};self.work={};self.seen=set();self.done=set()
  self.side=0;self.loss=0;self.history=[]
 def options(self):
  if self.t==5:return []
  xs=['SIDE']
  for k in 'AB':
   if k in self.live:xs+=['WORK_'+k,'DROP_'+k]
   elif k not in self.seen and (not self.early or k!='A' or self.t==0) and (self.parallel or not self.live):xs+=['OPEN_'+k]
  return xs
 def act(self,cmd):
  if cmd not in self.options():raise ValueError('Illegal action: '+cmd)
  typ,*arg=cmd.split('_'); self.t+=1; reveal=None
  if typ=='SIDE':self.side+=1
  else:
   k=arg[0]
   if typ=='OPEN':
    self.seen.add(k);self.live[k]=self.t;self.work[k]=0;reveal=self.event[k]
   elif typ=='DROP':self.live.pop(k);self.work.pop(k)
   elif typ=='WORK':
    self.work[k]+=1
    if self.work[k]==2:
     self.done.add(k);self.live.pop(k);self.work.pop(k)
  expired=[]
  for k,start in list(self.live.items()):
   if self.t-start>=3:
    self.loss+=4;expired.append(k);self.live.pop(k);self.work.pop(k)
  self.history.append((cmd,reveal,expired))
 def result(self):
  assert self.t==5
  payoff=sum({'A':{'H':14,'L':5},'B':{'H':14,'L':0}}[k][self.event[k]] for k in self.done)+self.side-self.loss-4*len(self.live)
  return payoff,'A' in self.done,'B' in self.done
def run(early,parallel,event,actions):
 s=Session(early,parallel,event)
 for x in actions.split():s.act(x)
 return s.result()
def validate():
 target={'parallel':(F(51,4),F(3,4),F(1,4)),
         'serial':(F(47,4),F(1,2),F(1,4)),
         'reopen':(F(51,4),F(1,2),F(1,2))}
 for group,expected in target.items():
  rows=[]
  for a,b in product('HL',repeat=2):
   if group=='parallel':
    route='OPEN_A WORK_A WORK_A SIDE SIDE' if a=='H' else ('OPEN_A OPEN_B DROP_A WORK_B WORK_B' if b=='H' else 'OPEN_A OPEN_B WORK_A WORK_A DROP_B')
   elif group=='serial':
    route='OPEN_A WORK_A WORK_A SIDE SIDE' if a=='H' else ('OPEN_A DROP_A OPEN_B WORK_B WORK_B' if b=='H' else 'OPEN_A DROP_A OPEN_B DROP_B SIDE')
   else:route='OPEN_B WORK_B WORK_B SIDE SIDE' if b=='H' else 'OPEN_B DROP_B OPEN_A WORK_A WORK_A'
   rows.append(run(group!='reopen',group=='parallel',{'A':a,'B':b},route))
  observed=tuple(F(sum(x[i] for x in rows),4) for i in range(3))
  assert observed==expected,(group,observed)
 assert run(True,True,{'A':'L','B':'L'},'OPEN_A OPEN_B WORK_A WORK_A DROP_B')[0]==5
 assert run(True,False,{'A':'L','B':'L'},'OPEN_A DROP_A OPEN_B DROP_B SIDE')[0]==1
 try:run(True,False,{'A':'L','B':'L'},'OPEN_A OPEN_B SIDE SIDE SIDE')
 except ValueError:pass
 else:raise AssertionError('Illegal overlap was accepted')
 for early,parallel in product((False,True),repeat=2):
  s=Session(early,parallel,{'A':'L','B':'L'})
  assert 'OPEN_A' in s.options();s.act('OPEN_B')
  assert ('OPEN_A' in s.options())==(not early and parallel)
 print('PASS: 4 protocol variants, 12 representative trajectories, legality and counterfactual')
if __name__=='__main__':validate()
