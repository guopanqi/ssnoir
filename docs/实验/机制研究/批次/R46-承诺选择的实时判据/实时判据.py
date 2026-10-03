"""R46明确局部策略：选择只看一手后立刻专注的值；执行后再算。"""
from pathlib import Path
from fractions import Fraction as F
from functools import lru_cache
from collections import defaultdict
import sys,json,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
R45=HERE.parent/'R45-公开专注与承诺时点';sys.path.insert(0,str(R45))
import 专注模型 as rules
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
@lru_cache(None)
def single(need,h):
    distribution={0:F(1)}
    for d in h:
        nxt=defaultdict(F)
        for x,p in distribution.items():
            for g,q in enumerate(rules.odds(d,0)):nxt[min(need,x+g)]+=p*q
        distribution=nxt
    return distribution.get(need,F(0))
def lock_values(a,b,h):return (single(a,h),2*single(b,h))
def immediate(a,b,h):
    end=rules.terminal(a,b,h)
    return rules.score(end) if end is not None else max(lock_values(a,b,h))
def trial(a,b,h,d,t,r):
    rest=list(h);rest.remove(d)
    value=F(0)
    for g,p in enumerate(rules.odds(d,-1)):
        needs=[a,b];needs[t]=max(0,needs[t]-(g if r else int(g==2)))
        value+=p*immediate(*needs,tuple(rest))
    return value
class Policy:
    def __init__(self,family):
        assert family in ('one_step_all','lowest_B')
        self.family=family;self.solve=lru_cache(None)(self._solve);self.choices={};self.decisions={}
    def choose(self,a,b,h):
        lv=lock_values(a,b,h);target=max(range(2),key=lambda t:lv[t]);L=lv[target]
        candidates=[(min(h),1,True)] if self.family=='lowest_B' else [(d,t,r) for d in sorted(set(h)) for t in (0,1) for r in (True,False)]
        q,ch=max(((trial(a,b,h,*ch),ch) for ch in candidates),key=lambda x:x[0])
        self.decisions[a,b,h]=dict(L=str(L),lock_A=str(lv[0]),lock_B=str(lv[1]),Q=str(q),trial=ch)
        return ('act',*ch) if q>L else ('commit',target)
    def action(self,a,b,h,locked,ch):
        _,d,t,r=ch;rest=list(h);rest.remove(d);dist=[F(0)]*3
        for g,p in enumerate(rules.odds(d,locked)):
            if not p:continue
            needs=[a,b];needs[t]=max(0,needs[t]-(g if r else int(g==2)))
            nxt=self.solve(*needs,tuple(rest),locked)
            for i,v in enumerate(nxt):dist[i]+=p*v
        return tuple(dist)
    def _solve(self,a,b,h,locked=-1):
        end=rules.terminal(a,b,h)
        if end is not None:return end
        ch=('act',min(h),locked,True) if locked>=0 else self.choose(a,b,h)
        self.choices[a,b,h,locked]=ch
        if ch[0]=='commit':return self.solve(a,b,h,ch[1])
        return self.action(a,b,h,locked,ch)
    def summary(self):
        rows=[];total=[F(0)]*3
        for h in rules.HANDS:
            dist=self.solve(3,6,h);w=rules.weight(h)
            for i,v in enumerate(dist):total[i]+=w*v
            rows.append(dict(hand=h,weight=str(w),value=str(rules.score(dist)),distribution=list(map(str,dist)),initial_action=self.choices[3,6,h,-1],decision=self.decisions[3,6,h]))
        return dict(family=self.family,value=str(rules.score(total)),expected=float(rules.score(total)),distribution=list(map(str,total)),perhand=rows)
def forward(policy):
    active=defaultdict(F)
    for h in rules.HANDS:active[3,6,h,-1,-1,-1]=rules.weight(h)
    end=[F(0)]*3;timing=defaultdict(F);change=F(0);visits=defaultdict(F);level=0
    while active:
        nxt=defaultdict(F)
        for (a,b,h,lock,first,at),mass in active.items():
            done=rules.terminal(a,b,h)
            if done is not None:
                for i,v in enumerate(done):end[i]+=mass*v
                timing[str(at)]+=mass
                if lock>=0 and first>=0 and lock!=first:change+=mass
                continue
            policy.solve(a,b,h,lock);ch=policy.choices[a,b,h,lock]
            if lock<0:visits[a,b,h]+=mass
            if ch[0]=='commit':nxt[a,b,h,ch[1],first,4-len(h)]+=mass
            else:
                _,d,t,r=ch;rest=list(h);rest.remove(d)
                for g,p in enumerate(rules.odds(d,lock)):
                    if p:
                        needs=[a,b];needs[t]=max(0,needs[t]-(g if r else int(g==2)))
                        nxt[*needs,tuple(rest),lock,t if first<0 else first,at]+=mass*p
        active=nxt;level+=1;assert level<=9
    assert sum(end)==sum(timing.values())==1
    return dict(distribution=list(map(str,end)),value=str(rules.score(end)),commit_timing={k:str(v) for k,v in sorted(timing.items())},
                delayed_commit=str(sum((v for k,v in timing.items() if int(k)>0),F(0))),commit_other_first=str(change),
                visits=[dict(remaining=[a,b],dice=h,mass=str(p),decision=policy.decisions[a,b,h],action=policy.choices[a,b,h,-1]) for (a,b,h),p in sorted(visits.items())])
def main():
    version=json.loads((R45/'版本.json').read_text())
    for rel,digest in version['source_sha256'].items():assert sha(ROOT/rel)==digest,rel
    prior=json.loads((R45/'数学结果.json').read_text());rows=[]
    free=prior['results'][0];initial=prior['results'][2]
    for family in ('one_step_all','lowest_B'):
        p=Policy(family);row=p.summary();row['normal_forward']=forward(p)
        assert row['normal_forward']['distribution']==row['distribution']
        for optimum,early,actual in zip(free['perhand'],initial['perhand'],row['perhand']):
            assert F(optimum['value'])>=F(actual['value'])>=F(early['value'])
        row['gap_to_free']=str(F(free['value'])-F(row['value']))
        row['strict_initial_hands']=sum(F(x['value'])>F(y['value']) for x,y in zip(free['perhand'],row['perhand']))
        rows.append(row);print(json.dumps({k:v for k,v in row.items() if k not in ('perhand','normal_forward')},ensure_ascii=False),flush=True)
    out=dict(batch='R46',frozen_references=[{k:v for k,v in r.items() if k!='perhand'} for r in prior['results'][:4]],results=rows,
             input_sha256={'R45/专注模型.py':sha(R45/'专注模型.py'),'R45/数学结果.json':sha(R45/'数学结果.json')},
             scope='R45冻结公开规则；局部判据非自由递归，锁定后固定低骰序；正常前向非玩家／独立优化；正式0真人0')
    (HERE/'判据结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
