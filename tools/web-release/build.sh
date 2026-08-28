#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SOURCE_PROJECT="$REPO_ROOT/UnityClient"
CACHE_ROOT="$REPO_ROOT/.cache/taptap-build"
STAGING_ROOT="$CACHE_ROOT/staging"
STAGING_PROJECT="$STAGING_ROOT/UnityClient"
OUTPUT_ROOT="$SOURCE_PROJECT/Build/WebRelease"
OFFLINE_SERVER_SOURCE="$SCRIPT_DIR/offline-server/main.go"
OFFLINE_LAUNCHER_DIRECTORY="$SCRIPT_DIR/offline-launcher"

usage() {
    echo "用法: $0"
}

remove_macos_metadata() {
    find "$1" -type f \( -name '.DS_Store' -o -name '._*' \) -delete
}

if [[ $# -gt 0 ]]; then
    case "$1" in
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
fi

AVAILABLE_KB="$(df -Pk "$REPO_ROOT" | awk 'NR == 2 { print $4 }')"
REQUIRED_KB=$((2 * 1024 * 1024))
if [[ "$AVAILABLE_KB" -lt "$REQUIRED_KB" ]]; then
    AVAILABLE_MB=$((AVAILABLE_KB / 1024))
    echo "磁盘可用空间只有 ${AVAILABLE_MB} MiB；WebGL 构建至少预留 2048 MiB。" >&2
    exit 3
fi

mkdir -p "$CACHE_ROOT" "$STAGING_ROOT"
if [[ ! -d "$STAGING_PROJECT" ]]; then
    echo "[WebRelease] 首次创建 APFS staging 快照..."
    cp -cR "$SOURCE_PROJECT" "$STAGING_PROJECT"
fi

echo "[WebRelease] 同步当前开发工程（保留全部资源和本地过场视频）..."
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
[[ -f "$OFFLINE_SERVER_SOURCE" ]] || {
    echo "找不到离线启动服务源码: $OFFLINE_SERVER_SOURCE" >&2
    exit 2
}
[[ -d "$OFFLINE_LAUNCHER_DIRECTORY" ]] || {
    echo "找不到离线启动器模板: $OFFLINE_LAUNCHER_DIRECTORY" >&2
    exit 2
}
command -v go >/dev/null 2>&1 || {
    echo "找不到 Go。离线评审包需要 Go 编译内置本地服务器。" >&2
    exit 2
}

BUILD_STAMP="$(date '+%Y%m%d-%H%M%S')"
OUTPUT_DIR="$OUTPUT_ROOT/$BUILD_STAMP"
LOG_PATH="$OUTPUT_ROOT/$BUILD_STAMP.build.log"
OFFLINE_BUNDLE="$OUTPUT_ROOT/$BUILD_STAMP-offline/SSNoir-WebDemo"
ARCHIVE_PATH="$OUTPUT_ROOT/SSNoir-WebDemo-$BUILD_STAMP.zip"
ITCH_ARCHIVE_PATH="$OUTPUT_ROOT/SSNoir-ItchWeb-$BUILD_STAMP.zip"
mkdir -p "$OUTPUT_DIR"

echo "[WebRelease] 构建标准浏览器 WebGL 正式包（非 Development Build）..."
echo "[WebRelease] 输出目录: $OUTPUT_DIR"

set +e
SSNOIR_WEB_RELEASE_OUTPUT="$OUTPUT_DIR" \
    "$UNITY_EXECUTABLE" \
        -batchmode \
        -quit \
        -nographics \
        -buildTarget WebGL \
        -projectPath "$STAGING_PROJECT" \
        -executeMethod SSNoir.Editor.WebReleaseCommandLineBuilder.Build \
        -logFile "$LOG_PATH"
UNITY_EXIT=$?
set -e

if [[ "$UNITY_EXIT" -ne 0 ]]; then
    echo "[WebRelease] Unity 构建失败，退出码: $UNITY_EXIT" >&2
    tail -n 120 "$LOG_PATH" >&2 || true
    exit "$UNITY_EXIT"
fi

[[ -f "$OUTPUT_DIR/index.html" ]] || {
    echo "[WebRelease] 缺少 WebGL 入口文件: $OUTPUT_DIR/index.html" >&2
    exit 4
}
remove_macos_metadata "$OUTPUT_DIR"

echo "[WebRelease] 打包无需额外运行时的离线评审版..."
mkdir -p "$OFFLINE_BUNDLE"
rsync -a "$OUTPUT_DIR/" "$OFFLINE_BUNDLE/"
cp "$OFFLINE_LAUNCHER_DIRECTORY/Start SSNoir Demo.command" "$OFFLINE_BUNDLE/"
cp "$OFFLINE_LAUNCHER_DIRECTORY/Start SSNoir Demo.bat" "$OFFLINE_BUNDLE/"
chmod +x "$OFFLINE_BUNDLE/Start SSNoir Demo.command"
CGO_ENABLED=0 GOOS=darwin GOARCH=arm64 go build -o "$OFFLINE_BUNDLE/SSNoirDemoServer-mac-arm64" "$OFFLINE_SERVER_SOURCE"
CGO_ENABLED=0 GOOS=darwin GOARCH=amd64 go build -o "$OFFLINE_BUNDLE/SSNoirDemoServer-mac-x64" "$OFFLINE_SERVER_SOURCE"
CGO_ENABLED=0 GOOS=windows GOARCH=amd64 go build -o "$OFFLINE_BUNDLE/SSNoirDemoServer-win-x64.exe" "$OFFLINE_SERVER_SOURCE"

rm -f "$ARCHIVE_PATH"
(
    cd "$(dirname "$OFFLINE_BUNDLE")"
    zip -q -r "$ARCHIVE_PATH" "$(basename "$OFFLINE_BUNDLE")" \
        -x '*/.DS_Store' '*/._*'
)
unzip -tq "$ARCHIVE_PATH" >/dev/null

rm -f "$ITCH_ARCHIVE_PATH"
(
    cd "$OUTPUT_DIR"
    zip -q -r "$ITCH_ARCHIVE_PATH" . -x '*/.DS_Store' '*/._*'
)
unzip -tq "$ITCH_ARCHIVE_PATH" >/dev/null
unzip -Z1 "$ITCH_ARCHIVE_PATH" | grep -qx 'index.html' || {
    echo "[WebRelease] itch 上传包缺少根目录 index.html。" >&2
    exit 4
}

echo "[WebRelease] 构建完成: $OUTPUT_DIR"
echo "[WebRelease] 离线评审包: $ARCHIVE_PATH"
echo "[WebRelease] itch HTML5 上传包: $ITCH_ARCHIVE_PATH"
