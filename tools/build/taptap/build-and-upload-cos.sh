#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
COSCLI_BIN="${COSCLI_PATH:-}"
COS_CONFIG="${COS_CONFIG:-$HOME/.cos.yaml}"
BUCKET=""
REGION=""
OBJECT_PREFIX="ssnoir/taptap"
CDN_URL=""

usage() {
    echo "用法: $0 --bucket <bucket-appid> --region <ap-region> --cdn-url <https-url> [--prefix <object-prefix>]"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --bucket)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            BUCKET="$2"
            shift 2
            ;;
        --region)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            REGION="$2"
            shift 2
            ;;
        --prefix)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            OBJECT_PREFIX="${2#/}"
            OBJECT_PREFIX="${OBJECT_PREFIX%/}"
            shift 2
            ;;
        --cdn-url)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            CDN_URL="${2%/}"
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

[[ "$BUCKET" =~ ^[a-z0-9][a-z0-9-]*-[0-9]+$ ]] || {
    echo "Bucket 必须是完整名称，例如 ssnoir-1250000000。" >&2
    exit 2
}
[[ "$REGION" =~ ^ap-[a-z0-9-]+$ ]] || {
    echo "地域格式不正确，例如 ap-guangzhou。" >&2
    exit 2
}
[[ -n "$OBJECT_PREFIX" && "$OBJECT_PREFIX" != *".."* && "$OBJECT_PREFIX" != *"//"* ]] || {
    echo "COS 对象前缀不正确: $OBJECT_PREFIX" >&2
    exit 2
}
[[ "$CDN_URL" == https://* ]] || {
    echo "COS CDN 地址必须使用 HTTPS: $CDN_URL" >&2
    exit 2
}

if [[ -z "$COSCLI_BIN" ]]; then
    if command -v coscli >/dev/null 2>&1; then
        COSCLI_BIN="$(command -v coscli)"
    elif [[ -x "$HOME/.local/bin/coscli" ]]; then
        COSCLI_BIN="$HOME/.local/bin/coscli"
    else
        echo "找不到 COSCLI。" >&2
        exit 2
    fi
fi
[[ -f "$COS_CONFIG" ]] || {
    echo "COSCLI 还没有配置凭据: $COS_CONFIG" >&2
    echo "请先执行: $COSCLI_BIN config init" >&2
    exit 3
}

echo "[TapTapCOS] 验证 Bucket 访问权限..."
if ! "$COSCLI_BIN" ls "cos://$BUCKET/" --limit 1 \
    --config-path "$COS_CONFIG" --disable-log >/dev/null; then
    echo "COSCLI 无法访问 Bucket。请检查 Secret ID、Secret Key 和 Bucket 授权。" >&2
    exit 3
fi

CDN_URL="$CDN_URL/$OBJECT_PREFIX"
echo "[TapTapCOS] CDN 地址: $CDN_URL"
"$SCRIPT_DIR/build.sh" --cdn-url "$CDN_URL"

CDN_DIRECTORY="$REPO_ROOT/UnityClient/Build/TapTapCdn"
DATA_FILE=""
DATA_FILE_COUNT=0
while IFS= read -r -d '' CANDIDATE; do
    DATA_FILE="$CANDIDATE"
    DATA_FILE_COUNT=$((DATA_FILE_COUNT + 1))
done < <(find "$CDN_DIRECTORY" -maxdepth 1 -type f \
    -name '*.webgl.data.unityweb.bin*' -print0)
[[ "$DATA_FILE_COUNT" -eq 1 ]] || {
    echo "本地 CDN 目录应只有 1 个 Data 文件，实际为 $DATA_FILE_COUNT 个。" >&2
    exit 4
}
VIDEO_FILE_COUNT="$(find "$CDN_DIRECTORY/Cutscenes" -maxdepth 1 -type f -name '*.mp4' 2>/dev/null | wc -l | tr -d ' ')"
[[ "$VIDEO_FILE_COUNT" -gt 0 ]] || {
    echo "TapTap 正式发布必须包含远程过场视频，当前为 0 个。" >&2
    exit 4
}

DATA_NAME="$(basename "$DATA_FILE")"
OBJECT_URL="$CDN_URL/$DATA_NAME"

echo "[TapTapCOS] 上传并验证 Data 与 $VIDEO_FILE_COUNT 个远程视频..."
ASSET_COUNT=0
while IFS= read -r -d '' LOCAL_FILE; do
    RELATIVE_PATH="${LOCAL_FILE#"$CDN_DIRECTORY/"}"
    ENCODED_PATH="$(python3 -c 'import sys, urllib.parse; print(urllib.parse.quote(sys.argv[1], safe="/"))' "$RELATIVE_PATH")"
    REMOTE_URL="$CDN_URL/$ENCODED_PATH"
    CONTENT_TYPE="application/octet-stream"
    [[ "$RELATIVE_PATH" != *.mp4 ]] || CONTENT_TYPE="video/mp4"

    echo "[TapTapCOS] 上传: cos://$BUCKET/$OBJECT_PREFIX/$RELATIVE_PATH"
    "$COSCLI_BIN" cp "$LOCAL_FILE" "cos://$BUCKET/$OBJECT_PREFIX/$RELATIVE_PATH" \
        --acl public-read \
        --meta "Cache-Control:public,max-age=31536000,immutable#Content-Type:$CONTENT_TYPE" \
        --config-path "$COS_CONFIG" --disable-log

    HEADERS="$(curl -fsSI --max-time 15 \
        -H 'Origin: https://taptap.cn' "$REMOTE_URL")"
    echo "$HEADERS" | rg -qi '^HTTP/[^ ]+ 200'
    echo "$HEADERS" | rg -qi '^access-control-allow-origin:[[:space:]]*(\*|https://taptap\.cn)'
    echo "$HEADERS" | rg -qi "^content-type:[[:space:]]*$CONTENT_TYPE"
    REMOTE_BYTES="$(echo "$HEADERS" \
        | sed -nE 's/^[Cc]ontent-[Ll]ength:[[:space:]]*([0-9]+)\r?$/\1/p' \
        | tail -n 1)"
    LOCAL_BYTES="$(stat -f '%z' "$LOCAL_FILE")"
    [[ "$REMOTE_BYTES" == "$LOCAL_BYTES" ]] || {
        echo "COS 对象大小不一致: $RELATIVE_PATH local=$LOCAL_BYTES remote=$REMOTE_BYTES" >&2
        exit 4
    }
    ASSET_COUNT=$((ASSET_COUNT + 1))
done < <(find "$CDN_DIRECTORY" -type f -print0)

[[ "$ASSET_COUNT" -ge $((VIDEO_FILE_COUNT + 1)) ]] || {
    echo "远程资源数量不完整: total=$ASSET_COUNT videos=$VIDEO_FILE_COUNT" >&2
    exit 4
}

set +e
SPEED_RESULT="$(curl -L --max-time 30 -o /dev/null -sS \
    -w 'http=%{http_code} bytes=%{size_download} speed=%{speed_download} time=%{time_total}' \
    "$OBJECT_URL" 2>&1)"
SPEED_EXIT=$?
set -e
echo "[TapTapCOS] 测速: exit=$SPEED_EXIT $SPEED_RESULT"

LATEST_OUTPUT="$(find "$REPO_ROOT/UnityClient/Build/TapTapRelease" \
    -mindepth 1 -maxdepth 1 -type d -print | sort | tail -n 1)"
echo "[TapTapCOS] 构建包: $LATEST_OUTPUT"
echo "[TapTapCOS] 远程 Data: $OBJECT_URL"
echo "[TapTapCOS] 远程视频: $CDN_URL/Cutscenes/ ($VIDEO_FILE_COUNT 个)"
