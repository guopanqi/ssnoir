"""R43 冻结模型：开局一次抽取真值，公开反馈进行精确 Bayes 更新。"""
from collections import Counter
from fractions import Fraction as F
from functools import lru_cache
from hashlib import sha256
from itertools import combinations_with_replacement, permutations
from math import factorial, gcd
from pathlib import Path
import json
import time

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
COUNTS = {1:(3,3,0),2:(2,3,1),3:(1,3,2),4:(1,2,3),5:(0,2,4),6:(0,1,5),7:(0,0,6),8:(0,0,6)}
POLICIES = {}

def reduced(a, b):
    assert a >= 0 and b >= 0 and a+b > 0
    divisor = gcd(a,b)
    return a//divisor, b//divisor

def row(die, method, truth):
    skill = 2 if method != truth else 0
    return COUNTS[min(8,die+skill)]

def transitions(die, method, a, b):
    r0, r1 = row(die,method,0), row(die,method,1)
    for gain in range(3):
        na, nb = a*r0[gain], b*r1[gain]
        if na+nb:
            yield gain, F(na+nb,6*(a+b)), reduced(na,nb)

def key(family,dice,need,a,b):
    return f"{family}|{','.join(map(str,dice))}|{need}|{a}|{b}"

@lru_cache(None)
def solve(family,dice,need,a=1,b=1):
    if need <= 0:return F(1)
    if not dice or need > 2*len(dice):return F(0)
    if family == 'ordered': indices=(0,)
    else: indices=tuple(i for i in range(len(dice)) if i==0 or dice[i] != dice[i-1])
    methods=(0,1)
    if family == 'greedy':methods=(1,) if a >= b else (0,)
    best=F(-1);best_action=None
    for i in indices:
        die=dice[i];rest=dice[:i]+dice[i+1:]
        for method in methods:
            q=sum((p*solve(family,rest,need-gain,*posterior)
                   for gain,p,posterior in transitions(die,method,a,b)),F(0))
            if q > best:best=q;best_action=(die,method)
    POLICIES[key(family,dice,need,a,b)]=best_action
    return best

@lru_cache(None)
def fixed(dice,need,a=1,b=1):
    if need <= 0:return F(1)
    if not dice or need > 2*len(dice):return F(0)
    return sum((p*fixed(dice[1:],need-gain,*posterior)
                for gain,p,posterior in transitions(dice[0],0,a,b)),F(0))

def mult(hand):
    n=factorial(len(hand))
    for c in Counter(hand).values():n//=factorial(c)
    return n

def atomic_json(name,obj):
    temporary=HERE/(name+'.tmp')
    temporary.write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    temporary.replace(HERE/name)

def main():
    start=time.time()
    hashes={str(p.relative_to(ROOT)):sha256(p.read_bytes()).hexdigest() for p in
            (ROOT/'Engine/Runtime/Core/FateStrip.cs',HERE/'有界研究计划.md',Path(__file__),HERE/'独立固定真值复核.py')}
    atomic_json('执行登记.json',{
        'status':'执行中','date':'2026-10-03','source_sha256':hashes,
        'live_session_id':None,'exit_code':None,
        'command':'python3 docs/实验/机制研究/批次/R43-共同隐藏条件与连续反馈/隐藏反馈模型.py',
        'frozen':{'dice':4,'die_distribution':'独立公平d6','target':4,'calm':5,
                  'truth':'开局固定，先验各半；真值0甲技能0乙技能2，真值1交换',
                  'objective':'纯完成概率','actions':'主值快做弱支配稳做，只保留快做0/1/2'},
        'evidence':'数学模型；原生0、真人0；没有真值重抽'})
    names=('free','fixed_method','greedy','highest','best_initial_order')
    totals={n:F(0) for n in names};roots=[];orders=0
    for hand in combinations_with_replacement(range(1,7),4):
        distinct=sorted(set(permutations(hand)));orders+=len(distinct)
        best_order=max(distinct,key=lambda order:solve('ordered',order,4))
        vals={
            'free':solve('free',hand,4), 'fixed_method':fixed(hand,4),
            'greedy':solve('greedy',hand,4),
            'highest':solve('ordered',tuple(reversed(hand)),4),
            'best_initial_order':solve('ordered',best_order,4)}
        weight=F(mult(hand),6**4)
        for n in names:totals[n]+=weight*vals[n]
        roots.append({'hand':hand,'multiplicity':mult(hand),'best_order':best_order,
                      'values':{n:str(v) for n,v in vals.items()},
                      'free_minus_best_order':str(vals['free']-vals['best_initial_order']),
                      'free_minus_greedy':str(vals['free']-vals['greedy'])})
    assert orders==1296 and sum(r['multiplicity'] for r in roots)==1296
    for r in roots:
        v={n:F(x) for n,x in r['values'].items()}
        assert v['free']>=v['greedy'] and v['free']>=v['best_initial_order']>=v['highest']
        assert v['free']>=v['fixed_method']
    local=[]
    beliefs=((1,1),(2,1),(1,2),(3,2),(1,0),(0,1))
    for size in (1,2):
        for dice in combinations_with_replacement(range(1,7),size):
            for need in range(1,5):
                for a,b in beliefs:
                    local.append({'dice':dice,'need':need,'belief':[a,b],
                                  'value':str(solve('free',dice,need,a,b)),
                                  'greedy_value':str(solve('greedy',dice,need,a,b)),
                                  'mirrored_value':str(solve('free',dice,need,b,a)),
                                  'mirrored_greedy_value':str(solve('greedy',dice,need,b,a))})
    gaps={}
    for n in names[1:]:
        strict=[r for r in roots if F(r['values']['free'])>F(r['values'][n])]
        gaps[n]={'strict_hands':len(strict),'strict_hand_mass':str(F(sum(r['multiplicity'] for r in strict),1296)),
                 'average_gap':str(totals['free']-totals[n]),
                 'largest_root_gap':str(max(F(r['values']['free'])-F(r['values'][n]) for r in roots))}
    atomic_json('数学结果.json',{'status':'完成','source_sha256':hashes,'truth_fixed':True,
                'totals':{n:{'fraction':str(v),'probability':float(v)} for n,v in totals.items()},
                'gaps':gaps,'unique_initial_orders':orders,'roots':roots,'local_cases':local,
                'cache':solve.cache_info()._asdict(),'elapsed_seconds':time.time()-start})
    atomic_json('公开策略.json',POLICIES)
    print(json.dumps({'totals':{n:str(v) for n,v in totals.items()},'gaps':gaps,
                      'policy_states':len(POLICIES),'elapsed_seconds':time.time()-start},ensure_ascii=False,indent=2),flush=True)

if __name__=='__main__':main()
