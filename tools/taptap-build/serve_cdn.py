#!/usr/bin/env python3

import argparse
import errno
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class CdnHandler(SimpleHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def end_headers(self) -> None:
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Range, Content-Type")
        self.send_header("Access-Control-Expose-Headers", "Content-Length, Content-Range")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Accept-Ranges", "bytes")
        super().end_headers()

    def do_OPTIONS(self) -> None:
        self.send_response(204)
        self.send_header("Content-Length", "0")
        self.end_headers()


def main() -> None:
    parser = argparse.ArgumentParser(description="Serve temporary TapTap CDN assets.")
    parser.add_argument("--directory", required=True, type=Path)
    parser.add_argument("--port", required=True, type=int)
    args = parser.parse_args()

    directory = args.directory.resolve()
    directory.mkdir(parents=True, exist_ok=True)
    handler = partial(CdnHandler, directory=str(directory))
    try:
        server = ThreadingHTTPServer(("127.0.0.1", args.port), handler)
    except OSError as exception:
        if exception.errno == errno.EADDRINUSE:
            raise SystemExit(f"CDN 测试端口 {args.port} 已被占用。") from None
        raise
    server.daemon_threads = True
    print(f"[TapTapCdn] local=http://127.0.0.1:{args.port}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
