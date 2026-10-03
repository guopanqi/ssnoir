"""独立誊录命运条，不导入主模型；局部优化＋全初始固定真值策略执行。"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement
from math import gcd
from pathlib import Path
from hashlib import sha256
import json
import time

HERE=Path(__file__).resolve().parent
# 直接誊录正式表各六个命运面，不使用主模型的分布函数或转移函数。
SKILL_ZERO={1:(0,0,0,1,1,1),2:(0,0,1,1,1,2),3:(0,1,1,1,2,2),
            4:(0,1,1,2,2,2),5:(1,1,2,2,2,2),6:(1,2,2,2,2,2)}
SKILL_TWO={1:(0,1,1,1,2,2),2:(0,1,1,2,2,2),3:(1,1,2,2,2,2),
           4:(1,2,2,2,2,2),5:(2,2,2,2,2,2),6:(2,2,2,2,2,2)}

def faces(die,method,truth):
    return SKILL_ZERO[die] if method==truth else SKILL_TWO[die]

def norm(a,b):
    assert a+b>0
    g=gcd(a,b);return a//g,b//g

def likelihood(die,method,gain):
    return tuple(faces(die,method,t).count(gain) for t in (0,1))

@lru_cache(None)
def independent_tree(dice,need,a,b,family='free'):
    # 未归一化先验各分量乘固定真值下的命运序列计数。
    # 终端补足未消费命运面，因此始终对应原固定长度的完整命运序列。
    if need<=0:return (a+b)*6**len(dice)
    if not dice:return 0
    best=0
    for die in set(dice):
        rest=list(dice);rest.remove(die);rest=tuple(rest)
        methods=(0,1) if family=='free' else ((1,) if a>=b else (0,))
        for method in methods:
            total=0
            for gain in (0,1,2):
                c0,c1=likelihood(die,method,gain)
                if a*c0+b*c1:
                    total+=independent_tree(rest,need-gain,a*c0,b*c1,family)
            best=max(best,total)
    return best

def policy_key(family,dice,need,a,b):
    return f"{family}|{','.join(map(str,dice))}|{need}|{a}|{b}"

def fixed_truth_count(name,dice,need,a,b,truth,policies,visited):
    # 真值在整棵树保持不变；六个命运面逐面展开，绝不重抽条件。
    if need<=0:return 6**len(dice)
    if not dice or need>2*len(dice):return 0
    family='ordered' if name in ('highest','best_initial_order') else name
    if name=='fixed_method':die,method=dice[0],0
    else:die,method=policies[policy_key(family,dice,need,a,b)]
    visited.add((family,dice,need,a,b,truth))
    rest=list(dice);rest.remove(die);rest=tuple(rest)
    count=0
    for gain in faces(die,method,truth):
        c0,c1=likelihood(die,method,gain)
        na,nb=norm(a*c0,b*c1)
        count+=fixed_truth_count(name,rest,need-gain,na,nb,truth,policies,visited)
    return count

def main():
    start=time.time()
    primary=json.loads((HERE/'数学结果.json').read_text())
    policies=json.loads((HERE/'公开策略.json').read_text())
    checked=0;mirrored=0;greedy_different=0;ties_checked=0
    for case in primary['local_cases']:
        dice=tuple(case['dice']);a,b=case['belief'];need=case['need']
        own=F(independent_tree(dice,need,a,b),(a+b)*6**len(dice))
        assert own==F(case['value']),(case,str(own))
        greedy=F(independent_tree(dice,need,a,b,'greedy'),(a+b)*6**len(dice))
        assert greedy==F(case['greedy_value'])
        mirror=F(independent_tree(dice,need,b,a),(a+b)*6**len(dice))
        mirror_greedy=F(independent_tree(dice,need,b,a,'greedy'),(a+b)*6**len(dice))
        assert mirror==own==F(case['mirrored_value'])
        assert mirror_greedy==greedy==F(case['mirrored_greedy_value'])
        mirrored+=1;greedy_different+=greedy!=own
        if a==b:
            for die in set(dice):
                rest=list(dice);rest.remove(die);rest=tuple(rest)
                qs=[]
                for method in (0,1):
                    total=0
                    for gain in (0,1,2):
                        c0,c1=likelihood(die,method,gain)
                        if a*c0+b*c1:
                            total+=independent_tree(rest,need-gain,a*c0,b*c1,'greedy')
                    qs.append(total)
                assert qs[0]==qs[1]
                ties_checked+=1
        checked+=1
    posterior_checks=[]
    # 甲用1，真值0技能0、真值1技能2；逐面数反推后验。
    for gain in (0,1,2):
        c0,c1=likelihood(1,0,gain)
        posterior_checks.append({'die':1,'method':'甲','gain':gain,
            'probability':str(F(c0+c1,12)),'posterior_truth0':str(F(c0,c0+c1))})
    assert [x['posterior_truth0'] for x in posterior_checks]==['3/4','1/2','0']
    totals={n:F(0) for n in primary['totals']};visited=set();root_checks=[]
    for r in primary['roots']:
        for name in totals:
            hand=tuple(r['hand'])
            if name=='highest':dice=tuple(reversed(hand))
            elif name=='best_initial_order':dice=tuple(r['best_order'])
            else:dice=hand
            counts=[fixed_truth_count(name,dice,4,1,1,t,policies,visited) for t in (0,1)]
            own=F(sum(counts),2*6**4)
            assert own==F(r['values'][name]),(name,r['hand'],str(own),r['values'][name])
            totals[name]+=F(r['multiplicity'],6**4)*own
            root_checks.append({'hand':hand,'family':name,'success_counts_by_fixed_truth':counts,
                                'value':str(own)})
    assert all(totals[n]==F(primary['totals'][n]['fraction']) for n in totals)
    result={'status':'通过','identity':'局部独立最优树＋全初始固定真值执行公开策略；非独立全局优化',
            'local_optimal_cases':checked,'local_max_remaining_dice':2,
            'mirrored_posterior_cases':mirrored,'equal_posterior_method_ties':ties_checked,
            'local_free_greedy_different_cases':greedy_different,
            'posterior_checks':posterior_checks,'fixed_truth_root_family_checks':len(root_checks),
            'conceptual_complete_fate_sequences':len(root_checks)*2*6**4,
            'visited_public_states_with_truth':len(visited),
            'totals':{n:str(v) for n,v in totals.items()},'root_checks':root_checks,
            'input_sha256':{p.name:sha256(p.read_bytes()).hexdigest() for p in
                           (HERE/'数学结果.json',HERE/'公开策略.json',Path(__file__))},
            'elapsed_seconds':time.time()-start}
    (HERE/'独立复核结果.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:v for k,v in result.items() if k not in ('root_checks','input_sha256')},
                     ensure_ascii=False,indent=2),flush=True)

if __name__=='__main__':main()
