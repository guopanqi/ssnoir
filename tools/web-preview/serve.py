#!/usr/bin/env python3

import argparse
import errno
import os
import re
import socket
import subprocess
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class PreviewHandler(SimpleHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def end_headers(self) -> None:
        self.send_header("Cache-Control", "no-store, no-cache, must-revalidate")
        self.send_header("Pragma", "no-cache")
        self.send_header("Accept-Ranges", "bytes")
        super().end_headers()

    def guess_type(self, path: str) -> str:
        if path.endswith(".wasm"):
            return "application/wasm"
        return super().guess_type(path)

    def send_head(self):
        range_header = self.headers.get("Range")
        if not range_header:
            return super().send_head()

        path = self.translate_path(self.path)
        if os.path.isdir(path):
            return super().send_head()

        try:
            file_size = os.path.getsize(path)
            match = re.fullmatch(r"bytes=(\d*)-(\d*)", range_header.strip())
            if not match:
                self.send_error(416, "Invalid byte range")
                return None

            start_text, end_text = match.groups()
            if start_text:
                start = int(start_text)
                end = int(end_text) if end_text else file_size - 1
            else:
                suffix_length = int(end_text)
                start = max(0, file_size - suffix_length)
                end = file_size - 1

            if start >= file_size or start > end:
                self.send_error(416, "Requested range not satisfiable")
                return None
            end = min(end, file_size - 1)

            file = open(path, "rb")
            self.send_response(206)
            self.send_header("Content-Type", self.guess_type(path))
            self.send_header("Content-Range", f"bytes {start}-{end}/{file_size}")
            self.send_header("Content-Length", str(end - start + 1))
            self.send_header("Last-Modified", self.date_time_string(os.path.getmtime(path)))
            self.end_headers()
            file.seek(start)
            self._remaining_bytes = end - start + 1
            return file
        except OSError:
            self.send_error(404, "File not found")
            return None

    def copyfile(self, source, outputfile) -> None:
        remaining = getattr(self, "_remaining_bytes", None)
        if remaining is None:
            try:
                return super().copyfile(source, outputfile)
            except (BrokenPipeError, ConnectionResetError):
                return

        try:
            while remaining > 0:
                chunk = source.read(min(64 * 1024, remaining))
                if not chunk:
                    break
                outputfile.write(chunk)
                remaining -= len(chunk)
        except (BrokenPipeError, ConnectionResetError):
            # 浏览器切换视频分段时会主动取消旧请求，不需要打印异常堆栈。
            return


def discover_lan_ip() -> str | None:
    # macOS 上 VPN/代理常把默认路由指向 198.18.x.x；手机实际需要 Wi-Fi/有线网卡地址。
    for interface in ("en0", "en1", "en2", "en3", "en4", "en5"):
        try:
            result = subprocess.run(
                ("ipconfig", "getifaddr", interface),
                check=False,
                capture_output=True,
                text=True,
            )
        except FileNotFoundError:
            break
        address = result.stdout.strip()
        if address.startswith(("10.", "192.168.", "172.")):
            return address

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        sock.connect(("8.8.8.8", 80))
        address = sock.getsockname()[0]
        return None if address.startswith("127.") else address
    except OSError:
        return None
    finally:
        sock.close()


def main() -> None:
    parser = argparse.ArgumentParser(description="Serve an SSNoir WebGL preview on the local network.")
    parser.add_argument("--directory", required=True, type=Path)
    parser.add_argument("--port", required=True, type=int)
    args = parser.parse_args()

    directory = args.directory.resolve()
    if not (directory / "index.html").is_file():
        raise SystemExit(f"Missing WebGL index: {directory / 'index.html'}")

    handler = partial(PreviewHandler, directory=str(directory))
    try:
        server = ThreadingHTTPServer(("0.0.0.0", args.port), handler)
    except OSError as exception:
        if exception.errno == errno.EADDRINUSE:
            raise SystemExit(
                f"端口 {args.port} 已被占用。可能已有预览服务器正在运行；"
                f"可直接访问它，或改用 --port {args.port + 1}。"
            ) from None
        raise
    server.daemon_threads = True

    lan_ip = discover_lan_ip()
    print("[WebPreview] 服务器已启动，按 Ctrl-C 停止。", flush=True)
    print(f"[WebPreview] 本机: http://127.0.0.1:{args.port}", flush=True)
    if lan_ip:
        print(f"[WebPreview] 手机: http://{lan_ip}:{args.port}", flush=True)
    else:
        print("[WebPreview] 未自动识别局域网 IP，请在系统网络设置中查看本机 IP。", flush=True)

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n[WebPreview] 服务器已停止。", flush=True)
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
