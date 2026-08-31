#!/usr/bin/env python3
"""Agent-facing batch video generation for SSNoir through LibTV."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from typing import Any


DEFAULT_WORKSPACE = "SSNoir 分镜"
DEFAULT_MODEL = "Seedance 2.0 Mini"
ALLOWED_MODELS = ("Seedance 2.0 Mini",)
DEFAULT_DURATION = 6
DEFAULT_TAKES = 2
DEFAULT_RESOLUTION = "720p"
ALLOWED_RESOLUTIONS = ("480p", "720p")

# 固定在内部，不暴露给调用方：
#   mixed2video    —— 场景底片 + 角色参考同时挂载，只有这个模式支持。
#   16:9           —— 与游戏画面一致；换比例等于把场景底片裁掉一块。
#   autoCompliance —— 参考图全是插画，跳过真人合规检测省一轮等待。
FIXED_MODE = "mixed2video"
FIXED_RATIO = "16:9"
FIXED_AUTO_COMPLIANCE = "0"

DURATION_RANGE = (4, 15)
TAKES_RANGE = (1, 4)

VERIFY = Path(__file__).resolve().parent / "verify_shot.sh"


def fail(message: str) -> None:
    raise RuntimeError(message)


def note(message: str) -> None:
    print(f"[LibTV Video Generate] {message}", file=sys.stderr, flush=True)


def libtv(*args: str) -> dict[str, Any]:
    result = subprocess.run(["libtv", *args], text=True, capture_output=True, check=False, stdin=subprocess.DEVNULL)
    if result.returncode != 0:
        fail(result.stderr.strip() or result.stdout.strip() or "未知 LibTV 错误")
    # `node create --run` 会先吐建节点响应，紧接着吐任务完成响应。合法的 JSON 流不是解析
    # 失败，取最后一个完整对象——出片地址在它里面。
    decoder = json.JSONDecoder()
    cursor = 0
    values: list[Any] = []
    output = result.stdout
    while cursor < len(output):
        while cursor < len(output) and output[cursor].isspace():
            cursor += 1
        if cursor == len(output):
            break
        try:
            value, cursor = decoder.raw_decode(output, cursor)
        except json.JSONDecodeError:
            fail(f"LibTV 返回包含无法解析的内容：{output.strip()}")
        values.append(value)
    if not values or not isinstance(values[-1], dict):
        fail(f"LibTV 返回的不是 JSON 对象：{output.strip()}")
    return values[-1]


def workspace_id(name: str) -> int:
    payload = libtv("workspace", "list", "--name", name, "-s", "100")
    matches = [item for item in payload.get("folders", []) if item.get("name") == name]
    if len(matches) == 1:
        return int(matches[0]["id"])
    if not matches:
        note(f"创建工作区「{name}」。")
        created = libtv("workspace", "create", name, "-d", "SSNoir 的分镜与候选视频")
        try:
            return int(created["workspaceId"])
        except (KeyError, TypeError, ValueError):
            fail(f"创建工作区的返回缺少 workspaceId：{created}")
    fail(f"工作区「{name}」有 {len(matches)} 个同名项；请先在 LibTV 整理。")


def resolve_canvas(workspace: int, session: str) -> str:
    canvas_name = f"视频会话｜{session}"
    payload = libtv("project", "list", "-w", str(workspace), "--name", canvas_name, "-s", "100")
    matches = [item for item in payload.get("projectMetaList", []) if item.get("name") == canvas_name]
    if len(matches) > 1:
        fail(f"工作区内有多个同名视频会话「{session}」；请先在 LibTV 整理重复画布。")
    if matches:
        return str(matches[0]["uuid"])
    note(f"创建会话画布「{session}」。")
    payload = libtv("project", "create", canvas_name, "-d", "由 LibTV Video Generate 自动创建；候选集中在此，选定后才进入工程资产。", "-w", str(workspace))
    try:
        return str(payload["projectMeta"]["uuid"])
    except KeyError:
        fail(f"创建画布的返回缺少 UUID：{payload}")


def existing_nodes(project_uuid: str) -> dict[str, str]:
    payload = libtv("node", "list", "-p", project_uuid)
    return {str(item["name"]): str(item["id"]) for item in payload.get("nodes", [])}


def asset_node_name(kind: str, label: str, path: Path) -> str:
    """素材节点名带内容哈希：同一张图永远同名（可复用），换了内容自然换名（不会拿错）。"""
    digest = hashlib.sha1(path.read_bytes()).hexdigest()[:8]
    return f"{kind}｜{label}｜{digest}"


def ensure_asset(project_uuid: str, nodes: dict[str, str], kind: str, label: str, path: Path, y: int) -> str:
    name = asset_node_name(kind, label, path)
    if name not in nodes:
        note(f"上传{kind}「{label}」。")
        uploaded = libtv("upload", name, "-p", project_uuid, "-t", "image", "-f", str(path), "--x", "0", "--y", str(y))
        nodes[name] = str(uploaded["nodeKey"])
    return name


def safe_component(text: str) -> str:
    cleaned = re.sub(r"[/\\:*?\"<>|\x00-\x1f]", "_", text).strip().strip(".")
    return cleaned or "未命名"


def build_prompt(scene_node: str, character_nodes: list[tuple[str, str]], story: str) -> str:
    """把业务语义拼成 LibTV 提示词。调用方只写 story，占位语法由本工具负责。

    「全程镜头固定」和「禁止 BGM」是固定的：它们绑的是 mixed2video + 场景底片这个机制，
    不是创作口味。见 README。
    """
    lines = [f'{name}是 {{{{Node "{node}"}}}}' for name, node in character_nodes]
    lines.append(f'场景是 {{{{Node "{scene_node}"}}}}')
    lines.append(f'以 {{{{Node "{scene_node}"}}}} 的机位开始，全程镜头完全固定')
    lines.append("禁止生成背景音乐（BGM），保留人声和环境音效。")
    return "\n".join(lines) + "\n\n" + story.strip()


def validate_request(raw: Any) -> dict[str, Any]:
    if not isinstance(raw, dict):
        fail("请求必须是 JSON 对象。")
    if not isinstance(raw.get("session"), str) or not raw["session"].strip():
        fail("请求缺少非空字符串 session。")

    scene = raw.get("scene")
    if not isinstance(scene, dict) or not isinstance(scene.get("path"), str) or not scene["path"].strip():
        fail("请求缺少 scene.path（游戏内截取的场景图路径）。")

    characters = raw.get("characters", [])
    if not isinstance(characters, list):
        fail("characters 必须是数组。")
    for index, item in enumerate(characters, 1):
        ok = isinstance(item, dict) and isinstance(item.get("name"), str) and item["name"].strip() and isinstance(item.get("image"), str) and item["image"].strip()
        if not ok:
            fail(f"characters[{index}] 必须包含非空 name 和 image。")

    variants = raw.get("variants")
    if not isinstance(variants, list) or not variants:
        fail("请求必须含有至少一个 variants 项。")
    for index, item in enumerate(variants, 1):
        ok = isinstance(item, dict) and isinstance(item.get("name"), str) and item["name"].strip() and isinstance(item.get("prompt"), str) and item["prompt"].strip()
        if not ok:
            fail(f"variants[{index}] 必须包含非空 name 和 prompt。")

    model = raw.get("model", DEFAULT_MODEL)
    if model not in ALLOWED_MODELS:
        fail(f"model 只支持 {'、'.join(ALLOWED_MODELS)}；收到「{model}」。")

    duration = raw.get("duration", DEFAULT_DURATION)
    if not isinstance(duration, int) or isinstance(duration, bool) or not DURATION_RANGE[0] <= duration <= DURATION_RANGE[1]:
        fail(f"duration 必须是 {DURATION_RANGE[0]}–{DURATION_RANGE[1]} 的整数秒。")

    takes = raw.get("takes", DEFAULT_TAKES)
    if not isinstance(takes, int) or isinstance(takes, bool) or not TAKES_RANGE[0] <= takes <= TAKES_RANGE[1]:
        fail(f"takes 必须是 {TAKES_RANGE[0]}–{TAKES_RANGE[1]} 的整数。")

    resolution = raw.get("resolution", DEFAULT_RESOLUTION)
    if resolution not in ALLOWED_RESOLUTIONS:
        fail(f"resolution 只支持 {'、'.join(ALLOWED_RESOLUTIONS)}；收到「{resolution}」。")

    if not isinstance(raw.get("sound", True), bool):
        fail("sound 必须是布尔值。")

    if "audio" in raw:
        fail("本工具当前不支持参考音频：这条路径还没有实测过，不做未验证的承诺。")
    for key in ("aspectRatio", "ratio", "modeType"):
        if key in raw:
            fail(f"{key} 固定在内部，不接受传入（16:9 / mixed2video）。")

    return raw


def check_files(request: dict[str, Any]) -> tuple[Path, list[tuple[str, Path]]]:
    scene = Path(request["scene"]["path"]).expanduser().resolve()
    characters = [(item["name"].strip(), Path(item["image"]).expanduser().resolve()) for item in request.get("characters", [])]
    missing = [str(scene)] if not scene.is_file() else []
    missing += [str(path) for _, path in characters if not path.is_file()]
    if missing:
        fail("素材文件不存在：" + "、".join(missing))
    return scene, characters


def verify(video: Path, scene: Path, outdir: Path) -> dict[str, str]:
    if not VERIFY.is_file():
        return {"verifyError": f"缺少验证脚本 {VERIFY}"}
    if shutil.which("ffmpeg") is None:
        return {"verifyError": "未找到 ffmpeg，跳过抽帧与首帧对比"}
    run = subprocess.run(["bash", str(VERIFY), str(video), str(scene), str(outdir)], text=True, capture_output=True, check=False)
    if run.returncode != 0:
        return {"verifyError": (run.stderr or run.stdout).strip()[:400]}
    strip = outdir / f"{video.stem}-抽帧条.jpg"
    plate = outdir / f"{video.stem}-首帧对比.jpg"
    out: dict[str, str] = {}
    if strip.is_file():
        out["frameStrip"] = str(strip)
    if plate.is_file():
        out["plateComparison"] = str(plate)
    return out


def generate_variant(project_uuid: str, nodes: dict[str, str], request: dict[str, Any], variant: dict[str, Any], scene: Path, characters: list[tuple[str, Path]], outdir: Path, run_stamp: str) -> dict[str, Any]:
    scene_node = ensure_asset(project_uuid, nodes, "场景", scene.stem, scene, 0)
    character_nodes = [
        (name, ensure_asset(project_uuid, nodes, "角色", name, path, 420 * (index + 1)))
        for index, (name, path) in enumerate(characters)
    ]

    prompt = build_prompt(scene_node, character_nodes, variant["prompt"])
    # 每次提交都是一轮新候选：画布上建新节点，本地也落新文件名，两边共用同一个 run_stamp。
    # 同名方案重复提交绝不能盖掉上一轮——上一轮是用来对比的。
    node_name = f"生成｜{variant['name']}｜{run_stamp}"

    command = [
        "node", "--x", "760", "--y", "0", "create", node_name, "-p", project_uuid, "-t", "video",
        "-s", f"model={request.get('model', DEFAULT_MODEL)}",
        "-s", f"modeType={FIXED_MODE}",
        "-s", f"count={request.get('takes', DEFAULT_TAKES)}",
        "-s", f"ratio={FIXED_RATIO}",
        "-s", f"resolution={request.get('resolution', DEFAULT_RESOLUTION)}",
        "-s", f"duration={request.get('duration', DEFAULT_DURATION)}",
        "-s", f"enableSound={'on' if request.get('sound', True) else 'off'}",
        "-s", f"autoCompliance={FIXED_AUTO_COMPLIANCE}",
    ]
    for _, node in character_nodes:
        command.extend(("--left", node))
    command.extend(("--left", scene_node))
    command.extend(("--prompt", prompt, "--run"))

    # LibTV 只在服务端任务完成后才把生成节点写进画布。等待期间没有进度输出、画布上
    # 看不到这个节点，都是正常语义——不要重复提交，也不要去查节点状态。
    note(f"正在生成「{variant['name']}」（{request.get('duration', DEFAULT_DURATION)}s ×{request.get('takes', DEFAULT_TAKES)}）；完成前无进度输出属正常，请勿重复提交。")
    completed = libtv(*command)
    urls = completed.get("data", {}).get("url") or []
    if not urls:
        fail("LibTV 已返回完成响应，但没有视频 URL。")

    takes: list[dict[str, Any]] = []
    stem = safe_component(variant["name"])
    for index, url in enumerate(urls):
        tag = chr(ord("A") + index)
        video = outdir / f"{stem}｜{run_stamp}-{tag}.mp4"
        download = subprocess.run(["curl", "-sSL", "-o", str(video), url], text=True, capture_output=True, check=False)
        entry: dict[str, Any] = {"take": tag, "videoUrl": url}
        if download.returncode != 0 or not video.is_file():
            entry["error"] = (download.stderr or "下载失败").strip()[:400]
        else:
            entry["video"] = str(video)
            entry.update(verify(video, scene, outdir))
        takes.append(entry)

    return {"name": variant["name"], "status": "completed", "prompt": prompt, "takes": takes}


def load_request(argument: str) -> dict[str, Any]:
    try:
        text = sys.stdin.read() if argument == "-" else Path(argument).read_text(encoding="utf-8")
        return validate_request(json.loads(text))
    except OSError as exc:
        fail(f"无法读取请求文件：{exc}")
    except json.JSONDecodeError as exc:
        fail(f"请求不是合法 JSON：{exc}")


def generate(request: dict[str, Any]) -> dict[str, Any]:
    if shutil.which("libtv") is None:
        fail("未找到 libtv。请先完成 LibTV CLI 的安装与登录。")
    scene, characters = check_files(request)
    session = str(request["session"]).strip()

    outdir = Path(request["outputDir"]).expanduser() if isinstance(request.get("outputDir"), str) else Path("tmp/libtv") / safe_component(session)
    outdir.mkdir(parents=True, exist_ok=True)

    workspace = workspace_id(str(request.get("workspace", DEFAULT_WORKSPACE)))
    project_uuid = resolve_canvas(workspace, session)
    nodes = existing_nodes(project_uuid)

    run_stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    results: list[dict[str, Any]] = []
    for variant in request["variants"]:
        try:
            results.append(generate_variant(project_uuid, nodes, request, variant, scene, characters, outdir, run_stamp))
        except RuntimeError as exc:
            results.append({"name": variant["name"], "status": "failed", "takes": [], "error": str(exc)})

    failed = [item for item in results if item["status"] != "completed"]
    return {
        "status": "completed" if not failed else "partial_failure",
        "session": session,
        "canvasUrl": f"https://www.liblib.tv/canvas?spaceId={workspace}&projectId={project_uuid}",
        "model": request.get("model", DEFAULT_MODEL),
        "duration": request.get("duration", DEFAULT_DURATION),
        "takes": request.get("takes", DEFAULT_TAKES),
        "resolution": request.get("resolution", DEFAULT_RESOLUTION),
        "aspectRatio": FIXED_RATIO,
        "sound": request.get("sound", True),
        "outputDir": str(outdir),
        "runStamp": run_stamp,
        "results": results,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="LibTV Video Generate：提交一批候选镜头并等待结果。")
    parser.add_argument("--request", required=True, help="请求 JSON 文件路径；传 - 时从标准输入读取。")
    parser.add_argument("--dry-run", action="store_true", help="仅校验请求 JSON 与素材文件，不访问 LibTV。")
    args = parser.parse_args()
    try:
        request = load_request(args.request)
        if args.dry_run:
            check_files(request)
            print(json.dumps({"status": "valid", "request": request}, ensure_ascii=False))
            return 0
        result = generate(request)
        print(json.dumps(result, ensure_ascii=False))
        return 0 if result["status"] == "completed" else 1
    except RuntimeError as exc:
        print(f"错误：{exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
