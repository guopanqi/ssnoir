#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/../common.sh"
ssnoir_build_init "web-release"
SOURCE_PROJECT="$SSNOIR_SOURCE_PROJECT"
OUTPUT_ROOT="$SOURCE_PROJECT/Build/WebRelease"
OFFLINE_SERVER_SOURCE="$SCRIPT_DIR/offline-server/main.go"
OFFLINE_LAUNCHER_DIRECTORY="$SCRIPT_DIR/offline-launcher"
ITCH_TARGET="${ITCH_TARGET:-guopanqi/noir:web}"
PUBLISH_TO_ITCH=false
REVIEW_NO_VIDEO=false

usage() {
    cat <<EOF
用法: $0 [--itch] [--review-no-video]

  --itch  构建完成后上传到 itch.io（默认渠道: ${ITCH_TARGET}）
  --review-no-video  即使场景引用视频也不放入评审包

可通过 ITCH_TARGET=user/game:channel 覆盖默认 itch 目标。
EOF
}

remove_macos_metadata() {
    find "$1" -type f \( -name '.DS_Store' -o -name '._*' \) -delete
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --itch)
            PUBLISH_TO_ITCH=true
            ;;
        --review-no-video)
            REVIEW_NO_VIDEO=true
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
    shift
done

if [[ "$PUBLISH_TO_ITCH" == true ]]; then
    command -v butler >/dev/null 2>&1 || {
        echo "找不到 butler。请先安装并登录 itch.io Butler，或去掉 --itch 只构建。" >&2
        exit 2
    }
fi

ssnoir_require_build_space "WebGL 构建"
ssnoir_prepare_staging "WebRelease"
ssnoir_generate_font_subset "WebRelease"
ssnoir_require_unity
VIDEO_MODE="local"
[[ "$REVIEW_NO_VIDEO" == false ]] || VIDEO_MODE="none"
[[ -f "$OFFLINE_SERVER_SOURCE" ]] || {
    echo "找不到离线启动服务源码: $OFFLINE_SERVER_SOURCE" >&2
    exit 2
}
for launcher_file in "START-Mac.command" "START-Windows.bat" "README.txt"; do
    [[ -f "$OFFLINE_LAUNCHER_DIRECTORY/$launcher_file" ]] || {
        echo "找不到离线启动器模板: $OFFLINE_LAUNCHER_DIRECTORY/$launcher_file" >&2
        exit 2
    }
done
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
ssnoir_run_unity \
    SSNoir.Editor.WebReleaseCommandLineBuilder.Build \
    "$LOG_PATH" \
    "SSNOIR_BUILD_RESOURCE_PLAN=$SSNOIR_RESOURCE_PLAN" \
    "SSNOIR_BUILD_FONT_DIR=$SSNOIR_GENERATED_FONT_DIR" \
    "SSNOIR_BUILD_VIDEO_MODE=$VIDEO_MODE" \
    "SSNOIR_WEB_RELEASE_OUTPUT=$OUTPUT_DIR"
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
mkdir -p "$OFFLINE_BUNDLE/game"
rsync -a "$OUTPUT_DIR/" "$OFFLINE_BUNDLE/game/"
cp "$OFFLINE_LAUNCHER_DIRECTORY/START-Mac.command" "$OFFLINE_BUNDLE/"
cp "$OFFLINE_LAUNCHER_DIRECTORY/START-Windows.bat" "$OFFLINE_BUNDLE/"
cp "$OFFLINE_LAUNCHER_DIRECTORY/README.txt" "$OFFLINE_BUNDLE/"
chmod +x "$OFFLINE_BUNDLE/START-Mac.command"
CGO_ENABLED=0 GOOS=darwin GOARCH=arm64 go build -o "$OFFLINE_BUNDLE/game/SSNoirDemoServer-mac-arm64" "$OFFLINE_SERVER_SOURCE"
CGO_ENABLED=0 GOOS=darwin GOARCH=amd64 go build -o "$OFFLINE_BUNDLE/game/SSNoirDemoServer-mac-x64" "$OFFLINE_SERVER_SOURCE"
CGO_ENABLED=0 GOOS=windows GOARCH=amd64 go build -o "$OFFLINE_BUNDLE/game/SSNoirDemoServer-win-x64.exe" "$OFFLINE_SERVER_SOURCE"

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
# 先把清单落到变量里再 grep：直接管道给 grep -q 时，grep 命中即退出会给 unzip 一个
# SIGPIPE，pipefail 于是把这次成功的检查判成失败——文件在不在纯看谁先跑完，偶发。
ITCH_ARCHIVE_ENTRIES="$(unzip -Z1 "$ITCH_ARCHIVE_PATH")"
grep -qx 'index.html' <<<"$ITCH_ARCHIVE_ENTRIES" || {
    echo "[WebRelease] itch 上传包缺少根目录 index.html。" >&2
    exit 4
}

ssnoir_cleanup_web_release_history "$OUTPUT_ROOT" "$BUILD_STAMP"

echo "[WebRelease] 构建完成: $OUTPUT_DIR"
echo "[WebRelease] 离线评审包: $ARCHIVE_PATH"
echo "[WebRelease] itch HTML5 上传包: $ITCH_ARCHIVE_PATH"

if [[ "$PUBLISH_TO_ITCH" == true ]]; then
    echo "[WebRelease] 上传 itch.io: $ITCH_TARGET"
    butler push "$OUTPUT_DIR" "$ITCH_TARGET"
    echo "[WebRelease] itch.io 上传完成: https://guopanqi.itch.io/noir"
fi
