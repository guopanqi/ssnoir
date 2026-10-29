#!/usr/bin/env python3
"""Check and rig an existing humanoid model with Tripo v3."""
import argparse
import json
import mimetypes
import time
import uuid
from pathlib import Path

from model import api, download, key, save, ROOT


def upload_model(path, token):
    path = path.expanduser().resolve()
    if not path.is_file() or path.suffix.lower() not in {'.glb', '.fbx', '.obj', '.stl'}:
        raise ValueError('模型必须是已有的 GLB、FBX、OBJ 或 STL 文件。')
    if path.stat().st_size > 150_000_000:
        raise ValueError('模型超过 150 MB。')
    boundary = uuid.uuid4().hex
    mime = mimetypes.guess_type(path.name)[0] or 'application/octet-stream'
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{path.name}"\r\n'
            f'Content-Type: {mime}\r\n\r\n').encode() + path.read_bytes() + f'\r\n--{boundary}--\r\n'.encode()
    return api('/files', token, body, f'multipart/form-data; boundary={boundary}')['file_token']


def wait(task_id, token, job_dir, seconds, timeout):
    deadline = time.monotonic() + timeout
    while True:
        result = api(f'/tasks/{task_id}', token)
        save(job_dir / 'manifest.json', result)
        status = result.get('status')
        if status == 'success':
            return result
        if status in {'failed', 'cancelled'}:
            raise RuntimeError(f'任务 {task_id} {status}: {result.get("error_message", "")}')
        if time.monotonic() >= deadline:
            raise RuntimeError(f'等待超时；任务 {task_id} 可用 --task-id 续查。')
        time.sleep(seconds)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument('--model-file', type=Path)
    source.add_argument('--input', help='已有生成任务 ID、文件 token 或公开模型 URL')
    source.add_argument('--task-id', help='续查已有绑骨任务，不重复提交')
    parser.add_argument('--check-only', action='store_true', help='只检查模型是否适合绑骨')
    parser.add_argument('--out', type=Path, default=ROOT / 'tmp' / 'tripo-rig')
    parser.add_argument('--poll-seconds', type=int, default=10)
    parser.add_argument('--timeout-seconds', type=int, default=900)
    args = parser.parse_args()
    if args.poll_seconds <= 0 or args.timeout_seconds <= 0 or (args.task_id and args.check_only):
        parser.error('轮询间隔和超时必须大于零；--task-id 只用于续查绑骨任务。')
    token = key()
    if args.task_id:
        task_id = args.task_id
    else:
        source_id = upload_model(args.model_file, token) if args.model_file else args.input
        check_id = api('/animations/rig-check', token, json.dumps({'input': source_id}).encode())['task_id']
        print(f'已提交绑骨检查 {check_id}', flush=True)
        checked = wait(check_id, token, args.out / check_id, args.poll_seconds, args.timeout_seconds)
        result = checked.get('output') or {}
        print(json.dumps(result, ensure_ascii=False), flush=True)
        if not result.get('riggable') or result.get('rig_type') != 'biped':
            raise RuntimeError('模型未通过人形绑骨检查。')
        if args.check_only:
            return
        payload = {'input': source_id, 'model': 'v3.0-20260909', 'rig_type': 'biped',
                   'spec': 'mixamo', 'out_format': 'fbx'}
        task_id = api('/animations/rig', token, json.dumps(payload).encode())['task_id']
        print(f'已提交绑骨任务 {task_id}；可用 --task-id 续查。', flush=True)
    job_dir = args.out / task_id
    result = wait(task_id, token, job_dir, args.poll_seconds, args.timeout_seconds)
    url = (result.get('output') or {}).get('model_url')
    if not url:
        raise RuntimeError('绑骨任务成功但没有 model_url。')
    job_dir.mkdir(parents=True, exist_ok=True)
    files = download(url, job_dir / 'rigged.fbx')
    print(json.dumps({'task_id': task_id, 'credits_consumed': result.get('credits_consumed'), 'file': files}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    try:
        main()
    except (RuntimeError, OSError, ValueError, KeyError) as exc:
        raise SystemExit(f'错误：{exc}')
