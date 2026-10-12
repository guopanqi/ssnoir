#!/usr/bin/env python3
"""S2-D independent negative control: reveal-before-commit with NO main deadline.
Exact Fraction synthetic paper; not original R119, C#+Scheme, or human play.
A: search 0/1/2 to clue threshold 2, then a guaranteed die-costly 'secure'.
B: help 0/1/2 to 2, available only day 1 (first three of six actions).
C: follow clue 0/1/2 to 2, available at A progress 2 before A secured.
'Secure' closes unfinished C; reaching clue NEVER secures the letter.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import permutations,product
import json
G=((0,F(1,3)),(1,F(1,3)),(2,F(1,3)))

def model(B=F(3,2),C=F(2),side=True,gate=True,closure=True,urgent=True,
          report_paths=True):
 def acts(a,b,c,secured,n):
  if n==0:return ()
  x=[]
  if not secured:x += ["查信"] if a<2 else ["截信"]
  if b<2 and (not urgent or n>3):x+=["帮艾迪"]
  if side and c<2 and (not gate or a==2) and (not closure or not secured):x+=["追线索"]
  return tuple(x)
 def moves(a,b,c,secured,n,op):
  if op=="截信":return ((F(1),a,b,c,True,n-1),)
  return tuple((pr,min(2,a+g) if op=="查信" else a,
     min(2,b+g) if op=="帮艾迪" else b,
     min(2,c+g) if op=="追线索" else c,secured,n-1) for g,pr in G)
 def score(a,b,c,secured):return 2*F(secured)+B*F(b==2)+C*F(c==2)
 @lru_cache(None)
 def V(a,b,c,secured,n):
  choices=acts(a,b,c,secured,n)
  if not choices or n==0:return score(a,b,c,secured)
  return max(sum(p*V(aa,bb,cc,ss,nn) for p,aa,bb,cc,ss,nn
                 in moves(a,b,c,secured,n,op)) for op in choices)
 def qvalues(a,b,c,secured,n):
  return {op:sum(p*V(aa,bb,cc,ss,nn) for p,aa,bb,cc,ss,nn
                 in moves(a,b,c,secured,n,op)) for op in acts(a,b,c,secured,n)}
 def policy_value(choose):
  @lru_cache(None)
  def P(a,b,c,secured,n):
   opts=acts(a,b,c,secured,n)
   if not opts or n==0:return score(a,b,c,secured)
   op=choose(a,b,c,secured,n,opts)
   assert op in opts,(op,opts)
   return sum(p*P(aa,bb,cc,ss,nn) for p,aa,bb,cc,ss,nn in moves(a,b,c,secured,n,op))
  return P(0,0,0,False,6)
 free=V(0,0,0,False,6)
 if not report_paths:return free
 orders=[(policy_value(lambda a,b,c,s,n,ops,order=order:next(x for x in order if x in ops)),
         order) for order in permutations(("查信","截信","帮艾迪","追线索"))]
 best_fixed=max(orders)
 # Strong legal and public-only baseline: no dynamic programming values read.
 baselines=[]
 for trace_n,b_threshold,b_priority,late in product(range(1,5),range(3),(4,5,6),range(1,4)):
  def pick(a,b,c,s,n,opts,trace_n=trace_n,b_threshold=b_threshold,
           b_priority=b_priority,late=late):
   if "帮艾迪" in opts and b>=b_threshold and 3<n<=b_priority:return "帮艾迪"
   if "追线索" in opts and "截信" in opts:
    return "追线索" if n>=trace_n else "截信"
   if "追线索" in opts and "查信" in opts and n<=late:return "追线索"
   return next(x for x in ("查信","追线索","截信","帮艾迪") if x in opts)
  baselines.append((policy_value(pick),trace_n,b_threshold,b_priority,late))
 best_simple=max(baselines)
 first_m=F(0);strict={x:F(0) for x in ("追线索","截信","帮艾迪")};witness={}
 any_m=F(0);any_strict={x:F(0) for x in strict}
 def traverse(a,b,c,secured,n,mass,seen):
  nonlocal first_m,any_m
  qs=qvalues(a,b,c,secured,n)
  if not qs or n==0:return
  val=max(qs.values())
  chosen=next(x for x in ("帮艾迪","追线索","截信","查信") if x in qs and qs[x]==val)
  first=(a==2 and not secured and "追线索" in qs and "截信" in qs)
  if first:
   any_m+=mass
   is_strict=(len(qs)>1 and val>max(v for k,v in qs.items() if k!=chosen))
   if is_strict and chosen in strict:any_strict[chosen]+=mass
   if not seen:
    first_m+=mass
    if is_strict and chosen in strict:
     strict[chosen]+=mass
     if chosen not in witness:witness[chosen]={"public_state":[a,b,c,int(secured),n],
      "path_mass_of_one_history":str(mass),"action_values":{k:str(v) for k,v in qs.items()}}
    seen=True
  for p,aa,bb,cc,ss,nn in moves(a,b,c,secured,n,chosen):
   traverse(aa,bb,cc,ss,nn,mass*p,seen)
 traverse(0,0,0,False,6,F(1),False)
 assert free>=best_fixed[0] and free>=best_simple[0]
 assert first_m>0 and any(x>0 for x in strict.values()),strict\n # The third strict choice is expected only in the core B=3/2 case;\n # changing its reward can legitimately remove the first-day help priority.
 return {"B_reward":str(B),"C_reward":str(C),"optimal":str(free),
  "fixed_priority":{"value":str(best_fixed[0]),"order":best_fixed[1],
                    "gap":str(free-best_fixed[0])},
  "strong_public_baseline":{"value":str(best_simple[0]),"params":best_simple[1:],
                            "gap":str(free-best_simple[0])},
  "first_clue_response_probability":str(first_m),
  "first_strict_public_choice_probabilities":{k:str(v) for k,v in strict.items()},
  "expected_response_visits":str(any_m),
  "strict_response_expected_visits":{k:str(v) for k,v in any_strict.items()},
  "first_witnesses":witness}

def main():
 rows=[model(B=b,C=c) for b,c in ((F(3,2),F(2)),(F(2),F(2)),
                                    (F(3,2),F(3)),(F(1),F(2)))]
 base=rows[0]
 off=model(side=False,report_paths=False)
 ungated=model(gate=False,report_paths=False)
 no_closure=model(closure=False,report_paths=False)
 no_deadline=model(urgent=False,report_paths=False)
 assert F(base["optimal"])>=off
 assert ungated>=F(base["optimal"])
 assert no_closure>=F(base["optimal"])
 assert no_deadline>=F(base["optimal"])
 assert F(base["first_strict_public_choice_probabilities"]["追线索"])>0
 assert F(base["first_strict_public_choice_probabilities"]["截信"])>0
 assert F(base["first_strict_public_choice_probabilities"]["帮艾迪"])>0
 print(json.dumps({"batch":"S2-D-nonurgent-closure-negative-control","source_type":"Exact Fraction paper only",
  "rules":"Reveal at progress2; finishing main letter costs a NEW die and closes clue, not same-action reveal+close",
  "rows":rows,"core_ablations":{"remove_side_C":str(off),"open_C_immediately":str(ungated),
   "remove_C_closure":str(no_closure),"remove_B_day1_deadline":str(no_deadline),
   "C_independent_value":str(F(base["optimal"])-off),
   "C_closure_marginal":str(no_closure-F(base["optimal"])),
   "B_deadline_cost":str(no_deadline-F(base["optimal"]))},
  "warning":"Expected visits are not probability of a human choice; first clue probability and strict choices count only path's first revealed opportunity. C closure zero gain is a meaningful negative result."},
  ensure_ascii=False,indent=2))
if __name__=="__main__":main()
