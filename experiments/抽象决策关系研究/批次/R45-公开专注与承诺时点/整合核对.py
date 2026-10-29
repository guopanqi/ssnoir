"""独立局部与父模型逐项核对；实际可达与宽许可域分开。"""
from pathlib import Path
from fractions import Fraction as F
import json,hashlib
import 专注模型 as m
HERE=Path(__file__).resolve().parent
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    local=json.loads((HERE/'审查两骰结果.json').read_text());main=json.loads((HERE/'数学结果.json').read_text());forward=json.loads((HERE/'承诺前向结果.json').read_text())
    assert local['status']=='passed' and local['source_sha256']==sha(HERE/'审查两骰枚举.py')
    assert main['source_sha256']==sha(m.SOURCE)
    probabilities=0
    for skill,rows in local['literal_fate'].items():
        for d,faces in rows.items():
            for grade in range(3):assert m.odds(int(d),-1 if int(skill)==1 else 0)[grade]==F(faces.count(grade),6);probabilities+=1
    models={(f,r,n):m.Model(f,r,n) for f,r in [('free',False),('never',False),('initial',False),('one_feedback',False),('free',True)] for n in (1,2,4)}
    values=0
    for row in local['cases']:
        a,b=row['needs'];h=tuple(row['dice'])
        for field,f,r in [('free','free',False),('never','never',False),('initial_lock','initial',False),('first_feedback','one_feedback',False),('free_risk_only','free',True)]:
            assert m.score(models[f,r,len(h)].solve(a,b,h))==F(row[field]),(row,field);values+=1
    for row in main['results']:
        dist=tuple(map(F,row['distribution']));assert sum(dist)==1 and m.score(dist)==F(row['value'])
        for base,control in zip(main['results'][0]['perhand'],row['perhand']):assert F(base['value'])>=F(control['value'])
    assert forward['terminal_distribution']==main['results'][0]['distribution'] and forward['value']==main['results'][0]['value']
    assert sum(map(F,forward['commit_timing'].values()))==1
    assert F(forward['delayed_commit_probability'])==sum((F(v) for k,v in forward['commit_timing'].items() if k.isdigit() and int(k)>0),F(0))
    # Every normally visited unlocked witness has the necessary remaining-capacity bound.
    for row in forward['strict_delay_witnesses']:
        a,b=row['remaining'];n=len(row['dice']);assert a+b>=2*n+1
    ex=models['free',False,4];h=(1,1,1,3);assert ex.solve(3,6,h)==m.Model('free').solve(3,6,h)
    assert ex.choices[3,6,h,-1]==('act',1,1,True)
    branches=[]
    for g,p in enumerate(m.odds(1,-1)):
        nxt=(3,6-g,(1,1,3),-1);v=ex.solve(*nxt);ch=ex.choices[nxt]
        assert ch==('commit',0 if g<2 else 1)
        branches.append(dict(grade=g,conditional_mass=str(p),initial_hand_joint_mass=str(m.weight(h)*p),next_action=ch,value=str(m.score(v))))
    out=dict(batch='R45',passed=True,independent_conditions=len(local['cases']),matched_values=values,probability_components=probabilities,
             witness=dict(hand=h,initial_mass=str(m.weight(h)),first_action=('act',1,1,True),branches=branches),
             forward_matches_root=True,input_sha256={n:sha(HERE/n) for n in ('数学结果.json','审查两骰结果.json','承诺前向结果.json')},
             scope='独立仅局部；正常前向共享主模型；保留未锁双动作及即时终止；正式0真人0')
    (HERE/'整合核对.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
if __name__=='__main__':main()
