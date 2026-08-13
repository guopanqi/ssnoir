#!/usr/bin/env bash

set -euo pipefail

now_ns() {
    python3 -c 'import time; print(time.time_ns())'
}

BUILD_START_NS="$(now_ns)"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SOURCE_PROJECT="$REPO_ROOT/UnityClient"
CACHE_ROOT="$REPO_ROOT/.cache/taptap-build"
STAGING_ROOT="$CACHE_ROOT/staging"
STAGING_PROJECT="$STAGING_ROOT/UnityClient"
GENERATED_FONT_DIR="$CACHE_ROOT/generated-fonts"
DEFAULT_PLAN="$SCRIPT_DIR/release-plan.json"
PLAN_PATH="$DEFAULT_PLAN"
CLEAN_CACHE=0
CDN_URL=""
CDN_PUBLISH_DIR="$SOURCE_PROJECT/Build/TapTapCdn"

usage() {
    echo "用法: $0 [--plan <release-plan.json>] [--clean-cache] [--cdn-url <https-url>]"
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
    case "$STAGING_ROOT" in
        "$REPO_ROOT/.cache/taptap-build/staging") rm -rf "$STAGING_ROOT" ;;
        *) echo "拒绝清理意外路径: $STAGING_ROOT" >&2; exit 2 ;;
    esac
fi

AVAILABLE_KB="$(df -Pk "$REPO_ROOT" | awk 'NR == 2 { print $4 }')"
REQUIRED_KB=$((2 * 1024 * 1024))
if [[ "$AVAILABLE_KB" -lt "$REQUIRED_KB" ]]; then
    AVAILABLE_MB=$((AVAILABLE_KB / 1024))
    echo "磁盘可用空间只有 ${AVAILABLE_MB} MiB；TapTap WebGL 构建至少预留 2048 MiB。" >&2
    exit 3
fi

STAGING_START_NS="$(now_ns)"
mkdir -p "$CACHE_ROOT" "$STAGING_ROOT"

if [[ ! -d "$STAGING_PROJECT" ]]; then
    echo "[TapTapBuild] 首次创建 APFS staging 快照..."
    cp -cR "$SOURCE_PROJECT" "$STAGING_PROJECT"
fi

# Library 是 staging 自己的持久缓存；构建产物和临时目录不从开发项目同步。
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
STAGING_END_NS="$(now_ns)"

echo "[TapTapBuild] 扫描文字并生成 SourceHan 子集..."
FONT_START_NS="$(now_ns)"
python3 "$SCRIPT_DIR/subset_fonts.py" \
    --repo-root "$REPO_ROOT" \
    --output-dir "$GENERATED_FONT_DIR"
FONT_END_NS="$(now_ns)"

UNITY_EXECUTABLE="${UNITY_PATH:-/Applications/Unity/Unity.app/Contents/MacOS/Unity}"
[[ -x "$UNITY_EXECUTABLE" ]] || {
    echo "找不到 Unity。请通过 UNITY_PATH 指定 Unity 可执行文件。" >&2
    exit 2
}

BUILD_STAMP="$(date '+%Y%m%d-%H%M%S')"
OUTPUT_DIR="$SOURCE_PROJECT/Build/TapTapRelease/$BUILD_STAMP"
LOG_PATH="$OUTPUT_DIR/build.log"
SNAPSHOT_PLAN="$OUTPUT_DIR/release-plan.json"
mkdir -p "$OUTPUT_DIR"
cp "$PLAN_PATH" "$SNAPSHOT_PLAN"
cp "$GENERATED_FONT_DIR/font-report.json" "$OUTPUT_DIR/font-report.json"

echo "[TapTapBuild] 在 staging 中构建 TapTap 小游戏..."
echo "[TapTapBuild] 输出目录: $OUTPUT_DIR"

UNITY_START_NS="$(now_ns)"
set +e
SSNOIR_TAPTAP_RELEASE_PLAN="$SNAPSHOT_PLAN" \
SSNOIR_TAPTAP_FONT_DIR="$GENERATED_FONT_DIR" \
SSNOIR_TAPTAP_OUTPUT="$OUTPUT_DIR" \
SSNOIR_TAPTAP_CDN_URL="$CDN_URL" \
    "$UNITY_EXECUTABLE" \
        -batchmode \
        -quit \
        -nographics \
        -buildTarget WebGL \
        -projectPath "$STAGING_PROJECT" \
        -executeMethod SSNoir.Editor.TapTapCommandLineBuilder.Build \
        -logFile "$LOG_PATH"
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
    --release-plan "$SNAPSHOT_PLAN" \
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

echo "[TapTapBuild] 构建完成: $OUTPUT_DIR"
if [[ -n "$CDN_URL" ]]; then
    echo "[TapTapBuild] CDN 地址: $CDN_URL"
fi
