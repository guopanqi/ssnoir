#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
source "$SCRIPT_DIR/common.sh"

CLEAN_BUILDS=0
CLEAN_CACHES=0
CLEAN_OLD_RELEASES=0

usage() {
    cat <<'EOF'
用法: ./tools/build/clean.sh [选项]

选项:
  --old-releases  清理 Web Release 和 TapTap Release 的旧版本，保留各自最新版本
  --builds  清理 UnityClient/Build 下的 Web Preview、Web Release、TapTap Release 和 CDN 产物
  --cache   清理 .cache/build 和旧版 .cache/taptap-build 构建缓存
  --all     同时清理构建产物和构建缓存
  -h, --help  显示帮助

不会清理 .cache/saves、Unity 工程资源或 Git 历史。
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --old-releases)
            CLEAN_OLD_RELEASES=1
            shift
            ;;
        --builds)
            CLEAN_BUILDS=1
            shift
            ;;
        --cache|--caches)
            CLEAN_CACHES=1
            shift
            ;;
        --all)
            CLEAN_BUILDS=1
            CLEAN_CACHES=1
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

if [[ "$CLEAN_BUILDS" == 0 && "$CLEAN_CACHES" == 0 && "$CLEAN_OLD_RELEASES" == 0 ]]; then
    usage >&2
    exit 2
fi

remove_tree() {
    local target="$1"
    [[ -e "$target" ]] || return 0
    echo "[Clean] 删除: $target"
    find "$target" -depth -delete
}

latest_release_stamp() {
    local output_root="$1"
    [[ -d "$output_root" ]] || return 0

    find "$output_root" -mindepth 1 -maxdepth 1 -type d -print \
        | while IFS= read -r candidate; do
            local name="${candidate##*/}"
            if [[ "$name" =~ ^[0-9]{8}-[0-9]{6}$ ]]; then
                echo "$name"
            fi
        done \
        | sort \
        | tail -n 1
}

if [[ "$CLEAN_OLD_RELEASES" == 1 ]]; then
    WEB_RELEASE_ROOT="$REPO_ROOT/UnityClient/Build/WebRelease"
    TAPTAP_RELEASE_ROOT="$REPO_ROOT/UnityClient/Build/TapTapRelease"

    WEB_LATEST_STAMP="$(latest_release_stamp "$WEB_RELEASE_ROOT")"
    if [[ -n "$WEB_LATEST_STAMP" ]]; then
        ssnoir_cleanup_web_release_history "$WEB_RELEASE_ROOT" "$WEB_LATEST_STAMP"
    fi

    TAPTAP_LATEST_STAMP="$(latest_release_stamp "$TAPTAP_RELEASE_ROOT")"
    if [[ -n "$TAPTAP_LATEST_STAMP" ]]; then
        ssnoir_cleanup_timestamped_releases \
            "$TAPTAP_RELEASE_ROOT" "$TAPTAP_RELEASE_ROOT/$TAPTAP_LATEST_STAMP"
    fi
fi

if [[ "$CLEAN_BUILDS" == 1 ]]; then
    remove_tree "$REPO_ROOT/UnityClient/Build"
fi

if [[ "$CLEAN_CACHES" == 1 ]]; then
    remove_tree "$REPO_ROOT/.cache/build"
    remove_tree "$REPO_ROOT/.cache/taptap-build"
fi

echo "[Clean] 完成。"
