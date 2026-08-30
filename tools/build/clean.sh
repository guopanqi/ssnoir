#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

CLEAN_BUILDS=0
CLEAN_CACHES=0

usage() {
    cat <<'EOF'
用法: ./tools/build/clean.sh [选项]

选项:
  --builds  清理 UnityClient/Build 下的 Web Preview、Web Release、TapTap Release 和 CDN 产物
  --cache   清理 .cache/build 和旧版 .cache/taptap-build 构建缓存
  --all     同时清理构建产物和构建缓存
  -h, --help  显示帮助

不会清理 .cache/saves、Unity 工程资源或 Git 历史。
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
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

if [[ "$CLEAN_BUILDS" == 0 && "$CLEAN_CACHES" == 0 ]]; then
    usage >&2
    exit 2
fi

remove_tree() {
    local target="$1"
    [[ -e "$target" ]] || return 0
    echo "[Clean] 删除: $target"
    find "$target" -depth -delete
}

if [[ "$CLEAN_BUILDS" == 1 ]]; then
    remove_tree "$REPO_ROOT/UnityClient/Build"
fi

if [[ "$CLEAN_CACHES" == 1 ]]; then
    remove_tree "$REPO_ROOT/.cache/build"
    remove_tree "$REPO_ROOT/.cache/taptap-build"
fi

echo "[Clean] 完成。"
