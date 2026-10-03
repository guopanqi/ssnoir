"""第二轮机制研究：正式会话、有限求解与自然可达状态的占用概率。

analyze 枚举；play 只用公开观察执行；verify 核对新原型转移并严格回放。
状态占用及策略偏离概率是模型证据，不能当作玩家体验分数。
"""
import argparse
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor
from functools import lru_cache
import gzip
import hashlib
import json
from pathlib import Path
import re
import runpy
import subprocess
import tempfile

M=runpy.run_path(str(Path(__file__).with_name('mechanism-solver.py')))
H=M['HELPERS'];ROOT=H['ROOT'];DLL=H['DLL'];Solver=M['Solver']
VARIANTS={'整备借力':['整备借力','借力短目标','借力紧目标','借力长目标','借力无准备'],
          '稳险选择':['稳险选择','稳险短目标','稳险长目标'],
          '分段兑现':['分段兑现'],'余热转用':['余热长目标']}
POLICIES={'整备借力':['直接推进','固定配对','按手牌配对','逐步收益','当轮规划'],
          '稳险选择':['一直稳进','一直冒进','按骰面收益','剩余目标','当轮规划'],
          '分段兑现':['满5'],'余热转用':['余热前瞻']}
BENCHMARK={'整备借力':'当轮规划','稳险选择':'按骰面收益','分段兑现':'满5','余热转用':'余热前瞻'}


def family_of(variant):
    return next(f for f,variants in VARIANTS.items() if variant in variants)


def prepared_odds(variant,growth):
    found={}
    for seed in range(1,31):
        session=H['Session']('研究·'+variant,seed,growth)
        try:
            op=min((a for a in session.observation['Operations'] if a['Card']=='铺垫'),key=lambda a:a['DieValue'])
            session.act(op,'赔率采样：读取准备后的公开命运条，不采样判定结果。')
            for a in session.observation['Operations']:
                if a['Card']=='推进':
                    p=H['odds'](a['Odds']);found[a['DieValue']]=(p['坏'],p['中'],p['好'])
        finally:session.close()
        if len(found)==6:return found
    raise RuntimeError('准备命运条未覆盖六种骰面')


def make_solver(variant,growth,probabilities,boosted,objective='保守'):
    model=Solver(family_of(variant),variant,growth,probabilities,objective)
    if model.family=='整备借力':model.boosted_probabilities=boosted
    return model


def public_state(o,family):
    c={x['Label']:x for x in o['Clocks']}
    key={'整备借力':'准备','分段兑现':'储备','余热转用':'余热'}.get(family)
    return (c['目标']['Max']-c['目标']['Current'],c[key]['Current'] if key else 0,
            c['已过回合']['Max']-c['已过回合']['Current'],
            tuple(sorted(d['Value'] for a in o['Actors'] if a['Id']=='player' for d in a['Dice'])))


def observation(model,r,s,t,hand):
    clocks=[dict(Label='目标',Current=model.target-r,Max=model.target),
            dict(Label='已过回合',Current=model.turns-t,Max=model.turns)]
    key={'整备借力':'准备','分段兑现':'储备','余热转用':'余热'}.get(model.family)
    if key:clocks.append(dict(Label=key,Current=s,Max=1 if key=='准备' else 5 if key=='储备' else 6))
    operations=[]
    for action in model.actions(s):
        for die in sorted(set(hand)):
            p=model.boosted_probabilities[die] if model.family=='整备借力' and s and action=='推进' else model.probabilities[die]
            roll=action not in ('铺垫','兑现') and not (action=='稳进' and model.family=='分段兑现')
            first=1;faces=[]
            for weight,label in zip(p,('坏','中','好')):
                count=round(weight*6)
                if count:
                    last=first+count-1;faces.append(f'{first} {label}' if first==last else f'{first}–{last} {label}');first=last+1
            operations.append(dict(Kind='action',Card=action,DieValue=die,Odds=' · '.join(faces) if roll else None))
    operations.append(dict(Kind='end-turn',Card=None,DieValue=None,Odds=None))
    return dict(Scene='研究·'+model.variant,Version=1,Clocks=clocks,Cards=[],Operations=operations,
                Actors=[dict(Id='player',Dice=[dict(Value=d) for d in hand])])


@lru_cache(None)
def local_plan(model,r,s,hand):
    """只优化这一轮的期望进度，不读取下一轮手牌。"""
    if r<=0 or not hand:return (0.0,('end-turn',None))
    best=(-1.0,None)
    for die in sorted(set(hand)):
        rest=list(hand);rest.remove(die);rest=tuple(rest)
        for action in model.actions(s):
            value=0.0
            for p,new_r,new_s,failed in model.outcomes(action,r,s,die):
                if failed:continue
                gain=r-max(0,new_r)
                future=local_plan(model,new_r,new_s,rest)[0]
                value+=p*(gain+future)
            if value>best[0]+1e-12:best=(value,(action,die))
    return best


def choose(model,o,policy):
    assert policy in POLICIES[model.family]+['状态求解'],(model.family,policy)
    r,s,t,hand=public_state(o,model.family)
    if policy=='状态求解':
        model.value(r,s,t,hand);return model.decisions[(r,s,t,hand)]
    if not hand:return ('end-turn',None)
    if model.family in ('分段兑现','余热转用'):
        op=H['choose'](o,model.family,policy);return (op['Card'] or op['Kind'],op['DieValue'])
    if policy=='当轮规划':return local_plan(model,r,s,hand)[1]
    if model.family=='整备借力':
        if policy=='直接推进':return ('推进',max(hand))
        if r<=1:return ('铺垫',min(hand))
        if policy=='固定配对':return ('推进',max(hand)) if s else ('铺垫',min(hand))
        if policy=='按手牌配对':
            if s:
                sure=[d for d in hand if model.boosted_probabilities[d][2]>=1-1e-12]
                return ('推进',min(sure) if sure else max(hand))
            if max(hand)>=5 or len(hand)==1:return ('推进',max(hand))
            return ('铺垫',min(hand)) if min(hand)<=2 else ('推进',max(hand))
    if model.family=='稳险选择':
        if policy=='一直稳进':return ('稳进',max(hand))
        if policy=='一直冒进':return ('冒进',max(hand))
    # 按骰面收益不裁切超额进度；其他贪心策略按当前剩余目标裁切。
    cap=100 if policy=='按骰面收益' else r
    choices=[]
    for action in model.actions(s):
        for die in sorted(set(hand),reverse=True):
            value=sum(p*min(cap,r-new_r) for p,new_r,st,failed in model.outcomes(action,r,s,die) if not failed)
            choices.append((value,action,die))
    _,action,die=max(choices,key=lambda x:x[0]);return action,die


def q_value(model,r,s,t,hand,action,die):
    if action=='end-turn':
        p,time=model.next_hand(r,model.end_state(s),t-1) if t>1 else (0,0)
        return p,time-p
    rest=list(hand);rest.remove(die);rest=tuple(rest);p=time=0.0
    for w,nr,ns,failed in model.outcomes(action,r,s,die):
        if failed:continue
        pp,tt=model.value(nr,ns,t,rest);p+=w*pp;time+=w*tt
    return p,time


def fixed_value(model,policy):
    @lru_cache(None)
    def next_hand(r,s,t):
        p=time=0.0
        for hand,w in M['HANDS']:
            pp,tt=value(r,s,t,hand);p+=w*pp;time+=w*tt
        return p,time
    @lru_cache(None)
    def value(r,s,t,hand):
        if r<=0:return 1.0,0.0
        if t<=0:return 0.0,0.0
        action,die=choose(model,observation(model,r,s,t,hand),policy)
        if action=='end-turn':
            p,time=next_hand(r,model.end_state(s),t-1) if t>1 else (0,0)
            return p,time-p
        rest=list(hand);rest.remove(die);rest=tuple(rest);p=time=0.0
        for w,nr,ns,failed in model.outcomes(action,r,s,die):
            if failed:continue
            pp,tt=value(nr,ns,t,rest);p+=w*pp;time+=w*tt
        return p,time
    p,time=next_hand(model.target,0,model.turns)
    return dict(policy=policy,success=p,success_rounds=1-time/p if p else None)


def occupancy(model,benchmark):
    """精确传播所选策略的到达概率；每条轨迹同时记录三档偏离损失标记。"""
    layers=defaultdict(lambda:defaultdict(lambda:defaultdict(float)))
    for hand,w in M['HANDS']:layers[(model.turns,4)][(model.target,0,hand)][0]+=w
    terminal=defaultdict(float);visits=defaultdict(float);actions=defaultdict(float);examples=defaultdict(dict)
    contexts=defaultdict(dict);decisions=disagreements=0.0
    def send(r,s,t,hand,mask,mass,failed=False):
        if failed or r<=0 or t<=0:terminal[(r<=0 and not failed,mask)]+=mass
        else:layers[(t,len(hand))][(r,s,hand)][mask]+=mass
    for t in range(model.turns,0,-1):
        for size in range(4,-1,-1):
            for (r,s,hand),masks in list(layers[(t,size)].items()):
                mass=sum(masks.values());state=(r,s,t,hand);visits[state]+=mass
                model.value(*state);action,die=model.decisions[state]
                best=model.value(*state)[0]
                other=choose(model,observation(model,*state),benchmark)
                regret=max(0,best-q_value(model,*state,*other)[0])
                bits=sum(1<<i for i,threshold in enumerate((.001,.01,.05)) if regret>=threshold-1e-12)
                if hand:
                    decisions+=mass;disagreements+=mass*((action,die)!=other);actions[action]+=mass
                    by_action={}
                    for a in model.actions(s):
                        candidates=[(*q_value(model,*state,a,d),d) for d in sorted(set(hand))]
                        by_action[a]=max(candidates,key=lambda x:(x[0],x[1]))
                    alternatives=[x[0] for a,x in by_action.items() if a!=action]
                    if action!='end-turn' and alternatives and best-max(alternatives)>=.01-1e-12 and best>=.2:
                        item=dict(hand=hand,action=action,die=die,success=best,visit_mass=mass,
                                  alternatives={a:dict(success=v[0],die=v[2]) for a,v in by_action.items()})
                        key=(r,s,t,size)
                        if mass>examples[key].get(action,{}).get('visit_mass',-1):examples[key][action]=item
                        # 同一手牌、同一回合，目标距离改变导致严格的动作反转。
                        # 排除只差几格时的直接收尾，用来排查固定按骰面匹配。
                        if r>5:
                            context_key=(s,t,hand)
                            context_item=dict(item,remaining=r)
                            if mass>contexts[context_key].get(action,{}).get('visit_mass',-1):
                                contexts[context_key][action]=context_item
                for mask,w in masks.items():
                    new_mask=mask|bits
                    if action=='end-turn':
                        if t==1:send(r,model.end_state(s),0,(),new_mask,w)
                        else:
                            for next_dice,prob in M['HANDS']:send(r,model.end_state(s),t-1,next_dice,new_mask,w*prob)
                    else:
                        rest=list(hand);rest.remove(die);rest=tuple(rest)
                        for prob,nr,ns,failed in model.outcomes(action,r,s,die):send(nr,ns,t,rest,new_mask,w*prob,failed)
    assert abs(sum(terminal.values())-1)<1e-9
    assert abs(sum(w for (success,mask),w in terminal.items() if success)-model.initial()[0])<1e-9
    pairs=[dict(remaining=r,mechanism_state=s,turns_left=t,hand_size=size,choices=list(options.values()))
           for (r,s,t,size),options in examples.items() if len(options)>1]
    pairs.sort(key=lambda item:min(x['visit_mass'] for x in item['choices']),reverse=True)
    context_pairs=[dict(mechanism_state=s,turns_left=t,hand=hand,choices=list(options.values()))
                   for (s,t,hand),options in contexts.items() if len(options)>1]
    context_pairs.sort(key=lambda item:min(x['visit_mass'] for x in item['choices']),reverse=True)
    return dict(benchmark=benchmark,expected_decisions=decisions,expected_different_choices=disagreements,
                episode_has_probability_regret={str(threshold):sum(w for (success,mask),w in terminal.items() if mask&(1<<i))
                                                for i,threshold in enumerate((.001,.01,.05))},
                expected_actions=dict(actions),visited_states=len(visits),reachable_reversals=pairs[:6],
                context_reversals=context_pairs[:6])


def analyze(args):
    rows=[];odds={}
    for growth in args.growth:
        ordinary=M['collect_odds'](growth);odds[str(growth)]={'ordinary':ordinary,'prepared':{}}
        for variant in args.variants:
            f=family_of(variant);boost=prepared_odds(variant,growth) if f=='整备借力' else None
            if boost:odds[str(growth)]['prepared'][variant]=boost
            model=make_solver(variant,growth,ordinary,boost,args.objective);p,time=model.initial()
            row=dict(variant=variant,family=f,growth=growth,parameters=model.parameters,
                     success=p,success_rounds=1-time/p,expected_reward=12*p+3*time,objective=args.objective)
            row['fixed']=[fixed_value(model,policy) for policy in POLICIES[f]]
            if args.objective=='保守' and variant in ('分段兑现','余热长目标','整备借力','稳险选择'):
                row['occupancy']=occupancy(model,BENCHMARK[f])
            rows.append(row);print(f'{variant} g{growth}: P={p:.6f}, R={row["success_rounds"]:.4f}',flush=True)
            (args.output/'分析.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
            model.value.cache_clear();model.next_hand.cache_clear();local_plan.cache_clear()
    (args.output/'赔率.json').write_text(json.dumps(odds,ensure_ascii=False,indent=2)+'\n')


def sweep(args):
    """只改变模型目标，不增加或改写正式 Content；输出明确标记这个边界。"""
    rows=[]
    for growth in args.growth:
        ordinary=M['collect_odds'](growth)
        for variant in args.variants:
            boost=prepared_odds(variant,growth) if family_of(variant)=='整备借力' else None
            for target in args.targets:
                model=make_solver(variant,growth,ordinary,boost)
                model.target=target;model.parameters=dict(model.parameters,目标上限=target)
                p,time=model.initial()
                row=dict(variant=variant,growth=growth,target=target,success=p,success_rounds=1-time/p,
                         model_only=True,fixed=[fixed_value(model,policy) for policy in POLICIES[model.family]])
                rows.append(row);print(f'模型扫参 {variant} 目标{target}: P={p:.6f}',flush=True)
                model.value.cache_clear();model.next_hand.cache_clear();local_plan.cache_clear()
                (args.output/'扫参.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')


def play(args):
    models={};probabilities={g:M['collect_odds'](g) for g in args.growth}
    for growth in args.growth:
        for variant in args.variants:
            boosted=prepared_odds(variant,growth) if family_of(variant)=='整备借力' else None
            model=make_solver(variant,growth,probabilities[growth],boosted);model.initial()
            models[(variant,growth)]=model
            # 实际局只需预热后的决定表；释放全局方法缓存，避免跨配置保留大图。
            model.value.cache_clear();model.next_hand.cache_clear()
    records=args.output/'轨迹';records.mkdir();jobs=[]
    for variant in args.variants:
        for growth in args.growth:
            for policy in args.policies or POLICIES[family_of(variant)]+['状态求解']:
                for seed in args.seeds:jobs.append((variant,growth,policy,seed))
    def run(job):
        variant,growth,policy,seed=job;model=models[(variant,growth)]
        session=H['Session']('研究·'+variant,seed,growth)
        try:
            for _ in range(100):
                o=session.observation
                if o['EncounterResult'] is not None:break
                if policy=='状态求解':
                    state=public_state(o,model.family);action,die=model.decisions[state]
                else:action,die=choose(model,o,policy)
                op=next(a for a in o['Operations'] if ((a['Card'] or a['Kind']),a['DieValue'])==(action,die))
                session.act(op,'研究策略（非新玩家）：'+policy)
            else:raise RuntimeError('100步未终止')
            name=f'{variant}-{policy}-s{seed}-g{growth}.json.gz';revision=session.save(records/name)
            fields=session.observation['EncounterResult'].strip('()').split()
            return dict(variant=variant,family=model.family,growth=growth,policy=policy,seed=seed,
                        status=fields[0].lstrip("'"),progress=int(fields[1]),rounds=int(fields[2]),calm=int(fields[3]),
                        record='轨迹/'+name,revision=revision)
        finally:session.close()
    rows=[]
    with ThreadPoolExecutor(max_workers=4) as pool:
        for row in pool.map(run,jobs):
            rows.append(row)
            if len(rows)%24==0:print(f'{len(rows)}/{len(jobs)}局完成',flush=True)
    assert len({r['revision'] for r in rows})==1,'期间版本变化'
    (args.output/'结果.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
    print(f'正式会话 {len(rows)} 局完成',flush=True)


def check_new(path):
    data=json.loads(gzip.decompress(path.read_bytes()));setup=data['Setup'];variant=setup['Entry'].split('研究·')[-1]
    family=family_of(variant)
    if family not in ('整备借力','稳险选择'):
        return runpy.run_path(str(Path(__file__).with_name('check-mechanism-model.py')))['check'](path)
    model=make_solver(variant,setup['Growth'],{},{});count=0
    for step in data['Steps']:
        before,after=step['Before'],step['After'];r,s,t,hand=public_state(before,family)
        op=next(a for a in before['Operations'] if a['Id']==step['OperationId']);action=op['Card'] or op['Kind']
        if action=='end-turn':candidates=[(r,0,t-1,False)]
        else:
            if op['Odds']:
                label=next(e['Text'].split('，')[0].split('：')[1] for e in step['Events'] if e['Text'].startswith('判定：'))
                index={'Fail':0,'Neutral':1,'Success':2}[label];probs=[0,0,0];probs[index]=1
            else:probs=(1,0,0)
            model.probabilities[op['DieValue']]=probs
            if family=='整备借力':model.boosted_probabilities[op['DieValue']]=probs
            candidates=[(nr,ns,t,failed) for prob,nr,ns,failed in model.outcomes(action,r,s,op['DieValue']) if prob]
        if after['EncounterResult'] is None:
            rr,ss,tt,h=public_state(after,family);assert (rr,ss,tt,False) in candidates,(path,step['Version'],candidates,(rr,ss,tt))
        else:
            fields=after['EncounterResult'].strip('()').split();status=fields[0].lstrip("'");progress=int(fields[1])
            matching=[(nr,ns,tt) for nr,ns,tt,failed in candidates if not failed and
                      ((status=='success' and nr<=0 and progress==model.target) or
                       (status=='timeout' and tt==0 and progress==model.target-nr))]
            assert matching,(path,step['Version'],candidates,fields)
            assert any(int(fields[2])==min(model.turns,model.turns-tt+1) for nr,ns,tt in matching)
            player=next(a for a in before['Actors'] if a['Id']=='player')
            assert int(fields[3])==player['Composure']-(action=='end-turn')
            if family=='整备借力':
                extra=int(re.search(r'\((\d+)\)\)$',after['EncounterResult'])[1])
                assert any(ns==extra for nr,ns,tt in matching)
        count+=1
    return count


def verify(args):
    paths=sorted(args.output.rglob('*.json.gz'));steps=sum(check_new(p) for p in paths)
    selected=[];seen=set()
    for path in paths:
        data=json.loads(gzip.decompress(path.read_bytes()));setup=data['Setup']
        status=data['Steps'][-1]['After']['EncounterResult'].split()[0]
        policy=next((name for name in ('状态求解','当轮规划') if name in path.name),'基线')
        key=(setup['Entry'],setup['Growth'],status,'作者' if '作者' in str(path) else policy)
        if key not in seen:seen.add(key);selected.append(path)
    def replay(path):
        with tempfile.TemporaryDirectory(prefix='round2-replay-') as tmp:
            raw=Path(tmp)/'record.json';raw.write_bytes(gzip.decompress(path.read_bytes()))
            result=subprocess.run(['dotnet',str(DLL),'--replay',str(raw)],cwd=ROOT,capture_output=True,text=True)
            assert result.returncode==0,(path,result.stdout,result.stderr)
        return dict(path=str(path.relative_to(args.output)),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),passed=True)
    with ThreadPoolExecutor(max_workers=4) as pool:replays=list(pool.map(replay,selected))
    report=dict(model_games=len(paths),model_steps=steps,strict_replays=replays,unity_visual_checked=False)
    (args.output/'验证.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(f'模型核对 {len(paths)} 局 {steps} 步；严格回放 {len(replays)} 局通过',flush=True)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('mode',choices=('analyze','play','verify','sweep'));p.add_argument('output',type=Path)
    p.add_argument('--variants',nargs='+',choices=sum(VARIANTS.values(),[]),default=sum(VARIANTS.values(),[]))
    p.add_argument('--growth',type=int,nargs='+',default=[1]);p.add_argument('--seeds',type=int,nargs='+',default=list(range(701,717)));p.add_argument('--policies',nargs='+')
    p.add_argument('--targets',type=int,nargs='+',default=[26,28,30,32,34,36])
    p.add_argument('--objective',choices=('保守','效率'),default='保守')
    args=p.parse_args()
    assert all(g>0 for g in args.growth),'技能必须正数'
    assert all(t>0 for t in args.targets),'目标必须正数'
    assert args.objective=='保守' or args.mode=='analyze','效率偏好只用于分析，不冒充正式会话策略'
    if args.mode!='verify':
        if args.output.exists():raise FileExistsError(args.output)
        args.output.mkdir(parents=True)
    {'analyze':analyze,'play':play,'verify':verify,'sweep':sweep}[args.mode](args)


if __name__=='__main__':main()
