#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GAME_DIR="$SCRIPT_DIR/game"
case "$(uname -m)" in
    arm64) SERVER="$GAME_DIR/SSNoirDemoServer-mac-arm64" ;;
    x86_64) SERVER="$GAME_DIR/SSNoirDemoServer-mac-x64" ;;
    *)
        echo "不支持的 Mac 架构: $(uname -m)" >&2
        exit 2
        ;;
esac

xattr -dr com.apple.quarantine "$SCRIPT_DIR" >/dev/null 2>&1 || true
chmod +x "$SERVER" >/dev/null 2>&1 || true

exec "$SERVER" --directory "$GAME_DIR" --open
