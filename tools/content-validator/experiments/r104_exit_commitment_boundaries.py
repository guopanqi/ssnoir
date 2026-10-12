#!/usr/bin/env python3
"""R104: Falsify unlimited robustness of R97b's one-line delayed-seal rule.
Base R97 paper: goal10, composure4; unsealed deadline two rounds,
sealed deadline 2+extension. Four actions/round; talk 0/1/2 with
1/6,2/6,3/6, bad talk and round transition cost one composure.
Seal costs one die, then 1/2/3 ladder repairs. Full DP vs no seal
and a legible 'seal second round last action if remaining <= future
talk ceiling' rule. No change to actual P20 and no human evidence.
"""
from functools import lru_cache
from fractions import Fraction as F
import json,sys
ROLL=((0,F(1,6)),(1,F(2,6)),(2,F(3,6)))
def evaluate(repairs,extension,goal=10,health=4):
 def solve(heuristic=False,seal_available=True):
  @lru_cache(None)
  def dp(p,h,t,n,sealed,ladder):
   if h<=0:return F(0)
   if p>=goal and (not sealed or ladder>=repairs):return F(1)
   if t>=2+extension*sealed:return F(0)
   if n==0:
    if t+1>=2+extension*sealed:return F(0)
    return dp(p,h-1,t+1,4,sealed,ladder)
   talk=sum(pr*dp(min(goal,p+g),h-int(g==0),t,n-1,sealed,ladder) for g,pr in ROLL) if p<goal else F(-2)
   fix=dp(p,h,t,n-1,sealed,ladder+1) if sealed and ladder<repairs else F(-2)
   switch=dp(p,h,t,n-1,1,ladder) if seal_available and not sealed else F(-2)
   if heuristic:
    if (not sealed and seal_available and t==1 and n==1 and h>=2 and p<goal
          and goal-p<=2*(4-repairs)):return switch
    if sealed and ladder<repairs and (p>=goal or n<=repairs-ladder):return fix
    return talk if p<goal else (fix if sealed and ladder<repairs else F(0))
   return max(talk,fix,switch)
  return dp(0,health,0,4,0,0)
 free=solve();none=solve(seal_available=False);simple=solve(heuristic=True)
 assert free>=simple and free>=none
 return {'ladder_repairs':repairs,'extra_sealed_rounds':extension,
         'optimal':float(free),'never_seal':float(none),
         'one_line_rule':float(simple),'seal_option_gain':float(free-none),
         'unexplained_gain':float(free-simple)}
def main():
 rows=[evaluate(repairs,extra) for repairs in (1,2,3) for extra in (1,2)]
 control=next(x for x in rows if x['ladder_repairs']==2 and x['extra_sealed_rounds']==1)
 hard=next(x for x in rows if x['ladder_repairs']==3 and x['extra_sealed_rounds']==1)
 assert control['unexplained_gain']==0
 assert hard['seal_option_gain']==0
 assert any(x['unexplained_gain']>0 for x in rows)
 out={'study':'R104','scope':'Exact Fraction independent variation of R97 paper, not official P20','results':rows}
 s=json.dumps(out,ensure_ascii=False,indent=2)+'\n'
 if len(sys.argv)>1:
  from pathlib import Path
  Path(sys.argv[1]).write_text(s,encoding='utf8')
 else:print(s)
if __name__=='__main__':main()
