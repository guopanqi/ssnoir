"""Compare complete imported curve snapshots and phase timings (exact key/tangent bits)."""
import argparse
import json
from pathlib import Path
import re

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('baseline', type=Path)
parser.add_argument('optimized', type=Path)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()
results = []
for path in sorted(args.baseline.glob('*.json')):
    if path.name in {'request.json', 'completed.json', 'profiles.json'}:
        continue
    before = json.loads(path.read_text())
    if 'clips' not in before:
        continue
    after = json.loads((args.optimized / path.name).read_text())
    assert before['asset'] == after['asset']
    first = {c['name']: c for c in before['clips']}
    second = {c['name']: c for c in after['clips']}
    assert first.keys() == second.keys(), 'Clip names differ'
    differences = []
    keys = curves = 0
    for name, clip in first.items():
        counterpart = second[name]
        for field in ('length', 'frameRate', 'legacy', 'loopTime', 'wrapMode', 'objectCurvesAndEvents'):
            if clip[field] != counterpart[field]: differences.append(dict(clip=name, field=field))
        identity = lambda r: (r['path'], r['type'], r['property'])
        original = {identity(r): r for r in clip['curves']}
        updated = {identity(r): r for r in counterpart['curves']}
        if original.keys() != updated.keys():
            differences.append(dict(clip=name, field='bindings', removed=list(original.keys() - updated.keys())[:5], added=list(updated.keys() - original.keys())[:5]))
        for binding, curve in original.items():
            curves += 1
            keys += curve['keys']
            if binding not in updated: continue
            for field in ('keys', 'digest', 'preWrap', 'postWrap'):
                if curve[field] != updated[binding][field]:
                    differences.append(dict(clip=name, binding=binding, field=field,
                        before=curve[field], after=updated[binding][field]))
    results.append(dict(asset=before['asset'], clips=len(first), curves=curves, keys=keys,
                        exact=differences == [], differences=differences[:30], difference_count=len(differences)))
assert results, 'No snapshots to compare'
def timings(folder):
    text = (folder / 'unity.log').read_text(errors='replace')
    values = {}
    for line in text.splitlines():
        if not line.startswith('ANIM_IMPORT_PROFILE|'): continue
        for field in line.split(';'):
            key, sep, value = field.partition('=')
            if key.endswith('_ms'): values[key] = values.get(key, 0) + float(value)
    assets = []
    current = None
    for line in text.splitlines():
        match = re.search(r'Start importing (.*?) using Guid', line)
        if match: current = match[1]
        match = re.search(r"-> \(artifact id:.*?\) in ([\d.]+) seconds", line)
        if match and current:
            assets.append(dict(asset=current, seconds=float(match[1]))); current=None
    return dict(callback_ms=values, imports=assets)
result = dict(comparisons=results, baseline=timings(args.baseline), optimized=timings(args.optimized))
args.out.write_text(json.dumps(result, ensure_ascii=False, indent=2))
print(json.dumps(dict(comparisons=[{k:v for k,v in r.items() if k!='differences'} for r in results],
    baseline_callback_ms=result['baseline']['callback_ms'].get('total_ms'),
    optimized_callback_ms=result['optimized']['callback_ms'].get('total_ms')), ensure_ascii=False))
assert all(r['exact'] for r in results), 'Imported curve differences; inspect report'
