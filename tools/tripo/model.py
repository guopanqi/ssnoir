#!/usr/bin/env python3
"""Submit, resume and download Tripo v3 model generation tasks."""

import argparse
import hashlib
import json
import mimetypes
import os
import subprocess
import sys
import time
import uuid
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen

BASE = "https://openapi.tripo3d.ai/v3"
MODEL = "P1-20260311"
SERVICE = "com.ssnoir.tripo.api-key"
ACCOUNT = "SSNoir Tripo 3D"
ROOT = Path(__file__).resolve().parents[2]


def key():
    result = subprocess.run(
        ["security", "find-generic-password", "-s", SERVICE, "-a", ACCOUNT, "-w"],
        capture_output=True, text=True, check=False,
    ) if sys.platform == "darwin" else None
    value = (result.stdout.strip() if result and result.returncode == 0 else "") or os.environ.get("TRIPO_API_KEY", "").strip()
    if not value:
        raise RuntimeError("找不到 Tripo API Key；参见 README 的 Keychain 设置命令。")
    return value


def api(path, token, body=None, content_type="application/json"):
    headers = {"Authorization": f"Bearer {token}"}
    if body is not None:
        headers["Content-Type"] = content_type
    request = Request(BASE + path, data=body, headers=headers, method="POST" if body is not None else "GET")
    try:
        with urlopen(request, timeout=90) as response:
            result = json.load(response)
    except HTTPError as exc:
        raise RuntimeError(f"Tripo HTTP {exc.code}: {exc.read(1000).decode('utf-8', 'replace')}") from None
    except URLError as exc:
        raise RuntimeError(f"Tripo 连接失败: {exc.reason}") from None
    if result.get("code") != 0 or not isinstance(result.get("data"), dict):
        raise RuntimeError(f"Tripo API 错误: {result}")
    return result["data"]


def upload(path, token):
    path = Path(path).expanduser().resolve()
    if not path.is_file() or path.suffix.lower() not in {".png", ".jpg", ".jpeg"}:
        raise RuntimeError("本地参考图必须是已有的 PNG 或 JPEG 文件。")
    if path.stat().st_size > 20_000_000:
        raise RuntimeError("参考图超过 Tripo 的 20 MB 上限。")
    boundary = uuid.uuid4().hex
    mime = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{path.name}\"\r\n"
            f"Content-Type: {mime}\r\n\r\n").encode() + path.read_bytes() + f"\r\n--{boundary}--\r\n".encode()
    return api("/files", token, body, f"multipart/form-data; boundary={boundary}")["file_token"]


def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def download(url, path):
    if urlparse(url).scheme != "https":
        raise RuntimeError("Tripo 返回的下载地址不是 HTTPS。")
    digest = hashlib.sha256()
    temporary = path.with_suffix(path.suffix + ".tmp")
    try:
        with urlopen(url, timeout=120) as response, temporary.open("wb") as output:
            while chunk := response.read(1024 * 1024):
                output.write(chunk)
                digest.update(chunk)
        temporary.replace(path)
    except Exception:
        temporary.unlink(missing_ok=True)
        raise
    return {"path": str(path), "bytes": path.stat().st_size, "sha256": digest.hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--image", help="本地 PNG/JPEG，提交新 image-to-model 任务")
    source.add_argument("--image-url", help="公开的参考图 URL")
    source.add_argument("--prompt", help="提交新 text-to-model 任务")
    source.add_argument("--task-id", help="继续查询已有任务，不重复生成")
    parser.add_argument("--out", type=Path, default=ROOT / "tmp" / "tripo-3d-generate")
    parser.add_argument("--face-limit", type=int, help="可选面数上限，实际拓扑需审计")
    parser.add_argument("--poll-seconds", type=int, default=10)
    parser.add_argument("--timeout-seconds", type=int, default=900)
    args = parser.parse_args()
    if args.face_limit is not None and not 50 <= args.face_limit <= 20_000:
        parser.error("P1 的 --face-limit 必须在 50–20000 之间")
    if args.poll_seconds <= 0 or args.timeout_seconds <= 0:
        parser.error("轮询间隔和超时必须大于 0")
    token = key()
    output = args.out.expanduser().resolve()
    if args.task_id:
        task_id = args.task_id
        job_dir = output / task_id
    else:
        image = args.image or args.image_url
        endpoint = "/generation/image-to-model" if image else "/generation/text-to-model"
        value = upload(args.image, token) if args.image else image
        payload = {"input" if image else "prompt": value or args.prompt, "model": MODEL}
        if args.face_limit is not None:
            payload["face_limit"] = args.face_limit
        task_id = api(endpoint, token, json.dumps(payload).encode())["task_id"]
        job_dir = output / task_id
        save(job_dir / "manifest.json", {"task_id": task_id, "status": "submitted", "source": args.image or args.image_url or args.prompt, "model": MODEL})
        print(f"已提交 {task_id}；中断后用 --task-id {task_id} 继续。", file=sys.stderr)
    manifest_path = job_dir / "manifest.json"
    deadline = time.monotonic() + args.timeout_seconds
    while True:
        result = api(f"/tasks/{task_id}", token)
        manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {"task_id": task_id}
        manifest.update({"status": result.get("status"), "progress": result.get("progress"), "credits_consumed": result.get("credits_consumed"), "task": result})
        save(manifest_path, manifest)
        status = result.get("status")
        if status in {"success", "failed", "cancelled"}:
            break
        if time.monotonic() >= deadline:
            raise RuntimeError(f"本地等待超时；任务可继续查询：{task_id}，清单：{manifest_path}")
        time.sleep(args.poll_seconds)
    if status != "success":
        raise RuntimeError(f"Tripo 任务 {status}：{result.get('error_message', '')}；清单：{manifest_path}")
    outputs = result.get("output") or {}
    if not outputs.get("model_url"):
        raise RuntimeError(f"任务成功但没有 model_url；清单：{manifest_path}")
    files = {}
    for field, basename in (("model_url", "model"), ("rendered_image_url", "preview")):
        url = outputs.get(field)
        if url:
            suffix = Path(urlparse(url).path).suffix.lower() or (".glb" if basename == "model" else ".png")
            files[basename] = download(url, job_dir / (basename + suffix))
    manifest["files"] = files
    save(manifest_path, manifest)
    print(json.dumps({"task_id": task_id, "status": status, "credits_consumed": result.get("credits_consumed"), "files": files, "manifest": str(manifest_path)}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, ValueError, KeyError) as exc:
        print(f"错误：{exc}", file=sys.stderr)
        sys.exit(1)
