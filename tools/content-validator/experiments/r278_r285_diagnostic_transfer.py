#!/usr/bin/env python3
"""R278-R285: structural transfer and falsification of active diagnosis.
Every model has the same four secret states, three binary questions, two
answers, and one optimal terminal guess. No extra complexity as a crutch.
"""
from fractions import Fraction as F
from itertools import product
import json

H=range(4)
PRIOR=(F(1,4),)*4
SETS={
 "branch_required_112":({0},{2},{0,1}),
 "branch_required_123":({0},{0,1},{0,1,2}),
 "fixed_suffices_222":({0,1},{0,2},{0,3}),
 "fixed_suffices_nested":({0},{0,1},{0,2}),
 "no_complete_identification_111":({0},{1},{2}),
}
def joint(h,qs,noise,prior=PRIOR):
 w=prior[h]
 for test,answer in qs:
  truth=int(h in test)
  w*=1-noise if truth==answer else noise
 return w

def execute(tests,plan,noise=F(0),prior=PRIOR):
 # Independent complete forward enumeration of all response leaves.
 first,second_false,second_true=plan
 return sum(max(joint(h,((tests[first],a),(tests[second_true if a else second_false],b)),noise,prior)
                for h in H) for a,b in product((0,1),repeat=2))

def exhaustive(tests,noise=F(0),prior=PRIOR):
 vals={p:execute(tests,p,noise,prior) for p in product(range(3),repeat=3)}
 fixed={p:execute(tests,(p[0],p[1],p[1]),noise,prior)
        for p in product(range(3),repeat=2)}
 best=max(vals.values());open_best=max(fixed.values())
 return (best,open_best,
   sorted(p for p,v in vals.items() if v==best),
   sorted(p for p,v in fixed.items() if v==open_best))

def stagewise(tests,noise=F(0),prior=PRIOR):
 # Independent DP: compute best second question for each first answer,
 # rather than invoking / enumerating complete policies.
 roots={};branches={}
 for first in range(3):
  q={}
  for answer in (0,1):
   q[answer]={}
   for second in range(3):
    q[answer][second]=sum(
      max(joint(h,((tests[first],answer),(tests[second],b)),noise,prior) for h in H)
      for b in (0,1))
  roots[first]=sum(max(q[a].values()) for a in (0,1))
  branches[first]=q
 return max(roots.values()),roots,branches

def stringify(x):
 if isinstance(x,F):return str(x)
 if isinstance(x,dict):return {str(k):stringify(v) for k,v in x.items()}
 if isinstance(x,(tuple,list)):return [stringify(v) for v in x]
 return x

def main():
 output={}
 for name,raw in SETS.items():
  tests=tuple(map(frozenset,raw))
  best,fixed,plans,fixed_plans=exhaustive(tests)
  dp,roots,branches=stagewise(tests)
  assert best==dp,(name,best,dp)
  output[name]={
   "tests":[sorted(x) for x in tests],"adaptive":best,"best_fixed":fixed,
   "adaptivity_gap":best-fixed,"optimal_conditional_plans":plans,
   "fixed_witnesses":fixed_plans,"first_question_Q":roots,
   "conditional_second_Q":branches
  }
 assert output["branch_required_112"]["adaptive"]==1
 assert output["branch_required_112"]["best_fixed"]==F(3,4)
 assert output["branch_required_123"]["adaptive"]==1
 assert output["branch_required_123"]["best_fixed"]==F(3,4)
 for key in ("fixed_suffices_222","fixed_suffices_nested"):
  assert (output[key]["adaptive"],output[key]["best_fixed"])==(1,1)
 assert (output["no_complete_identification_111"]["adaptive"],
         output["no_complete_identification_111"]["best_fixed"])==(F(3,4),F(3,4))
 noisy={}
 for name in ("branch_required_123","fixed_suffices_222","no_complete_identification_111"):
  tests=tuple(map(frozenset,SETS[name]))
  for noise in (F(1,10),F(1,2)):
   a,b,_,_=exhaustive(tests,noise)
   dp,_,_=stagewise(tests,noise)
   assert a==dp
   noisy[f"{name}@{noise}"]={"adaptive":a,"fixed":b,"gap":a-b}
 assert noisy["branch_required_123@1/10"]=={"adaptive":F(81,100),"fixed":F(63,100),"gap":F(9,50)}
 assert noisy["fixed_suffices_222@1/10"]["gap"]==0
 assert all(v["adaptive"]==F(1,4) for k,v in noisy.items() if k.endswith("@1/2"))
 # Biased cases do not repair a missing structural split, and can reduce
 # the value of some observations. No arbitrary player persona in prior.
 biased=(F(1,10),F(2,10),F(3,10),F(4,10))
 biased_a,biased_fixed,_,_=exhaustive(tuple(map(frozenset,SETS["branch_required_123"])),F(1,10),biased)
 assert (biased_a,biased_fixed)==(F(81,100),F(153,200))
 return stringify({"schema":"ssnoir.r278-r285.structural-transfer.v1",
  "evidence":"exhaustive 3^3 contingent questions, independent two-stage Bayes DP, all answer leaves",
  "contract":"Four uniform hidden states, only three public test subsets, two yes/no questions; final optimal MAP decoder. No free peek at hidden answer.",
  "R278_transfer_112":output["branch_required_112"],
  "R279_transfer_123":output["branch_required_123"],
  "R280_control_all_balanced_pairs":output["fixed_suffices_222"],
  "R281_control_nested_questions":output["fixed_suffices_nested"],
  "R282_insufficient_three_singletons":output["no_complete_identification_111"],
  "R283_noisy_answers_and_full_noise":noisy,
  "R284_biased_prior":{"prior":biased,"adaptive":biased_a,"fixed":biased_fixed},
  "R285_novelty_verdict":"Conditional second questions matter only for certain test-set partitions. Repeating ACB does not generalize across test sets. Remains classic twenty questions / active diagnosis, not certified independent innovative gameplay."})
if __name__=="__main__":print(json.dumps(main(),ensure_ascii=False,indent=2))
