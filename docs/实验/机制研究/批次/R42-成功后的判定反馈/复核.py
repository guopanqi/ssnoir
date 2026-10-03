"""将独立两骰枚举对到主模型；初始族、支配与选定最优前缀核对。"""
from pathlib import Path
from fractions import Fraction as F
import runpy,json,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
K=runpy.run_path(str(HERE/'反馈模型.py'));Model=K['Model']
def main():
    independent=json.loads((HERE/'审查两骰结果.json').read_text())
    assert hashlib.sha256((ROOT/independent['fate_source']).read_bytes()).hexdigest()==independent['fate_source_sha256']
    assert hashlib.sha256((HERE/'审查两骰枚举.py').read_bytes()).hexdigest()==independent['script_sha256']
    free=Model();ordered=Model('ordered');count=0
    for row in independent['comparisons']:
        a,b=row['dice'];left,alert=row['remaining'],row['alarm']
        low,high=F(row['low_first']),F(row['high_first'])
        assert free.solve(left,alert,(a,b))==max(low,high)
        assert ordered.solve(left,alert,(a,b))==low
        assert ordered.solve(left,alert,(b,a))==high
        count+=3
    data=json.loads((HERE/'反馈结果.json').read_text());rows=data['results'];f,low,high,fixed,plain,risk=rows
    for i,x in enumerate(f['perhand']):
        assert all(x['hand']==y['perhand'][i]['hand'] for y in rows)
        assert F(x['exact'])==F(risk['perhand'][i]['exact'])
        assert max(F(low['perhand'][i]['exact']),F(high['perhand'][i]['exact']))<=F(fixed['perhand'][i]['exact'])<=F(x['exact'])
        assert F(x['exact'])<=F(plain['perhand'][i]['exact'])
    # 明确取全快做最优策略，其平手按骰点升序；不是注入许可状态。
    policy=Model(risky_only=True);root=(4,0,(1,2,3,4));policy.solve(*root)
    assert policy.choices[root]==(1,True)
    bad_parent=(4,0,(2,3,4));policy.solve(*bad_parent)
    assert policy.choices[bad_parent]==(2,True)
    witnesses=[]
    for grade,conditional in ((0,F(1,18)),(1,F(1,6))):
        state=(4-grade,0,(3,4));policy.solve(*state)
        assert policy.choices[state]==(3 if grade==0 else 4,True)
        witnesses.append(dict(initial_hand=[1,2,3,4],initial_weight='1/54',prefix=[dict(die=1,grade='坏'),dict(die=2,grade='坏' if grade==0 else '中')],
                              remaining=4-grade,alarm=0,hand=[3,4],cold=3,selected_die=policy.choices[state][0],
                              conditional_prefix_mass=str(conditional),unconditional_prefix_mass=str(conditional/54),
                              low_first=str(policy.action(*state,3,True)),high_first=str(policy.action(*state,4,True))))
    out=dict(batch='R42',passed=True,independent_two_die_conditions=168,independent_branches=12096,model_comparisons=count,
             root_hands_checked=126,risky_only_root_exact_equal=True,family_containment=True,selected_optimal_prefixes=witnesses,
             scope='独立两骰枚举与共享主模型逐项核对；前缀属于指定全快做最优策略，质量只计指定初始手和两条历史，不是全局反转频率；非独立四骰全优化；正式0真人0')
    (HERE/'复核.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
if __name__=='__main__':main()
