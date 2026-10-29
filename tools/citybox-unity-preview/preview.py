"""复制正式 Unity 工程到独立缓存，以同一 Main、资产和运行时代码截图。"""
import argparse
import hashlib
import html
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
    parser.add_argument('--clip', help='审阅一个已发布的完整 clip 名；需指定一个 --scene 机位，不进入 Play')
    parser.add_argument('--times', help='片段内采样秒数，逗号分隔；默认首、中、末')
    parser.add_argument('--pose', action='append', default=[], help='先采样同模型的准备姿态 clip 首帧，可重复')
    parser.add_argument('--unity', default='/Applications/Unity/Unity.app/Contents/MacOS/Unity')
    parser.add_argument('--timeout', type=int, default=600)
    args = parser.parse_args()
    if args.clip and (args.verify_play or not args.scene or len(args.scene) != 1):
        parser.error('--clip 需要且只接受一个 --scene，不能与 --verify-play 合用')
    if not args.clip and (args.times is not None or args.pose):
        parser.error('--times / --pose 只用于 --clip')
    try:
        times = [] if args.times is None else [float(t) for t in args.times.split(',')]
        if any(not math.isfinite(t) or t < 0 for t in times):
            raise ValueError()
    except ValueError:
        parser.error('--times 必须是逗号分隔的非负有限秒数')
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
               SSNOIR_PREVIEW_PLAY='1' if args.verify_play else '0',
               SSNOIR_PREVIEW_MOTION=json.dumps(dict(clip=args.clip, times=times, poses=args.pose), ensure_ascii=False) if args.clip else '')
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
    if args.clip:
        motion = json.loads((output / 'motion.json').read_text())
        for frame in motion['frames']:
            for suffix in ('png', 'json'):
                path = output / f"frame-{frame['index']:03d}.{suffix}"
                files.append({'file': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
        cards = ''.join(f"<figure><a href='{f['image']}'><img src='{f['image']}'></a><figcaption>{f['seconds']:.3f}s</figcaption></figure>"
                        for f in motion['frames'])
        (output / 'index.html').write_text('<!doctype html><meta charset="utf-8"><title>动画审阅</title>'
            '<style>body{background:#17191c;color:#ddd;font:16px sans-serif}main{display:flex;flex-wrap:wrap}'
            'figure{margin:8px;width:min(480px,95vw)}img{width:100%}</style>'
            f'<h1>{html.escape(args.clip)}</h1><p>固定帧不代替动作与构图审阅，点击图片看原图。</p><main>{cards}</main>')
        files.append({'file': 'motion.json', 'sha256': hashlib.sha256((output / 'motion.json').read_bytes()).hexdigest()})
        if motion['negativeScaleChannels']:
            print('注意：导入曲线含负缩放，请判断是否为有意镜像；详见 motion.json')
    for scene in ([] if args.clip else scenes):
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
