"""复制正式 Unity 工程到独立缓存，以同一 Main、资产和运行时代码截图。"""
import argparse
import hashlib
import json
import math
import os
import re
from pathlib import Path
import sys
import subprocess
import time
from datetime import datetime

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'UnityClient'
SCENES = ('晚宴', '别给他们想要的', '老街酒馆', '码头')
MARKER = '.ssnoir-world-preview'
sys.path.insert(0, str(ROOT / 'tools/unity'))
from staging import sync_project, PROJECT_FOLDERS


def differences(a, b, path=''):
    if isinstance(a, dict) and isinstance(b, dict):
        return [d for key in sorted(set(a) | set(b)) if key != 'mode'
                for d in differences(a.get(key), b.get(key), path + '/' + key)]
    if isinstance(a, list) and isinstance(b, list) and len(a) == len(b):
        return [d for i, (x, y) in enumerate(zip(a, b)) for d in differences(x, y, f'{path}/{i}')]
    if isinstance(a, (int, float)) and not isinstance(a, bool) and isinstance(b, (int, float)):
        if math.isclose(a, b, rel_tol=1e-5, abs_tol=1e-4): return []
    elif a == b: return []
    return [{'property': path, 'editor': a, 'play': b}]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scene', action='append', help='正式机位的地点名，可重复；默认四场景')
    parser.add_argument('--project', type=Path, default=ROOT / '.cache/citybox-unity-preview-formal')
    parser.add_argument('--out', type=Path, help='输出根目录；每轮创建独立目录，失败不会复用旧 PNG')
    parser.add_argument('--height', type=int, default=900)
    parser.add_argument('--verify-play', action='store_true', help='额外启动正式 Main 的 Play，比较同机位配置')
    parser.add_argument('--unity', default='/Applications/Unity/Unity.app/Contents/MacOS/Unity')
    parser.add_argument('--timeout', type=int, default=600)
    args = parser.parse_args()
    project = args.project.resolve()
    if project == SOURCE or project in SOURCE.parents or SOURCE in project.parents:
        parser.error('--project 必须是独立目录')
    marker = project / MARKER
    if project.exists() and any(project.iterdir()) and not marker.is_file():
        parser.error('缓存目录未由此工具创建，请使用空目录')
    if marker.exists() and json.loads(marker.read_text())['source'] != str(SOURCE):
        parser.error('缓存目录属于另一个源工程')
    if not Path(args.unity).is_file() or args.height < 64 or args.timeout < 1:
        parser.error('需要有效的 Unity、height >= 64、timeout > 0')
    project.mkdir(parents=True, exist_ok=True)
    marker.write_text(json.dumps({'source': str(SOURCE)}, ensure_ascii=False))
    started = time.monotonic()
    sync_project(SOURCE, project)
    digest = hashlib.sha256()
    for folder in PROJECT_FOLDERS:
        for path in sorted((project / folder).rglob('*')):
            if path.is_file():
                digest.update(str(path.relative_to(project)).encode('utf-8'))
                digest.update(hashlib.sha256(path.read_bytes()).digest())
    input_sha256 = digest.hexdigest()
    output_root = (args.out or ROOT / '.cache/citybox-previews').resolve()
    if output_root == SOURCE or SOURCE in output_root.parents or output_root == project or project in output_root.parents:
        parser.error('--out 不得在源工程或缓存工程内部')
    output_root.mkdir(parents=True, exist_ok=True)
    output = output_root / datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    output.mkdir()
    scenes = args.scene or SCENES
    env = dict(os.environ, SSNOIR_PREVIEW_OUTPUT=str(output),
               SSNOIR_PREVIEW_SCENES='|'.join(scenes), SSNOIR_PREVIEW_HEIGHT=str(args.height),
               SSNOIR_PREVIEW_PLAY='1' if args.verify_play else '0')
    # 不使用 -quit：Editor→Play 的异步截图完成后由 C# 明确退出。
    with (output / 'launch.log').open('w') as log:
        result = subprocess.run([args.unity, '-batchmode', '-job-worker-count', '2', '-buildTarget', 'WebGL', '-projectPath', str(project),
            '-executeMethod', 'SSNoir.Editor.CityWorldPreview.Render', '-logFile', str(output / 'unity.log')],
            env=env, stdout=log, stderr=subprocess.STDOUT, timeout=args.timeout)
    if result.returncode or not (output / 'complete.json').is_file():
        raise RuntimeError(f'本轮预览失败，不能使用旧图：{output / "unity.log"}')
    log_text = (output / 'unity.log').read_text(errors='replace')
    if re.search(r'^(?:[A-Za-z0-9_.]*)Exception:|^Shader error|\berror CS[0-9]+|^Assertion failed', log_text, re.MULTILINE):
        raise RuntimeError(f'Unity 导入或运行日志包含错误，本轮不能通过：{output / "unity.log"}')
    complete = json.loads((output / 'complete.json').read_text())
    if complete['playVerified'] != args.verify_play:
        raise RuntimeError('未完成要求的 Play 验证')
    files = []
    comparisons = {}
    for scene in scenes:
        modes = ('editor', 'play') if args.verify_play else ('editor',)
        for mode in modes:
            for suffix in ('png', 'json'):
                path = output / f'{mode}-{scene}.{suffix}'
                files.append({'file': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
        if args.verify_play:
            a, b = [json.loads((output / f'{mode}-{scene}.json').read_text()) for mode in modes]
            comparisons[scene] = differences(a, b)
    report = dict(complete, inputSha256=input_sha256, scenes=list(scenes), files=files, configurationDifferences=comparisons,
                  seconds=round(time.monotonic() - started, 1), project=str(project))
    (output / 'result.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print(f'正式 Unity 预览：{output}\n耗时 {report["seconds"]} 秒')
    if any(comparisons.values()):
        raise RuntimeError('Editor/Play 配置有差异，详见 result.json')


if __name__ == '__main__':
    main()
