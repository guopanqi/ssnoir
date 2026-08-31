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
VOICE_CLONE_MODEL = "mimo-v2.5-tts-voiceclone"
VOICE_DESIGN_MODEL = "mimo-v2.5-tts-voicedesign"
KEYCHAIN_SERVICE = "com.ssnoir.mimo-tts.api-key"
KEYCHAIN_ACCOUNT = "SSNoir MiMo TTS"


def fail(message: str) -> None:
    raise SystemExit(f"错误：{message}")


def load_config() -> dict:
    try:
        config = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    except FileNotFoundError:
        fail(f"找不到角色配置：{CONFIG_PATH}")
    except json.JSONDecodeError as error:
        fail(f"角色配置不是有效 JSON：{error}")

    if config.get("version") != 1 or not isinstance(config.get("characters"), dict):
        fail("角色配置版本或 characters 字段无效")
    return config


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="用 MiMo 生成一条待验收的游戏配音，默认写入角色库候选目录。"
    )
    parser.add_argument("--speaker", help="角色名；配置中的角色会使用其参考样音。")
    parser.add_argument("--id", help="本条语音的稳定 ID；可用 / 表示剧情层级。")
    parser.add_argument("--text", help="要念出的游戏台词。")
    parser.add_argument("--direction", default="", help="本句额外演出指令；会追加到角色默认指令。")
    parser.add_argument(
        "--style",
        default="",
        help="非角色库角色必填：一句声音形象描述，改用 MiMo VoiceDesign。",
    )
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


def build_request(speaker: str, text: str, direction: str, style: str, config: dict) -> tuple[dict, str]:
    character = config["characters"].get(speaker)
    if character is not None:
        sample_path = (CONFIG_PATH.parent / character["sample"]).resolve()
        effective_direction = " ".join(part for part in (character.get("direction", ""), direction) if part)
        messages = [{"role": "assistant", "content": text}]
        if effective_direction:
            messages.insert(0, {"role": "user", "content": effective_direction})
        return (
            {
                "model": config.get("model", VOICE_CLONE_MODEL),
                "messages": messages,
                "audio": {"format": "wav", "voice": encode_sample(sample_path)},
            },
            "VoiceClone",
        )

    if not style:
        fail(f"「{speaker}」不在角色库；请用 --style 写明声音形象")
    return (
        {
            "model": VOICE_DESIGN_MODEL,
            "messages": [
                {"role": "user", "content": style},
                {"role": "assistant", "content": text},
            ],
            "audio": {"format": "wav"},
        },
        "VoiceDesign",
    )


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
    voice_id = cue.get("id", "")
    speaker = cue.get("speaker", "")
    text = cue.get("text", "")
    direction = cue.get("direction", "")
    style = cue.get("style", "")
    if not isinstance(voice_id, str) or not isinstance(speaker, str) or not isinstance(text, str):
        fail("每条配音必须有字符串 id、speaker、text")
    if not text.strip():
        fail(f"语音「{voice_id}」的 text 不能为空")

    logical_path = voice_id_path(voice_id)
    output = output_override or (DEFAULT_CANDIDATE_DIR / logical_path).with_suffix(".wav")
    output = output.resolve()
    payload, mode = build_request(speaker, text, direction, style, config)

    print(f"模式：{mode}")
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
    return cues


def main() -> None:
    args = parse_args()
    config = load_config()
    if args.manifest:
        if any(value is not None for value in (args.speaker, args.id, args.text)) or args.output:
            fail("--manifest 不可与 --speaker、--id、--text、--output 混用")
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
    generate_cue(
        {"id": args.id, "speaker": args.speaker, "text": args.text,
         "direction": args.direction, "style": args.style},
        config,
        args.dry_run,
        args.output,
    )


if __name__ == "__main__":
    main()
