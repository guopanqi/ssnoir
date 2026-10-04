"""把 CityBox 候选导入独立 URP 验证工程并实际截图；不操作正在打开的 Unity。"""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parents[2]
CITYBOX = ROOT / "city-box"
UNITY = ROOT / "UnityClient"
SCENES = ("别给他们想要的", "晚宴", "老街酒馆", "码头")


def run(args, log, env=None):
    with log.open("w") as output:
        result = subprocess.run(args, stdout=output, stderr=subprocess.STDOUT, env=env)
    if result.returncode:
        raise RuntimeError(f"命令失败 ({result.returncode})：详见 {log}")


def prepare(project):
    assets = project / "Assets"
    (assets / "Editor").mkdir(parents=True, exist_ok=True)
    (project / "Packages").mkdir(exist_ok=True)
    if not (project / "ProjectSettings").exists():
        shutil.copytree(UNITY / "ProjectSettings", project / "ProjectSettings")
    shutil.copy2(UNITY / "Assets/Editor/Pipeline/CityBoxVisualReview.cs",
                 assets / "Editor/CityBoxVisualReview.cs")
    script = UNITY / 'Assets/Scripts/Runtime/Environment/CityBoxReviewSettings.cs'
    shutil.copy2(script, assets / script.name)
    shutil.copy2(Path(str(script) + '.meta'), assets / (script.name + '.meta'))
    for name in ("UniversalRP-Asset.asset", "UniversalRP-Asset_Renderer.asset"):
        shutil.copy2(UNITY / "Assets" / name, assets / name)
        shutil.copy2(UNITY / "Assets" / (name + ".meta"), assets / (name + ".meta"))
    # 校准基线只保留原项目的 URP 配置；不复制游戏脚本、平台 SDK 或场景后处理 Feature。
    renderer = assets / "UniversalRP-Asset_Renderer.asset"
    text = "\n--- ".join(renderer.read_text().split("\n--- ")[:2])
    text = re.sub(r"  m_RendererFeatures:\n(?:  -.*\n)+", "  m_RendererFeatures: []\n", text)
    text = re.sub(r"  m_RendererFeatureMap: .*", "  m_RendererFeatureMap: ", text)
    renderer.write_text(text)
    shutil.copytree(ROOT / "tools/citybox-unity-preview/shaders", assets / "CityBoxReview/Shaders", dirs_exist_ok=True)
    version = json.loads((UNITY / "Packages/manifest.json").read_text())["dependencies"]["com.unity.render-pipelines.universal"]
    manifest = {"dependencies": {
        "com.unity.render-pipelines.universal": version,
        "com.unity.modules.imageconversion": "1.0.0",
        "com.unity.modules.physics": "1.0.0",
        "com.unity.modules.jsonserialize": "1.0.0",
    }}
    (project / "Packages/manifest.json").write_text(json.dumps(manifest, indent=2))


def apply_overrides(spec, patch):
    """仅允许明确的视觉参数，不接受几何/材质槽或任意 Unity 属性。"""
    allowed = {
        "materials": {"baseLinear", "emissionLinear", "roughness", "metallic"},
        "lights": {"position", "direction", "up", "colorLinear", "energy", "size", "spotAngle", "spotBlend", "shadows"},
        "cameras": {"position", "direction", "up", "verticalFov"},
    }
    if set(patch) - set(allowed):
        raise ValueError("参数文件只接受 materials、lights、cameras")
    import math
    for group, updates in patch.items():
        if not isinstance(updates, dict): raise ValueError(f"{group} 应为以名称索引的对象")
        rows = {row['name']: row for row in spec[group]}
        for name, values in updates.items():
            if name not in rows: raise ValueError(f"未知 {group} 名称：{name}")
            if not isinstance(values, dict) or set(values) - allowed[group]:
                raise ValueError(f"不支持的参数：{group}/{name}")
            row = rows[name]
            for key, value in values.items():
                current = row[key]
                if isinstance(current, dict):
                    if not isinstance(value, dict) or set(value) != set(current):
                        raise ValueError(f"{name}/{key} 必须提供完整分量 {tuple(current)}")
                    numbers = value.values()
                elif isinstance(current, bool):
                    if not isinstance(value, bool): raise ValueError(f"{name}/{key} 必须为布尔值")
                    numbers = []
                else: numbers = [value]
                if any(isinstance(n, bool) or not isinstance(n, (int,float)) or not math.isfinite(n) for n in numbers):
                    raise ValueError(f"{name}/{key} 必须为有限数字")
                if key in {"roughness", "metallic", "spotBlend"} and not 0 <= value <= 1:
                    raise ValueError(f"{name}/{key} 必须在 0–1 之间")
                if key in {"energy", "size"} and value < 0:
                    raise ValueError(f"{name}/{key} 不得小于零")
                if key == 'size' and row['type'] == 'AREA' and value == 0:
                    raise ValueError("面光尺寸必须大于零")
                if key in {'direction','up'} and sum(n*n for n in value.values()) == 0:
                    raise ValueError(f"{name}/{key} 不得为零向量")
                if key in {'verticalFov','spotAngle'} and not 0 < value < 180:
                    raise ValueError(f"{name}/{key} 必须在 0–180 度之间")
                row[key] = value
    return spec


def parameter_preview(args, project):
    if not args.scene or len(args.scene) != 1:
        raise ValueError('--parameters 需要一个 --scene')
    name = args.scene[0]
    baseline = project / 'Assets/CityBoxReview' / name / 'scene.visual.json'
    if not baseline.is_file() or not baseline.with_name('review.unity').is_file():
        raise ValueError('先用同一 --project 执行一次完整场景预览，建立几何缓存')
    spec = apply_overrides(json.loads(baseline.read_text()), json.loads(args.parameters.read_text()))
    output = (args.out or ROOT / '.cache/citybox-parameters' / name).resolve()
    if output == UNITY.resolve() or UNITY.resolve() in output.parents:
        raise ValueError('--out 不得位于 UnityClient 中')
    output.mkdir(parents=True, exist_ok=True)
    payload = output / 'parameters.visual.json'
    payload.write_text(json.dumps(spec,ensure_ascii=False,indent=2) + '\n')
    prepare(project)
    env = dict(os.environ, CITYBOX_PARAMETER_SCENE=name,
               CITYBOX_PARAMETER_INPUT=str(payload), CITYBOX_PARAMETER_OUTPUT=str(output),
               CITYBOX_VISUAL_BAKE='1' if args.bake_lights else '0')
    for name in ("result.json", "preview.png"):
        (output / name).unlink(missing_ok=True)
    started = time.monotonic()
    run([args.unity, '-batchmode', '-quit', '-projectPath', str(project),
         '-executeMethod', 'SSNoir.Editor.CityBoxVisualReview.RenderParameters',
         '-logFile', str(output/'unity.log')], output/'launch.log', env)
    print(f'Unity 真实预览：{output / "preview.png"}')
    print(f'参数预览耗时：{time.monotonic()-started:.1f} 秒；复用几何与 Library')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scene", action="append", choices=SCENES, help="不传则比较四个场景")
    parser.add_argument("--project", type=Path, default=ROOT / ".cache/citybox-unity-preview")
    parser.add_argument("--blender", default=shutil.which("blender"))
    parser.add_argument("--unity", default="/Applications/Unity/Unity.app/Contents/MacOS/Unity")
    parser.add_argument('--publish-trial', action='store_true', help='把独立试用场景发布到 UnityClient/Assets/CityBoxReview')
    parser.add_argument('--bake-lights', action='store_true', help='晚宴实验：烘焙柔光与静态反射')
    parser.add_argument("--parameters", type=Path, help="复用已生成场景，仅修改命名材质/灯光/相机参数并渲染")
    parser.add_argument("--out", type=Path, help="参数预览输出目录")
    args = parser.parse_args()
    if args.bake_lights and args.scene != ['晚宴']:
        parser.error('--bake-lights 目前只验证了 --scene 晚宴')
    if (not args.blender and not args.parameters) or not Path(args.unity).is_file():
        parser.error("需要 Blender 和 Unity 可执行文件；可通过 --blender / --unity 指定")
    project = args.project.resolve()
    if project == UNITY.resolve() or project in UNITY.resolve().parents or UNITY.resolve() in project.parents:
        parser.error("--project 必须是独立验证目录")
    marker = project / '.citybox-visual-review'
    if project.exists() and any(project.iterdir()) and not marker.is_file():
        parser.error("验证目录已有内容且没有 .citybox-visual-review 标记，请使用空目录")
    project.mkdir(parents=True, exist_ok=True)
    marker.touch()
    if args.parameters:
        if args.publish_trial: parser.error("参数实验不直接发布，请先完成视觉验收")
        parameter_preview(args,project)
        return
    if args.out: parser.error("--out 目前仅用于 --parameters")
    output = CITYBOX / "prefabs/review/noir-study/unity"
    for scene in args.scene or SCENES:
        source = CITYBOX / "prefabs/review" / scene / "noir-study/candidate.blend"
        if not source.is_file():
            raise FileNotFoundError(f"先生成候选：{source}")
        run([args.blender, "-b", "--python", str(CITYBOX / "pipeline/visual_export.py"), "--",
             "--blend", str(source), "--out", str(output / scene), "--camera", "Camera_" + scene],
            project / (scene + ".export.log"))
    prepare(project)
    env = dict(os.environ, CITYBOX_VISUAL_SOURCE=str(output),
               CITYBOX_VISUAL_SCENES='|'.join(args.scene or SCENES),
               CITYBOX_VISUAL_BAKE='1' if args.bake_lights else '0')
    run([args.unity, "-batchmode", "-quit", "-projectPath", str(project),
         "-executeMethod", "SSNoir.Editor.CityBoxVisualReview.Build",
         "-logFile", str(project / "unity.log")], project / "launch.log", env)
    print(f"Unity 实际对照图：{output}")
    print(f"独立 Unity 工程：{project}")
    if args.publish_trial:
        publish_trial(project)


def publish_trial(project):
    source = project / 'Assets/CityBoxReview'
    assert (source / 'ReviewPipeline.asset').is_file()
    target = UNITY / 'Assets/CityBoxReview'
    cache = ROOT / '.cache'
    cache.mkdir(exist_ok=True)
    # 在 Assets 外准备完整包，再整体移动，避免编辑器读到只复制了一半的场景。
    with tempfile.TemporaryDirectory(prefix='citybox-publish-', dir=cache) as directory:
        staging = Path(directory) / 'CityBoxReview'
        shutil.copytree(source, staging)
        backup = Path(directory) / 'previous'
        if target.exists():
            target.rename(backup)
        try:
            staging.rename(target)
        except BaseException:
            if backup.exists():
                backup.rename(target)
            raise
    print(f'已发布 Unity 独立试用场景：{target}')


if __name__ == "__main__":
    main()
