"""对现有原生轨迹逐步核对数学模型的转移；不重放或修改游戏。"""
import argparse,gzip,json,re,runpy
from pathlib import Path
SOLVER=runpy.run_path(str(Path(__file__).with_name('mechanism-solver.py')))

def check(path):
    transcript=json.loads(gzip.decompress(path.read_bytes()))
    config=transcript['Setup'];variant=config['Entry'].split('研究·')[-1]
    family=next((f for f in ('分段兑现','余热转用','情报承诺') if f in ('分段兑现' if variant.startswith('兑现') else '余热转用' if variant.startswith('余热') else '情报承诺' if variant.startswith('情报') else variant)),None)
    if not family:return 0
    model=SOLVER['Solver'](family,variant,config['Growth'],{})
    checked=0
    for step in transcript['Steps']:
        before,after=step['Before'],step['After']
        clocks={c['Label']:c['Current'] for c in before['Clocks']}
        op=next(o for o in before['Operations'] if o['Id']==step['OperationId'])
        if family=='情报承诺':
            # 此处只核对公开信息对应的模式；隐藏条件的单局真值不用于推荐。
            cards={c['Name']:c['Text'] for c in before['Cards']}
            if '方案失配' in cards['标注：方案']:mode='F'
            elif '方案匹配' in cards['标注：方案']:mode='M'
            elif '条件是' in cards['标注：线索']:mode='K'
            else:mode='U'
            state=(mode,0)
        else:state=clocks['储备' if family=='分段兑现' else '余热']
        action=op['Card'] or op['Kind']
        if action in ('抽烟','喝酒'):raise ValueError('模型条件不包括背包恢复品')
        old_p=clocks['目标'];old_t=clocks['已过回合']
        if action=='end-turn':
            if family=='分段兑现':new_state=max(0,state-model.parameters['储备衰减'])
            elif family=='余热转用':new_state=max(0,state-1)
            else:new_state=state
            candidates=[(old_p,new_state,old_t+1,'timeout' if old_t+1>=model.turns else None)]
        else:
            if op['Odds']:
                label=next(e['Text'].split('，')[0].split('：')[1] for e in step['Events'] if e['Text'].startswith('判定：'))
                index={'Fail':0,'Neutral':1,'Success':2}[label]
                probs=[0,0,0];probs[index]=1;model.probabilities[op['DieValue']]=tuple(probs)
            else:model.probabilities[op['DieValue']]=(0,0,0)
            normalized='押定' if action.startswith('押定') else action
            candidates=[]
            for probability,r,st,failed in model.outcomes(normalized,model.target-old_p,state,op['DieValue']):
                if not probability:continue
                progress=min(model.target,model.target-r)
                # 余热满六是在推进之前结束，进度保持旧值。
                if failed and family=='余热转用':progress=old_p
                status='overheat' if failed and family=='余热转用' else 'alarm' if failed else 'success' if progress>=model.target else None
                candidates.append((progress,st,old_t,status))
        if after['EncounterResult'] is None:
            c={x['Label']:x['Current'] for x in after['Clocks']}
            if family=='情报承诺':
                cards={x['Name']:x['Text'] for x in after['Cards']}
                st=('F' if '方案失配' in cards['标注：方案'] else 'M' if '方案匹配' in cards['标注：方案'] else 'K' if '条件是' in cards['标注：线索'] else 'U',0)
            else:st=c['储备' if family=='分段兑现' else '余热']
            actual=(c['目标'],st,c['已过回合'],None)
            assert actual in candidates,(path,step['Version'],actual,candidates)
        else:
            result=after['EncounterResult'];fields=result.strip('()').split();status=fields[0].lstrip("'");progress=int(fields[1])
            if family!='情报承诺':
                mechanism=int(re.search(r'\((\d+)\)\)$',result)[1])
                assert any((p,st,s)==(progress,mechanism,status) for p,st,t,s in candidates),(path,step['Version'],result,candidates)
            else:assert any((p,s)==(progress,status) for p,st,t,s in candidates),(path,step['Version'],result,candidates)
        checked+=1
    return checked

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('directories',nargs='+',type=Path);a=p.parse_args()
    files=steps=0
    for directory in a.directories:
        paths=[directory] if directory.is_file() else directory.rglob('*.json.gz')
        for path in paths:
            n=check(path);files+=bool(n);steps+=n
    print(f'模型转移对照通过：{files}局，{steps}步')
