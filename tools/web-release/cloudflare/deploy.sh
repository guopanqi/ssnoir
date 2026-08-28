#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
RELEASE_DIR=""
BUCKET_NAME="ssnoir-web-demo"
WRANGLER="$SCRIPT_DIR/node_modules/.bin/wrangler"

usage() {
    echo "用法: $0 --release-dir <UnityClient/Build/WebRelease/时间戳目录>"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --release-dir)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            RELEASE_DIR="$2"
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

[[ -n "$RELEASE_DIR" ]] || { echo "缺少 --release-dir。" >&2; usage >&2; exit 2; }
RELEASE_DIR="$(cd "$RELEASE_DIR" && pwd)"
[[ -f "$RELEASE_DIR/index.html" ]] || {
    echo "找不到 Web Release 入口文件: $RELEASE_DIR/index.html" >&2
    exit 2
}

if [[ ! -x "$WRANGLER" ]]; then
    echo "[Cloudflare] 安装 Worker 发布工具..."
    (cd "$SCRIPT_DIR" && npm install --save-dev wrangler@latest typescript@latest)
fi

"$WRANGLER" whoami >/dev/null

if ! "$WRANGLER" r2 bucket info "$BUCKET_NAME" >/dev/null 2>&1; then
    echo "[Cloudflare] 创建 R2 bucket: $BUCKET_NAME"
    "$WRANGLER" r2 bucket create "$BUCKET_NAME" --location apac
fi

content_type() {
    case "$1" in
        *.html) echo "text/html; charset=utf-8" ;;
        *.css) echo "text/css; charset=utf-8" ;;
        *.js|*.js.br) echo "application/javascript; charset=utf-8" ;;
        *.wasm|*.wasm.br) echo "application/wasm" ;;
        *.json) echo "application/json; charset=utf-8" ;;
        *.svg) echo "image/svg+xml" ;;
        *.png) echo "image/png" ;;
        *.jpg|*.jpeg) echo "image/jpeg" ;;
        *.ico) echo "image/x-icon" ;;
        *.mp4) echo "video/mp4" ;;
        *) echo "application/octet-stream" ;;
    esac
}

echo "[Cloudflare] 上传 Web Release 到私有 R2..."
while IFS= read -r -d '' file; do
    key="${file#"$RELEASE_DIR/"}"
    arguments=(r2 object put "$BUCKET_NAME/$key" --file "$file" --content-type "$(content_type "$key")")
    if [[ "$key" == "index.html" ]]; then
        arguments+=(--cache-control "no-store, no-cache, must-revalidate")
    else
        arguments+=(--cache-control "public, max-age=31536000, immutable")
    fi
    if [[ "$key" == *.br ]]; then
        arguments+=(--content-encoding br)
    fi
    "$WRANGLER" "${arguments[@]}"
done < <(find "$RELEASE_DIR" -type f ! -name '.DS_Store' ! -name '._*' -print0)

echo "[Cloudflare] 部署 Worker 到 workers.dev..."
(cd "$SCRIPT_DIR" && "$WRANGLER" types && "$WRANGLER" deploy)

echo "[Cloudflare] 部署完成。请访问 Wrangler 输出的 workers.dev 地址。"
