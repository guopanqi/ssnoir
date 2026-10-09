#!/usr/bin/env python3
"""R67–R75, nine bounded hypothesis tests including one structural audit.

R67 is a causal-equivalence/provenance audit, not a new mathematical reward.
R68–R75 each have a distinct compact transition system with FateStrip skill1,
four d6 rolls, exact rational optimal policy and strong per-hand baselines:
frozen action sequences (remaining dice still chosen adaptively), plus a
publicly described family of state-reactive human-simple heuristics.
Outcome values are NOT cross-model comparable. No human or C# evidence.
"""
from __future__ import annotations
import json
from functools import lru_cache
from itertools import product,combinations_with_replacement
from fractions import Fraction as F
from r56_r65_autonomous_search import odds,hand_weight

N=4
MODES={
"R68":("付费验证","verify","先用一个行动提高后续判定可信度"),
"R69":("可撤销承诺","commit","继续当前路线有加成，改签会损失未完成进度"),
"R70":("公开机会时窗","window","前半段甲占优、后半段乙占优"),
"R71":("已做工作可搬移","transfer","花一次行动把甲进度改派乙，代价是损失甲进度"),
"R72":("推进与事故清理","hazard","冲刺推进同时积累坏结果，清理能化解债务"),
"R73":("失败外溢","spill","在一条路线失败会影响另一条的未完成进度"),
"R74":("可选兑现时点","cash","滚动积累收益，但失败清空，何时停止兑现"),
"R75":("一次确定行动","guarantee","每局一次小收益保底动作与高波动推进竞用骰子"),
}
LIMITS={"R68":(4,0),"R69":(2,3),"R70":(2,3),
        "R71":(2,3),"R72":(4,0),"R73":(2,3),
        "R74":(8,0),"R75":(4,0)}

def actions(mode,s,t):
 a,b,x=s
 if mode=="R68":return (0,1) if not b and t<3 else (0,)
 if mode=="R71":return (0,1,2) if a>=1 else (0,1)
 if mode=="R72":return (0,1) if b>0 else (0,)
 if mode=="R74":return (0,1) if a>0 else (0,)
 if mode=="R75":return (0,1) if not b else (0,)
 return (0,1)
def finished(mode,s):
 a,b,x=s
 if mode=="R74":return x==1
 if mode=="R68":return a>=4
 if mode=="R72":return a>=4 and b==0
 if mode=="R75":return a>=4
 if mode in ("R69","R70","R71","R73"):return a>=2 and b>=3
 return False
def value(mode,s):
 a,b,x=s
 if mode=="R68":return F(2*(a>=4))
 if mode=="R72":return F(2*(a>=4))-F(b,3)
 if mode=="R74":return F(b) if x==1 else F(0)
 if mode=="R75":return F(2*(a>=4))
 return F(a>=2)+F(2*(b>=3))
def apply(mode,s,action,gain,t):
 a,b,x=s
 if mode=="R68":
  if action==1:b=int(gain>0)
  else:a=min(4,a+gain+(int(b>0 and gain>0)))
 elif mode=="R69":
  if x and x!=action+1:
   if x==1 and a<2:a=max(0,a-1)
   if x==2 and b<3:b=max(0,b-1)
  extra=int(x==action+1 and gain>0)
  if action==0:a=min(2,a+gain+extra)
  else:b=min(3,b+gain+extra)
  x=action+1
 elif mode=="R70":
  if action==0:a=min(2,a+gain+int(t<2 and gain>0))
  else:b=min(3,b+gain+int(t>=2 and gain>0))
 elif mode=="R71":
  if action==0:a=min(2,a+gain)
  elif action==1:b=min(3,b+gain)
  else:a=max(0,a-1);b=min(3,b+1)
 elif mode=="R72":
  if action==0:
   a=min(4,a+gain*2)
   if gain==0:b=min(3,b+1)
  else:b=max(0,b-gain)
 elif mode=="R73":
  if action==0:
   a=min(2,a+gain)
   if gain==0 and b<3:b=max(0,b-1)
  else:
   b=min(3,b+gain)
   if gain==0 and a<2:a=max(0,a-1)
 elif mode=="R74":
  if action==1:b=a;x=1
  else:a=0 if gain==0 else min(8,a+gain)
 elif mode=="R75":
  if action==1:a=min(4,a+1);b=1
  else:a=min(4,a+gain)
 else:raise ValueError(mode)
 return (a,b,x)
def heuristic(mode,k,s,t):
 a,b,x=s;possible=actions(mode,s,t)
 if mode=="R68" and 1 in possible:
  if k==2:return 1 if a<=2 else 0
  if k==3:return 1 if t==0 else 0
 if mode=="R71" and 2 in possible:
  if k==2:return 2 if b<=1 else 1
  if k==3:return 2 if a>=2 and b<3 else 1
 if mode=="R72" and 1 in possible:
  if k>=2:return 1 if b>=k-1 else 0
 if mode=="R74" and 1 in possible:
  if k==0:return 1 if a>=2 or t==N-1 else 0
  if k==1:return 1 if a>=3 or t==N-1 else 0
  if k==2:return 1 if a>=4 or t==N-1 else 0
  if k==3:return 1 if t==N-1 else 0
 if mode=="R75" and 1 in possible:
  if k==2:return 1 if t==N-1 and a<4 else 0
  if k==3:return 1 if a>=3 else 0
 if k==0:return 0
 if k==1:return 1 if 1 in possible else 0
 if k==2:return (0 if a<2 else 1) if (0 if a<2 else 1) in possible else possible[0]
 return (1 if b<3 else 0) if (1 if b<3 else 0) in possible else possible[0]

def test(mode):
 @lru_cache(None)
 def free(s,hand):
  if not hand or finished(mode,s):return value(mode,s)
  return max(q(s,hand,g,i,lambda ns,r:free(ns,r))
    for g in actions(mode,s,N-len(hand)) for i in range(len(hand)))
 def q(s,hand,g,i,fn):
  rest=hand[:i]+hand[i+1:]
  return sum(p*fn(apply(mode,s,g,v,N-len(hand)),rest)
             for v,p in enumerate(odds(hand[i])) if p)
 @lru_cache(None)
 def fixed(s,hand,script):
  if not hand or finished(mode,s):return value(mode,s)
  valid=actions(mode,s,N-len(hand))
  g=script[0] if script[0] in valid else valid[0]
  return max(q(s,hand,g,i,lambda ns,r:fixed(ns,r,script[1:]))
                 for i in range(len(hand)))
 @lru_cache(None)
 def reactive(s,hand,k):
  if not hand or finished(mode,s):return value(mode,s)
  g=heuristic(mode,k,s,N-len(hand))
  return max(q(s,hand,g,i,lambda ns,r:reactive(ns,r,k))
                 for i in range(len(hand)))
 num=3 if mode=="R71" else 2
 scripts=tuple(product(range(num),repeat=N))
 opt_total=fixed_total=heuristic_total=strong_total=F(0);strict_roots=0
 for hand in combinations_with_replacement(range(1,7),N):
  w=hand_weight(hand)
  best=free((0,0,0),hand)
  frozen=max(fixed((0,0,0),hand,sc) for sc in scripts)
  intuitive=max(reactive((0,0,0),hand,k) for k in range(4))
  strong=max(frozen,intuitive)
  assert best>=strong,(mode,hand,best,strong)
  opt_total+=w*best;fixed_total+=w*frozen
  heuristic_total+=w*intuitive;strong_total+=w*strong
  strict_roots+=best>strong
 return {"id":mode,"name":MODES[mode][0],"hypothesis":MODES[mode][2],
         "adaptive":round(float(opt_total),9),
         "frozen_script_select_remaining_dice":round(float(fixed_total),9),
         "intuitive_policy_select_remaining_dice":round(float(heuristic_total),9),
         "best_per_root_baseline":round(float(strong_total),9),
         "gap":round(float(opt_total-strong_total),9),
         "strict_root_hands":strict_roots,"free_states":free.cache_info().currsize}

def r67():
 """Equivalence-class audit: identical trigger cause, different consequence timing."""
 def milestone(prev,after,debt):return debt+int(prev<3<=after)
 def deferred_pressure(unrepaired,round_ended,pressure):
  return pressure+int(bool(unrepaired) and round_ended)
 # R57 and R37 share the trigger relation but are NOT state-transition equal:
 # R57 penalizes residual debt at the terminal time; R37 ticks pressure only
 # at specified round boundaries and retains pressure after repair.
 assert milestone(2,3,0)==1
 assert deferred_pressure(1,False,0)==0
 assert deferred_pressure(1,True,0)==1
 assert deferred_pressure(0,True,1)==1
 return {"id":"R67","relation":"crossing a public progress threshold creates a maintenance obligation",
  "abstract_equivalence":"R57 and R37 share the progress-threshold -> extra obligation causal pattern",
  "not_identical":"R57 scores unsettled debt immediately at terminal; R37 adds persistent pressure on round endings and contains repair/health/time mechanics.",
  "witness":{"progress_crosses_3":True,"unrepaired":1,
             "R57_outstanding_debt_immediate":1,
             "R37_pressure_before_round_end":0,
             "R37_pressure_after_round_end":1},
  "verdict":"Do not promote R57 as a distinct pattern. Treat as a simplified variant of R37."}

def main():
 rows=[r67()]+[test(id) for id in MODES]
 out={"schema":"ssnoir.autonomous-abstract-tests/v2",
  "scope":"R67 structural audit; R68–R75 eight exact four-die paper models; not native, not human",
  "hand_mass":"126 unordered roots weighed by permutations / 6^4; exact Fraction",
  "baseline":"Each root chooses strongest among all fixed 4-action scripts and four transparent reactive heuristics; both can choose each remaining die adaptively",
  "results":rows}
 import sys
 data=json.dumps(out,ensure_ascii=False,indent=2)+"\n"
 if len(sys.argv)>1:
  from pathlib import Path
  Path(sys.argv[1]).write_text(data,encoding="utf8")
 else:print(data)
if __name__=="__main__":main()
