#!/usr/bin/env python3
"""从角色库的 MiMo 配音配置生成一条候选台词音频。"""

from __future__ import annotations

import argparse
import base64
import json
import os
import subprocess
import sys
import urllib.error
import urllib.request
from pathlib import Path


WORKSPACE = Path(__file__).resolve().parents[2]
VOICE_LIBRARY = WORKSPACE / "角色库"
CONFIG_PATH = VOICE_LIBRARY / "配音" / "角色.json"
DEFAULT_CANDIDATE_DIR = VOICE_LIBRARY / "配音" / "候选"
API_URL = "https://api.xiaomimimo.com/v1/chat/completions"
PRESET_MODEL = "mimo-v2.5-tts"
VOICE_CLONE_MODEL = "mimo-v2.5-tts-voiceclone"
VOICE_DESIGN_MODEL = "mimo-v2.5-tts-voicedesign"
KEYCHAIN_SERVICE = "com.ssnoir.mimo-tts.api-key"
KEYCHAIN_ACCOUNT = "SSNoir MiMo TTS"

# MiMo V2.5 官方内置预置音色
PRESET_VOICES = {
    # 中文音色
    "冰糖": "中文 / 活泼少女",
    "茉莉": "中文 / 知性女声",
    "苏打": "中文 / 阳光少年",
    "白桦": "中文 / 成熟男声",
    # 英文音色
    "Mia": "English / Female / Lively girl",
    "Chloe": "English / Female / Sweet Dreamy",
    "Milo": "English / Male / Sunny boy",
    "Dean": "English / Male / Steady Gentle",
}
ALLOWED_PRESET_VOICES = set(PRESET_VOICES.keys())


MODES = ("preset", "clone", "design")


def fail(message: str) -> None:
    raise SystemExit(f"错误：{message}")


def validate_preset_voice(voice: str, context: str = "") -> None:
    prefix = f"{context}：" if context else ""
    if not isinstance(voice, str) or not voice.strip():
        fail(f"{prefix}预置音色（voice）必须是非空字符串")
    if voice not in ALLOWED_PRESET_VOICES:
        supported = ", ".join(sorted(PRESET_VOICES.keys()))
        fail(f"{prefix}未知的预置音色「{voice}」；MiMo V2.5 支持的预置音色包括：{supported}")


def validate_character_config(name: str, char_data: dict) -> None:
    if not isinstance(char_data, dict):
        fail(f"角色「{name}」的配置必须是字典对象")

    mode = char_data.get("mode")
    has_voice = "voice" in char_data
    has_sample = "sample" in char_data

    if mode is not None and mode not in {"preset", "clone"}:
        fail(f"角色「{name}」配置了无效的 mode「{mode}」；仅支持 'preset' 或 'clone'")

    if mode == "preset" or (has_voice and mode != "clone"):
        if not has_voice:
            fail(f"角色「{name}」为 preset 模式，必须包含 'voice' 字段")
        if has_sample:
            fail(f"角色「{name}」为 preset 模式，不可包含 'sample' 样音字段")
        if "model" in char_data and char_data["model"] != PRESET_MODEL:
            fail(
                f"角色「{name}」为 preset 模式，必须强制使用模型 {PRESET_MODEL}，"
                f"不可指定其他模型「{char_data['model']}」"
            )
        voice_val = char_data["voice"]
        if not isinstance(voice_val, str) or not voice_val.strip():
            fail(f"角色「{name}」的 voice 必须是非空字符串，实际为 {repr(voice_val)}")
        validate_preset_voice(voice_val, context=f"角色「{name}」")
    elif mode == "clone" or (has_sample and mode != "preset"):
        if not has_sample:
            fail(f"角色「{name}」为 clone 模式，必须包含 'sample' 字段")
        if has_voice:
            fail(f"角色「{name}」为 clone 模式，不可包含 'voice' 预置音色字段")
        sample_val = char_data["sample"]
        if not isinstance(sample_val, str) or not sample_val.strip():
            fail(f"角色「{name}」的 sample 必须是非空字符串")
    else:
        fail(f"角色「{name}」必须配置 'voice'（预置音色）或 'sample'（克隆样音）之一")


def load_config() -> dict:
    try:
        config = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    except FileNotFoundError:
        fail(f"找不到角色配置：{CONFIG_PATH}")
    except json.JSONDecodeError as error:
        fail(f"角色配置不是有效 JSON：{error}")

    if config.get("version") != 1 or not isinstance(config.get("characters"), dict):
        fail("角色配置版本或 characters 字段无效")

    for name, char_data in config["characters"].items():
        validate_character_config(name, char_data)

    return config


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="用 MiMo 生成一条待验收的游戏配音，默认写入角色库候选目录。"
    )
    parser.add_argument("--speaker", help="角色名；配置中的角色会使用其默认配置。")
    parser.add_argument("--id", help="本条语音的稳定 ID；可用 / 表示剧情层级。")
    parser.add_argument("--text", help="要念出的游戏台词。")
    parser.add_argument(
        "--mode",
        choices=["preset", "clone", "design"],
        help="配音模式：preset（预置音色）、clone（样音克隆）、design（声音设计）。省略时从角色配置继承或由传参推断。",
    )
    parser.add_argument(
        "--voice",
        help=f"预置音色名（mode=preset 时使用，可选：{', '.join(sorted(PRESET_VOICES.keys()))}）。",
    )
    parser.add_argument(
        "--sample",
        type=Path,
        help="参考样音路径（mode=clone 时使用，支持 WAV/MP3）。",
    )
    parser.add_argument(
        "--style",
        help="声音形象描述（mode=design 时使用）。",
    )
    parser.add_argument("--direction", default="", help="本句额外演出指令；会追加到角色默认指令（全模式通用）。")
    parser.add_argument("--output", type=Path, help="候选输出路径；省略时写入角色库候选目录。")
    parser.add_argument("--manifest", type=Path, help="批量任务清单 JSON；与单条参数二选一。")
    parser.add_argument("--only", action="append", default=[], help="只生成清单中的一个语音 ID；可重复。")
    parser.add_argument("--dry-run", action="store_true", help="只校验参数和样音，不调用 API。")
    return parser.parse_args()


def voice_id_path(voice_id: str) -> Path:
    parts = voice_id.split("/")
    if not voice_id or Path(voice_id).is_absolute() or any(not part or part in {".", ".."} for part in parts):
        fail("语音 ID 必须是相对剧情路径，不可为空、绝对路径或包含 . / ..")
    return Path(*parts)


def encode_sample(path: Path) -> str:
    suffix = path.suffix.lower()
    mime_type = {".mp3": "audio/mpeg", ".wav": "audio/wav"}.get(suffix)
    if mime_type is None:
        fail(f"VoiceClone 样音只支持 MP3/WAV：{path}")
    try:
        raw = path.read_bytes()
    except FileNotFoundError:
        fail(f"找不到角色样音：{path}")
    encoded = base64.b64encode(raw).decode("ascii")
    if len(encoded) > 10 * 1024 * 1024:
        fail("样音的 Base64 编码超过 MiMo 的 10 MB 限制")
    return f"data:{mime_type};base64,{encoded}"


def build_request(
    speaker: str,
    text: str,
    direction: str,
    mode: str | None,
    voice: str | None,
    sample: str | Path | None,
    style: str | None,
    config: dict,
) -> tuple[dict, str]:
    character = config["characters"].get(speaker)

    # 1. 判定 effective_mode
    if mode is not None:
        if mode not in MODES:
            fail(f"未知的 mode「{mode}」；仅支持：{', '.join(MODES)}")
        effective_mode = mode
    else:
        # 未显式指定 mode 时，优先由传入参数推断
        if voice is not None and sample is None and style is None:
            effective_mode = "preset"
        elif sample is not None and voice is None and style is None:
            effective_mode = "clone"
        elif style is not None and voice is None and sample is None:
            effective_mode = "design"
        elif character is not None:
            effective_mode = character.get("mode", "preset" if "voice" in character else ("clone" if "sample" in character else None))
            if effective_mode not in MODES:
                fail(f"角色「{speaker}」的配置缺少有效 mode")
        else:
            provided = [
                k for k, v in [
                    ("--voice", voice is not None),
                    ("--sample", sample is not None),
                    ("--style", style is not None),
                ] if v
            ]
            if len(provided) > 1:
                fail(f"参数冲突：不可同时传入 {', '.join(provided)}")
            supported_voices = ", ".join(sorted(PRESET_VOICES.keys()))
            fail(
                f"角色「{speaker}」未收录在角色库中，请显式指定 --mode 或对应参数：\n"
                f"  - preset（预置音色）：--voice <音色名>（可选：{supported_voices}）\n"
                f"  - clone（样音克隆）：--sample <样音文件路径>\n"
                f"  - design（声音设计）：--style <声音形象白描描述>"
            )

    char_direction = character.get("direction", "") if character else ""
    effective_direction = " ".join(part for part in (char_direction, direction) if part)

    # 2. 根据 effective_mode 执行严格校验与参数构建
    if effective_mode == "preset":
        if sample is not None:
            fail("preset 模式不可指定 sample 参数")
        if style is not None:
            fail("preset 模式不可指定 style 参数")
        actual_voice = voice if voice is not None else (character.get("voice") if character else None)
        if not actual_voice or not isinstance(actual_voice, str) or not actual_voice.strip():
            supported_voices = ", ".join(sorted(PRESET_VOICES.keys()))
            fail(f"preset 模式必须指定有效 --voice 音色名（或在角色配置中设置 voice）。可选：{supported_voices}")
        validate_preset_voice(actual_voice)

        messages = [{"role": "assistant", "content": text}]
        if effective_direction:
            messages.insert(0, {"role": "user", "content": effective_direction})
        return (
            {
                "model": PRESET_MODEL,
                "messages": messages,
                "audio": {"format": "wav", "voice": actual_voice},
            },
            f"Preset({actual_voice})",
        )

    elif effective_mode == "clone":
        if voice is not None:
            fail("clone 模式不可指定 voice 参数")
        if style is not None:
            fail("clone 模式不可指定 style 参数")
        actual_sample = sample if sample is not None else (character.get("sample") if character else None)
        if not actual_sample:
            fail("clone 模式必须指定 --sample 样音路径（或在角色配置中设置 sample）")
        if isinstance(actual_sample, Path):
            sample_path = actual_sample.resolve()
        else:
            sample_path = (CONFIG_PATH.parent / actual_sample).resolve()

        messages = [{"role": "assistant", "content": text}]
        if effective_direction:
            messages.insert(0, {"role": "user", "content": effective_direction})
        return (
            {
                "model": character.get("model", config.get("model", VOICE_CLONE_MODEL)) if character else VOICE_CLONE_MODEL,
                "messages": messages,
                "audio": {"format": "wav", "voice": encode_sample(sample_path)},
            },
            "VoiceClone",
        )

    elif effective_mode == "design":
        if voice is not None:
            fail("design 模式不可指定 voice 参数")
        if sample is not None:
            fail("design 模式不可指定 sample 参数")
        if not style or not isinstance(style, str) or not style.strip():
            fail("design 模式必须指定 --style 声音形象描述")

        effective_style = f"{style} {direction}".strip() if direction else style
        return (
            {
                "model": VOICE_DESIGN_MODEL,
                "messages": [
                    {"role": "user", "content": effective_style},
                    {"role": "assistant", "content": text},
                ],
                "audio": {"format": "wav"},
            },
            "VoiceDesign",
        )

    fail(f"未处理的配音模式：{effective_mode}")


def request_audio(payload: dict) -> bytes:
    api_key = load_api_key()
    request = urllib.request.Request(
        API_URL,
        data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
        headers={"api-key": api_key, "Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=180) as response:
            result = json.load(response)
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", errors="replace")
        fail(f"MiMo API 返回 HTTP {error.code}：{detail}")
    except urllib.error.URLError as error:
        fail(f"MiMo API 网络请求失败：{error.reason}")

    try:
        return base64.b64decode(result["choices"][0]["message"]["audio"]["data"], validate=True)
    except (KeyError, IndexError, TypeError, ValueError) as error:
        fail(f"MiMo API 响应里没有有效音频：{error}")


def load_api_key() -> str:
    """优先读取 macOS Keychain；环境变量仅供 CI 或一次性覆盖。"""
    try:
        result = subprocess.run(
            [
                "security", "find-generic-password",
                "-a", KEYCHAIN_ACCOUNT,
                "-s", KEYCHAIN_SERVICE,
                "-w",
            ],
            check=True,
            capture_output=True,
            text=True,
        )
        api_key = result.stdout.strip()
        if api_key:
            return api_key
    except (FileNotFoundError, subprocess.CalledProcessError):
        pass

    api_key = os.environ.get("MIMO_API_KEY", "").strip()
    if api_key:
        return api_key
    fail(
        "未找到 MiMo API key；请先在 macOS Keychain 建立 "
        f"service「{KEYCHAIN_SERVICE}」、account「{KEYCHAIN_ACCOUNT}」的通用密码项"
    )


def generate_cue(cue: dict, config: dict, dry_run: bool, output_override: Path | None = None) -> None:
    if not isinstance(cue, dict):
        fail("每条配音必须是 JSON 对象")
    voice_id = cue.get("id")
    speaker = cue.get("speaker")
    text = cue.get("text")
    direction = cue.get("direction", "")
    mode = cue.get("mode")
    style = cue.get("style")
    voice = cue.get("voice")
    sample = cue.get("sample")

    if not isinstance(voice_id, str) or not isinstance(speaker, str) or not isinstance(text, str):
        fail("每条配音必须有字符串 id、speaker、text")
    if not text.strip():
        fail(f"语音「{voice_id}」的 text 不能为空")

    if not isinstance(direction, str):
        fail(f"语音「{voice_id}」的 direction 必须是字符串，实际为 {repr(direction)}")

    logical_path = voice_id_path(voice_id)
    output = output_override or (DEFAULT_CANDIDATE_DIR / logical_path).with_suffix(".wav")
    output = output.resolve()

    payload, mode_name = build_request(
        speaker=speaker,
        text=text,
        direction=direction,
        mode=mode,
        voice=voice,
        sample=sample,
        style=style,
        config=config,
    )

    print(f"模式：{mode_name}")
    print(f"语音 ID：{voice_id}")
    print(f"角色：{speaker}")
    print(f"候选输出：{output}")
    if dry_run:
        return

    audio = request_audio(payload)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(audio)
    print(f"已生成：{output}（{len(audio)} bytes）")


def load_manifest(path: Path) -> list[dict]:
    try:
        manifest = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        fail(f"找不到配音清单：{path}")
    except json.JSONDecodeError as error:
        fail(f"配音清单不是有效 JSON：{error}")
    cues = manifest.get("cues") if manifest.get("version") == 1 else None
    if not isinstance(cues, list) or not cues:
        fail("配音清单必须是 version=1 且包含非空 cues 数组")
    ids = [cue.get("id") for cue in cues if isinstance(cue, dict)]
    if len(ids) != len(cues) or len(set(ids)) != len(ids):
        fail("配音清单中的 id 必须存在且全局唯一")

    for cue in cues:
        if not isinstance(cue, dict):
            fail("配音清单条目必须是 JSON 对象")
        cue_id = cue.get("id", "<未知ID>")

        cue_mode = cue.get("mode")
        if cue_mode is not None and cue_mode not in MODES:
            fail(f"配音清单条目「{cue_id}」的 mode「{cue_mode}」无效；支持的模式为：{', '.join(MODES)}")

        has_voice = "voice" in cue
        has_sample = "sample" in cue
        has_style = "style" in cue

        if has_voice:
            cue_voice = cue["voice"]
            if not isinstance(cue_voice, str) or not cue_voice.strip():
                fail(f"配音清单条目「{cue_id}」的 voice 必须是非空字符串，实际为 {repr(cue_voice)}")
            validate_preset_voice(cue_voice, context=f"配音清单条目「{cue_id}」")

        if has_sample:
            cue_sample = cue["sample"]
            if not isinstance(cue_sample, str) or not cue_sample.strip():
                fail(f"配音清单条目「{cue_id}」的 sample 必须是非空字符串，实际为 {repr(cue_sample)}")

        if has_style:
            cue_style = cue["style"]
            if not isinstance(cue_style, str) or not cue_style.strip():
                fail(f"配音清单条目「{cue_id}」的 style 必须是非空字符串，实际为 {repr(cue_style)}")

        provided = [
            k for k, v in [
                ("voice", has_voice),
                ("sample", has_sample),
                ("style", has_style),
            ] if v
        ]
        if len(provided) > 1:
            fail(f"配音清单条目「{cue_id}」不可同时指定多个模式参数：{', '.join(provided)}")

        if cue_mode == "preset" and (has_sample or has_style):
            fail(f"配音清单条目「{cue_id}」为 preset 模式，不可包含 sample 或 style")
        if cue_mode == "clone" and (has_voice or has_style):
            fail(f"配音清单条目「{cue_id}」为 clone 模式，不可包含 voice 或 style")
        if cue_mode == "design" and (has_voice or has_sample):
            fail(f"配音清单条目「{cue_id}」为 design 模式，不可包含 voice 或 sample")

    return cues


def main() -> None:
    args = parse_args()
    config = load_config()
    if args.manifest:
        if any(value is not None for value in (args.speaker, args.id, args.text, args.mode, args.voice, args.sample, args.style)) or args.output:
            fail("--manifest 不可与 --speaker、--id、--text、--mode、--voice、--sample、--style、--output 混用")
        cues = load_manifest(args.manifest)
        selected = [cue for cue in cues if not args.only or cue["id"] in args.only]
        if not selected:
            fail("--only 没有匹配到清单中的语音 ID")
        for cue in selected:
            generate_cue(cue, config, args.dry_run)
        return

    if args.only:
        fail("--only 只能与 --manifest 同用")
    if not all((args.speaker, args.id, args.text)):
        fail("单条生成必须同时提供 --speaker、--id、--text")

    cue_data = {
        "id": args.id,
        "speaker": args.speaker,
        "text": args.text,
        "direction": args.direction,
    }
    if args.mode is not None:
        cue_data["mode"] = args.mode
    if args.voice is not None:
        cue_data["voice"] = args.voice
    if args.sample is not None:
        cue_data["sample"] = args.sample
    if args.style is not None:
        cue_data["style"] = args.style

    generate_cue(
        cue_data,
        config,
        args.dry_run,
        args.output,
    )


if __name__ == "__main__":
    main()
