#!/usr/bin/env python3
"""Execute one Scheme stage and render an approximate contact sheet for visual review."""

import argparse
import json
import math
import shutil
import subprocess
import wave
from functools import lru_cache
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / "UnityClient/Assets/Resources"
FONT_CANDIDATES = [
    "/System/Library/Fonts/PingFang.ttc",
    "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
    "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
]

# 与 StoryStageDrawer.TypewriterCharactersPerSecond 对齐。
TYPEWRITER_CPS = 30.0
# 对白拍在打字机 / VO 结束后额外留白，便于读完再切下一拍。
SAY_HOLD_SECONDS = 0.4
VIDEO_FPS = 12
AUDIO_EXTENSIONS = (".wav", ".ogg", ".mp3")


def font(size):
    for path in FONT_CANDIDATES:
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def command_kind(command):
    return command["Kind"].lower()


def position(entity, now):
    points, started, duration = entity["motion"]
    t = 1 if duration <= 0 else min(1, max(0, (now - started) / duration))
    work = [tuple(p) for p in points]
    for remaining in range(len(work) - 1, 0, -1):
        work = [tuple(a + (b - a) * t for a, b in zip(work[i], work[i + 1]))
                for i in range(remaining)]
    return work[0]


def motion(entity, points, now, duration):
    entity["motion"] = (points, now, duration)


def apply_beat(state, beat, now):
    effects = []
    sounds = []
    line = None
    duration = max((c["Seconds"] for c in beat["Commands"]), default=0)
    for command in beat["Commands"]:
        kind = command_kind(command)
        key = command["Id"]
        if kind in ("spawn", "prop", "propat"):
            x, y = command["X"], command["Y"]
            if kind == "propat":
                anchor = state[command["Anchor"]]
                ax, ay = position(anchor, now)
                x, y = ax + x, ay + y
            if key in state:
                raise ValueError(f"重复舞台对象：{key}")
            state[key] = dict(kind="actor" if kind == "spawn" else "prop", asset=command["Asset"],
                              layer=command["Layer"], pose="基础", on_left=x < 0,
                              motion=([(x, y)], now, 0))
        elif kind in ("move", "path"):
            entity = state[key]
            if kind == "move":
                points = [position(entity, now), (command["X"], command["Y"])]
            else:
                points = [(p["X"], p["Y"]) for p in command["Points"]]
                if command["Relative"]:
                    ox, oy = position(entity, now)
                    points = [(x + ox, y + oy) for x, y in points]
            motion(entity, points, now, command["Seconds"])
        elif kind == "remove":
            del state[key]
        elif kind == "pose":
            state[key]["pose"] = command["Asset"]
        elif kind == "effect":
            x, y = position(state[key], now)
            effects.append((command["Asset"], x + command["X"], y + command["Y"]))
        elif kind == "sound":
            sounds.append(command["Asset"])
        elif kind == "say":
            line = command["Line"]
    return duration, effects, sounds, line


def resolve_audio(folder, asset):
    """Resources/{folder}/{asset}.{wav|ogg|mp3}；asset 可含路径分隔。"""
    if not asset:
        return None
    for ext in AUDIO_EXTENSIONS:
        path = RES / folder / f"{asset}{ext}"
        if path.is_file():
            return path
    return None


def audio_duration(path):
    path = Path(path)
    if path.suffix.lower() == ".wav":
        with wave.open(str(path), "rb") as handle:
            rate = handle.getframerate()
            if rate <= 0:
                return 0.0
            return handle.getnframes() / float(rate)
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format=duration",
         "-of", "default=noprint_wrappers=1:nokey=1", str(path)],
        capture_output=True, text=True, check=True)
    return float(probe.stdout.strip() or 0)


def say_beat_seconds(line):
    """对白拍时长：max(打字机, VO) + 短留白。无 VoiceId / 文件缺失时仅打字机+留白。"""
    text = line.get("Text") or ""
    typewriter = len(text) / TYPEWRITER_CPS
    voice = 0.0
    voice_id = line.get("VoiceId")
    path = resolve_audio("Voices", voice_id) if voice_id else None
    if path is not None:
        voice = audio_duration(path)
    return max(typewriter, voice) + SAY_HOLD_SECONDS


def beat_duration(command_duration, line):
    if line:
        return say_beat_seconds(line)
    return command_duration


@lru_cache(maxsize=64)
def portrait(asset, pose, on_left, height):
    name = asset if pose == "基础" else f"{asset}_{pose}"
    path = RES / "Portraits/Neon" / f"{name}.png"
    if not path.is_file():
        raise FileNotFoundError(f"缺少姿势图：{path}")
    im = Image.open(path).convert("RGB")
    # LampPainter UV: x=.20..90, y=.02..99; PIL's vertical origin is opposite Unity's.
    w, h = im.size
    im = im.crop((round(w * .20), round(h * .01), round(w * .90), round(h * .98)))
    if not on_left:
        im = ImageOps.mirror(im)
    width = round(height * .70 / .97)
    im = im.resize((width, round(height)), Image.Resampling.LANCZOS)
    alpha = im.convert("L").point(lambda v: 0 if v < 10 else min(255, int(v * 1.8)))
    rgba = im.convert("RGBA")
    rgba.putalpha(alpha)
    return rgba


@lru_cache(maxsize=32)
def prop_image(asset, height):
    path = RES / "StageProps" / f"{asset}.png"
    if not path.is_file():
        raise FileNotFoundError(f"缺少道具图：{path}")
    im = Image.open(path).convert("RGBA")
    # StageProps currently use Unity's grayScaleToAlpha importer setting.
    if im.getchannel("A").getextrema() == (255, 255):
        alpha = im.convert("RGB").convert("L")
        im.putalpha(alpha)
    width = round(height * im.width / im.height)
    return im.resize((width, round(height)), Image.Resampling.LANCZOS)


def paste_glow(canvas, image, x, y):
    glow = image.copy()
    glow.putalpha(image.getchannel("A").filter(ImageFilter.GaussianBlur(7)).point(lambda v: int(v * .32)))
    canvas.alpha_composite(glow, (round(x), round(y)))
    canvas.alpha_composite(image, (round(x), round(y)))


def render(state, now, effects, line, beat_number, phase, width, height):
    canvas = Image.new("RGBA", (width, height), (3, 7, 17, 255))
    draw = ImageDraw.Draw(canvas)
    dialogue_top = height - min(230, height * .26) - 14
    portrait_height = min(max(dialogue_top + 42 - 28, height * .55), height * .92)
    layer_y = {"back": -20, "middle": 0, "front": 16}
    for entity in sorted((e for e in state.values() if e["kind"] == "actor"),
                         key=lambda e: (e is state.get(line.get("Speaker") if line else ""),
                                        list(layer_y).index(e["layer"]))):
        x, y = position(entity, now)
        image = portrait(entity["asset"], entity["pose"], entity["on_left"], portrait_height)
        left = width * (.5 + x / 20) - image.width / 2
        top = 28 + layer_y[entity["layer"]] - y * height / 20
        paste_glow(canvas, image, left, top)
    for entity in sorted((e for e in state.values() if e["kind"] == "prop"),
                         key=lambda e: list(layer_y).index(e["layer"])):
        x, y = position(entity, now)
        image = prop_image(entity["asset"], height * .22)
        cx = width * (.5 + x / 20)
        cy = dialogue_top - height * (.06 + y / 20)
        canvas.alpha_composite(image, (round(cx - image.width / 2), round(cy - image.height / 2)))
    overlay = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    effect_draw = ImageDraw.Draw(overlay)
    for effect, x, y, age in effects:
        cx = width * (.5 + x / 20)
        cy = dialogue_top - height * (.06 + y / 20)
        progress = min(1, max(0, age / .2))
        radius = height * (.13 + progress * .05) / 2
        if effect == "受击闪光":
            core = radius * .17
            effect_draw.ellipse((cx - core, cy - core, cx + core, cy + core),
                                fill=(255, 255, 255, round(220 * (1 - progress))))
        for i in range(8):
            angle = i * math.pi / 4
            effect_draw.line((cx + radius * .23 * math.cos(angle), cy + radius * .23 * math.sin(angle),
                              cx + radius * .82 * math.cos(angle), cy + radius * .82 * math.sin(angle)),
                             fill=(255, 255, 255, round(230 * (1 - progress))) if effect == "受击闪光"
                                  else (255, 227, 185, round(230 * (1 - progress))),
                             width=max(1, round(radius * .05)))
    canvas.alpha_composite(overlay)
    draw = ImageDraw.Draw(canvas)
    if line:
        box_width = min(1180, max(320, width - 80))
        box_height = 104
        box_left = (width - box_width) / 2
        box_top = height - box_height - 14
        draw.rectangle((box_left, box_top, box_left + box_width, box_top + box_height),
                       fill=(239, 234, 224, 255), outline=(28, 26, 21, 255), width=2)
        draw.text((box_left + 36, box_top + 12), line["Speaker"], font=font(17), fill=(28, 26, 21))
        draw.text((box_left + 36, box_top + 48), line["Text"], font=font(22), fill=(28, 26, 21))
    draw.rectangle((0, 0, width, 27), fill=(4, 8, 16, 230))
    draw.text((12, 3), f"拍 {beat_number:02d} · {phase} · {now:.2f}s · 离线构图预览",
              font=font(16), fill=(190, 207, 228))
    return canvas.convert("RGB")


def collect_audio_cues(sounds, line, start):
    cues = []
    for asset in sounds:
        path = resolve_audio("StageSounds", asset)
        if path is not None:
            cues.append((path, start))
    if line:
        voice_id = line.get("VoiceId")
        path = resolve_audio("Voices", voice_id) if voice_id else None
        if path is not None:
            cues.append((path, start))
    return cues


def encode_video(output, frame_paths, fps, audio_cues, total_duration):
    """静帧序列 → 无声视频，再 adelay+amix VO/SFX。"""
    if not frame_paths:
        raise ValueError("无视频帧可编码")
    work = output / "_video_work"
    if work.exists():
        shutil.rmtree(work)
    work.mkdir(parents=True)
    try:
        for index, path in enumerate(frame_paths, 1):
            shutil.copy2(path, work / f"frame-{index:05d}.png")
        silent = work / "silent.mp4"
        subprocess.run([
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-framerate", str(fps),
            "-i", str(work / "frame-%05d.png"),
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            str(silent),
        ], check=True)
        target = output / "preview.mp4"
        if not audio_cues:
            shutil.copy2(silent, target)
            return target
        # 去重同路径同起点，避免并行拍里重复资源被 amix 两次。
        unique = []
        seen = set()
        for path, start in audio_cues:
            key = (str(path), round(start, 4))
            if key in seen:
                continue
            seen.add(key)
            unique.append((path, start))
        cmd = ["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-i", str(silent)]
        for path, _ in unique:
            cmd.extend(["-i", str(path)])
        filters = []
        labels = []
        for index, (_, start) in enumerate(unique, 1):
            delay_ms = max(0, int(round(start * 1000)))
            # adelay 对每个声道各写一次；单声道也写两遍无害。
            label = f"a{index}"
            filters.append(f"[{index}:a]adelay={delay_ms}|{delay_ms},aformat=sample_fmts=fltp:channel_layouts=stereo[{label}]")
            labels.append(f"[{label}]")
        mix = "".join(labels) + (
            f"amix=inputs={len(labels)}:duration=longest:dropout_transition=0:normalize=0[amixed]"
        )
        filters.append(mix)
        # 补齐到视频时长，避免播放器在音轨结束后提前结束或显示音轨偏短。
        filters.append(f"[amixed]apad=whole_dur={total_duration:.3f}[aout]")
        cmd.extend([
            "-filter_complex", ";".join(filters),
            "-map", "0:v:0", "-map", "[aout]",
            "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
            "-t", f"{total_duration:.3f}",
            "-movflags", "+faststart",
            str(target),
        ])
        subprocess.run(cmd, check=True)
        return target
    finally:
        shutil.rmtree(work, ignore_errors=True)


def make_preview(sequence, output, width, height, columns, rows, gif_beats, gif_fps, gif_width, make_video):
    output.mkdir(parents=True, exist_ok=True)
    for pattern in ("[0-9][0-9][0-9]-beat-*.png", "contact-*.png"):
        for old in output.glob(pattern):
            old.unlink()
    (output / "preview.gif").unlink(missing_ok=True)
    (output / "preview.mp4").unlink(missing_ok=True)
    state = {}
    frames = []
    manifest = []
    active_effects = []
    gif_frames = []
    video_frame_paths = []
    audio_cues = []
    video_dir = output / "_video_frames"
    if make_video:
        if video_dir.exists():
            shutil.rmtree(video_dir)
        video_dir.mkdir(parents=True)
    now = 0.0
    for index, beat in enumerate(sequence["beats"], 1):
        command_duration, effects, sounds, line = apply_beat(state, beat, now)
        duration = beat_duration(command_duration, line)
        active_effects = [e for e in active_effects if now - e[0] < .2]
        active_effects.extend((now, *e) for e in effects)
        audio_cues.extend(collect_audio_cues(sounds, line, now))
        samples = [("开始", 0.0)]
        if any(command_kind(c) in ("move", "path") for c in beat["Commands"]):
            samples.append(("中途", duration / 2))
        if any(command_kind(c) == "effect" for c in beat["Commands"]):
            samples.append(("效果", min(.08, duration or .08)))
        if duration and not line:
            samples.append(("结束", duration))
        for phase, offset in samples:
            instant = now + offset
            visible_effects = [(name, x, y, instant - started) for started, name, x, y in active_effects
                               if 0 <= instant - started < .2]
            image = render(state, instant, visible_effects, line, index, phase, width, height)
            name = f"{len(frames) + 1:03d}-beat-{index:02d}-{phase}.png"
            image.save(output / name)
            frames.append(image)
            manifest.append(dict(file=name, beat=index, phase=phase, time=round(instant, 3),
                                 duration=round(duration, 3), sounds=sounds, line=line))
        if gif_beats and gif_beats[0] <= index <= gif_beats[1] and duration > 0:
            count = math.ceil(duration * gif_fps)
            for frame_index in range(count):
                instant = now + min(duration, frame_index / gif_fps)
                visible_effects = [(name, x, y, instant - started) for started, name, x, y in active_effects
                                   if 0 <= instant - started < .2]
                image = render(state, instant, visible_effects, line, index, "播放", width, height)
                gif_height = round(height * gif_width / width)
                gif_frames.append(image.resize((gif_width, gif_height), Image.Resampling.LANCZOS))
        if make_video and duration > 0:
            count = max(1, math.ceil(duration * VIDEO_FPS))
            for frame_index in range(count):
                instant = now + min(duration, frame_index / VIDEO_FPS)
                visible_effects = [(name, x, y, instant - started) for started, name, x, y in active_effects
                                   if 0 <= instant - started < .2]
                image = render(state, instant, visible_effects, line, index, "播放", width, height)
                # 偶数宽高，避免 libx264 yuv420p 报错。
                vw, vh = image.size
                if vw % 2 or vh % 2:
                    image = image.resize((vw + vw % 2, vh + vh % 2), Image.Resampling.LANCZOS)
                path = video_dir / f"frame-{len(video_frame_paths) + 1:05d}.png"
                image.save(path)
                video_frame_paths.append(path)
        elif make_video and duration <= 0:
            # 零时长拍（仅音效/效果）仍占一帧索引图，但不推进视频时钟；音效靠 adelay 叠到后续。
            pass
        now += duration
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2))
    cell_w, image_h = width // 3, height // 3
    cell_h = image_h + 25
    per_page = columns * rows
    for page_start in range(0, len(frames), per_page):
        sheet = Image.new("RGB", (cell_w * columns, cell_h * rows), (4, 8, 18))
        labels = ImageDraw.Draw(sheet)
        for i, frame in enumerate(frames[page_start:page_start + per_page]):
            x, y = i % columns * cell_w, i // columns * cell_h
            entry = manifest[page_start + i]
            caption = f'{entry["beat"]:02d} {entry["phase"]} {entry["time"]:.2f}s'
            if entry["line"]:
                caption += ' ' + entry["line"]["Text"][:10]
            labels.text((x + 6, y + 3), caption, font=font(15), fill=(210, 222, 238))
            sheet.paste(frame.resize((cell_w, image_h), Image.Resampling.LANCZOS), (x, y + 25))
        sheet.save(output / f"contact-{page_start // per_page + 1:02d}.png")
    if gif_frames:
        gif_frames[0].save(output / "preview.gif", save_all=True, append_images=gif_frames[1:],
                           duration=round(1000 / gif_fps), loop=0, optimize=True)
    video_path = None
    if make_video:
        if not video_frame_paths:
            raise ValueError("舞台总时长为 0，无法生成视频")
        video_path = encode_video(output, video_frame_paths, VIDEO_FPS, audio_cues, now)
        shutil.rmtree(video_dir, ignore_errors=True)
    summary = f"{len(frames)} 帧；{(len(frames) + per_page - 1) // per_page} 张序列图"
    if gif_frames:
        summary += f"；GIF {len(gif_frames) / gif_fps:.1f} 秒"
    if video_path:
        summary += f"；MP4 {now:.1f} 秒"
    print(f"{summary}：{output}")


def main():
    parser = argparse.ArgumentParser(description="按 Scheme 舞台入口生成 Agent 可审阅的演出序列图")
    parser.add_argument("expression", help="例如：(baines 'debug-play-street!)")
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--width", type=int, default=1227, help="虚拟画布宽度，默认 16:9")
    parser.add_argument("--height", type=int, default=690, help="虚拟画布高度，默认 UIScale Compact 档")
    parser.add_argument("--columns", type=int, default=4)
    parser.add_argument("--rows", type=int, default=4)
    gif_group = parser.add_mutually_exclusive_group()
    gif_group.add_argument("--gif", action="store_true", help="额外导出整场低分辨率 GIF（不替代序列帧）")
    gif_group.add_argument("--gif-beats", help="额外导出指定拍号范围的 GIF，例如 12:18")
    parser.add_argument("--gif-fps", type=int, default=10)
    parser.add_argument("--gif-width", type=int, default=640)
    parser.add_argument("--video", action="store_true",
                        help="额外导出带 VO/SFX 的 preview.mp4（ffmpeg；不替代序列帧）")
    args = parser.parse_args()
    if args.width <= 0 or args.height <= 0 or args.columns <= 0 or args.rows <= 0 or args.gif_fps <= 0 or args.gif_width <= 0:
        parser.error("尺寸与网格数必须为正数")
    gif_beats = None
    if args.gif_beats:
        try:
            first, last = (int(x) for x in args.gif_beats.split(":"))
            if first < 1 or last < first:
                raise ValueError()
            gif_beats = first, last
        except ValueError:
            parser.error("--gif-beats 应为 起始拍:结束拍，例如 12:18")
    args.out.mkdir(parents=True, exist_ok=True)
    exported = args.out / "stage.json"
    subprocess.run(["dotnet", "run", "--project",
                    str(ROOT / "tools/content-validator/SSNoir.ContentValidator.csproj"), "--",
                    "--stage-export", args.expression, str(exported)], cwd=ROOT, check=True)
    sequence = json.loads(exported.read_text())
    if args.gif:
        gif_beats = 1, len(sequence["beats"])
    make_preview(sequence, args.out, args.width, args.height,
                 args.columns, args.rows, gif_beats, args.gif_fps, args.gif_width, args.video)


if __name__ == "__main__":
    main()
