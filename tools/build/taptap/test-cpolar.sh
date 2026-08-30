#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
CDN_DIRECTORY="$REPO_ROOT/UnityClient/Build/TapTapCdn"
CDN_PORT="${SSNOIR_CPOLAR_PORT:-18081}"
CPOLAR_BIN="${CPOLAR_PATH:-}"
CPOLAR_CONFIG="${CPOLAR_CONFIG:-$HOME/.cpolar/cpolar.yml}"

if [[ -z "$CPOLAR_BIN" ]]; then
    if command -v cpolar >/dev/null 2>&1; then
        CPOLAR_BIN="$(command -v cpolar)"
    elif [[ -x "$HOME/.local/bin/cpolar" ]]; then
        CPOLAR_BIN="$HOME/.local/bin/cpolar"
    else
        echo "找不到 cpolar CLI。请先安装 cpolar。" >&2
        exit 2
    fi
fi

if [[ ! -f "$CPOLAR_CONFIG" ]] \
    || ! rg -q '^authtoken:[[:space:]]*[^[:space:]]+' "$CPOLAR_CONFIG"; then
    echo "cpolar 还没有配置账号 token。" >&2
    echo "登录 https://dashboard.cpolar.com/auth 后执行: $CPOLAR_BIN authtoken <token>" >&2
    exit 3
fi

mkdir -p "$CDN_DIRECTORY"
SERVER_LOG="$(mktemp -t ssnoir-cdn-server).log"
CPOLAR_LOG="$(mktemp -t ssnoir-cpolar).log"
SERVER_PID=""
CPOLAR_PID=""

cleanup() {
    [[ -z "$CPOLAR_PID" ]] || kill "$CPOLAR_PID" 2>/dev/null || true
    [[ -z "$SERVER_PID" ]] || kill "$SERVER_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

python3 "$SCRIPT_DIR/serve_cdn.py" \
    --directory "$CDN_DIRECTORY" \
    --port "$CDN_PORT" >"$SERVER_LOG" 2>&1 &
SERVER_PID=$!

env -u http_proxy -u https_proxy -u all_proxy \
    "$CPOLAR_BIN" http "$CDN_PORT" -region=cn \
    -log="$CPOLAR_LOG" -inspect-addr=127.0.0.1:4040 &
CPOLAR_PID=$!

CPOLAR_URL=""
for _ in {1..60}; do
    if ! kill -0 "$CPOLAR_PID" 2>/dev/null; then
        echo "cpolar 启动失败:" >&2
        tail -n 30 "$CPOLAR_LOG" >&2 || true
        exit 4
    fi
    CPOLAR_URL="$(rg 'Tunnel established at https://' "$CPOLAR_LOG" 2>/dev/null \
        | tail -n 1 \
        | sed -E 's/.*Tunnel established at (https:\/\/[^"[:space:]]+).*/\1/' \
        || true)"
    [[ -z "$CPOLAR_URL" ]] || break
    sleep 1
done

if [[ -z "$CPOLAR_URL" ]]; then
    echo "cpolar 未在 60 秒内生成 HTTPS 隧道。" >&2
    tail -n 30 "$CPOLAR_LOG" >&2 || true
    exit 4
fi

echo "[TapTapCdn] cpolar: $CPOLAR_URL"
"$SCRIPT_DIR/build.sh" --cdn-url "$CPOLAR_URL"

echo "[TapTapCdn] 远程 Data 目录: $CDN_DIRECTORY"
echo "[TapTapCdn] 隧道保持运行。上传刚生成的 game.zip 和 game_wasm_split.zip 测试，完成后按 Ctrl-C。"
wait "$CPOLAR_PID"
