#!/usr/bin/env bash

set -euo pipefail

now_ns() {
    python3 -c 'import time; print(time.time_ns())'
}

BUILD_START_NS="$(now_ns)"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/../build/common.sh"
ssnoir_build_init "taptap-release"
REPO_ROOT="$SSNOIR_REPO_ROOT"
SOURCE_PROJECT="$SSNOIR_SOURCE_PROJECT"
CACHE_ROOT="$SSNOIR_PROFILE_CACHE_ROOT"
GENERATED_FONT_DIR="$SSNOIR_GENERATED_FONT_DIR"
PLAN_PATH="$SSNOIR_RESOURCE_PLAN"
CLEAN_CACHE=0
CDN_URL=""
CDN_PUBLISH_DIR="$SOURCE_PROJECT/Build/TapTapCdn"
REMOTE_ASSET_OUTPUT="$CACHE_ROOT/remote-assets"
REVIEW_NO_VIDEO=0

usage() {
    echo "用法: $0 [--plan <resource-plan.json>] [--clean-cache] [--cdn-url <https-url>] [--review-no-video]"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --plan)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            PLAN_PATH="$2"
            shift 2
            ;;
        --clean-cache)
            CLEAN_CACHE=1
            shift
            ;;
        --cdn-url)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            CDN_URL="${2%/}"
            [[ "$CDN_URL" == https://* ]] || {
                echo "CDN 地址必须使用 HTTPS: $CDN_URL" >&2
                exit 2
            }
            shift 2
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

[[ -f "$PLAN_PATH" ]] || { echo "找不到发布计划: $PLAN_PATH" >&2; exit 2; }
PLAN_PATH="$(cd "$(dirname "$PLAN_PATH")" && pwd)/$(basename "$PLAN_PATH")"

if [[ "$CLEAN_CACHE" == 1 ]]; then
    case "$CACHE_ROOT" in
        "$REPO_ROOT/.cache/build/taptap-release") rm -rf "$CACHE_ROOT" ;;
        *) echo "拒绝清理意外路径: $CACHE_ROOT" >&2; exit 2 ;;
    esac
fi

STAGING_START_NS="$(now_ns)"
ssnoir_require_build_space "TapTap WebGL 构建"
ssnoir_prepare_staging "TapTapBuild"
STAGING_END_NS="$(now_ns)"

FONT_START_NS="$(now_ns)"
ssnoir_generate_font_subset "TapTapBuild"
FONT_END_NS="$(now_ns)"

ssnoir_require_unity

VIDEO_MODE="none"
if [[ -n "$CDN_URL" && "$REVIEW_NO_VIDEO" == 0 ]]; then
    VIDEO_MODE="remote"
fi
rm -rf "$REMOTE_ASSET_OUTPUT"
mkdir -p "$REMOTE_ASSET_OUTPUT"

BUILD_STAMP="$(date '+%Y%m%d-%H%M%S')"
OUTPUT_DIR="$SOURCE_PROJECT/Build/TapTapRelease/$BUILD_STAMP"
LOG_PATH="$OUTPUT_DIR/build.log"
SNAPSHOT_PLAN="$OUTPUT_DIR/resource-plan.json"
mkdir -p "$OUTPUT_DIR"
cp "$PLAN_PATH" "$SNAPSHOT_PLAN"
cp "$GENERATED_FONT_DIR/font-report.json" "$OUTPUT_DIR/font-report.json"

echo "[TapTapBuild] 在 staging 中构建 TapTap 小游戏..."
echo "[TapTapBuild] 输出目录: $OUTPUT_DIR"

UNITY_START_NS="$(now_ns)"
set +e
ssnoir_run_unity \
    SSNoir.Editor.TapTapCommandLineBuilder.Build \
    "$LOG_PATH" \
    "SSNOIR_BUILD_RESOURCE_PLAN=$SNAPSHOT_PLAN" \
    "SSNOIR_BUILD_FONT_DIR=$GENERATED_FONT_DIR" \
    "SSNOIR_BUILD_VIDEO_MODE=$VIDEO_MODE" \
    "SSNOIR_BUILD_REMOTE_ASSET_URL=$CDN_URL" \
    "SSNOIR_BUILD_REMOTE_ASSET_OUTPUT=$REMOTE_ASSET_OUTPUT" \
    "SSNOIR_TAPTAP_OUTPUT=$OUTPUT_DIR" \
    "SSNOIR_TAPTAP_CDN_URL=$CDN_URL"
UNITY_EXIT=$?
set -e
UNITY_END_NS="$(now_ns)"

if [[ "$UNITY_EXIT" -ne 0 ]]; then
    echo "[TapTapBuild] Unity 构建失败，退出码: $UNITY_EXIT" >&2
    tail -n 120 "$LOG_PATH" >&2 || true
    exit "$UNITY_EXIT"
fi

if [[ -n "$CDN_URL" ]]; then
    [[ -d "$OUTPUT_DIR/webgl" ]] || {
        echo "[TapTapBuild] CDN 构建缺少 webgl 远程资源目录。" >&2
        exit 4
    }
    CDN_STAGING_DIR="$(mktemp -d "$CACHE_ROOT/cdn-publish.XXXXXX")"
    DATA_FILE_COUNT=0
    while IFS= read -r -d '' DATA_FILE; do
        cp "$DATA_FILE" "$CDN_STAGING_DIR/"
        DATA_FILE_COUNT=$((DATA_FILE_COUNT + 1))
    done < <(find "$OUTPUT_DIR/webgl" -maxdepth 1 -type f \
        -name '*.webgl.data.unityweb.bin*' -print0)
    [[ "$DATA_FILE_COUNT" -eq 1 ]] || {
        echo "[TapTapBuild] CDN 构建应产生 1 个首包 Data，实际为 $DATA_FILE_COUNT 个。" >&2
        exit 4
    }
    if [[ -d "$OUTPUT_DIR/webgl/Assets" ]]; then
        rsync -a "$OUTPUT_DIR/webgl/Assets/" "$CDN_STAGING_DIR/Assets/"
    fi
    if [[ "$VIDEO_MODE" == "remote" ]]; then
        rsync -a "$REMOTE_ASSET_OUTPUT/" "$CDN_STAGING_DIR/"
    fi
    mkdir -p "$CDN_PUBLISH_DIR"
    rsync -a --delete "$CDN_STAGING_DIR/" "$CDN_PUBLISH_DIR/"
    rm -rf "$CDN_STAGING_DIR"
    echo "[TapTapBuild] CDN 资源已发布: $CDN_PUBLISH_DIR"
fi

# 两个 zip 已通过完整性检查；原始 WebGL 与转换目录可由同一构建重建，不作为交付物保留。
CLEANUP_START_NS="$(now_ns)"
rm -rf "$OUTPUT_DIR/webgl" "$OUTPUT_DIR/minigame"
CLEANUP_END_NS="$(now_ns)"

python3 "$SCRIPT_DIR/write_build_report.py" \
    --output-dir "$OUTPUT_DIR" \
    --font-report "$GENERATED_FONT_DIR/font-report.json" \
    --resource-plan "$SNAPSHOT_PLAN" \
    --build-log "$LOG_PATH" \
    --build-start-ns "$BUILD_START_NS" \
    --staging-start-ns "$STAGING_START_NS" \
    --staging-end-ns "$STAGING_END_NS" \
    --font-start-ns "$FONT_START_NS" \
    --font-end-ns "$FONT_END_NS" \
    --unity-start-ns "$UNITY_START_NS" \
    --unity-end-ns "$UNITY_END_NS" \
    --cleanup-start-ns "$CLEANUP_START_NS" \
    --cleanup-end-ns "$CLEANUP_END_NS"

ssnoir_cleanup_timestamped_releases "$SOURCE_PROJECT/Build/TapTapRelease" "$OUTPUT_DIR"

echo "[TapTapBuild] 构建完成: $OUTPUT_DIR"
if [[ -n "$CDN_URL" ]]; then
    echo "[TapTapBuild] CDN 地址: $CDN_URL"
fi
