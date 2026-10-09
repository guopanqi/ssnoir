#!/usr/bin/env python3
"""R49 原生 C# Session 最小端到端验证：盲准备→揭示→兑现／超时→严格回放。"""
from __future__ import annotations

import json
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ENTRY = 'encounters/研究·迟到的目标'
CMD = ['dotnet', 'run', '--no-build', '--project',
       'tools/content-validator/SSNoir.ContentValidator.csproj', '--']


def one(seed: int, growth: int, directory: Path) -> dict:
    process = subprocess.Popen(CMD + ['--session', ENTRY, '--seed', str(seed), '--growth', str(growth)],
                               cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE, text=True)

    def read():
        line = process.stdout.readline()
        if not line:
            raise RuntimeError(f'会话 seed={seed} 退出：{process.poll()}')
        message = json.loads(line)
        if message.get('type') == 'error':
            raise RuntimeError(str(message))
        return message

    def command(value):
        process.stdin.write(json.dumps(value, ensure_ascii=False) + '\n')
        process.stdin.flush()
        return read()

    try:
        obs = read()['observation']
        assert len([o for o in obs['Operations'] if o['Card'] and o['Card'].startswith('准备')]) >= 3
        for pref in ('准备甲', '准备乙'):
            options = [o for o in obs['Operations'] if o['Card'] == pref]
            assert options, f'缺少准备动作：{pref}'
            picked = min(options, key=lambda o: (o['DieValue'] or 0, o['Id']))
            obs = command({'command': 'act', 'version': obs['Version'],
                           'operationId': picked['Id'], 'reason': 'R49原生阶段核对'})['observation']
        if obs['EncounterResult'] is None:
            next_ops = [o for o in obs['Operations'] if o['Card'] and o['Card'].startswith('推进')]
            assert len(set(o['Card'] for o in next_ops)) == 1, '揭示后必须只剩唯一目标'
            assert not any(o['Card'] and o['Card'].startswith('准备') for o in obs['Operations'])
        total = 0
        while obs['EncounterResult'] is None and total < 8:
            steps = [o for o in obs['Operations'] if o['Card'] and o['Card'].startswith('推进')]
            if steps:
                chosen = max(steps, key=lambda o: (o['DieValue'] or 0, o['Id']))
            else:
                chosen = next(o for o in obs['Operations'] if o['Kind'] == 'end-turn')
            obs = command({'command': 'act', 'version': obs['Version'],
                           'operationId': chosen['Id'], 'reason': 'R49揭示后完成或超时'})['observation']
            total += 1
        assert obs['EncounterResult'] is not None, '必须在结果或一轮结束时结算'
        target = str(obs['EncounterResult'])
        assert any(x in target for x in ('甲', '乙', '丙'))
        transcript = directory / f'R49-{growth}-{seed}.json'
        saved = command({'command': 'save', 'path': str(transcript)})
        assert saved['type'] == 'saved' and saved['steps'] >= 2
        assert command({'command': 'quit'})['type'] == 'bye'
        process.wait(timeout=10)
        assert process.returncode == 0
        verify = subprocess.run(CMD + ['--replay', str(transcript)], cwd=ROOT,
                                text=True, capture_output=True, timeout=90)
        assert verify.returncode == 0, verify.stdout + '\n' + verify.stderr
        return {'seed': seed, 'growth': growth, 'steps': saved['steps'],
                'result': target, 'replay': 'pass'}
    finally:
        if process.poll() is None:
            process.kill()
            process.wait(timeout=10)


def main():
    with tempfile.TemporaryDirectory(prefix='r49-native-') as temp:
        directory = Path(temp)
        results = [one(seed, growth, directory) for growth in (1, 2) for seed in (7, 19, 41)]

        def revealed(row):
            options = [name for name in ('甲', '乙', '丙') if "'" + name in row['result']]
            assert len(options) == 1, f"无法区分指定目标: {row['result']}"
            return options[0]

        seen = {revealed(row) for row in results}
        for seed in range(1, 33):
            if seen == {'甲', '乙', '丙'}:
                break
            if seed in (7, 19, 41):
                continue
            row = one(seed, 1, directory)
            results.append(row)
            seen.add(revealed(row))
        assert seen == {'甲', '乙', '丙'}, f'种子1..32未覆盖所有指定目标: {sorted(seen)}'
    print(json.dumps({'native_sessions': len(results), 'revealed_targets': sorted(seen),
                      'results': results}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
