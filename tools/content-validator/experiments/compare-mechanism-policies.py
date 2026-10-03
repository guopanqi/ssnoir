"""同一个有限数学模型内比较固定策略，消除不同策略消耗随机数的样本噪声。

固定策略仍调用 mechanism-study.py 的公开观察策略函数；数学转移使用
mechanism-solver.py，后者必须由 check-mechanism-model.py 与正式轨迹核对。
"""
import argparse
from functools import lru_cache
import json
from pathlib import Path
import runpy
M=runpy.run_path(str(Path(__file__).with_name('mechanism-solver.py')))
H=M['HELPERS']


def evaluate(model,policy):
    def fake_observation(remaining,state,turns,hand):
        clocks=[dict(Label='目标',Current=model.target-remaining,Max=model.target),
                dict(Label='已过回合',Current=model.turns-turns,Max=model.turns)]
        cards=[]
        if model.family=='分段兑现':clocks.append(dict(Label='储备',Current=state,Max=5))
        elif model.family=='余热转用':clocks.append(dict(Label='余热',Current=state,Max=6))
        else:
            mode=state[0]
            cards=[dict(Name='标注：线索',Text='条件是A。' if mode=='K' else '条件未知。'),
                   dict(Name='标注：方案',Text='方案匹配' if mode=='M' else '方案失配' if mode=='F' else '尚未押定')]
        operations=[]
        for action in model.actions(state):
            names=('押定A','押定B') if action=='押定' else (action,)
            for name in names:
                for die in sorted(set(hand)):
                    roll=name in ('备料','强推','借热','探查','执行方案')
                    text=None
                    if roll:
                        faces=[];start=1
                        for prob,label in zip(model.probabilities[die],('坏','中','好')):
                            count=round(prob*6)
                            if count:
                                end=start+count-1
                                faces.append(f'{start} {label}' if start==end else f'{start}–{end} {label}')
                                start=end+1
                        text=' · '.join(faces)
                    operations.append(dict(Kind='action',Card=name,DieValue=die,Odds=text))
        operations.append(dict(Kind='end-turn',Card=None,DieValue=None,Odds=None))
        return dict(Scene='研究·'+model.variant,Version=2 if model.turns-turns or model.target-remaining else 1,
                    Clocks=clocks,Cards=cards,Operations=operations,
                    Actors=[dict(Id='player',Dice=[dict(Value=d) for d in hand])])

    @lru_cache(None)
    def next_hand(r,s,t):
        p,cost=0.0,0.0
        for hand,weight in M['HANDS']:
            pp,cc=v(r,s,t,hand);p+=weight*pp;cost+=weight*cc
        return p,cost

    @lru_cache(None)
    def v(r,s,t,hand):
        if r<=0:return (1.0,0.0)
        if t<=0:return (0.0,0.0)
        o=fake_observation(r,s,t,hand)
        op=H['choose'](o,model.family,policy)
        if op['Kind']=='end-turn':
            st=max(0,s-model.parameters['储备衰减']) if model.family=='分段兑现' else max(0,s-1) if model.family=='余热转用' else s
            pp,cc=next_hand(r,st,t-1) if t>1 else (0.0,0.0)
            return pp,cc-pp
        action='押定' if op['Card'].startswith('押定') else op['Card']
        rest=list(hand);rest.remove(op['DieValue']);rest=tuple(rest)
        p,cost=0.0,0.0
        for weight,new_r,new_s,failed in model.outcomes(action,r,s,op['DieValue']):
            if not weight or failed:continue
            pp,cc=v(new_r,new_s,t,rest);p+=weight*pp;cost+=weight*cc
        return p,cost
    state=('U',0) if model.family=='情报承诺' else 0
    p,t=next_hand(model.target,state,model.turns)
    return dict(success=p,success_rounds=1-t/p if p else None,expected_reward=12*p+3*t,
                states=v.cache_info().currsize)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('output',type=Path);a=parser.parse_args()
    if a.output.exists():raise FileExistsError(a.output)
    probabilities=M['collect_odds'](1);rows=[]
    for family,variant in [('余热转用','余热转用'),('余热转用','余热长目标'),('分段兑现','分段兑现'),('情报承诺','情报承诺')]:
        model=M['Solver'](family,variant,1,probabilities)
        for policy in H['POLICIES'][family]:
            # 一次探查依赖已经尝试过几次。当前有限状态没有这段历史，
            # 因此只在正式会话中评估它，不输出错误的精确概率。
            if policy=='一次探查':continue
            result=evaluate(model,policy);result.update(family=family,variant=variant,policy=policy,growth=1)
            rows.append(result);print(f'{variant} {policy}: P={result["success"]:.6f}, R={result["success_rounds"]}, 收益={result["expected_reward"]:.4f}',flush=True)
    a.output.write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
