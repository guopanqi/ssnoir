#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SOURCE_PROJECT="$REPO_ROOT/UnityClient"
CACHE_ROOT="$REPO_ROOT/.cache/taptap-build"
STAGING_ROOT="$CACHE_ROOT/staging"
STAGING_PROJECT="$STAGING_ROOT/UnityClient"
OUTPUT_DIR="$SOURCE_PROJECT/Build/WebPreview"
PORT=8000
SERVE_ONLY=0

usage() {
    echo "用法: $0 [--port <端口>] [--serve-only]"
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
    AVAILABLE_KB="$(df -Pk "$REPO_ROOT" | awk 'NR == 2 { print $4 }')"
    REQUIRED_KB=$((2 * 1024 * 1024))
    if [[ "$AVAILABLE_KB" -lt "$REQUIRED_KB" ]]; then
        AVAILABLE_MB=$((AVAILABLE_KB / 1024))
        echo "磁盘可用空间只有 ${AVAILABLE_MB} MiB；WebGL 构建至少预留 2048 MiB。" >&2
        exit 3
    fi

    mkdir -p "$CACHE_ROOT" "$STAGING_ROOT"
    if [[ ! -d "$STAGING_PROJECT" ]]; then
        echo "[WebPreview] 首次创建 APFS staging 快照..."
        cp -cR "$SOURCE_PROJECT" "$STAGING_PROJECT"
    fi

    echo "[WebPreview] 同步当前开发工程（保留全部开发资源）..."
    rsync -a --delete \
        --exclude '/Library/' \
        --exclude '/Temp/' \
        --exclude '/Logs/' \
        --exclude '/Build/' \
        --exclude '/Builds/' \
        --exclude '/UserSettings/' \
        --exclude '/Screenshots/' \
        --exclude '/CutsceneSource~/' \
        "$SOURCE_PROJECT/" "$STAGING_PROJECT/"

    mkdir -p "$STAGING_ROOT/Content"
    rsync -a --delete "$REPO_ROOT/Content/" "$STAGING_ROOT/Content/"

    rm -rf \
        "$STAGING_PROJECT/Build" \
        "$STAGING_PROJECT/Builds" \
        "$STAGING_PROJECT/Logs" \
        "$STAGING_PROJECT/Temp" \
        "$STAGING_PROJECT/Screenshots" \
        "$STAGING_PROJECT/CutsceneSource~"

    UNITY_EXECUTABLE="${UNITY_PATH:-/Applications/Unity/Unity.app/Contents/MacOS/Unity}"
    [[ -x "$UNITY_EXECUTABLE" ]] || {
        echo "找不到 Unity。请通过 UNITY_PATH 指定 Unity 可执行文件。" >&2
        exit 2
    }

    mkdir -p "$OUTPUT_DIR"
    LOG_PATH="$OUTPUT_DIR/build.log"
    echo "[WebPreview] 使用已有 staging Library 构建标准 WebGL..."
    echo "[WebPreview] 输出目录: $OUTPUT_DIR"

    set +e
    SSNOIR_WEB_PREVIEW_OUTPUT="$OUTPUT_DIR" \
        "$UNITY_EXECUTABLE" \
            -batchmode \
            -quit \
            -nographics \
            -buildTarget WebGL \
            -projectPath "$STAGING_PROJECT" \
            -executeMethod SSNoir.Editor.WebPreviewCommandLineBuilder.Build \
            -logFile "$LOG_PATH"
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
