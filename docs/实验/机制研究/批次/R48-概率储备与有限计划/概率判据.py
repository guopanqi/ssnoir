"""R48有限分段计划的概率估值；选择时不读取自由续局。"""
from pathlib import Path
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations
from collections import defaultdict
import sys,json,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
R47=HERE.parent/'R47-保底储备与连续试探';sys.path.insert(0,str(R47))
import 储备判据 as previous
base=previous.base;rules=previous.rules
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
@lru_cache(None)
def contribution(h,cap):
    dist={0:F(1)}
    for d in h:
        nxt=defaultdict(F)
        for x,p in dist.items():
            for g,q in enumerate(rules.odds(d,-1)):nxt[min(cap,x+g)]+=p*q
        dist=nxt
    return tuple(sorted(dist.items()))
@lru_cache(None)
def estimate(a,b,r,s):
    pa=base.single(a,r)
    return sum((p*(2 if x>=b else max(pa,2*base.single(b-x,r))) for x,p in contribution(s,b)),F(0))
@lru_cache(None)
def plans(a,b,h,family):
    reserves=(h[-2:],) if family=='top2' and len(h)>=2 else ()
    if family=='all_reserves':reserves=sorted({r for n in range(len(h)+1) for r in combinations(h,n)},key=lambda r:(len(r),r))
    return tuple(dict(reserve=r,spare=previous.subtract(h,r),H=str(estimate(a,b,r,previous.subtract(h,r)))) for r in reserves)
class Policy(base.Policy):
    def __init__(self,family):
        assert family in ('top2','all_reserves')
        self.family=family;self.solve=lru_cache(None)(self._solve);self.choices={};self.decisions={}
    def choose(self,a,b,h):
        lv=base.lock_values(a,b,h);t=max(range(2),key=lambda t:lv[t]);L=lv[t]
        ps=plans(a,b,h,self.family);best=max(ps,key=lambda p:F(p['H'])) if ps else None
        H=F(best['H']) if best else None
        self.decisions[a,b,h]=dict(L=str(L),lock_A=str(lv[0]),lock_B=str(lv[1]),H=str(H) if H is not None else None,plan=best)
        return ('act',min(best['spare']),1,True) if H is not None and H>L else ('commit',t)
    def summary(self):
        rows=[];total=[F(0)]*3
        for h in rules.HANDS:
            dist=self.solve(3,6,h);w=rules.weight(h);v=rules.score(dist);info=self.decisions[3,6,h]
            assert v>=F(info['L'])
            if info['H'] is not None:assert v>=F(info['H'])
            for i,p in enumerate(dist):total[i]+=w*p
            rows.append(dict(hand=h,weight=str(w),value=str(v),distribution=list(map(str,dist)),initial_action=self.choices[3,6,h,-1],decision=info))
        return dict(family=self.family,value=str(rules.score(total)),expected=float(rules.score(total)),distribution=list(map(str,total)),perhand=rows)
def main():
    version=json.loads((R47/'版本.json').read_text())
    for rel,digest in version['source_sha256'].items():assert sha(ROOT/rel)==digest,rel
    old=json.loads((R47/'储备结果.json').read_text());prior=json.loads((base.R45/'数学结果.json').read_text());one=json.loads((previous.R46/'判据结果.json').read_text())
    free=prior['results'][0];initial=prior['results'][2];rows=[]
    for family in ('top2','all_reserves'):
        p=Policy(family);row=p.summary();row['normal_forward']=base.forward(p)
        assert row['normal_forward']['distribution']==row['distribution']
        for optimum,early,actual in zip(free['perhand'],initial['perhand'],row['perhand']):assert F(optimum['value'])>=F(actual['value'])>=F(early['value'])
        row['gap_to_free']=str(F(free['value'])-F(row['value']))
        row['strict_initial_hands']=sum(F(x['value'])>F(y['value']) for x,y in zip(free['perhand'],row['perhand']))
        row['comparisons']={}
        for name,other in [('one_step',one['results'][0]),('guaranteed',old['results'][0])]:
            row['comparisons'][name]={kind:sum((F(a['value'])>F(b['value'])) if kind=='better' else ((F(a['value'])<F(b['value'])) if kind=='worse' else F(a['value'])==F(b['value'])) for a,b in zip(row['perhand'],other['perhand'])) for kind in ('better','worse','equal')}
        rows.append(row);print(json.dumps({k:v for k,v in row.items() if k not in ('perhand','normal_forward')},ensure_ascii=False),flush=True)
    residual=[dict(hand=a['hand'],weight=a['weight'],free=a['value'],actual=b['value'],free_action=a['initial_action'],actual_action=b['initial_action'],decision=b['decision'],gap=str(F(a['value'])-F(b['value']))) for a,b in zip(free['perhand'],rows[1]['perhand']) if F(a['value'])>F(b['value'])]
    diffs=[dict(hand=a['hand'],weight=a['weight'],top2=a['value'],all_reserves=b['value'],top2_action=a['initial_action'],all_action=b['initial_action']) for a,b in zip(rows[0]['perhand'],rows[1]['perhand']) if a['value']!=b['value'] or a['initial_action']!=b['initial_action']]
    out=dict(batch='R48',frozen_references=old['frozen_references']+[dict(batch='R47',family=r['family'],value=r['value']) for r in old['results']],results=rows,residual_roots=residual,reserve_family_differences=diffs,
             input_sha256={'R45/专注模型.py':sha(base.R45/'专注模型.py'),'R45/数学结果.json':sha(base.R45/'数学结果.json'),'R46/实时判据.py':sha(previous.R46/'实时判据.py'),'R46/判据结果.json':sha(previous.R46/'判据结果.json'),'R47/储备判据.py':sha(R47/'储备判据.py'),'R47/储备结果.json':sha(R47/'储备结果.json')},
             scope='H有限计划估值与滚动执行分开；试探仅快乙，固定最低S；所有空/全储备保留，不要求保证。正式0真人0')
    (HERE/'概率结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
