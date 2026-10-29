"""核对独立局部判据、单一四骰见证与冻结输入；不冒称独立全根。"""
from pathlib import Path
from fractions import Fraction as F
import json,hashlib
import 实时判据 as m
HERE=Path(__file__).resolve().parent
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    audit=json.loads((HERE/'审查两骰结果.json').read_text());out=json.loads((HERE/'判据结果.json').read_text())
    assert audit['status']=='passed' and audit['source_sha256']==sha(HERE/'审查两骰枚举.py')
    components=0
    for skill,rows in audit['literal_fate'].items():
        for d,faces in rows.items():
            for g in range(3):assert m.rules.odds(int(d),-1 if int(skill)==1 else 0)[g]==F(faces.count(g),6);components+=1
    pols={k:m.Policy(k) for k in ('one_step_all','lowest_B')};matched=decisions=0
    for row in audit['cases']:
        a,b=row['needs'];h=tuple(row['dice']);assert max(m.lock_values(a,b,h))==F(row['L']);matched+=1
        for family,field,qfield,decisionfield in [('one_step_all','rolling','best_Q','rolling_decision'),('lowest_B','lowest','lowest_Q','lowest_decision')]:
            p=pols[family];v=m.rules.score(p.solve(a,b,h));assert v==F(row[field]);matched+=1
            assert F(p.decisions[a,b,h]['Q'])==F(row[qfield]);matched+=1
            kind,chosen=row[decisionfield]
            expected=('commit',chosen) if kind=='lock' else ('act',chosen[0],chosen[1],chosen[2]=='risk')
            assert p.choices[a,b,h,-1]==expected;decisions+=1
    ref=m.rules.Model();canonical=audit['single_normal_four_dice_witness'];h=tuple(canonical['dice'])
    free=ref.solve(3,6,h);assert m.rules.score(free)==F(canonical['free'])
    assert ref.choices[3,6,h,-1]==('act',2,1,True)
    for family,field in [('one_step_all','rolling'),('lowest_B','lowest')]:assert m.rules.score(pols[family].solve(3,6,h))==F(canonical[field])
    branches=[]
    for g,p in enumerate(m.rules.odds(2,-1)):
        need=(3,6-g);rest=(2,5,5);fv=m.rules.score(ref.solve(*need,rest));now=max(m.lock_values(*need,rest))
        local=pols['one_step_all'];local.solve(*need,rest);decision=local.decisions[*need,rest]
        assert fv==F(canonical['first_2_risk_B_branches'][g]['free'])
        branches.append(dict(grade=g,remaining=need,dice=rest,conditional_mass=str(p),joint_mass=str(m.rules.weight(h)*p),
                             free=str(fv),L=str(now),Q=decision['Q'],local_choice=local.choices[*need,rest,-1]))
    for row in out['results']:
        assert row['normal_forward']['distribution']==row['distribution']
        assert sum(map(F,row['normal_forward']['commit_timing'].values()))==1
    integrated=dict(batch='R46',passed=True,independent_local_conditions=len(audit['cases']),matched_values=matched,
                    exact_policy_decisions=decisions,probability_components=components,
                    single_normal_root=dict(hand=h,weight=str(m.rules.weight(h)),L=canonical['initial_lock'],Q=canonical['max_Q'],free=canonical['free'],branches=branches,
                                            reserve_direct_36_cases_equal=True),
                    input_sha256={n:sha(HERE/n) for n in ('判据结果.json','审查两骰结果.json')},
                    scope='独立486局部及一个2255四骰根；非126根独立全局优化。正常前向共享策略，正式0真人0')
    (HERE/'整合核对.json').write_text(json.dumps(integrated,ensure_ascii=False,indent=2)+'\n');print(json.dumps(integrated,ensure_ascii=False))
if __name__=='__main__':main()
