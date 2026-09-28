#!/usr/bin/env python3
"""Agent-facing batch image generation for SSNoir through LibTV."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from typing import Any
from urllib.parse import urlparse
from urllib.request import urlopen


DEFAULT_WORKSPACE = "SSNoir 分镜"
DEFAULT_MODEL = "General image Pro"
DEFAULT_QUALITY = "1K"
DEFAULT_RATIO = "1:1"
REPO_ROOT = Path(__file__).resolve().parents[2]
CACHE_DIR = REPO_ROOT / "tmp" / "libtv-image-generate"


def fail(message: str) -> None:
    raise RuntimeError(message)


def note(message: str) -> None:
    print(f"[LibTV Image Generate] {message}", file=sys.stderr, flush=True)


def libtv(*args: str) -> dict[str, Any]:
    result = subprocess.run(["libtv", *args], text=True, capture_output=True, check=False, stdin=subprocess.DEVNULL)
    if result.returncode != 0:
        fail(result.stderr.strip() or result.stdout.strip() or "未知 LibTV 错误")
    # `node create --run` may emit a create response followed immediately by the
    # completed-task response. Keep the last complete JSON object, which contains
    # the image URLs, instead of treating valid JSON streams as a parse failure.
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
    if len(matches) != 1:
        fail(f"找不到唯一的 LibTV 工作区「{name}」；匹配数为 {len(matches)}。")
    return int(matches[0]["id"])


def resolve_canvas(workspace: int, session: str) -> str:
    canvas_name = f"图像会话｜{session}"
    payload = libtv("project", "list", "-w", str(workspace), "--name", canvas_name, "-s", "100")
    matches = [item for item in payload.get("projectMetaList", []) if item.get("name") == canvas_name]
    if len(matches) > 1:
        fail(f"工作区内有多个同名图像会话「{session}」；请先在 LibTV 整理重复画布。")
    if matches:
        return str(matches[0]["uuid"])
    note(f"创建会话画布「{session}」。")
    payload = libtv("project", "create", canvas_name, "-d", "由 LibTV Image Generate 自动创建；候选集中在此，验收后才进入工程资产。", "-w", str(workspace))
    try:
        return str(payload["projectMeta"]["uuid"])
    except KeyError:
        fail(f"创建画布的返回缺少 UUID：{payload}")


def existing_nodes(project_uuid: str) -> dict[str, str]:
    payload = libtv("node", "list", "-p", project_uuid)
    return {str(item["name"]): str(item["id"]) for item in payload.get("nodes", [])}


def result_index_path(project_uuid: str) -> Path:
    return CACHE_DIR / f"{project_uuid}.results.json"


def load_result_index(project_uuid: str) -> dict[str, str]:
    path = result_index_path(project_uuid)
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
        urls = raw.get("urls", {})
        return {str(url): str(node_key) for url, node_key in urls.items()} if isinstance(urls, dict) else {}
    except FileNotFoundError:
        return {}
    except (OSError, json.JSONDecodeError):
        note("本地结果索引无法读取；将安全地按普通参考图处理。")
        return {}


def remember_results(project_uuid: str, node_key: str | None, urls: list[str]) -> None:
    if not node_key or not urls:
        return
    index = load_result_index(project_uuid)
    index.update({url: node_key for url in urls})
    path = result_index_path(project_uuid)
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(".tmp")
        temporary.write_text(json.dumps({"urls": index}, ensure_ascii=False, indent=2), encoding="utf-8")
        temporary.replace(path)
    except OSError as exc:
        # The completed image itself is still valid; losing this optimization must
        # never turn a successful generation into a failed request.
        note(f"无法更新本地结果索引：{exc}。后续仍可把该图片作为普通参考图。")


def download_reference(url: str) -> Path:
    parsed = urlparse(url)
    suffix = Path(parsed.path).suffix or ".img"
    destination = CACHE_DIR / "references" / f"{hashlib.sha1(url.encode()).hexdigest()}{suffix}"
    if destination.is_file():
        return destination
    note("下载外部图片参考。")
    try:
        destination.parent.mkdir(parents=True, exist_ok=True)
        temporary = destination.with_suffix(destination.suffix + ".tmp")
        with urlopen(url, timeout=60) as response:
            temporary.write_bytes(response.read())
        temporary.replace(destination)
    except OSError as exc:
        fail(f"无法下载图片 reference：{url}（{exc}）")
    return destination


def reference_name(path: Path) -> str:
    return f"参考｜{path.stem}｜{hashlib.sha1(path.read_bytes()).hexdigest()[:8]}"


def prepare_references(project_uuid: str, raw_references: list[str]) -> list[str]:
    nodes = existing_nodes(project_uuid)
    indexed_results = load_result_index(project_uuid)
    known_node_keys = set(nodes.values())
    paths: list[Path] = []
    direct_keys: list[str] = []
    for reference in raw_references:
        if reference.startswith(("https://", "http://")):
            node_key = indexed_results.get(reference)
            if node_key in known_node_keys:
                note("复用本 session 中的上一轮生成图。")
                direct_keys.append(node_key)
            else:
                paths.append(download_reference(reference))
        else:
            paths.append(Path(reference).expanduser().resolve())
    missing = [str(path) for path in paths if not path.is_file()]
    if missing:
        fail("参考图不存在：" + "、".join(missing))
    keys = direct_keys
    for index, path in enumerate(paths):
        name = reference_name(path)
        if name not in nodes:
            note(f"上传参考图「{path.name}」。")
            uploaded = libtv("upload", name, "-p", project_uuid, "-f", str(path), "--x", "0", "--y", str(index * 380))
            nodes[name] = str(uploaded["nodeKey"])
        keys.append(nodes[name])
    return keys


def validate_request(raw: Any) -> dict[str, Any]:
    if not isinstance(raw, dict):
        fail("请求必须是 JSON 对象。")
    if not isinstance(raw.get("session"), str) or not raw["session"].strip():
        fail("请求缺少非空字符串 session。")
    variants = raw.get("variants")
    if not isinstance(variants, list) or not variants:
        fail("请求必须含有至少一个 variants 项。")
    if not isinstance(raw.get("references", []), list) or not all(isinstance(item, str) and item.strip() for item in raw.get("references", [])):
        fail("references 必须是非空本地路径或图片 URL 的数组。")
    if raw.get("quality", DEFAULT_QUALITY) != "1K" or raw.get("ratio", DEFAULT_RATIO) != "1:1":
        fail("本工具当前固定输出 1K、1:1；不要传 quality 或 ratio。")
    for index, item in enumerate(variants, 1):
        valid = isinstance(item, dict) and isinstance(item.get("name"), str) and item["name"].strip() and isinstance(item.get("prompt"), str) and item["prompt"].strip()
        if not valid:
            fail(f"variants[{index}] 必须包含非空 name 和 prompt。")
        if "references" in item and (not isinstance(item["references"], list) or not all(isinstance(value, str) and value.strip() for value in item["references"])):
            fail(f"variants[{index}].references 必须是非空本地路径或图片 URL 的数组。")
        if item.get("count", raw.get("count", 1)) not in (1, 2, 4):
            fail(f"variants[{index}].count 只能是 1、2 或 4。")
    return raw


def generate_variant(project_uuid: str, request: dict[str, Any], variant: dict[str, Any]) -> dict[str, Any]:
    references = list(request.get("references", [])) + list(variant.get("references", []))
    reference_keys = prepare_references(project_uuid, references)
    count = variant.get("count", request.get("count", 1))
    node_name = f"生成｜{variant['name']}｜{datetime.now().strftime('%H%M%S')}"
    command = ["node", "--x", "520", "--y", "0", "create", node_name, "-p", project_uuid, "-t", "image", "-s", f"model={request.get('model', DEFAULT_MODEL)}", "-s", f"quality={DEFAULT_QUALITY}", "-s", f"ratio={DEFAULT_RATIO}", "-s", f"count={count}"]
    if reference_keys:
        command.extend(("-s", "modeType=image2image"))
        for key in reference_keys:
            command.extend(("--left", key))
    command.extend(("--prompt", variant["prompt"], "--run"))

    # LibTV writes generated nodes only when its service task has completed. The final
    # --run response is authoritative; never poll the individual node afterward.
    note(f"正在生成「{variant['name']}」；完成前无进度输出和无生成节点都正常，请勿重复提交。")
    completed = libtv(*command)
    data = completed.get("data", {})
    images = data.get("url", [])
    if not images:
        fail("LibTV 已返回完成响应，但没有图片 URL。")
    node_key = completed.get("nodeKey")
    remember_results(project_uuid, str(node_key) if node_key else None, images)
    return {"name": variant["name"], "status": "completed", "images": images, "referenceCount": len(references)}


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
    workspace = workspace_id(str(request.get("workspace", DEFAULT_WORKSPACE)))
    session = str(request["session"]).strip()
    project_uuid = resolve_canvas(workspace, session)
    results: list[dict[str, Any]] = []
    for variant in request["variants"]:
        try:
            results.append(generate_variant(project_uuid, request, variant))
        except RuntimeError as exc:
            results.append({"name": variant["name"], "status": "failed", "images": [], "error": str(exc)})
    failed = [item for item in results if item["status"] != "completed"]
    return {"status": "completed" if not failed else "partial_failure", "session": session, "canvasUrl": f"https://www.liblib.tv/canvas?spaceId={workspace}&projectId={project_uuid}", "model": request.get("model", DEFAULT_MODEL), "quality": DEFAULT_QUALITY, "ratio": DEFAULT_RATIO, "results": results}


def main() -> int:
    parser = argparse.ArgumentParser(description="LibTV Image Generate：提交一批候选方案并等待结果。")
    parser.add_argument("--request", required=True, help="请求 JSON 文件路径；传 - 时从标准输入读取。")
    parser.add_argument("--dry-run", action="store_true", help="仅校验请求 JSON，不访问 LibTV。")
    args = parser.parse_args()
    try:
        request = load_request(args.request)
        if args.dry_run:
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
