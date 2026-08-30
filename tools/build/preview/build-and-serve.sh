#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/../common.sh"
ssnoir_build_init "web-preview"
SOURCE_PROJECT="$SSNOIR_SOURCE_PROJECT"
OUTPUT_DIR="$SOURCE_PROJECT/Build/WebPreview"
PORT=8000
SERVE_ONLY=0
REVIEW_NO_VIDEO=0

usage() {
    echo "用法: $0 [--port <端口>] [--serve-only] [--review-no-video]"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --port)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            PORT="$2"
            shift 2
            ;;
        --serve-only)
            SERVE_ONLY=1
            shift
            ;;
        --review-no-video)
            REVIEW_NO_VIDEO=1
            shift
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

[[ "$PORT" =~ ^[0-9]+$ ]] && [[ "$PORT" -ge 1 ]] && [[ "$PORT" -le 65535 ]] || {
    echo "端口必须是 1 到 65535 的整数: $PORT" >&2
    exit 2
}

if [[ "$SERVE_ONLY" == 0 ]]; then
    ssnoir_require_build_space "WebGL 构建"
    ssnoir_prepare_staging "WebPreview"
    ssnoir_generate_font_subset "WebPreview"
    ssnoir_require_unity
    VIDEO_MODE="local"
    [[ "$REVIEW_NO_VIDEO" == 0 ]] || VIDEO_MODE="none"

    mkdir -p "$OUTPUT_DIR"
    LOG_PATH="$OUTPUT_DIR/build.log"
    echo "[WebPreview] 使用已有 staging Library 构建标准 WebGL..."
    echo "[WebPreview] 输出目录: $OUTPUT_DIR"

    set +e
    ssnoir_run_unity \
        SSNoir.Editor.WebPreviewCommandLineBuilder.Build \
        "$LOG_PATH" \
        "SSNOIR_BUILD_RESOURCE_PLAN=$SSNOIR_RESOURCE_PLAN" \
        "SSNOIR_BUILD_FONT_DIR=$SSNOIR_GENERATED_FONT_DIR" \
        "SSNOIR_BUILD_VIDEO_MODE=$VIDEO_MODE" \
        "SSNOIR_WEB_PREVIEW_OUTPUT=$OUTPUT_DIR"
    UNITY_EXIT=$?
    set -e

    if [[ "$UNITY_EXIT" -ne 0 ]]; then
        echo "[WebPreview] Unity 构建失败，退出码: $UNITY_EXIT" >&2
        tail -n 120 "$LOG_PATH" >&2 || true
        exit "$UNITY_EXIT"
    fi

fi

[[ -f "$OUTPUT_DIR/index.html" ]] || {
    echo "找不到 Web 预览产物: $OUTPUT_DIR/index.html" >&2
    echo "请先去掉 --serve-only 完成一次构建。" >&2
    exit 2
}

exec python3 "$SCRIPT_DIR/serve.py" --directory "$OUTPUT_DIR" --port "$PORT"
