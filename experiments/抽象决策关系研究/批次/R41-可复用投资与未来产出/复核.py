"""共享全局族核对＋独立Fraction末回合树与进入下一回合顺序。"""
from pathlib import Path
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial
import json, runpy
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE/'投资模型.py')); Model=K['Model']
# 从正式表另行逐项誊录，不调用主模型概率/伤势/动作转移。
TABLE=((3,3,0),(2,3,1),(1,3,2),(1,2,3),(0,2,4),(0,1,5),(0,0,6))
PAY=(((0,1),(0,0),(1,0)),((0,1),(1,1),(2,0)))
def spend(c,k,q,cost):
    if c>=cost: return [(F(1),c-cost,k,q)]
    nk=k+cost-c
    if nk>=7: return [(F(1),0,7,0)]
    if k==0 and nk<4: return [(F(1,4),0,nk,1),(F(3,4),0,nk,0)]
    return [(F(1),0,nk,q if nk<4 else 0)]
def tail(left,c,k,q,h):
    if k>=7: return F(0)
    if left<=0: return F(1)
    best=F(0)
    for d in set(h):
        rest=list(h);rest.remove(d);rest=tuple(rest)
        counts=TABLE[d-1 if 1<=k<=3 and q else d]
        for act in PAY:
            v=F(0)
            for n,(gain,cost) in zip(counts,act):
                if n:
                    v+=F(n,6)*sum(z*tail(left-gain,nc,nk,nq,rest) for z,nc,nk,nq in spend(c,k,q,cost))
            best=max(best,v)
    return best
cached_tail=lru_cache(None)(tail)
def enter(left,f,c,k,q,t):
    out=F(0)
    for w,nc,nk,nq in spend(c,k,q,1):
        if nk>=7 or t<=1: continue
        if left-f<=0: out+=w;continue
        n=3 if nk>=4 else 4
        for h in combinations_with_replacement(range(1,7),n):
            count=factorial(n)
            for d in set(h):count//=factorial(h.count(d))
            # 末回合独立递归可缓存根结果，不复用主模型。
            out+=w*F(count,6**n)*cached_tail(left-f,nc,nk,nq,h)
    return out
def main():
    data=json.loads((HERE/'投资结果.json').read_text()); checks=[]
    max_root_error=0.; max_low_primary=0.
    for obj in ('completion','early'):
        rows={r['policy']:r for r in data['results'] if r['objective']==obj}
        for x,y,z in zip(rows['自由']['perhand'],rows['仅首回合投资']['perhand'],rows['首手最低骰投资']['perhand']):
            assert x['hand']==y['hand']==z['hand']
            max_root_error=max(max_root_error,max(abs(a-b) for a,b in zip(x['value'],y['value'])))
            assert abs(Model(obj).score(x['value'])-Model(obj).score(y['value']))<1e-11
            if obj=='completion':
                e=abs(sum(x['value'][:3])-sum(z['value'][:3]));max_low_primary=max(max_low_primary,e);assert e<1e-11
    m=Model('completion');prob_checks=0
    for skill in (0,1):
        for d in range(1,7):
            assert max(abs(a-b/6) for a,b in zip(K['ODDS'](d,skill),TABLE[d-1+skill]))<1e-14
            prob_checks+=3
    # 4种健康，包括3级头伤跨重伤、6伤先失败；21种两骰、剩余1..4。
    max_tail_error=0.; count=0
    for c,k,q in ((2,0,0),(0,2,0),(0,3,1),(0,6,0)):
        for left in range(1,5):
            for h in combinations_with_replacement(range(1,7),2):
                expected=tail(left,c,k,q,h)
                actual=sum(m.value(16-left,0,0,c,k,q,1,h)[:3])
                error=abs(float(expected)-actual);max_tail_error=max(max_tail_error,error);assert error<1e-11
                count+=1
    endings=[(1,1,0,6,0,2),(1,1,1,6,0,2),(1,1,0,2,0,1),
             (3,1,0,2,0,2),(3,0,0,2,0,2),(2,1,0,3,1,2)]
    max_end_error=0.
    for left,f,c,k,q,t in endings:
        expected=enter(left,f,c,k,q,t);actual=m.end(16-left,f,0,c,k,q,t)
        error=abs(float(expected)-sum(actual[:3]));max_end_error=max(max_end_error,error);assert error<1e-11
        if left==1 and f==1 and c==1 and k==6 and t==2:
            assert actual[:3]==(0.,0.,1.),'进入第三回合的产出算第三回合成功'
        checks.append(dict(state=dict(left=left,factory=f,cold=c,injury=k,head=q,t=t),fraction=str(expected),actual=actual))
    out=dict(batch='R41',passed=True,shared_root_components=1512,shared_root_max_error=max_root_error,
             low_first_completion_hands=126,low_first_primary_max_error=max_low_primary,
             independent_probability_components=prob_checks,independent_uncached_two_die_trees=count,
             independent_tail_max_error=max_tail_error,independent_endings=checks,independent_end_max_error=max_end_error,
             scope='全局族是共享模型；独立字面概率/伤势转移、无缓存两骰末回合树与6结束条件；非独立三回合全优化；正式0真人0')
    (HERE/'复核.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
if __name__=='__main__':main()
