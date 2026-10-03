"""重新执行作者既有决策，逐项比对完整观测与事件，保存新回归轨迹并严格重放。
不修改旧指纹，不生成新试玩决策；任一差异直接失败。
"""
import gzip
import json
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
RECORDS = {
 '橱窗里的脸': '手动会话-seed19.json.gz',
 '逼他出门': '作者逐步-seed19.json.gz',
 '枪口认路': '作者逐步-seed19.json.gz',
 '最后一页': '作者逐步-v2-seed31.json.gz',
 '赃款会拖脚': '作者逐步-v2-seed31.json.gz',
 '只够一条线': '作者逐步-seed19.json.gz',
 '信封换人': '作者逐步-seed19.json.gz',
 '证词掺水': '作者逐步-seed19.json.gz',
 '封死退路': '作者逐步-v3-seed41.json.gz',
 '等买主现身': '作者逐步-seed19.json.gz',
}


def run(scene, filename):
    source = ROOT / 'docs/实验' / scene / filename
    previous = json.loads(gzip.decompress(source.read_bytes()))
    setup = previous['Setup']
    command = ['dotnet', 'run', '--no-build', '--project', 'tools/content-validator/SSNoir.ContentValidator.csproj',
               '--', '--session', setup['Entry'], '--seed', str(setup['Seed']), '--growth', str(setup['Growth'])]
    for name, amount in setup['Items'].items():
        command.extend(['--item', f'{name}={amount}'])
    process = subprocess.Popen(command, cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)

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

    try:
        message = read()
        assert message['events'] == previous['OpeningEvents'], (scene, '入场事件变化')
        observation = message['observation']
        for index, step in enumerate(previous['Steps']):
            assert observation == step['Before'], (scene, index, '行动前完整观测变化')
            message = send({'command': 'act', 'version': observation['Version'],
                            'operationId': step['OperationId'], 'reason': '回归复核既有作者决策：' + (step['Reason'] or '')})
            observation = message['observation']
            assert observation == step['After'], (scene, index, '行动后完整观测变化')
            assert message['events'] == step['Events'], (scene, index, '事件变化')
        assert observation['EncounterResult'] is not None, (scene, '未结束')
        with tempfile.TemporaryDirectory(prefix='noir-revalidate-') as scratch:
            raw = Path(scratch) / 'current.json'
            assert send({'command': 'save', 'path': str(raw)})['type'] == 'saved'
            replay = subprocess.run(['./run', '--replay', str(raw)], cwd=ROOT, capture_output=True, text=True)
            assert replay.returncode == 0, (scene, replay.stdout, replay.stderr)
            current = json.loads(raw.read_text())
            destination = source.parent / '当前回归复核.json.gz'
            with gzip.open(destination, 'wb') as target:
                target.write(raw.read_bytes())
        send({'command': 'quit'})
        result = {'scene': scene, 'authorRecord': str(source.relative_to(ROOT)),
                  'regressionRecord': str(destination.relative_to(ROOT)), 'steps': len(previous['Steps']),
                  'contentRevision': current['ContentRevision'], 'result': observation['EncounterResult'],
                  'completeObservationsAndEventsMatch': True, 'strictReplayPassed': True}
        print(json.dumps(result, ensure_ascii=False), flush=True)
        return result
    finally:
        if process.poll() is None:
            process.stdin.close()
            process.wait(timeout=10)


if __name__ == '__main__':
    results = [run(scene, filename) for scene, filename in RECORDS.items()]
    (ROOT / 'docs/实验/最终轨迹复核.json').write_text(json.dumps(results, ensure_ascii=False, indent=2) + '\n')
