#!/usr/bin/env python3
"""R97b: does one public, comprehensible delayed-seal rule match free DP?
Independent paper P20 foil; NOT actual native P20 scene or human UX.
4 actions/turn, unsealed 2 turns, sealed 3; seal costs 1 action and 2
ladder repairs, talks +0/+1/+2 with 1/6,2/6,3/6; bad and round end
each lose one composure. This rule may fail outside the declared envelope.
"""
from functools import lru_cache
from fractions import Fraction as F
import json,sys
ODDS=((0,F(1,6)),(1,F(2,6)),(2,F(3,6)))

def audit(goal,composure):
    def build(bounded):
        @lru_cache(None)
        def v(p,h,t,slots,sealed,ladder):
            if h<=0:return F(0)
            if p>=goal and (not sealed or ladder>=2):return F(1)
            if t>=2+sealed:return F(0)
            if slots==0:
                if t+1>=2+sealed:return F(0)
                return v(p,h-1,t+1,4,sealed,ladder)
            if bounded:
                # A sealed third round has four actions: two mandatory
                # ladder repairs, at most two persuasive actions (max +4).
                # Hold the option until the unsealed deadline's final die.
                if (not sealed and t==1 and slots==1 and h>=2
                        and max(0,goal-4)<=p<goal):
                    return v(p,h,t,slots-1,1,ladder)
                if sealed and ladder<2 and (p>=goal or slots<=2):
                    return v(p,h,t,slots-1,sealed,ladder+1)
                if p<goal:return sum(pr*v(min(goal,p+g),h-int(g==0),t,slots-1,sealed,ladder)
                                     for g,pr in ODDS)
                return F(0)
            choices=[]
            if p<goal:choices.append(sum(pr*v(min(goal,p+g),h-int(g==0),t,slots-1,sealed,ladder)
                                         for g,pr in ODDS))
            if not sealed:choices.append(v(p,h,t,slots-1,1,ladder))
            if sealed and ladder<2:choices.append(v(p,h,t,slots-1,sealed,ladder+1))
            return max(choices,default=F(0))
        return v
    free=build(False)(0,composure,0,4,0,0)
    simple=build(True)(0,composure,0,4,0,0)
    assert free>=simple
    return {'goal':goal,'initial_composure':composure,'adaptive':float(free),
            'single_explainable_rule':float(simple),'gap':float(free-simple)}
def main():
    rows=[audit(g,h) for g in range(8,13) for h in range(3,6)]
    assert len(rows)==15 and all(r['gap']==0 for r in rows),rows
    out={'id':'R97b','model':'R97 public 2 vs 3-turn deadline variant, NOT native P20',
         'simple_rule':'Keep original exit; only on second round last action, if composure>=2 and goal-progress<=4, seal. In final round, talk until target, repair if target met or only two actions left.',
         'evaluation':'Exact Fraction, 15 goal/composure boundaries; scripts can follow publicly visible progress/health; no human play',
         'cases':rows}
    s=json.dumps(out,ensure_ascii=False,indent=2)+'\n'
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(s,encoding='utf8')
    else:print(s)
if __name__=='__main__':main()
