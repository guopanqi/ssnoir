#!/usr/bin/env python3
"""R269-R277: two genuinely adaptive questions about four hidden types.

A tests membership in {0,1}; B in {0}; C in {2}.
Two yes/no answers and one final classification. First question cannot know
hidden type; second may use ONLY observed first answer.
Independent exhaustive 27 contingent policies vs memoized branch DP.
"""
import json
from fractions import Fraction as F
from itertools import product
TESTS={"A":{0,1},"B":{0},"C":{2}}
H=range(4)
def likelihood(theta,test,answer,noise):
 truth=int(theta in TESTS[test])
 return 1-noise if answer==truth else noise
def weight(theta,path,noise):
 v=F(1,4)
 for q,ans in path:v*=likelihood(theta,q,ans,noise)
 return v
def classify_policy(first,after_no,after_yes,noise):
 # Four publicly distinct observation leaves. Best guess maximizes joint mass.
 return sum(max(weight(h,((first,a),(after_yes if a else after_no,b)),noise) for h in H)
            for a in (0,1) for b in (0,1))
def exhaustive(noise):
 all_policy={f+x+y:classify_policy(f,x,y,noise) for f,x,y in product(TESTS,repeat=3)}
 fixed={f+s:classify_policy(f,s,s,noise) for f,s in product(TESTS,repeat=2)}
 a=max(all_policy.values());b=max(fixed.values())
 return a,b,[k for k,v in all_policy.items() if v==a],[k for k,v in fixed.items() if v==b]
def dp(noise):
 # Separate stagewise calculation, not relying on classify_policy.
 q={}
 for first in TESTS:
  branch={}
  for answer in (0,1):
   branch[str(answer)]={}
   for second in TESTS:
    branch[str(answer)][second]=sum(
      max(weight(h,((first,answer),(second,b)),noise) for h in H) for b in (0,1))
  q[first]=branch
 roots={f:sum(max(sub.values()) for sub in branch.values()) for f,branch in q.items()}
 return max(roots.values()),roots,q
def encode(v):
 if isinstance(v,F):return str(v)
 if isinstance(v,dict):return {str(k):encode(x) for k,x in v.items()}
 if isinstance(v,(tuple,list)):return [encode(x) for x in v]
 return v
def main():
 runs={}
 for label,noise in (("perfect",F(0)),("noise10",F(1,10)),("noise50",F(1,2))):
  best,fixed,plans,fp=exhaustive(noise)
  independent,roots,branch=dp(noise)
  assert best==independent
  runs[label]={"noise":noise,"adaptive":best,"fixed":fixed,"improvement":best-fixed,
               "optimal_conditional_policies":plans,"best_fixed_test_pairs":fp,
               "first_Q":roots,"conditional_second_Q":branch}
 p,n,u=runs["perfect"],runs["noise10"],runs["noise50"]
 assert (p["adaptive"],p["fixed"],p["optimal_conditional_policies"])==(1,F(3,4),["ACB"])
 assert (n["adaptive"],n["fixed"])==(F(81,100),F(63,100))
 assert (u["adaptive"],u["fixed"])==(F(1,4),F(1,4))
 assert p["first_Q"]=={"A":F(1),"B":F(3,4),"C":F(3,4)}
 assert p["conditional_second_Q"]["A"]["0"]["C"]==F(1,2)
 assert p["conditional_second_Q"]["A"]["1"]["B"]==F(1,2)
 # Labels of hypotheses may change without changing the two-answer decision tree.
 relabel={0:2,1:3,2:0,3:1}
 mapped={key:{relabel[h] for h in values} for key,values in TESTS.items()}
 assert len({frozenset(x) for x in mapped.values()})==3
 return encode({"schema":"ssnoir.r269-r277.adaptive-diagnostic-tests.v1",
  "scope":"two-stage exact paper diagnosis only; no native/actual players",
  "public_test_definitions":{k:sorted(v) for k,v in TESTS.items()},
  "rule":"4 equally likely unknown hypotheses, 2 yes/no questions, answers independently flipped with public noise; guess a type and score 1 if right; second question can react to first answer.",
  "R269_no_noise_adaptive":p["adaptive"],
  "R270_best_two_fixed_queries":p["fixed"],
  "R271_contingent_query_plan":p["optimal_conditional_policies"],
  "R272_reachable_strict_second_query_values":p["conditional_second_Q"]["A"],
  "R273_noisy_tenth":n,
  "R274_all_questions_pure_noise":u,
  "R275_first_question_values":p["first_Q"],
  "R276_exhaustive_27_question_trees":"All 3*3*3 contingent question trees compared independently with stagewise belief DP at noise 0, 1/10, 1/2.",
  "R277_assessment":"One answer alters which next question is informative: a real multi-step question-selection decision. Not an independent verified game; related to classic twenty questions and P03/P08, and simple four-hypothesis tree is memorizable."})
if __name__=="__main__":print(json.dumps(main(),ensure_ascii=False,indent=2))
