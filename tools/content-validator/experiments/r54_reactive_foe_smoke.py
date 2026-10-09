#!/usr/bin/env python3
"""R54 uses the *real* C# headless Scheme session, with action/attack trace + exact replay.

This checks executable consequences and deterministic reproducibility, NOT policy optimality.
"""
from __future__ import annotations

import json
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CMD = ['dotnet', 'run', '--no-build', '--project',
       'tools/content-validator/SSNoir.ContentValidator.csproj', '--']
ENTRY = 'encounters/研究·追击领先目标'


def progress(observation):
    clocks = {x['Label']: x['Current'] for x in observation['Clocks']}
    return clocks['甲'], clocks['乙']


def one(seed: int, growth: int, pattern: str, directory: Path) -> dict:
    p = subprocess.Popen(CMD + ['--session', ENTRY, '--seed', str(seed), '--growth', str(growth)],
                         cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                         stderr=subprocess.PIPE, text=True)

    def receive():
        line = p.stdout.readline()
        if not line:
            raise RuntimeError(f'R54 seed {seed}: stdout closed, code={p.poll()}')
        result = json.loads(line)
        if result.get('type') == 'error':
            raise AssertionError(result)
        return result

    def request(message):
        p.stdin.write(json.dumps(message, ensure_ascii=False) + '\n')
        p.stdin.flush()
        return receive()

    hit_targets = set()
    attempted = []
    try:
        observation = receive()['observation']
        assert progress(observation) == (0, 0)
        for action_count in range(6):
            if observation['EncounterResult'] is not None:
                break
            actions = [o for o in observation['Operations'] if o['Kind'] == 'action'
                       and o['Card'] in ('推进甲', '推进乙')]
            if actions:
                target = pattern[min(action_count, len(pattern) - 1)]
                opts = [o for o in actions if o['Card'] == ('推进甲' if target == 'A' else '推进乙')]
                assert opts
                pick = (min(opts, key=lambda x: (x['DieValue'], x['Id']))
                        if pattern == 'ABAB' else max(opts, key=lambda x: (x['DieValue'], x['Id'])))
                before = progress(observation)
                reply = request({'command': 'act', 'version': observation['Version'],
                                 'operationId': pick['Id'], 'reason': 'R54核对规则与回击'})
                observation = reply['observation']
                after = progress(observation)
                attempted.append(pick['Card'])
                attack_lines = [event['Text'] for event in reply['events']
                                if '敌方反击' in event['Text']]
                if attack_lines:
                    assert action_count + 1 in (2, 4), (action_count, attack_lines)
                    assert len(attack_lines) == 1
                    hit = 'A' if '反击甲' in attack_lines[0] else 'B'
                    hit_targets.add(hit)
                    # Verify one of the 0/1/2 actual dice outcomes exactly yields the
                    # visible post-state after the documented reactive targeting.
                    matched = False
                    for gain in (0, 1, 2):
                        a = min(3, before[0] + (gain if target == 'A' else 0))
                        b = min(5, before[1] + (gain if target == 'B' else 0))
                        if a >= 3 or b >= 5:
                            continue  # already completed, no enemy attack
                        expected_hit = 'A' if a >= b else 'B'
                        aa, bb = (max(0, a - 2), b) if expected_hit == 'A' else (a, max(0, b - 2))
                        if (aa, bb) == after and hit == expected_hit:
                            matched = True
                    assert matched, f'incorrect reactive damage: {before=}, {after=}, {attack_lines=}'
            else:
                ends = [o for o in observation['Operations'] if o['Kind'] == 'end-turn']
                assert ends, 'no end-turn option after using four dice'
                reply = request({'command': 'act', 'version': observation['Version'],
                                 'operationId': ends[0]['Id'], 'reason': 'R54标准期限'})
                observation = reply['observation']

        assert observation['EncounterResult'] is not None, 'R54 must settle within one round'
        tape = directory / f'{seed}-{growth}-{pattern}.json'
        saved = request({'command': 'save', 'path': str(tape)})
        assert saved['type'] == 'saved'
        assert request({'command': 'quit'})['type'] == 'bye'
        assert p.wait(timeout=10) == 0
        replay = subprocess.run(CMD + ['--replay', str(tape)], cwd=ROOT,
                                capture_output=True, text=True, timeout=90)
        assert replay.returncode == 0, replay.stdout + replay.stderr
        return {'seed': seed, 'growth': growth, 'pattern': pattern,
                'attacked': sorted(hit_targets),
                'actions': attempted, 'result': observation['EncounterResult'],
                'replay': 'pass'}
    finally:
        if p.poll() is None:
            p.kill()
            p.wait(timeout=10)


def main():
    rows = []
    seen = set()
    with tempfile.TemporaryDirectory(prefix='r54-native-') as d:
        folder = Path(d)
        for growth in (1, 2):
            for seed in (7, 19, 41):
                for pattern in ('ABBB', 'BAAA', 'BBBB', 'ABAB'):
                    row = one(seed, growth, pattern, folder)
                    rows.append(row)
                    seen.update(row['attacked'])
        if seen != {'A', 'B'}:
            for seed in range(1, 17):
                if seen == {'A', 'B'}:
                    break
                if seed in (7, 19, 41):
                    continue
                row = one(seed, 1, 'ABAB', folder)
                rows.append(row)
                seen.update(row['attacked'])
    assert seen == {'A', 'B'}, f'official runs did not exercise both attacks: {seen}'
    assert all(row['replay'] == 'pass' for row in rows)
    print(json.dumps({'native_sessions': len(rows), 'observed_attack_targets': sorted(seen),
                      'results': rows}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
