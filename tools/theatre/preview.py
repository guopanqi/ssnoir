#!/usr/bin/env python3
"""使用项目的 Unity 舞台绘制代码导出序列帧、对白索引图和无声 GIF。"""
import argparse
import fcntl
import json
import shutil
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TEMPLATE = Path(__file__).resolve().parent / "preview"


def copy_file(source, destination):
    if not source.is_file():
        raise FileNotFoundError(source)
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)


def owned_directory(path, marker):
    path.mkdir(parents=True, exist_ok=True)
    if any(path.iterdir()) and not (path / marker).exists():
        raise ValueError(f"目录已有其他文件，请指定空目录：{path}")
    (path / marker).touch()


def prepare_project(project, scene):
    assets = project / "Assets"
    for name in ("TheatreScene.cs", "TheatreSession.cs"):
        copy_file(ROOT / "Engine/Runtime/Theatre" / name, assets / "Runtime" / name)
    for name in ("TheatreMesh.cs", "TheatreSurface.cs", "TheatreSettings.cs"):
        source = ROOT / "UnityClient/Assets/Scripts/Runtime/Theatre" / name
        copy_file(source, assets / "Runtime" / name)
        copy_file(source.with_suffix(".cs.meta"), assets / "Runtime" / (name + ".meta"))
    (assets / "Runtime/Init.cs").write_text("namespace System.Runtime.CompilerServices { internal static class IsExternalInit {} }\n")
    copy_file(TEMPLATE / "RenderPreview.cs", assets / "Editor/RenderPreview.cs")
    copy_file(TEMPLATE / "Composite.shader", assets / "Resources/Preview/Composite.shader")
    resource_root = ROOT / "UnityClient/Assets/Resources"
    for name in ("LineTheatre.shader", "LineTheatreSettings.asset", "LineTheatreSettings.asset.meta"):
        copy_file(resource_root / "Theatre" / name, assets / "Resources/Theatre" / name)
    required = {(node["Asset"], ".png") for node in scene["Nodes"] if node["Shape"] == "Image"}

    def walk(cue):
        if cue["Kind"] == "Image":
            required.add((cue["Asset"], ".png"))
        if cue["Kind"] == "Sound":
            required.add((cue["Asset"], ".wav"))
        for child in cue["Children"]:
            walk(child)

    walk(scene["Program"])
    required.update((asset + "_silhouette", ".png") for asset, extension in list(required)
                    if extension == ".png" and asset.startswith("Portraits/Neon/"))
    required.update((asset + ".neon", ".json") for asset, extension in list(required)
                    if extension == ".png" and asset.startswith("Portraits/Neon/") and not asset.endswith("_silhouette"))
    for asset, extension in required:
        relative = Path(asset + extension)
        for suffix in ("", ".meta"):
            copy_file(resource_root / (str(relative) + suffix), assets / "Resources" / (str(relative) + suffix))
    dlls = list((ROOT / "UnityClient/Library/PackageCache").glob("com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll"))
    if len(dlls) != 1:
        raise ValueError("需要已导入的 Unity Newtonsoft.Json 包，未找到唯一 DLL")
    copy_file(dlls[0], assets / "Plugins/Newtonsoft.Json.dll")
    copy_file(ROOT / "UnityClient/ProjectSettings/ProjectVersion.txt", project / "ProjectSettings/ProjectVersion.txt")
    packages = project / "Packages"
    packages.mkdir(exist_ok=True)
    (packages / "manifest.json").write_text('{"dependencies":{}}\n')


def run_unity(unity, project, output):
    result_file = output / "result.json"
    result_file.unlink(missing_ok=True)
    process = subprocess.Popen([str(unity), "-batchmode", "-quit", "-projectPath", str(project),
                                "-executeMethod", "RenderPreview.Run", "-logFile", str(output / "unity.log")],
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    started, completed = time.monotonic(), None
    try:
        while process.poll() is None:
            if result_file.exists():
                if completed is None:
                    completed = time.monotonic()
                if time.monotonic() - completed > 10:
                    break  # Unity 已写出结果，但某些版本会卡在关闭；仅终止本工具启动的进程。
            if time.monotonic() - started > 600:
                raise TimeoutError(f"Unity 超过 10 分钟；日志：{output / 'unity.log'}")
            time.sleep(.25)
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
    if not result_file.exists():
        raise RuntimeError(f"Unity 未生成结果，退出码 {process.returncode}；日志：{output / 'unity.log'}")
    result = json.loads(result_file.read_text())
    if not result["success"]:
        raise RuntimeError(result["error"])


def make_overview(output):
    from PIL import Image, ImageDraw, ImageFont
    manifest = json.loads((output / "manifest.json").read_text())
    frames = manifest["frames"]
    if not frames:
        raise ValueError("演出没有生成任何帧")
    # 完整动画使用全部采样帧，索引图同时覆盖均匀采样与每次台词变化。
    animation = []
    for frame in frames:
        with Image.open(output / frame["file"]) as image:
            image.thumbnail((800, 800), Image.Resampling.LANCZOS)
            animation.append(image.convert("P", palette=Image.Palette.ADAPTIVE, colors=128))
    animation[0].save(output / "show.gif", save_all=True, append_images=animation[1:],
                      duration=round(1000 / manifest["fps"]), loop=0, disposal=2, optimize=False)
    chosen = {round(i * (len(frames) - 1) / 11) for i in range(12)}
    previous = None
    for i, frame in enumerate(frames):
        text = frame.get("caption")
        if text != previous and text:
            chosen.add(i)
        previous = text
    chosen = sorted(chosen)
    font_path = ROOT / "UnityClient/Assets/Resources/Fonts/SourceHanSerifCN-Regular.ttf"
    font = ImageFont.truetype(str(font_path), 14)
    tile_width = 360
    tile_height = round(tile_width * manifest["height"] / manifest["width"])
    sheet = Image.new("RGB", (tile_width * 3, (tile_height + 76) * ((len(chosen) + 2) // 3)), "#101219")
    draw = ImageDraw.Draw(sheet)
    for slot, i in enumerate(chosen):
        frame = frames[i]
        x, y = (slot % 3) * tile_width, (slot // 3) * (tile_height + 76)
        with Image.open(output / frame["file"]) as image:
            sheet.paste(image.resize((tile_width, tile_height)), (x, y))
        label = f"{i:04d} · {frame['time']:.2f}s · {frame['phase']}"
        draw.text((x + 8, y + tile_height + 4), label, font=font, fill="#AAB2C2")
        caption = (frame.get("speaker") or "") + "  " + (frame.get("caption") or "")
        row, rows = "", []
        for char in caption:
            if font.getlength(row + char) > tile_width - 16:
                rows.append(row)
                row = ""
            row += char
        rows.append(row)
        if len(rows) > 2:
            rows[1] = rows[1][:-1] + "…"
        for line, text in enumerate(rows[:2]):
            draw.text((x + 8, y + tile_height + 25 + line * 20), text, font=font, fill="#E4DDD0")
    sheet.save(output / "index.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("script", help="相对 Resources/Content 的 Scheme 文件路径")
    parser.add_argument("expression", help="调用演出的 Scheme 表达式，如 (雨夜来访-演出)")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--unity", type=Path, default=Path("/Applications/Unity/Unity.app/Contents/MacOS/Unity"))
    parser.add_argument("--width", type=int, default=1600)
    parser.add_argument("--height", type=int, default=900)
    parser.add_argument("--fps", type=int, default=4)
    parser.add_argument("--manual-wait", type=float, default=0, help="显式模拟点击等待秒数；默认遇到等待点击报错")
    parser.add_argument("--max-seconds", type=float, default=240)
    args = parser.parse_args()
    if not (1 <= args.fps <= 60 and 64 <= args.width <= 3840 and 64 <= args.height <= 2160
            and 0 <= args.manual_wait <= 120 and 0 < args.max_seconds <= 3600):
        parser.error("尺寸、帧率或时长超出支持范围")
    if not args.unity.is_file():
        parser.error("未找到 Unity；使用 --unity 指定路径")
    content = ROOT / "UnityClient/Assets/Resources/Content"
    script = (content / args.script).resolve()
    script.relative_to(content)
    if not script.is_file():
        parser.error("Scheme 文件不存在")
    output = (args.output or ROOT / ".cache/theatre-preview" / script.stem).resolve()
    project = ROOT / ".cache/theatre-preview-project"
    owned_directory(output, ".theatre-preview")
    owned_directory(project, ".theatre-preview-project")
    with (project / ".preview-lock").open("w") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        # 清掉上次成功产物，避免本次失败后误看旧预览。
        for name in ("manifest.json", "index.png", "show.gif", "scene.json", "result.json"):
            (output / name).unlink(missing_ok=True)
        frame_dir = output / "frames"
        if frame_dir.exists():
            shutil.rmtree(frame_dir)
        frame_dir.mkdir()
        expression = "(begin (load-file " + json.dumps(args.script, ensure_ascii=False) + ") " + args.expression + ")"
        with (output / "export.log").open("w") as log:
            subprocess.run(["dotnet", "run", "--project", "tools/content-validator/SSNoir.ContentValidator.csproj",
                            "--", "--theatre-export", expression, str(output / "scene.json")], cwd=ROOT,
                           stdout=log, stderr=subprocess.STDOUT, check=True)
        prepare_project(project, json.loads((output / "scene.json").read_text()))
        settings_text = (ROOT / "UnityClient/ProjectSettings/ProjectSettings.asset").read_text()
        request = dict(ScenePath=str(output / "scene.json"), Output=str(output), Width=args.width, Height=args.height,
                       Fps=args.fps, MaxSeconds=args.max_seconds, ManualWait=args.manual_wait,
                       Linear="m_ActiveColorSpace: 1" in settings_text)
        (project / "request.json").write_text(json.dumps(request))
        run_unity(args.unity, project, output)
        make_overview(output)
    print(f"舞台层预览（无声、无游戏字幕排版）：{output / 'show.gif'}")
    print(f"对白与时间索引：{output / 'index.png'}")


if __name__ == "__main__":
    main()
