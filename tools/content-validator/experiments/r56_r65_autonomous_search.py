#!/usr/bin/env python3
"""R56–R65: ten bounded, independent abstract-mechanic hypotheses.

Exact four-die FateStrip dynamic programs with observable states.
Each baseline can inspect the complete initial hand, choose a frozen action
script, and then choose the best remaining die AFTER every prior result.
A separate priority family can choose actions reactively; strongest benchmark
is selected independently for each initial hand. No native gameplay or humans.
"""
import json,sys,time
from itertools import combinations_with_replacement,product
from math import factorial
from functools import lru_cache
from fractions import Fraction as F

N=4
SPECS={
"R56":("接力奖励","relay","完成一项后，下手转向另一项获得额外进度"),
"R57":("进展引发维护债","liability","推进跨阈值创造待修问题；选择修理还是推进"),
"R58":("付费获知真实优先级","reveal","牺牲一次行动获知应优先完成的目标"),
"R59":("前置解锁","gate","完成前置进度才开放高收益目标"),
"R60":("临时储备","investment","先行动储存下次一次性推进奖励"),
"R61":("异线进度衰减","decay","推进一边会损失另一边的部分进度"),
"R62":("失败防护","shield","先准备防护抵消后续失败带来的损失"),
"R63":("机会反馈","window","可见的下一手优势目标随当前结果改变"),
"R64":("精确终点","exact","一次行动超过终点会使该目标归零"),
"R65":("止损入袋","bank","选择收下已得收益或冒险继续积累"),
}
def odds(die):
 n=die+1
 if n<=1:return (F(3,6),F(3,6),F(0))
 bad,mid={2:(2,3),3:(1,3),4:(1,2),5:(0,2),6:(0,1)}.get(n,(0,0))
 return F(bad,6),F(mid,6),F(6-bad-mid,6)
def hand_weight(hand):
 w=F(factorial(N),6**N)
 for die in set(hand):w/=factorial(hand.count(die))
 return w
LIMITS={"relay":(2,3),"liability":(2,3),"reveal":(3,3),
"gate":(2,3),"investment":(2,4),"decay":(2,3),"shield":(2,3),
"window":(2,3),"exact":(3,3),"bank":(99,99)}
def actions(mode,s):
 a,b,x,y=s
 if mode=="bank":return (0,1) if a>0 else (0,)
 if mode in ("reveal","liability","investment","shield"):
  possibilities=[0,1,2]
  if mode!="reveal":
   ma,mb=LIMITS[mode]
   if a>=ma:possibilities.remove(0)
   if b>=mb:possibilities.remove(1)
  return tuple(possibilities)
 ma,mb=LIMITS[mode]
 return tuple(g for g,val in enumerate((a,b)) if val<(ma,mb)[g]) or (0,)
def score(mode,s):
 a,b,x,y=s
 if mode=="bank":return F(b)
 if mode=="reveal":
  if x==0:return F(3,2)*(int(a>=3)+int(b>=3))
  return 3*F((a>=3 and x==1) or (b>=3 and x==2))
 ma,mb=LIMITS[mode]
 val=F(a>=ma)+2*F(b>=mb)
 if mode=="liability":val-=F(2,3)*x
 if mode=="shield":val-=F(2,3)*y
 return val
def done(mode,s):
 a,b,x,y=s
 if mode=="bank":return y==1
 if mode=="liability":return False
 ma,mb=LIMITS[mode]
 return a>=ma and b>=mb
def next_states(mode,s,action,gain):
 a,b,x,y=s
 if mode=="bank":
  if action==1:return [(F(1),(0,a,0,1))]
  return [(F(1),(0 if gain==0 else min(9,a+gain),b,0,0))]
 if mode=="reveal":
  if action==2:
   return [(F(1),s)] if x else [(F(1,2),(a,b,1,y)),(F(1,2),(a,b,2,y))]
  if action==0:a=min(3,a+gain)
  if action==1:b=min(3,b+gain)
 elif mode=="liability":
  if action==2:return [(F(1),(a,b,max(0,x-gain),y))]
  old_a,old_b=a,b
  if action==0:a=min(2,a+gain)
  if action==1:b=min(3,b+gain)
  if action==0 and a>=2 and old_a<2 and not y&1:x+=1;y|=1
  if action==1 and b>=2 and old_b<2 and not y&2:x+=1;y|=2
 elif mode=="relay":
  ma,mb=LIMITS[mode]
  extra=int(x>0 and x-1!=action)
  old=(a,b)[action]
  if action==0:a=min(ma,a+gain+extra)
  else:b=min(mb,b+gain+extra)
  x=action+1 if old<(ma,mb)[action] and (a,b)[action]==(ma,mb)[action] else 0
 elif mode=="gate":
  if action==0:a=min(2,a+gain)
  if action==1 and a>=2:b=min(3,b+gain)
 elif mode=="investment":
  if action==2:return [(F(1),(a,b,min(2,x+gain),y))]
  if action==0:a=min(2,a+gain+x)
  else:b=min(4,b+gain+x)
  x=0
 elif mode=="decay":
  if action==0:a=min(2,a+gain);b=max(0,b-1)
  else:b=min(3,b+gain);a=max(0,a-1)
 elif mode=="shield":
  if action==2:return [(F(1),(a,b,int(gain>0),y))]
  if gain==0:
   if x:x=0
   else:y+=1
  if action==0:a=min(2,a+gain)
  else:b=min(3,b+gain)
 elif mode=="window":
  bonus=int(action==x)
  if action==0:a=min(2,a+gain+bonus)
  else:b=min(3,b+gain+bonus)
  x=1-action if gain==2 else action
 elif mode=="exact":
  if action==0:a=0 if a+gain>3 else a+gain
  else:b=0 if b+gain>3 else b+gain
 else:raise ValueError(mode)
 return [(F(1),(a,b,x,y))]
def heuristic(mode,k,s,turn):
 a,b,x,y=s;allowed=actions(mode,s)
 if mode=="bank":return (1 if a>=k or turn==N-1 else 0) if 1 in allowed else 0
 if mode=="liability" and k>=2 and x>0 and 2 in allowed:return 2
 if mode=="shield" and k==3 and x==0 and 2 in allowed and turn<2:return 2
 if mode=="reveal" and k>=2:
  if x==0 and 2 in allowed:return 2
  if x and x-1 in allowed:return x-1
 if mode=="investment" and k>=2 and x==0 and 2 in allowed and turn<2:return 2
 first=k%2
 return first if first in allowed else 1-first if 1-first in allowed else allowed[0]
def evaluate(code,mode):
 @lru_cache(None)
 def free(s,hand):
  if not hand or done(mode,s):return score(mode,s)
  return max(outcome(s,hand,a,i,None) for a in actions(mode,s) for i in range(len(hand)))
 @lru_cache(None)
 def fixed(s,hand,script):
  if not hand or done(mode,s):return score(mode,s)
  a=script[0]
  if a not in actions(mode,s):a=actions(mode,s)[0]
  return max(outcome(s,hand,a,i,script) for i in range(len(hand)))
 @lru_cache(None)
 def outcome(s,hand,a,i,script):
  rest=hand[:i]+hand[i+1:]
  return sum(prob*branch*(free(ns,rest) if script is None else fixed(ns,rest,script[1:]))
             for gain,prob in enumerate(odds(hand[i])) if prob
             for branch,ns in next_states(mode,s,a,gain))
 @lru_cache(None)
 def priority(s,hand,k):
  if not hand or done(mode,s):return score(mode,s)
  a=heuristic(mode,k,s,N-len(hand))
  return max(sum(prob*branch*priority(ns,hand[:i]+hand[i+1:],k)
                 for gain,prob in enumerate(odds(d)) if prob
                 for branch,ns in next_states(mode,s,a,gain))
             for i,d in enumerate(hand))
 num_actions=3 if mode in ("liability","reveal","investment","shield") else 2
 scripts=tuple(product(range(num_actions),repeat=N))
 free_mean=script_mean=priority_mean=strong_mean=F(0)
 strict=0
 for hand in combinations_with_replacement(range(1,7),N):
  w=hand_weight(hand);s=(0,0,0,0)
  best=free(s,hand)
  openloop=max(fixed(s,hand,sc) for sc in scripts)
  rule=max(priority(s,hand,k) for k in range(6 if mode=="bank" else 4))
  strong=max(openloop,rule)
  assert best>=strong,(code,hand,best,strong)
  free_mean+=w*best;script_mean+=w*openloop
  priority_mean+=w*rule;strong_mean+=w*strong
  strict+=best>strong
 assert sum(hand_weight(h) for h in combinations_with_replacement(range(1,7),N))==1
 return dict(id=code,name=SPECS[code][0],hypothesis=SPECS[code][2],
             free=round(float(free_mean),6),openloop=round(float(script_mean),6),
             reactive_simple=round(float(priority_mean),6),
             strong=round(float(strong_mean),6),
             gain=round(float(free_mean-strong_mean),6),strict_hands=strict,
             states=free.cache_info().currsize)
def main():
 result=[]
 for code,(title,mode,_) in SPECS.items():
  row=evaluate(code,mode)
  result.append(row)
  print(code,row["free"],row["strong"],row["gain"],row["strict_hands"],flush=True)
 data=dict(schema="ssnoir.autonomous-mechanism-search/v1",horizon=4,skill=1,
           probability="prepared value=d6+1; bad/neutral/good progress0/1/2",
           baseline="maximum separately on each initial hand: frozen action script with adaptive dice OR tested reactive priority heuristic",
           warning="Purely exact bounded maths, not Unity/C# evidence nor human play",
           results=result)
 if len(sys.argv)>1:
  from pathlib import Path
  Path(sys.argv[1]).write_text(json.dumps(data,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
 else:print(json.dumps(data,ensure_ascii=False,indent=2))
if __name__=="__main__":main()
