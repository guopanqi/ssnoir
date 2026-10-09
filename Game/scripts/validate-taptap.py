"""Validate the actual official converter artifact, not an independently repacked ZIP."""
import json
import sys
import zipfile
from pathlib import Path

root = Path(sys.argv[1]).resolve()
zip_path = root / "game.zip"
if not zip_path.is_file():
    raise RuntimeError("Converter did not produce game.zip")
if zip_path.stat().st_size >= 20_000_000:
    raise RuntimeError("TapTap release package reached the published 20MB limit")

with zipfile.ZipFile(zip_path) as archive:
    if archive.testzip() is not None:
        raise RuntimeError("Corrupt ZIP entry")
    names = set(archive.namelist())
    required = {"game.js", "game.json", "check-version.js"}
    if not required.issubset(names):
        raise RuntimeError(f"Missing converted files: {required - names}")
    manifest = json.loads(archive.read("game.json"))
    if manifest.get("convertScriptVersion") != "2.0.5":
        raise RuntimeError("Unexpected converter version")
    if manifest.get("deviceOrientation") != "landscape":
        raise RuntimeError("Converted orientation changed")
    code = archive.read("game.js")
    disk_code = (root / "game" / "game.js").read_bytes()
    if code != disk_code:
        raise RuntimeError("ZIP entry differs from converted disk file")
    if b"GameGlobal.fetch" not in code:
        raise RuntimeError("Original converter runtime injection missing")
    if b"__SSNOIR_WECHAT_FOUNDATION__" not in code:
        raise RuntimeError("SSNoir entry point missing after Babel")
    print(f"PASS: official TapTap 2.0.5 ZIP validated ({zip_path.stat().st_size} bytes, {len(names)} files)")
