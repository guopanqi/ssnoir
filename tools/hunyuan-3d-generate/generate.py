#!/usr/bin/env python3
"""Generate one low-poly 3D asset through Tencent TokenHub HY-3D 3.0."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import mimetypes
import os
import re
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen


API_BASE = "https://tokenhub.tencentmaas.com/v1/api/3d"
MODEL = "hy-3d-3.0"
GENERATE_TYPE = "low_poly"
REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_OUTPUT_ROOT = REPO_ROOT / "tmp" / "hunyuan-3d-generate"
SUPPORTED_IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png", ".webp"}
MAX_IMAGE_BYTES = 6 * 1024 * 1024
MIN_IMAGE_EDGE = 128
MAX_IMAGE_EDGE = 5000


def fail(message: str) -> None:
    raise RuntimeError(message)


def note(message: str) -> None:
    print(f"[Hunyuan 3D Generate] {message}", file=sys.stderr, flush=True)


def read_image_size(path: Path) -> tuple[int, int]:
    data = path.read_bytes()
    if data.startswith(b"\x89PNG\r\n\x1a\n") and len(data) >= 24:
        return int.from_bytes(data[16:20], "big"), int.from_bytes(data[20:24], "big")
    if data[:2] == b"\xff\xd8":
        index = 2
        while index + 9 < len(data):
            if data[index] != 0xFF:
                index += 1
                continue
            marker = data[index + 1]
            index += 2
            if marker in (0xD8, 0xD9) or 0xD0 <= marker <= 0xD7:
                continue
            if index + 2 > len(data):
                break
            length = int.from_bytes(data[index:index + 2], "big")
            if marker in {0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF}:
                if index + 7 > len(data):
                    break
                return int.from_bytes(data[index + 5:index + 7], "big"), int.from_bytes(data[index + 3:index + 5], "big")
            if length < 2:
                break
            index += length
    if data[:4] in (b"RIFF",) and data[8:12] == b"WEBP":
        chunk = data[12:16]
        if chunk == b"VP8X" and len(data) >= 30:
            return 1 + int.from_bytes(data[24:27], "little"), 1 + int.from_bytes(data[27:30], "little")
        if chunk == b"VP8 " and len(data) >= 30 and data[23:26] == b"\x9d\x01\x2a":
            return int.from_bytes(data[26:28], "little") & 0x3FFF, int.from_bytes(data[28:30], "little") & 0x3FFF
        if chunk == b"VP8L" and len(data) >= 25 and data[20] == 0x2F:
            bits = int.from_bytes(data[21:25], "little")
            return (bits & 0x3FFF) + 1, ((bits >> 14) & 0x3FFF) + 1
    fail(f"无法读取图片尺寸或图片格式不受支持：{path}")


def validate_local_image(raw_path: str) -> Path:
    path = Path(raw_path).expanduser().resolve()
    if not path.is_file():
        fail(f"参考图不存在：{path}")
    if path.suffix.lower() not in SUPPORTED_IMAGE_SUFFIXES:
        fail("参考图只支持 jpg、jpeg、png、webp。")
    size = path.stat().st_size
    if size > MAX_IMAGE_BYTES:
        fail(f"参考图超过本工具的 6 MiB Base64 输入上限：{size} bytes。")
    width, height = read_image_size(path)
    if not (MIN_IMAGE_EDGE <= width <= MAX_IMAGE_EDGE and MIN_IMAGE_EDGE <= height <= MAX_IMAGE_EDGE):
        fail(f"参考图尺寸必须每边在 128–5000 像素之间；当前为 {width}x{height}。")
    return path


def validate_request(raw: Any, encode_image: bool = True) -> dict[str, Any]:
    if not isinstance(raw, dict):
        fail("请求必须是 JSON 对象。")
    allowed = {"session", "prompt", "image", "image_url", "polygon_type", "enable_pbr"}
    unknown = sorted(set(raw) - allowed)
    if unknown:
        fail("不支持的请求字段：" + "、".join(unknown))
    session = raw.get("session")
    if not isinstance(session, str) or not session.strip():
        fail("请求缺少非空字符串 session。")
    sources = [key for key in ("prompt", "image", "image_url") if raw.get(key)]
    if len(sources) != 1:
        fail("prompt、image、image_url 必须且只能提供一个。")
    if sources[0] == "prompt":
        prompt = raw["prompt"]
        if not isinstance(prompt, str) or not prompt.strip():
            fail("prompt 必须是非空字符串。")
        if len(prompt) > 1024:
            fail("prompt 过长；不得超过 1024 个字符。")
    if sources[0] == "image_url":
        image_url = raw["image_url"]
        if not isinstance(image_url, str) or not image_url.strip() or urlparse(image_url).scheme not in ("http", "https"):
            fail("image_url 必须是 http 或 https URL。")
    polygon_type = raw.get("polygon_type", "triangle")
    if polygon_type not in ("triangle", "quadrilateral"):
        fail("polygon_type 只能是 triangle 或 quadrilateral。")
    enable_pbr = raw.get("enable_pbr", False)
    if not isinstance(enable_pbr, bool):
        fail("enable_pbr 必须是布尔值。")

    request: dict[str, Any] = {
        "session": session.strip(),
        "model": MODEL,
        "generate_type": GENERATE_TYPE,
        "polygon_type": polygon_type,
        "enable_pbr": enable_pbr,
    }
    if sources[0] == "image":
        if not isinstance(raw["image"], str) or not raw["image"].strip():
            fail("image 必须是非空本地路径。")
        path = validate_local_image(raw["image"])
        request["image"] = str(path)
        if encode_image:
            request["image_base64"] = base64.b64encode(path.read_bytes()).decode("ascii")
    else:
        request[sources[0]] = raw[sources[0]].strip()
    return request


def api_payload(request: dict[str, Any]) -> dict[str, Any]:
    payload = {key: request[key] for key in ("model", "generate_type", "polygon_type", "enable_pbr")}
    for key in ("prompt", "image_url", "image_base64"):
        if key in request:
            payload[key] = request[key]
    return payload


def call_api(endpoint: str, payload: dict[str, Any], api_key: str) -> dict[str, Any]:
    body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    request = Request(
        f"{API_BASE}/{endpoint}",
        data=body,
        headers={"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urlopen(request, timeout=60) as response:
            result = json.loads(response.read().decode("utf-8"))
    except HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")[:2000]
        fail(f"TokenHub HTTP {exc.code}：{detail}")
    except URLError as exc:
        fail(f"无法连接 TokenHub：{exc.reason}")
    except json.JSONDecodeError:
        fail("TokenHub 返回了无法解析的 JSON。")
    if not isinstance(result, dict):
        fail("TokenHub 返回的不是 JSON 对象。")
    return result


def safe_name(value: str) -> str:
    name = re.sub(r"[\\/:*?\"<>|\x00-\x1f]", "_", value).strip(" .")
    return name[:80] or "unnamed"


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def download(url: str, destination: Path) -> dict[str, Any]:
    digest = hashlib.sha256()
    temporary = destination.with_suffix(destination.suffix + ".tmp")
    try:
        with urlopen(url, timeout=120) as response, temporary.open("wb") as output:
            while chunk := response.read(1024 * 1024):
                output.write(chunk)
                digest.update(chunk)
        temporary.replace(destination)
    except (HTTPError, URLError, OSError) as exc:
        temporary.unlink(missing_ok=True)
        fail(f"下载生成结果失败：{url}（{exc}）")
    return {"path": str(destination), "sha256": digest.hexdigest(), "bytes": destination.stat().st_size}


def result_suffix(item: dict[str, Any], url: str) -> str:
    path_suffix = Path(urlparse(url).path).suffix.lower()
    if path_suffix:
        return path_suffix
    result_type = str(item.get("type", "bin")).lower()
    return mimetypes.guess_extension(f"model/{result_type}") or f".{result_type}"


def run(request: dict[str, Any], output_root: Path, poll_seconds: float, timeout_seconds: float) -> dict[str, Any]:
    api_key = os.environ.get("HUNYUAN_3D_API_KEY", "").strip()
    if not api_key:
        fail("缺少环境变量 HUNYUAN_3D_API_KEY。")
    submitted = call_api("submit", api_payload(request), api_key)
    job_id = str(submitted.get("id", "")).strip()
    if not job_id:
        fail(f"TokenHub 提交响应缺少任务 id：{submitted}")

    timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    output_dir = output_root.expanduser().resolve() / f"{safe_name(request['session'])}-{timestamp}-{job_id}"
    manifest_path = output_dir / "manifest.json"
    public_request = {key: value for key, value in request.items() if key != "image_base64"}
    manifest: dict[str, Any] = {"status": submitted.get("status", "queued"), "jobId": job_id, "request": public_request, "submitResponse": submitted, "files": []}
    write_json(manifest_path, manifest)
    note(f"已提交任务 {job_id}；任务完成前不要重复提交。")

    deadline = time.monotonic() + timeout_seconds
    result = submitted
    while result.get("status") not in ("completed", "failed"):
        if time.monotonic() >= deadline:
            manifest["status"] = "in_progress"
            manifest["message"] = "本地等待超时；任务仍可用 jobId 查询。"
            write_json(manifest_path, manifest)
            fail(f"等待任务超时；任务 {job_id} 仍可继续查询。清单：{manifest_path}")
        time.sleep(poll_seconds)
        result = call_api("query", {"model": MODEL, "id": job_id}, api_key)
        manifest["status"] = result.get("status", "unknown")
        manifest["queryResponse"] = result
        write_json(manifest_path, manifest)

    if result.get("status") == "failed":
        fail(f"混元任务失败；完整响应已写入 {manifest_path}")

    files: list[dict[str, Any]] = []
    for index, item in enumerate(result.get("data", []), 1):
        if not isinstance(item, dict) or not item.get("url"):
            continue
        model_url = str(item["url"])
        model_suffix = result_suffix(item, model_url)
        model_file = download(model_url, output_dir / f"model-{index}-{safe_name(str(item.get('type', 'result')))}{model_suffix}")
        file_record: dict[str, Any] = {"type": item.get("type"), "model": model_file}
        preview_url = item.get("preview_image_url")
        if preview_url:
            preview_suffix = Path(urlparse(str(preview_url)).path).suffix or ".png"
            file_record["preview"] = download(str(preview_url), output_dir / f"preview-{index}{preview_suffix}")
        files.append(file_record)
    if not files:
        manifest["status"] = "failed"
        manifest["message"] = "任务已完成但响应中没有可下载的模型。"
        write_json(manifest_path, manifest)
        fail(f"任务已完成但没有可下载的模型；完整响应已写入 {manifest_path}")
    manifest["status"] = "completed"
    manifest["files"] = files
    manifest["completedAt"] = datetime.now(timezone.utc).isoformat()
    write_json(manifest_path, manifest)
    return {"status": "completed", "jobId": job_id, "outputDir": str(output_dir), "manifest": str(manifest_path), "files": files}


def load_request(argument: str, encode_image: bool) -> dict[str, Any]:
    try:
        raw_text = sys.stdin.read() if argument == "-" else Path(argument).read_text(encoding="utf-8")
        return validate_request(json.loads(raw_text), encode_image=encode_image)
    except OSError as exc:
        fail(f"无法读取请求：{exc}")
    except json.JSONDecodeError as exc:
        fail(f"请求不是合法 JSON：{exc}")


def main() -> int:
    parser = argparse.ArgumentParser(description="通过 TokenHub HY-3D 3.0 直接生成低拓扑模型。")
    parser.add_argument("--request", required=True, help="请求 JSON 路径；传 - 时从标准输入读取。")
    parser.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT, help="生成结果的父目录。")
    parser.add_argument("--poll-seconds", type=float, default=10.0, help="查询任务状态的间隔秒数。")
    parser.add_argument("--timeout-seconds", type=float, default=1800.0, help="本地等待超时秒数。")
    parser.add_argument("--dry-run", action="store_true", help="只校验请求，不访问 TokenHub、不产生费用。")
    args = parser.parse_args()
    try:
        if args.poll_seconds <= 0 or args.timeout_seconds <= 0:
            fail("poll-seconds 和 timeout-seconds 必须大于 0。")
        request = load_request(args.request, encode_image=not args.dry_run)
        if args.dry_run:
            print(json.dumps({"status": "valid", "request": request}, ensure_ascii=False))
            return 0
        result = run(request, args.output_root, args.poll_seconds, args.timeout_seconds)
        print(json.dumps(result, ensure_ascii=False))
        return 0
    except RuntimeError as exc:
        print(f"错误：{exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
