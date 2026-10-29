"""读取冻结公开策略，按开局固定真值前向；不修改或导入主模型。"""
from collections import defaultdict
from fractions import Fraction as F
from hashlib import sha256
from math import gcd
from pathlib import Path
import json

HERE=Path(__file__).resolve().parent
ZERO={1:(3,3,0),2:(2,3,1),3:(1,3,2),4:(1,2,3),5:(0,2,4),6:(0,1,5)}
TWO={1:(1,3,2),2:(1,2,3),3:(0,2,4),4:(0,1,5),5:(0,0,6),6:(0,0,6)}

def row(die,method,truth):
    return ZERO[die] if method==truth else TWO[die]

def key(dice,need,a,b):
    return f"greedy|{','.join(map(str,dice))}|{need}|{a}|{b}"

def forward(truth,primary,policies):
    active=defaultdict(F)
    for r in primary['roots']:
        active[(tuple(r['hand']),4,1,1,-1,False,False)]+=F(r['multiplicity'],1296)
    terminal=defaultdict(F);switched=F(0);strict_switched=F(0);expected=F(0)
    event_by_action=defaultdict(F);state_count=0
    for size in range(4,-1,-1):
        next_active=defaultdict(F)
        for (dice,need,a,b,last,ever,ever_strict),mass in active.items():
            assert len(dice)==size
            state_count+=1
            if need<=0:ending='success'
            elif not dice:ending='exhausted'
            elif need>2*len(dice):ending='capacity_impossible'
            else:ending=None
            if ending:
                terminal[(ending,4-size)]+=mass
                if ever:switched+=mass
                if ever_strict:strict_switched+=mass
                continue
            die,method=policies[key(dice,need,a,b)]
            assert method==(1 if a>=b else 0)
            change=last!=-1 and method!=last
            strict=change and ((a>b and method==1) or (b>a and method==0))
            expected+=mass*change
            if change:event_by_action[4-size+1]+=mass
            rest=list(dice);rest.remove(die);rest=tuple(rest)
            for gain,count in enumerate(row(die,method,truth)):
                if not count:continue
                na=a*row(die,method,0)[gain]
                nb=b*row(die,method,1)[gain]
                common=gcd(na,nb)
                assert common>0
                next_active[(rest,need-gain,na//common,nb//common,method,
                             ever or change,ever_strict or strict)]+=mass*F(count,6)
        active=next_active
    assert not active and sum(terminal.values())==1
    successes=sum(mass for (kind,step),mass in terminal.items() if kind=='success')
    return {'fixed_truth':truth,'at_least_one_method_change':str(switched),
            'expected_method_changes':str(expected),
            'at_least_one_strict_posterior_change':str(strict_switched),
            'success':str(successes),'public_history_states':state_count,
            'terminal_by_kind_and_actions':[
                {'kind':kind,'actions':step,'mass':str(mass)}
                for (kind,step),mass in sorted(terminal.items())],
            'method_change_event_expectation_by_action':{
                str(step):str(mass) for step,mass in sorted(event_by_action.items())}}

def main():
    primary=json.loads((HERE/'数学结果.json').read_text())
    policies=json.loads((HERE/'公开策略.json').read_text())
    by_truth=[forward(t,primary,policies) for t in (0,1)]
    fields=('at_least_one_method_change','expected_method_changes',
            'at_least_one_strict_posterior_change','success')
    aggregate={k:str(sum(F(r[k]) for r in by_truth)/2) for k in fields}
    assert F(aggregate['success'])==F(primary['totals']['greedy']['fraction'])
    terminal=defaultdict(F)
    for r in by_truth:
        for term in r['terminal_by_kind_and_actions']:
            terminal[(term['kind'],term['actions'])]+=F(term['mass'])/2
    result={'status':'通过','identity':'冻结后验贪心公开策略的正常前向；非原生／真人',
            'fixed_truth_prior':['1/2','1/2'],
            'policy_ties':'平后验选乙；骰子同值按主脚本的首个最优动作，频率依赖该选定策略',
            'capacity_stop':'公开剩余需求>2×余骰时停止；不把真实隐藏条件下不可能误作公开停止',
            'input_sha256':{p.name:sha256(p.read_bytes()).hexdigest() for p in
                           (HERE/'数学结果.json',HERE/'公开策略.json',Path(__file__))},
            'aggregate':aggregate,'by_fixed_truth':by_truth,
            'aggregate_terminal':[
                {'kind':kind,'actions':step,'mass':str(mass)}
                for (kind,step),mass in sorted(terminal.items())]}
    (HERE/'后验响应前向结果.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(result,ensure_ascii=False,indent=2),flush=True)

if __name__=='__main__':main()
