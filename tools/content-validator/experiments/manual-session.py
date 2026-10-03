"""逐步交锋实验控制台：只转发人工决策、压缩显示和保存原始协议记录，不选择动作。"""
import argparse
import gzip
import json
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[3]


def display(message):
    if 'observation' not in message:
        print(json.dumps(message, ensure_ascii=False), flush=True)
        return
    observation = message['observation']
    # 保留全部公开卡片、钟备注、赔率、背包和角色；反馈不截掉关键事件。
    view = dict(observation)
    view['Cards'] = [
        {k: v for k, v in card.items() if v not in ('', [], False) and k != 'Clocks'}
        for card in observation['Cards']
    ]
    view['Operations'] = [
        [op['Id'], op['Label'], op['Prepared'], op['Odds']]
        for op in observation['Operations']
    ]
    events = []
    for event in message.get('events', []):
        item = {'phase': event['Phase'], 'text': event['Text']}
        if event.get('State'):
            state = event['State']
            item['state'] = {'scene': state['Scene'], 'clocks': [(c['Label'], c['Current'], c['Max']) for c in state['Clocks']],
                             'actors': [(a['Id'], a['Composure'], a['Dice']) for a in state['Actors']],
                             'inventory': state['Inventory'], 'injury': state['InjurySeverity']}
        events.append(item)
    print(json.dumps({'observation': view, 'events': events}, ensure_ascii=False), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('entry')
    parser.add_argument('--seed', type=int, default=19)
    parser.add_argument('--record', type=Path, required=True, help='退出时保存原始会话为 .json.gz')
    args = parser.parse_args()
    if args.record.exists():
        raise FileExistsError(f'不覆盖已有记录：{args.record}')
    process = subprocess.Popen(
        ['dotnet', 'run', '--no-build', '--project', 'tools/content-validator/SSNoir.ContentValidator.csproj', '--', '--session', args.entry, '--seed', str(args.seed)],
        cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
    observation = None

    def send(command):
        process.stdin.write(json.dumps(command, ensure_ascii=False) + '\n')
        process.stdin.flush()
        return read()

    def read():
        nonlocal observation
        line = process.stdout.readline()
        if not line:
            raise RuntimeError(f'会话已退出：{process.poll()}')
        message = json.loads(line)
        if 'observation' in message:
            observation = message['observation']
        display(message)
        return message

    try:
        read()
        for line in sys.stdin:
            try:
                command = json.loads(line)
                if 'card' in command:
                    if not command.get('reason'):
                        raise ValueError('逐步行动必须写 reason')
                    candidates = [op for op in observation['Operations']
                                  if (op['Card'] or op['Kind']) == command['card']
                                  and ('die' not in command or op['DieValue'] == command['die'])]
                    if not candidates:
                        raise ValueError('当前观察中没有指定卡片／骰面的合法操作')
                    # 多槽或同点骰的组合可用原生 operationId 精确指定。
                    command = {'command': 'act', 'version': observation['Version'],
                               'operationId': candidates[0]['Id'], 'reason': command['reason']}
                if command.get('command') == 'quit':
                    with tempfile.TemporaryDirectory(prefix='noir-manual-') as scratch:
                        raw = Path(scratch) / 'session.json'
                        result = send({'command': 'save', 'path': str(raw)})
                        if result['type'] != 'saved':
                            raise RuntimeError('保存失败')
                        args.record.parent.mkdir(parents=True, exist_ok=True)
                        with gzip.open(args.record, 'wb') as target:
                            target.write(raw.read_bytes())
                    send(command)
                    break
                send(command)
            except (ValueError, KeyError) as error:
                print(json.dumps({'type': 'adapter-error', 'message': str(error)}, ensure_ascii=False), flush=True)
    finally:
        if process.poll() is None:
            process.stdin.close()
            process.wait(timeout=10)


if __name__ == '__main__':
    main()
