#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
OUTPUT_DIR=""
PORT=8000

usage() {
    echo "用法: $0 --directory <WebRelease 输出目录> [--port <端口>]"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --directory)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --port)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            PORT="$2"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "未知参数: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

[[ -n "$OUTPUT_DIR" ]] || { echo "缺少 --directory。" >&2; usage >&2; exit 2; }
[[ "$PORT" =~ ^[0-9]+$ ]] && [[ "$PORT" -ge 1 ]] && [[ "$PORT" -le 65535 ]] || {
    echo "端口必须是 1 到 65535 的整数: $PORT" >&2
    exit 2
}

OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"
[[ -f "$OUTPUT_DIR/index.html" ]] || {
    echo "找不到 Web Release 入口文件: $OUTPUT_DIR/index.html" >&2
    exit 2
}

exec python3 "$REPO_ROOT/tools/build/preview/serve.py" \
    --directory "$OUTPUT_DIR" \
    --port "$PORT" \
    --brotli \
    --cache-mode release
