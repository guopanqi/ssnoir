"""独立小树与主模型接合；不将共享模型重算称第二份独立优化。"""
from pathlib import Path
from fractions import Fraction as F
from itertools import combinations_with_replacement
import json,hashlib
import 成套模型 as m
HERE=Path(__file__).resolve().parent
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    independent=json.loads((HERE/'审查两骰结果.json').read_text());results=json.loads((HERE/'数学结果.json').read_text())
    assert independent['status']=='passed'
    assert results['source_sha256']==sha(m.SOURCE)
    assert independent['source_sha256']==sha(HERE/'审查两骰枚举.py')
    probability_checks=0
    for die,faces in independent['literal_fate'].items():
        for grade in range(3):assert m.ODDS[int(die)][grade]==F(faces.count(grade),6);probability_checks+=1
    models={(b,f):m.Model(b,f) for b in (2,3) for f in ('free','shortest','fixed_A','fixed_B')}
    values=partitions=0
    for row in independent['cases']:
        a,b=row['needs'];h=tuple(row['dice']);bonus=row['bundle_value']
        for field,family in [('free','free'),('short','shortest')]:
            assert m.score(models[bonus,family].solve(a,b,h),bonus)==F(row[field]),row;values+=1
        fixed=max(m.score(models[bonus,f].solve(a,b,h),bonus) for f in ('fixed_A','fixed_B'))
        assert fixed==F(row['best_fixed_target_order']);values+=1
        if a==b==3:assert m.partitions(h,bonus)[0][0]==F(row['partition']);partitions+=1
    root_checks=0
    for bonus in (2,3):
        rows=[r for r in results['results'] if r['both_value']==bonus]
        free=next(r for r in rows if r['family']=='free' and not r['both_actions'])
        for row in rows:
            dist=tuple(map(F,row['distribution']));assert sum(dist)==1
            assert m.score(dist,bonus)==F(row['value'])
        for i,h in enumerate(m.HANDS):
            v=F(free['perhand'][i]['value'])
            for family in ('fixed_A','fixed_B','shortest'):
                row=next(r for r in rows if r['family']==family)
                assert F(row['perhand'][i]['value'])==v;root_checks+=1
        same_score=next(c for c in results['cross_scoring'] if c['scoring_both_value']==bonus and c['policy_both_value']!=bonus)
        assert F(same_score['value'])==F(free['value'])
    out=dict(batch='R44',passed=True,independent_local_conditions=len(independent['cases']),matched_values=values,
             independent_partition_matches=partitions,probability_components=probability_checks,root_strong_family_values=root_checks,
             cross_scoring_compatible=True,input_sha256={name:sha(HERE/name) for name in ('数学结果.json','审查两骰结果.json')},
             scope='864独立局部条件与主模型对照；不是独立四骰全局优化。终局效用、族内优化骰序，正式0真人0')
    (HERE/'整合核对.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
if __name__=='__main__':main()
