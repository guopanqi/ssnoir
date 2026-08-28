#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
case "$(uname -m)" in
    arm64) SERVER="$SCRIPT_DIR/SSNoirDemoServer-mac-arm64" ;;
    x86_64) SERVER="$SCRIPT_DIR/SSNoirDemoServer-mac-x64" ;;
    *)
        echo "不支持的 Mac 架构: $(uname -m)" >&2
        exit 2
        ;;
esac

exec "$SERVER" --open
