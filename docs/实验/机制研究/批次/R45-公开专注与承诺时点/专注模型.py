"""R45精确终局效用；不将低价值路线提前终止误当进度单调。"""
from pathlib import Path
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial
import re,json,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];SOURCE=ROOT/'Engine/Runtime/Core/FateStrip.cs'
entries=re.findall(r'(<= 1|[2-6]|_) => \((\d), (\d)\)',SOURCE.read_text());assert len(entries)==7
counts={k:(int(b),int(m),6-int(b)-int(m)) for k,b,m in entries}
def odds(d,locked):
    p=d+1+int(locked>=0)
    return tuple(F(n,6) for n in counts['_' if p>=7 else str(p)])
HANDS=tuple(combinations_with_replacement(range(1,7),4))
def weight(h):
    n=factorial(len(h))
    for d in set(h):n//=factorial(h.count(d))
    return F(n,6**len(h))
def score(dist):return dist[1]+2*dist[2]
def terminal(a,b,h):
    if a<=0:return (F(0),F(1),F(0))
    if b<=0:return (F(0),F(0),F(1))
    if not h:return (F(1),F(0),F(0))
    return None
class Model:
    def __init__(self,family='free',risky_only=False,initial_dice_count=4):
        assert family in ('free','never','initial','one_feedback')
        self.family,self.risky_only=family,risky_only
        self.initial_dice_count=initial_dice_count
        self.solve=lru_cache(None)(self._solve);self.choices={};self.optimal={}
        self.never=Model('never',risky_only) if family=='one_feedback' else None
    def action(self,a,b,h,locked,ch):
        _,d,t,r=ch;rest=list(h);rest.remove(d);out=[F(0)]*3
        for g,p in enumerate(odds(d,locked)):
            if not p:continue
            needs=[a,b];needs[t]=max(0,needs[t]-(g if r else int(g==2)))
            dist=self.solve(*needs,tuple(rest),locked)
            for i,v in enumerate(dist):out[i]+=p*v
        return tuple(out)
    def candidates(self,a,b,h,locked):
        vals=[]
        if locked<0 and self.family!='never':
            vals.extend((self.solve(a,b,h,t),('commit',t)) for t in (0,1))
        if locked>=0:targets=(locked,);moves=True
        else:
            targets=(0,1);moves=self.family not in ('initial',) and not(self.family=='one_feedback' and len(h)<self.initial_dice_count)
            if self.family=='one_feedback' and len(h)<self.initial_dice_count:vals.append((self.never.solve(a,b,h,-1),('decline',)))
        if moves:
            # Single locked target is monotone; uncommitted still keeps steady.
            modes=(True,) if locked>=0 or self.risky_only else (True,False)
            vals.extend((self.action(a,b,h,locked,('act',d,t,r)),('act',d,t,r)) for d in sorted(set(h)) for t in targets for r in modes)
        return vals
    def _solve(self,a,b,h,locked=-1):
        end=terminal(a,b,h)
        if end is not None:return end
        vals=self.candidates(a,b,h,locked);best=max(score(v) for v,_ in vals)
        opts=[(v,ch) for v,ch in vals if score(v)==best]
        dist,ch=opts[0] # ties commit early, then A; no secondary utility
        key=(a,b,h,locked);self.choices[key]=ch;self.optimal[key]=tuple(c for _,c in opts)
        return dist
    def summary(self):
        assert self.initial_dice_count==4
        total=[F(0)]*3;rows=[]
        for h in HANDS:
            dist=self.solve(3,6,h,-1);w=weight(h)
            for i,p in enumerate(dist):total[i]+=w*p
            rows.append(dict(hand=h,weight=str(w),value=str(score(dist)),distribution=list(map(str,dist)),initial_action=self.choices[3,6,h,-1]))
        return dict(family=self.family,risky_only=self.risky_only,value=str(score(total)),expected=float(score(total)),
                    distribution=list(map(str,total)),perhand=rows,states=self.solve.cache_info().currsize)
def main():
    rows=[]
    for family,risky in [('free',False),('never',False),('initial',False),('one_feedback',False),('free',True)]:
        row=Model(family,risky).summary();rows.append(row)
        print(json.dumps({k:v for k,v in row.items() if k!='perhand'},ensure_ascii=False),flush=True)
    for row in rows:
        assert all(F(x['value'])>=F(y['value']) for x,y in zip(rows[0]['perhand'],row['perhand']))
        row['gap_to_free']=str(F(rows[0]['value'])-F(row['value']))
        row['strict_hands']=sum(F(x['value'])>F(y['value']) for x,y in zip(rows[0]['perhand'],row['perhand']))
    out=dict(batch='R45',rules=dict(A=3,B=6,A_value=1,B_value=2,skill=1,cold=5,dice=4,rounds=1,commit_prepared_bonus=1),
             results=rows,source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
             scope='终局效用/目标分布；完成任一立即退出；冷静5隔离伤势；未锁定保留稳险，锁定仅快做；正式0真人0')
    (HERE/'数学结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
