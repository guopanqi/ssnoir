"""最后四场的固定分支对照，作者逐步试玩之后执行；仅使用公开观测。"""
import gzip
import json
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
POLICIES = {
    '信封换人': ['全部付款', '近处强抢', '远处强抢', '等离开', '放弃'],
    '证词掺水': ['直接交稿', '两次逼问', '两次逼问两次核实', '全部听完', '等警车'],
    '封死退路': ['直接放弃', '封门后放弃', '不动等追兵', '封门后跳下'],
    '等买主现身': ['抓小鱼', '等买主', '不动等离开', '撤离'],
}


def run(scene, policy, seed):
    process = subprocess.Popen(['dotnet', 'run', '--no-build', '--project',
        'tools/content-validator/SSNoir.ContentValidator.csproj', '--', '--session',
        'encounters/实验·' + scene, '--seed', str(seed)], cwd=ROOT,
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)

    def read():
        line = process.stdout.readline()
        if not line:
            raise RuntimeError('会话已退出')
        message = json.loads(line)
        assert message['type'] != 'error', message
        return message

    def send(command):
        process.stdin.write(json.dumps(command, ensure_ascii=False) + '\n')
        process.stdin.flush()
        return read()

    observation = read()['observation']
    counts = {}
    try:
        for _ in range(60):
            if observation['EncounterResult'] is not None:
                break
            operations = observation['Operations']
            clocks = {c['Label']: c['Current'] for c in observation['Clocks']}

            def pick(card, high=False):
                options = [o for o in operations if o['Card'] == card]
                return (max if high else min)(options, key=lambda o: o['DieValue'] or 0) if options else None

            action = None
            if scene == '信封换人':
                if policy == '放弃':
                    action = pick('放弃交割')
                elif policy == '全部付款':
                    action = pick('递出信封')
                elif policy == '近处强抢':
                    action = pick('递出信封') if clocks['接近出口'] < 2 else pick('冲上去抢人', True)
                elif policy == '远处强抢':
                    action = pick('冲上去抢人', True)
            elif scene == '证词掺水':
                if policy == '直接交稿':
                    action = pick('交出笔记')
                elif policy in ('两次逼问', '两次逼问两次核实'):
                    if counts.get('逼他说名字', 0) < 2:
                        action = pick('逼他说名字')
                    elif policy == '两次逼问两次核实' and clocks['假话'] > 0:
                        action = pick('核对车牌')
                    else:
                        action = pick('交出笔记')
                elif policy == '全部听完':
                    action = pick('听他讲完', True) if clocks['口供'] < 4 else None
                    if not action:
                        action = pick('交出笔记')
            elif scene == '封死退路':
                if policy == '直接放弃':
                    action = pick('独自离开')
                elif policy == '封门后放弃':
                    action = pick('楔死楼梯门') or pick('独自离开')
                elif policy == '封门后跳下':
                    action = pick('楔死楼梯门') or pick('带她跳下去') or pick('让她相信你', True)
            else:
                if policy == '撤离':
                    action = pick('收手离开')
                elif policy in ('抓小鱼', '等买主'):
                    if policy == '抓小鱼':
                        action = pick('抓住送货人')
                    action = action or pick('抓住买主') or pick('布置伏击', True)
            if not action:
                action = next(o for o in operations if o['Kind'] == 'end-turn')
            card = action['Card'] or action['Kind']
            counts[card] = counts.get(card, 0) + 1
            observation = send({'command': 'act', 'version': observation['Version'],
                'operationId': action['Id'], 'reason': '固定结果分支对照：' + policy})['observation']
        else:
            raise RuntimeError('60步内没有结束')
        with tempfile.TemporaryDirectory(prefix='noir-branches-') as scratch:
            raw = Path(scratch) / 'record.json'
            assert send({'command': 'save', 'path': str(raw)})['type'] == 'saved'
            directory = ROOT / 'docs/实验' / scene / '最终分支记录'
            directory.mkdir(parents=True, exist_ok=True)
            with gzip.open(directory / f'{policy}-{seed}.json.gz', 'wb') as target:
                target.write(raw.read_bytes())
        send({'command': 'quit'})
        result = {'scene': scene, 'policy': policy, 'seed': seed,
            'result': observation['EncounterResult'], 'calm': observation['Actors'][0]['Composure'],
            'injury': observation['Injury'], 'money': observation['Inventory'].get('金钱'), 'actions': counts}
        print(json.dumps(result, ensure_ascii=False), flush=True)
        return result
    finally:
        if process.poll() is None:
            process.stdin.close()
            process.wait(timeout=10)


if __name__ == '__main__':
    results = [run(scene, policy, seed) for scene, policies in POLICIES.items()
               for policy in policies for seed in (7, 19, 31)]
    (ROOT / 'docs/实验/最终分支对照.json').write_text(json.dumps(results, ensure_ascii=False, indent=2) + '\n')
