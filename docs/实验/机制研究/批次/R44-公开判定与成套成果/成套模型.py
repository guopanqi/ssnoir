"""R44：公开判定，成套效用3/加和2；精确终局分布，不优化健康。"""
from pathlib import Path
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement, product
from math import factorial
import re,json,hashlib
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[4]
SOURCE=ROOT/'Engine/Runtime/Core/FateStrip.cs'
ENTRIES=re.findall(r'(<= 1|[2-6]|_) => \((\d), (\d)\)',SOURCE.read_text())
assert len(ENTRIES)==7
COUNTS={k:(int(b),int(m),6-int(b)-int(m)) for k,b,m in ENTRIES}
ODDS={d:tuple(F(n,6) for n in COUNTS['_' if d+1>=7 else str(d+1)]) for d in range(1,7)}
HANDS=tuple(combinations_with_replacement(range(1,7),4))
def weight(h):
    n=factorial(len(h))
    for d in set(h):n//=factorial(h.count(d))
    return F(n,6**len(h))
def score(dist,bonus):return dist[1]+bonus*dist[2]
def terminal(a,b):
    n=int(a==0)+int(b==0)
    return tuple(F(int(i==n)) for i in range(3))
class Model:
    def __init__(self,bonus=3,family='free',both_actions=False):
        assert family in ('free','fixed_A','fixed_B','shortest')
        self.bonus,self.family,self.both_actions=bonus,family,both_actions
        self.solve=lru_cache(None)(self._solve);self.choices={};self.optimal={}
    def actions(self,a,b,h):
        targets=[i for i,r in enumerate((a,b)) if r]
        if self.family.startswith('fixed'):
            pref=int(self.family=='fixed_B');targets=[pref if (a,b)[pref] else 1-pref]
        elif self.family=='shortest':targets=[t for t in targets if (a,b)[t]==min((a,b)[i] for i in targets)]
        return [(d,t,r) for d in sorted(set(h)) for t in targets for r in ((True,False) if self.both_actions else (True,))]
    def action(self,a,b,h,ch):
        d,t,r=ch;rest=list(h);rest.remove(d);out=[F(0)]*3
        for g,p in enumerate(ODDS[d]):
            if not p:continue
            need=[a,b];need[t]=max(0,need[t]-(g if r else int(g==2)))
            nxt=self.solve(*need,tuple(rest))
            for i,v in enumerate(nxt):out[i]+=p*v
        return tuple(out)
    def _solve(self,a,b,h):
        if not h or a+b==0:return terminal(a,b)
        vals=[(self.action(a,b,h,ch),ch) for ch in self.actions(a,b,h)]
        best=max(score(v,self.bonus) for v,_ in vals)
        opts=[(v,ch) for v,ch in vals if score(v,self.bonus)==best]
        dist,ch=opts[0] # deterministic traversal, no hidden secondary payoff
        key=(a,b,h);self.choices[key]=ch;self.optimal[key]=tuple(c for _,c in opts)
        return dist
@lru_cache(None)
def single(h):
    polynomial={0:F(1)}
    for d in h:
        nxt={}
        for x,p in polynomial.items():
            for g,q in enumerate(ODDS[d]):nxt[x+g]=nxt.get(x+g,F(0))+p*q
        polynomial=nxt
    return sum((p for x,p in polynomial.items() if x>=3),F(0))
def partitions(h,bonus):
    faces=sorted(set(h));vals=[]
    for counts in product(*(range(h.count(d)+1) for d in faces)):
        ah=tuple(d for d,n in zip(faces,counts) for _ in range(n))
        bh=tuple(d for d,n in zip(faces,counts) for _ in range(h.count(d)-n))
        pa,pb=single(ah),single(bh)
        dist=((1-pa)*(1-pb),pa*(1-pb)+pb*(1-pa),pa*pb)
        vals.append((score(dist,bonus),dist,ah,bh))
    best=max(v[0] for v in vals);choice=next(v for v in vals if v[0]==best)
    return choice,len(vals)
def summary(model):
    total=[F(0)]*3;rows=[]
    for h in HANDS:
        dist=model.solve(3,3,h);w=weight(h)
        for i,v in enumerate(dist):total[i]+=w*v
        rows.append(dict(hand=h,weight=str(w),value=str(score(dist,model.bonus)),distribution=list(map(str,dist))))
    return dict(both_value=model.bonus,family=model.family,both_actions=model.both_actions,value=str(score(total,model.bonus)),
                expected=float(score(total,model.bonus)),distribution=list(map(str,total)),perhand=rows,states=model.solve.cache_info().currsize)
def cross_evaluate(model,scoring):
    @lru_cache(None)
    def run(a,b,h):
        if not h or a+b==0:return score(terminal(a,b),scoring)
        d,t,r=model.choices[a,b,h];rest=list(h);rest.remove(d);v=F(0)
        for g,p in enumerate(ODDS[d]):
            if not p:continue
            need=[a,b];need[t]=max(0,need[t]-(g if r else int(g==2)))
            v+=p*run(*need,tuple(rest))
        return v
    return sum((weight(h)*run(3,3,h) for h in HANDS),F(0))
def main():
    models={};rows=[];partition_rows=[]
    for bonus in (3,2):
        for family in ('free','fixed_A','fixed_B','shortest'):
            m=Model(bonus,family);models[bonus,family]=m;row=summary(m);rows.append(row)
            print(json.dumps({k:v for k,v in row.items() if k!='perhand'},ensure_ascii=False),flush=True)
        full=summary(Model(bonus,'free',True));rows.append(full)
        assert all(x['value']==y['value'] for x,y in zip(full['perhand'],rows[-5]['perhand']))
        total=[F(0)]*3;prows=[];n=0
        for h in HANDS:
            (v,dist,ah,bh),count=partitions(h,bonus);n+=count
            for i,p in enumerate(dist):total[i]+=weight(h)*p
            prows.append(dict(hand=h,weight=str(weight(h)),value=str(v),distribution=list(map(str,dist)),A=ah,B=bh))
        part=dict(both_value=bonus,family='initial_partition',value=str(score(total,bonus)),expected=float(score(total,bonus)),
                  distribution=list(map(str,total)),perhand=prows,allocations=n)
        partition_rows.append(part)
        print(json.dumps({k:v for k,v in part.items() if k!='perhand'},ensure_ascii=False),flush=True)
    for bonus in (3,2):
        free=next(r for r in rows if r['both_value']==bonus and r['family']=='free' and not r['both_actions'])
        for row in [r for r in rows+partition_rows if r['both_value']==bonus]:
            assert all(F(x['value'])>=F(y['value']) for x,y in zip(free['perhand'],row['perhand']))
            row['gap_to_free']=str(F(free['value'])-F(row['value']))
            row['strict_hands']=sum(F(x['value'])>F(y['value']) for x,y in zip(free['perhand'],row['perhand']))
    cross=[dict(policy_both_value=b,scoring_both_value=s,value=str(cross_evaluate(models[b,'free'],s))) for b in (3,2) for s in (3,2)]
    # Compare reachable optimal action sets, not arbitrary choices among exact ties.
    visited=set();pending=[(3,3,h) for h in HANDS];disjoint=[]
    while pending:
        key=pending.pop()
        if key in visited:continue
        visited.add(key);a,b,h=key
        if not h or not a+b:continue
        for bonus in (3,2):models[bonus,'free'].solve(a,b,h)
        m3,m2=models[3,'free'],models[2,'free']
        if not set(m3.optimal[key])&set(m2.optimal[key]):
            disjoint.append(dict(remaining=[a,b],dice=h,optimal3=m3.optimal[key],optimal2=m2.optimal[key]))
        # Only paths of the specified bonus3 optimal policy are followed.
        d,t,r=m3.choices[key];rest=list(h);rest.remove(d)
        for g,p in enumerate(ODDS[d]):
            if p:
                need=[a,b];need[t]=max(0,need[t]-g);pending.append((*need,tuple(rest)))
    out=dict(batch='R44',rules=dict(rounds=1,dice=4,skill=1,cold=5,A=3,B=3,one_value=1,both_values=[3,2]),
             source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),results=rows+partition_rows,cross_scoring=cross,
             reachable_bonus3_states=len(visited),disjoint_optimal_action_states=disjoint,
             scope='数学终局效用与成果数量分布；不优化健康/时点；固定分区包括空组/全给一项；正式0真人0')
    (HERE/'数学结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(dict(cross=cross,disjoint=len(disjoint)),ensure_ascii=False))
if __name__=='__main__':main()
