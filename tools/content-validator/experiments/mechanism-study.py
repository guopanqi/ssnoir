"""正式会话协议上的机制研究。策略只读公开观察，不复写游戏结算。

screen: 六个候选的固定与状态依赖策略；study: 消融、邻域、留出种子；
replay: 在同一内容和构建上严格重放已有原生记录。输出目录不得已存在。
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import gzip
import json
from pathlib import Path
import re
import runpy
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
DLL = ROOT/'tools/content-validator/bin/Debug/net9.0/SSNoir.ContentValidator.dll'


def odds(text):
    """从正式引擎公布的六面条读概率，不维护一份判定表。"""
    result = {'坏': 0.0, '中': 0.0, '好': 0.0}
    for part in (text or '').split(' · '):
        match = re.fullmatch(r'(\d)(?:[–-](\d))? ([坏中好])', part)
        if match:
            first, last = int(match[1]), int(match[2] or match[1])
            result[match[3]] += (last-first+1)/6
    if text and abs(sum(result.values())-1) > 1e-9:
        raise ValueError(f'未知赔率格式：{text}')
    return result


class Session:
    def __init__(self, scene, seed, growth=1):
        self.process = subprocess.Popen(
            ['dotnet', str(DLL), '--session', 'encounters/'+scene,
             '--seed', str(seed), '--growth', str(growth),
             '--item', '香烟=0', '--item', '酒=0', '--item', '药品=0'],
            cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.PIPE, text=True)
        self.observation = self.read()['observation']

    def read(self):
        line = self.process.stdout.readline()
        if not line:
            raise RuntimeError(self.process.stderr.read())
        message = json.loads(line)
        if message['type'] == 'error':
            raise RuntimeError(message['message'])
        if 'observation' in message:
            self.observation = message['observation']
        return message

    def send(self, command):
        self.process.stdin.write(json.dumps(command, ensure_ascii=False)+'\n')
        self.process.stdin.flush()
        return self.read()

    def act(self, op, reason):
        return self.send(dict(command='act', version=self.observation['Version'],
                              operationId=op['Id'], reason=reason))

    def save(self, path):
        with tempfile.TemporaryDirectory(prefix='ssnoir-study-') as tmp:
            raw = Path(tmp)/'session.json'
            assert self.send(dict(command='save', path=str(raw)))['type'] == 'saved'
            transcript = json.loads(raw.read_text())
            with gzip.open(path, 'wt', encoding='utf-8') as stream:
                json.dump(transcript, stream, ensure_ascii=False, separators=(',', ':'))
            return transcript['ContentRevision']

    def close(self):
        try:
            if self.process.poll() is None:
                self.send(dict(command='quit'))
        finally:
            self.process.stdin.close()
            self.process.wait(timeout=10)
            self.process.stdout.close()
            self.process.stderr.close()


SOLVERS = {}


def choose(o, family, policy):
    if policy=='等待到期':
        return next(a for a in o['Operations'] if a['Kind']=='end-turn')
    if policy=='强推到底':
        actions=[a for a in o['Operations'] if a['Card']=='强推']
        return max(actions,key=lambda a:a['DieValue']) if actions else next(a for a in o['Operations'] if a['Kind']=='end-turn')
    if policy in ('状态求解','收益求解'):
        growth=next(a for a in o['Actors'] if a['Id']=='player')['Stats']['knowledge']
        return SOLVERS[(family,growth,'保守' if policy=='状态求解' else '效率')].recommend(o)
    clocks = {c['Label']: c for c in o['Clocks']}
    value = lambda k: clocks[k]['Current']
    remaining = clocks.get('目标', {}).get('Max', 0)-clocks.get('目标', {}).get('Current', 0)
    actions = [a for a in o['Operations'] if a['Kind'] == 'action' and a['Card'] not in ('抽烟','喝酒')]
    turn_end = next(a for a in o['Operations'] if a['Kind'] == 'end-turn')
    dice = next(a for a in o['Actors'] if a['Id']=='player')['Dice']
    def pick(card, high=True):
        choices = [a for a in actions if a['Card']==card]
        return (max if high else min)(choices, key=lambda a: a['DieValue'] or 0) if choices else None
    def expected(a, bad, middle, good):
        p=odds(a['Odds']);return p['坏']*bad+p['中']*middle+p['好']*good
    if family == '余热转用':
        h=value('余热')
        if not dice: return turn_end
        if policy=='强推泄热': return pick('借热',False) if h>=4 else pick('强推')
        if policy=='只借热': return pick('借热')
        if policy=='固定交替': return pick('强推') if h<2 else pick('借热')
        valid=[a for a in actions if a['Card'] in ('强推','借热') and (a['Card']!='强推' or h<4)]
        # 贪心基线也知道真实代价；另一个策略把留下的热量作为未来收益。
        def score(a):
            if a['Card']=='强推':
                ev=expected(a,0,min(remaining,1),min(remaining,3))
                return ev+(0.6 if policy=='余热前瞻' and len(dice)>1 else 0)
            bonus=0 if '无转用' in o['Scene'] else h//2
            return expected(a,0,min(remaining,1+bonus),min(remaining,2+bonus))
        return max(valid,key=score)
    if family == '分段兑现':
        k=value('储备')
        gain=k*(k+1)//2 if '线性' not in o['Scene'] else 2*k
        if not dice:return turn_end
        if gain>=remaining:return pick('兑现',False)
        if remaining<=len(dice) and policy=='手牌兑现':return pick('稳进',False)
        threshold=int(policy[-1]) if policy.startswith('满') else 4
        if k and (k>=threshold or (len(dice)==1 and policy in ('手牌兑现','满4','满5'))):return pick('兑现',False)
        if policy=='稳进到底':return pick('稳进',False)
        if policy=='手牌兑现' and k:
            a=pick('备料')
            p=odds(a['Odds'])
            if len(dice)==1 or (p['坏']>0 and k>=3 and a['DieValue']<=2):return pick('兑现',False)
        return pick('备料') or turn_end
    if family == '情报承诺':
        if not dice:return turn_end
        if pick('执行方案'):return pick('执行方案')
        clue=next(c['Text'] for c in o['Cards'] if c['Name']=='标注：线索')
        if '条件是A' in clue:return pick('押定A',False)
        if '条件是B' in clue:return pick('押定B',False)
        if policy=='直接押A':return pick('押定A',False)
        if policy=='直接押B':return pick('押定B',False)
        trial=pick('探查',policy!='弱骰探查')
        if policy=='弱骰探查' and trial['DieValue']>=4:return pick('押定A',False)
        if policy=='一次探查' and o['Version']>1:return pick('押定A',False)
        return trial
    if family == '窗口储备':
        if not dice:return turn_end
        k=value('储备');cash=pick('兑现',False)
        if policy=='直接到底':return pick('直接推进')
        if cash and (2*k>=remaining or k>=3 or len(dice)==1):return cash
        if policy=='只备料兑现':return pick('备料')
        if policy=='提前备料' and not cash and value('已过回合')%2==0 and k<4:return pick('备料')
        if cash and len(dice)>1 and k<3:return pick('备料')
        return pick('直接推进')
    if family == '承诺分流':
        if not dice:return turn_end
        if pick('强行推进'):return pick('强行推进')
        if policy=='立即快线':return pick('开快线',False)
        if policy=='一直稳步':return pick('稳步推进')
        high=sum(d['Value']>=4 for d in dice)
        if policy=='手牌承诺' and high>=2 and remaining>2*len(dice):return pick('开快线',False)
        if policy=='末轮快线' and value('已过回合')>=2 and remaining>2*len(dice):return pick('开快线',False)
        return pick('稳步推进')
    if family == '风险兑现':
        h=value('压力');progress=value('目标')
        if policy.startswith('压力收手') and h>=int(policy[-1]):return pick('带走成果')
        if policy=='一直冒险':return pick('继续取样') or turn_end
        if not dice:return turn_end
        if h>=4:
            if len(dice)==1 and value('已过回合')>=3:return pick('带走成果')
            return pick('整理成果',False)
        return pick('继续取样')
    raise ValueError((family,policy))


POLICIES={
'余热转用':['强推泄热','只借热','固定交替','即时收益','余热前瞻'],
'分段兑现':['稳进到底','满3','满4','满5','手牌兑现'],
'情报承诺':['直接押A','直接押B','高骰探查','弱骰探查','一次探查'],
'窗口储备':['直接到底','只备料兑现','提前备料','窗口自适应'],
'承诺分流':['立即快线','一直稳步','手牌承诺','末轮快线'],
'风险兑现':['一直冒险','压力收手3','压力收手4','压力收手5','整理续航'],
}
VARIANTS={
'余热转用':['余热转用','余热无转用','余热短目标','余热长目标'],
'分段兑现':['分段兑现','兑现线性','兑现无衰减','兑现短目标','兑现长目标'],
'情报承诺':['情报承诺','情报无信息','情报短目标','情报长目标'],
'窗口储备':['窗口储备','窗口常开'],
'承诺分流':['承诺分流'],
'风险兑现':['风险兑现'],
}


def run(job):
    family,variant,policy,seed,growth,directory=job
    scene='研究·'+variant
    session=Session(scene,seed,growth)
    choices=[]
    try:
        for _ in range(100):
            o=session.observation
            if o['EncounterResult'] is not None:break
            op=choose(o,family,policy)
            if op is None:raise RuntimeError((variant,policy,o))
            choices.append(dict(version=o['Version'],card=op['Card'] or op['Kind'],die=op['DieValue']))
            session.act(op,'固定策略对照（非玩家）：'+policy)
        else:raise RuntimeError('100步未结算，拒绝记录为有效实验')
        name=f'{variant}-{policy}-s{seed}-g{growth}.json.gz'
        revision=session.save(directory/'轨迹'/name)
        o=session.observation
        result=o['EncounterResult']
        fields=result.strip('()').split()
        status=fields[0].lstrip("'")
        row=dict(family=family,variant=variant,policy=policy,seed=seed,growth=growth,
                 result=result,status=status,success=status=='success',
                 progress=int(fields[1]),
                 rounds=int(fields[2]),
                 calm=int(fields[3]),
                 injury=o['Injury'],actions=choices,revision=revision,record='轨迹/'+name)
        return row
    finally:session.close()


def aggregate(rows):
    result=[]
    for family,variant,policy,growth in sorted({(r['family'],r['variant'],r['policy'],r['growth']) for r in rows}):
        group=[r for r in rows if (r['family'],r['variant'],r['policy'],r['growth'])==(family,variant,policy,growth)]
        n=len(group);wins=[r for r in group if r['success']]
        # 风险兑现失败时成果清零；其他族的部分进度不是奖励。
        bank=[r['progress'] if r['status'] in ('banked','success') else 0 for r in group] if family=='风险兑现' else []
        result.append(dict(family=family,variant=variant,policy=policy,growth=growth,n=n,wins=len(wins),
          mean_success_rounds=sum(r['rounds'] for r in wins)/len(wins) if wins else None,
          mean_calm=sum(r['calm'] for r in group)/n,
          mean_banked=sum(bank)/n if bank else None,
          statuses={s:sum(r['status']==s for r in group) for s in sorted({r['status'] for r in group})}))
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode',choices=['screen','study','replay'])
    parser.add_argument('output',type=Path)
    parser.add_argument('--families',nargs='+',choices=list(POLICIES))
    parser.add_argument('--seeds',type=int,nargs='+',default=list(range(101,113)))
    parser.add_argument('--growth',type=int,nargs='+',default=[1])
    parser.add_argument('--workers',type=int,default=4)
    parser.add_argument('--with-dp',action='store_true',help='只给三种基础原型加入有限状态求解策略')
    args=parser.parse_args()
    if args.mode=='replay':
        records=args.output/'轨迹'
        for path in sorted(records.glob('*.json.gz')):
            with tempfile.TemporaryDirectory() as tmp:
                raw=Path(tmp)/'replay.json';raw.write_bytes(gzip.decompress(path.read_bytes()))
                r=subprocess.run(['dotnet',str(DLL),'--replay',str(raw)],cwd=ROOT,capture_output=True,text=True,check=True)
        print(f'严格重放通过：{len(list(records.glob("*.json.gz")))}局')
        return
    if args.output.exists():raise FileExistsError(args.output)
    args.output.mkdir(parents=True);(args.output/'轨迹').mkdir()
    families=args.families or list(VARIANTS)
    if args.with_dp:
        math_tools=runpy.run_path(str(Path(__file__).with_name('mechanism-solver.py')))
        for growth in args.growth:
            probabilities=math_tools['collect_odds'](growth)
            for family in families:
                if family not in ('余热转用','分段兑现','情报承诺'):continue
                for objective in ('保守','效率'):
                    solver=math_tools['Solver'](family,family,growth,probabilities,objective)
                    initial=solver.initial()
                    SOLVERS[(family,growth,objective)]=solver
                    solver.value.cache_clear();solver.next_hand.cache_clear()
                    print(f'求解准备：{family} g{growth} {objective} P={initial[0]:.6f}',flush=True)
    jobs=[]
    for family in families:
        variants=VARIANTS[family] if args.mode=='study' else [family]
        for variant in variants:
            policies=POLICIES[family]+(['状态求解','收益求解'] if args.with_dp and variant==family and (family,args.growth[0],'保守') in SOLVERS else [])
            for policy in policies:
                for seed in args.seeds:
                    for growth in args.growth:jobs.append((family,variant,policy,seed,growth,args.output))
    rows=[]
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        for row in pool.map(run,jobs):
            rows.append(row)
            if len(rows)%24==0:print(f'{len(rows)}/{len(jobs)}完成',flush=True)
    revisions={r['revision'] for r in rows}
    if len(revisions)!=1:raise RuntimeError('运行期间内容或构建变化；不能作为同版结果汇总')
    (args.output/'结果.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
    summary=aggregate(rows)
    (args.output/'汇总.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n')
    for r in summary:
        print(f'{r["variant"]} / {r["policy"]} / g{r["growth"]}: {r["wins"]}/{r["n"]}, '
              f'成功轮数={r["mean_success_rounds"]},兑现={r["mean_banked"]}',flush=True)


if __name__=='__main__':main()
