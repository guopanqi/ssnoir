"""独立策略全根执行与独立局部自由树接合，身份不混同。"""
from pathlib import Path
from fractions import Fraction as F
from collections import defaultdict
import json,hashlib
import 储备判据 as m
HERE=Path(__file__).resolve().parent
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    audit=json.loads((HERE/'审查储备结果.json').read_text());out=json.loads((HERE/'储备结果.json').read_text())
    assert audit['status']=='passed' and audit['source_sha256']==sha(HERE/'审查储备枚举.py')
    components=0
    for skill,rows in audit['literal_fate'].items():
        for d,faces in rows.items():
            for g in range(3):assert m.rules.odds(int(d),-1 if int(skill)==1 else 0)[g]==F(faces.count(g),6);components+=1
    pols={k:m.Policy(k) for k in ('top2','all_reserves')};ref=m.rules.Model();values=choices=roots=0
    for row in audit['cases']:
        a,b=row['needs'];h=tuple(row['dice'])
        assert max(m.base.lock_values(a,b,h))==F(row['L']);values+=1
        assert m.rules.score(ref.solve(a,b,h))==F(row['free']);values+=1
        for family,p in pols.items():
            assert m.rules.score(p.solve(a,b,h))==F(row[family]);values+=1
            ch=row[family+'_choice'];expected=('commit',ch['pick']) if ch['kind']=='lock' else ('act',ch['pick'],1,True)
            assert p.choices[a,b,h,-1]==expected;choices+=1
            dec=p.decisions[a,b,h]
            if ch['kind']=='advance':assert dec['plan']['reserve']==tuple(ch['reserve']) and F(dec['G'])==F(ch['estimate'])
            else:assert F(dec['L'])==F(ch['estimate'])
            values+=1
    for row in audit['initial_strategy_execution']:
        h=tuple(row['dice']);assert m.rules.weight(h)==F(row['weight'])
        for family,result in zip(pols,out['results']):
            r=next(r for r in result['perhand'] if tuple(r['hand'])==h);ind=row[family]
            assert F(r['value'])==F(ind['value'])
            assert tuple(map(F,r['distribution']))==tuple(map(F,ind['distribution']));roots+=3
            c=ind['choice'];expected=('commit',c['pick']) if c['kind']=='lock' else ('act',c['pick'],1,True)
            assert tuple(r['initial_action'])==expected
    for family,result in zip(pols,out['results']):
        assert F(result['value'])==F(audit['initial_strategy_totals'][family]['value'])
        assert result['normal_forward']['distribution']==result['distribution']
        assert sum(map(F,result['normal_forward']['commit_timing'].values()))==1
    paths=[[(v['remaining'],v['dice'],v['mass'],v['action']) for v in r['normal_forward']['visits']] for r in out['results']]
    assert paths[0]==paths[1]
    witness=audit['normal_root_2245'];h=(2,2,4,5)
    assert F(witness['free'])==m.rules.score(ref.solve(3,6,h))==F(91,54)
    assert max(m.base.lock_values(3,6,h))==F(44,27)
    ps=m.plans(3,6,h,'all_reserves');assert max(F(p['G']) for p in ps)==F(13,9)
    contrib={0:F(1)}
    for d in (2,2):
        nxt=defaultdict(F)
        for x,p in contrib.items():
            for g,q in enumerate(m.rules.odds(d,-1)):nxt[x+g]+=p*q
        contrib=nxt
    finite=sum((mass*max(m.base.lock_values(3,6-x,(4,5))) for x,mass in contrib.items()),F(0))
    assert finite==F(91,54)
    for g,ind in enumerate(witness['first_2_risk_B_branches']):
        assert F(ind['free'])==m.rules.score(ref.solve(3,6-g,(2,4,5)))
        assert F(ind['L'])==max(m.base.lock_values(3,6-g,(2,4,5)))
    integrated=dict(batch='R47',passed=True,independent_local_conditions=len(audit['cases']),matched_local_values=values,exact_local_policy_decisions=choices,
                    independent_initial_strategy_hands=len(audit['initial_strategy_execution']),matched_initial_distribution_components=roots,probability_components=components,
                    selected_normal_policy_paths_equal=True,
                    single_normal_root=dict(hand=h,weight=str(m.rules.weight(h)),free=str(F(91,54)),L=str(F(44,27)),guaranteed_G=str(F(13,9)),finite_probability_plan=str(finite),spare_contribution={str(k):str(v) for k,v in contrib.items()}),
                    input_sha256={n:sha(HERE/n) for n in ('储备结果.json','审查储备结果.json')},
                    scope='独立1386局部自由树与126根明确策略执行；非126根独立自由优化。正常2245固定有限计划非全126概率判据。正式0真人0')
    (HERE/'整合核对.json').write_text(json.dumps(integrated,ensure_ascii=False,indent=2)+'\n');print(json.dumps(integrated,ensure_ascii=False))
if __name__=='__main__':main()
