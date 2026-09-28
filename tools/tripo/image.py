#!/usr/bin/env python3
"""Generate or edit a reference image with Tripo banana models."""

import argparse
import json
import sys
import time
from pathlib import Path

from model import ROOT, api, download, key, save, upload


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--prompt", help="文生图提示词")
    source.add_argument("--image", help="图生图的本地 PNG/JPEG 图片")
    source.add_argument("--task-id", help="续查已有图像任务，不重新提交")
    parser.add_argument("--edit", help="图生图编辑指令，与 --image 一起使用")
    parser.add_argument("--model", choices=("banana2", "banana_pro"), default="banana2")
    parser.add_argument("--ratio", choices=("1:1", "3:4", "4:3", "16:9", "9:16"), default="1:1")
    parser.add_argument("--out", type=Path, default=ROOT / "tmp" / "tripo-image-generate")
    parser.add_argument("--poll-seconds", type=int, default=5)
    parser.add_argument("--timeout-seconds", type=int, default=600)
    args = parser.parse_args()
    if bool(args.image) != bool(args.edit):
        parser.error("--image 与 --edit 必须一起使用")
    if args.poll_seconds <= 0 or args.timeout_seconds <= 0:
        parser.error("轮询间隔和超时必须大于 0")
    token = key()
    output = args.out.expanduser().resolve()
    if args.task_id:
        task_id = args.task_id
    else:
        size = "0.5K" if args.model == "banana2" else "1K"
        payload = {"model": args.model, "size": size, "aspect_ratio": args.ratio, "output_format": "png"}
        if args.image:
            payload.update({"input": upload(args.image, token), "prompt": args.edit})
            endpoint = "/generation/image-to-image"
        else:
            payload["prompt"] = args.prompt
            endpoint = "/generation/text-to-image"
        task_id = api(endpoint, token, json.dumps(payload, ensure_ascii=False).encode("utf-8"))["task_id"]
        save(output / task_id / "manifest.json", {"task_id": task_id, "status": "submitted", "source": args.image, "request": payload | {"input": args.image} if args.image else payload})
        print(f"已提交 {task_id}；中断后用 --task-id {task_id} 继续。", file=sys.stderr)
    job_dir = output / task_id
    manifest_path = job_dir / "manifest.json"
    deadline = time.monotonic() + args.timeout_seconds
    while True:
        result = api(f"/tasks/{task_id}", token)
        manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {"task_id": task_id}
        manifest.update({"status": result.get("status"), "progress": result.get("progress"), "credits_consumed": result.get("credits_consumed")})
        save(manifest_path, manifest)
        status = result.get("status")
        if status in {"success", "failed", "cancelled"}:
            break
        if time.monotonic() >= deadline:
            raise RuntimeError(f"本地等待超时；任务可继续查询：{task_id}，清单：{manifest_path}")
        time.sleep(args.poll_seconds)
    if status != "success":
        raise RuntimeError(f"Tripo 任务 {status}：{result.get('error_message', '')}；清单：{manifest_path}")
    image_url = (result.get("output") or {}).get("generated_image_url")
    if not image_url:
        raise RuntimeError(f"任务成功但没有 generated_image_url；清单：{manifest_path}")
    image = download(image_url, job_dir / "image.png")
    manifest["image"] = image
    save(manifest_path, manifest)
    print(json.dumps({"task_id": task_id, "image": image, "credits_consumed": result.get("credits_consumed"), "manifest": str(manifest_path)}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, ValueError, KeyError) as exc:
        print(f"错误：{exc}", file=sys.stderr)
        sys.exit(1)
