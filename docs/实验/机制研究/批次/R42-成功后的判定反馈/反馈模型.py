"""R42四骰纯完成概率；冷静5隔离伤势，不优化健康次目标。"""
from pathlib import Path
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement, permutations
from math import factorial
import re,json
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[4]
SOURCE=(ROOT/'Engine/Runtime/Core/FateStrip.cs').read_text()
ENTRIES=re.findall(r'(<= 1|[2-6]|_) => \((\d), (\d)\)',SOURCE)
assert len(ENTRIES)==7
COUNTS={k:(int(b),int(m),6-int(b)-int(m)) for k,b,m in ENTRIES}
def odds(d,alert):
    key=d+1-alert
    return tuple(F(n,6) for n in COUNTS['<= 1' if key<=1 else '_' if key>=7 else str(key)])
GOAL=4
class Model:
    def __init__(self,policy='free',feedback=True,risky_only=False):
        assert policy in ('free','lowest','highest','ordered')
        self.policy,self.feedback,self.risky_only=policy,feedback,risky_only
        self.solve=lru_cache(None)(self._solve);self.choices={}
    def action(self,left,alert,h,d,risky):
        rest=list(h);rest.remove(d);rest=tuple(rest)
        return sum((w*self.solve(left-(g if risky else int(g==2)),int(bool(alert or (self.feedback and g==2))),rest)
                    for g,w in enumerate(odds(d,alert)) if w),F(0))
    def _solve(self,left,alert,h):
        if left<=0:return F(1)
        if not h:return F(0)
        if self.policy=='ordered': dice=(h[0],)
        elif self.policy=='lowest':dice=(min(h),)
        elif self.policy=='highest':dice=(max(h),)
        else:dice=sorted(set(h))
        cs=[(self.action(left,alert,h,d,risky),(d,risky)) for d in dice for risky in ((True,) if self.risky_only else (False,True))]
        v,ch=max(cs,key=lambda z:z[0]);self.choices[left,alert,h]=ch;return v
    def summary(self):
        total=F(0);rows=[];sequences=0
        for h in combinations_with_replacement(range(1,7),4):
            mult=factorial(4)
            for d in set(h):mult//=factorial(h.count(d))
            w=F(mult,6**4)
            if self.policy=='ordered':
                orders=sorted(set(permutations(h)));sequences+=len(orders)
                v,order=max(((self.solve(GOAL,0,o),o) for o in orders),key=lambda z:z[0])
            else:v=self.solve(GOAL,0,h);order=None
            total+=w*v;rows.append(dict(hand=h,weight=str(w),exact=str(v),initial_order=order))
        return dict(policy=self.policy,feedback=self.feedback,risky_only=self.risky_only,exact=str(total),probability=float(total),
                    states=self.solve.cache_info().currsize,initial_sequences=sequences,perhand=rows)
def main():
    rows=[]
    for policy,feedback,risky_only in [('free',True,False),('lowest',True,False),('highest',True,False),
                                       ('ordered',True,False),('free',False,False),('free',True,True)]:
        row=Model(policy,feedback,risky_only).summary();rows.append(row)
        print(json.dumps({k:v for k,v in row.items() if k!='perhand'},ensure_ascii=False),flush=True)
    free=rows[0]
    for row in rows[1:]:
        if not row['feedback']:continue
        for x,y in zip(free['perhand'],row['perhand']):
            assert x['hand']==y['hand'] and F(x['exact'])>=F(y['exact'])
        row['gap_to_free']=str(F(free['exact'])-F(row['exact']))
        row['strict_initial_hands']=sum(F(x['exact'])>F(y['exact']) for x,y in zip(free['perhand'],row['perhand']))
    out=dict(batch='R42',goal=GOAL,rounds=1,skill=1,cold=5,results=rows,
             scope='纯完成数学：四动作最大4冷静加税1不超过初始5，伤势排除；不优化健康/冷静次目标；公平独立骰；无正式/真人')
    (HERE/'反馈结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
