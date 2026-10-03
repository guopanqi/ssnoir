"""R47保证退路的有限计划，与持续重算的实际策略分开。"""
from pathlib import Path
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations
from collections import defaultdict
import json,sys,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
R46=HERE.parent/'R46-承诺选择的实时判据';sys.path.insert(0,str(R46))
import 实时判据 as base
rules=base.rules
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def minimum(d):return next(g for g,p in enumerate(rules.odds(d,0)) if p)
def subtract(h,r):
    out=list(h)
    for d in r:out.remove(d)
    return tuple(out)
@lru_cache(None)
def tail(need,h):
    if need<=0:return F(1)
    dist={0:F(1)}
    for d in h:
        nxt=defaultdict(F)
        for x,p in dist.items():
            for g,q in enumerate(rules.odds(d,-1)):nxt[min(need,x+g)]+=p*q
        dist=nxt
    return dist.get(need,F(0))
@lru_cache(None)
def plans(a,b,h,family):
    reserves=(h[-2:],) if family=='top2' and len(h)>=2 else ()
    if family=='all_reserves':
        reserves=sorted({r for n in range(len(h)+1) for r in combinations(h,n)},key=lambda r:(len(r),r))
    out=[]
    for r in reserves:
        m=sum(map(minimum,r))
        if m<a:continue
        s=subtract(h,r);G=1+tail(b-m,s)
        out.append(dict(reserve=r,spare=s,minimum=m,threshold=b-m,G=str(G)))
    return tuple(out)
class Policy(base.Policy):
    def __init__(self,family):
        assert family in ('top2','all_reserves')
        self.family=family;self.solve=lru_cache(None)(self._solve);self.choices={};self.decisions={}
    def choose(self,a,b,h):
        lv=base.lock_values(a,b,h);t=max(range(2),key=lambda t:lv[t]);L=lv[t]
        ps=plans(a,b,h,self.family);best=max(ps,key=lambda p:F(p['G'])) if ps else None
        G=F(best['G']) if best else None
        self.decisions[a,b,h]=dict(L=str(L),lock_A=str(lv[0]),lock_B=str(lv[1]),G=str(G) if G is not None else None,plan=best)
        return ('act',min(best['spare']),1,True) if G is not None and G>L else ('commit',t)
    def summary(self):
        rows=[];total=[F(0)]*3
        for h in rules.HANDS:
            dist=self.solve(3,6,h);w=rules.weight(h);v=rules.score(dist);info=self.decisions[3,6,h]
            assert v>=F(info['L'])
            if info['G'] is not None:assert v>=F(info['G'])
            for i,p in enumerate(dist):total[i]+=w*p
            rows.append(dict(hand=h,weight=str(w),value=str(v),distribution=list(map(str,dist)),initial_action=self.choices[3,6,h,-1],decision=info))
        return dict(family=self.family,value=str(rules.score(total)),expected=float(rules.score(total)),distribution=list(map(str,total)),perhand=rows)
def main():
    v46=json.loads((R46/'版本.json').read_text())
    for rel,digest in v46['source_sha256'].items():assert sha(ROOT/rel)==digest,rel
    prior=json.loads((base.R45/'数学结果.json').read_text());one=json.loads((R46/'判据结果.json').read_text())
    free=prior['results'][0];initial=prior['results'][2];rows=[];policies={}
    for family in ('top2','all_reserves'):
        p=Policy(family);policies[family]=p;row=p.summary();row['normal_forward']=base.forward(p)
        assert row['normal_forward']['distribution']==row['distribution']
        for optimum,early,actual in zip(free['perhand'],initial['perhand'],row['perhand']):
            assert F(optimum['value'])>=F(actual['value'])>=F(early['value'])
        row['gap_to_free']=str(F(free['value'])-F(row['value']))
        row['strict_initial_hands']=sum(F(x['value'])>F(y['value']) for x,y in zip(free['perhand'],row['perhand']))
        row['versus_one_step_hands']={kind:sum((F(a['value'])>F(b['value'])) if kind=='better' else ((F(a['value'])<F(b['value'])) if kind=='worse' else F(a['value'])==F(b['value'])) for a,b in zip(row['perhand'],one['results'][0]['perhand'])) for kind in ('better','worse','equal')}
        rows.append(row);print(json.dumps({k:v for k,v in row.items() if k not in ('perhand','normal_forward')},ensure_ascii=False),flush=True)
    differences=[dict(hand=a['hand'],weight=a['weight'],top2=a['value'],all_reserves=b['value'],top2_action=a['initial_action'],all_action=b['initial_action']) for a,b in zip(rows[0]['perhand'],rows[1]['perhand']) if a['value']!=b['value'] or a['initial_action']!=b['initial_action']]
    residual=[dict(hand=a['hand'],weight=a['weight'],free=a['value'],reserve=b['value'],free_action=a['initial_action'],reserve_action=b['initial_action'],decision=b['decision'],gap=str(F(a['value'])-F(b['value']))) for a,b in zip(free['perhand'],rows[1]['perhand']) if F(a['value'])>F(b['value'])]
    out=dict(batch='R47',frozen_references=[dict(batch='R45',family=r['family'],value=r['value']) for r in prior['results'][:4]]+[dict(batch='R46',family=r['family'],value=r['value']) for r in one['results']],results=rows,reserve_family_differences=differences,residual_roots=residual,
             input_sha256={'R45/专注模型.py':sha(base.R45/'专注模型.py'),'R45/数学结果.json':sha(base.R45/'数学结果.json'),'R46/实时判据.py':sha(R46/'实时判据.py'),'R46/判据结果.json':sha(R46/'判据结果.json')},
             scope='保证小成果及保证大成果的有限计划；G估值≠全程效用；冷静5非约束、正式0真人0；不足两骰top2立即专注；所有储备同值取最少骰再字典序')
    (HERE/'储备结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
