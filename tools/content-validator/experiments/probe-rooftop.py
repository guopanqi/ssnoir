"""封死退路作者试玩后的固定策略对照；只读取公开观测，不作为玩家试玩。"""
import gzip
import json
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]


def run(policy, seed):
    process = subprocess.Popen(
        ['dotnet', 'run', '--no-build', '--project', 'tools/content-validator/SSNoir.ContentValidator.csproj',
         '--', '--session', 'encounters/实验·封死退路', '--seed', str(seed)],
        cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)

    def read():
        line = process.stdout.readline()
        if not line:
            raise RuntimeError('会话退出')
        message = json.loads(line)
        assert message['type'] != 'error', message
        return message

    def send(command):
        process.stdin.write(json.dumps(command, ensure_ascii=False) + '\n')
        process.stdin.flush()
        return read()

    observation = read()['observation']
    try:
        for _ in range(60):
            if observation['EncounterResult'] is not None:
                break
            operations = observation['Operations']
            clocks = {c['Label']: c['Current'] for c in observation['Clocks']}

            def pick(card, high=False):
                options = [o for o in operations if o['Card'] == card]
                return (max if high else min)(options, key=lambda o: o['DieValue'] or 0) if options else None

            action = pick('带她走楼梯') or pick('沿梯子下楼')
            if not action and policy == '开场封门':
                action = pick('楔死楼梯门')
            if not action and policy == '留最后骰保底' and clocks['楼梯追兵'] == 1:
                # 一颗骰尚未用掉时就必须决定；已经用完再想封门来不及。
                actor = observation['Actors'][0]
                if len(actor['Dice']) == 1:
                    action = pick('楔死楼梯门')
            if not action:
                action = pick('让她相信你', True)
            if not action:
                action = pick('接好逃生梯')
            if not action:
                action = next(o for o in operations if o['Kind'] == 'end-turn')
            observation = send({'command': 'act', 'version': observation['Version'],
                                'operationId': action['Id'], 'reason': '固定对照：' + policy})['observation']
        else:
            raise RuntimeError('60步内未结束')
        with tempfile.TemporaryDirectory(prefix='noir-roof-') as scratch:
            raw = Path(scratch) / 'record.json'
            send({'command': 'save', 'path': str(raw)})
            directory = ROOT / 'docs/实验/封死退路/对照记录'
            directory.mkdir(parents=True, exist_ok=True)
            with gzip.open(directory / f'{policy}-{seed}.json.gz', 'wb') as target:
                target.write(raw.read_bytes())
        send({'command': 'quit'})
        result = {'policy': policy, 'seed': seed, 'result': observation['EncounterResult'],
                  'calm': observation['Actors'][0]['Composure'], 'injury': observation['Injury']}
        print(json.dumps(result, ensure_ascii=False), flush=True)
        return result
    finally:
        if process.poll() is None:
            process.stdin.close()
            process.wait(timeout=10)


if __name__ == '__main__':
    results = [run(policy, seed) for policy in ('始终不封门', '开场封门', '留最后骰保底')
               for seed in (7, 11, 23, 31, 41)]
    (ROOT / 'docs/实验/封死退路/对照结果.json').write_text(
        json.dumps(results, ensure_ascii=False, indent=2) + '\n')
