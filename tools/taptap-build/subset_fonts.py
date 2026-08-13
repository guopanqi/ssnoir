#!/usr/bin/env python3

import argparse
import json
import re
import shutil
import subprocess
from pathlib import Path


FONT_FILES = (
    "SourceHanSerifCN-Regular.ttf",
    "SourceHanSerifCN-SemiBold.ttf",
)
TEXT_EXTENSIONS = {".scm", ".cs", ".unity", ".prefab", ".asset", ".json", ".txt", ".uxml"}
BASE_CHARACTERS = "".join(chr(codepoint) for codepoint in range(0x20, 0x7F)) + (
    "　，。！？；：、“”‘’（）《》〈〉【】—…·￥"
)
UNICODE_ESCAPE = re.compile(r"\\u([0-9a-fA-F]{4})|\\U([0-9a-fA-F]{8})")


def iter_text_files(repo_root: Path):
    roots = (
        repo_root / "Content",
        repo_root / "UnityClient" / "Assets" / "Scripts",
        repo_root / "UnityClient" / "Assets" / "Scenes",
        repo_root / "UnityClient" / "Assets" / "Resources",
    )
    ignored_parts = {"Fonts", "videos", "StreamingAssets"}
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if not path.is_file() or path.suffix.lower() not in TEXT_EXTENSIONS:
                continue
            if any(part in ignored_parts for part in path.parts):
                continue
            yield path


def escaped_codepoints(text: str) -> set[int]:
    raw = []
    for match in UNICODE_ESCAPE.finditer(text):
        raw.append(int(match.group(1) or match.group(2), 16))

    result: set[int] = set()
    index = 0
    while index < len(raw):
        value = raw[index]
        if 0xD800 <= value <= 0xDBFF and index + 1 < len(raw):
            low = raw[index + 1]
            if 0xDC00 <= low <= 0xDFFF:
                result.add(0x10000 + ((value - 0xD800) << 10) + (low - 0xDC00))
                index += 2
                continue
        if not 0xD800 <= value <= 0xDFFF:
            result.add(value)
        index += 1
    return result


def collect_codepoints(repo_root: Path) -> tuple[set[int], list[str]]:
    codepoints = {ord(character) for character in BASE_CHARACTERS}
    scanned_files = []
    for path in iter_text_files(repo_root):
        text = path.read_text(encoding="utf-8", errors="ignore")
        codepoints.update(ord(character) for character in text if not character.isspace())
        codepoints.update(escaped_codepoints(text))
        scanned_files.append(str(path.relative_to(repo_root)))
    return codepoints, scanned_files


def font_codepoints(hb_info: str, font_path: Path) -> set[int]:
    result = subprocess.run(
        [hb_info, "-q", "--list-unicodes", str(font_path)],
        check=True,
        capture_output=True,
        text=True,
    )
    codepoints: set[int] = set()
    for line in result.stdout.splitlines():
        unicode_field = line.split("\t", 1)[0]
        codepoints.update(
            int(match.group(1), 16)
            for match in re.finditer(r"U\+([0-9A-Fa-f]+)", unicode_field)
        )
    return codepoints


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()

    repo_root = args.repo_root.resolve()
    output_dir = args.output_dir.resolve()
    font_source_dir = repo_root / "Content" / "assets" / "fonts"
    hb_subset = shutil.which("hb-subset")
    hb_info = shutil.which("hb-info")
    if hb_subset is None or hb_info is None:
        raise SystemExit("缺少 HarfBuzz 命令行工具，请先执行: brew install harfbuzz")

    output_dir.mkdir(parents=True, exist_ok=True)
    codepoints, scanned_files = collect_codepoints(repo_root)
    report = {
        "scannedFileCount": len(scanned_files),
        "requestedCodepointCount": len(codepoints),
        "fonts": [],
    }

    missing_across_fonts: set[int] = set()
    for font_name in FONT_FILES:
        source_path = font_source_dir / font_name
        output_path = output_dir / font_name
        if not source_path.is_file():
            raise SystemExit(f"找不到源字体: {source_path}")

        supported = font_codepoints(hb_info, source_path)
        included = codepoints & supported
        missing = codepoints - supported
        missing_across_fonts.update(missing)
        text_path = output_dir / f"{font_name}.characters.txt"
        text_path.write_text("".join(chr(value) for value in sorted(included)), encoding="utf-8")

        subprocess.run(
            [
                hb_subset,
                str(source_path),
                f"--text-file={text_path}",
                f"--output-file={output_path}",
                "--name-IDs=*",
                "--name-languages=*",
                "--layout-features=*",
            ],
            check=True,
        )

        report["fonts"].append(
            {
                "name": font_name,
                "sourceBytes": source_path.stat().st_size,
                "subsetBytes": output_path.stat().st_size,
                "includedCodepointCount": len(included),
                "missingCodepointCount": len(missing),
            }
        )

    report["missingCodepoints"] = [f"U+{value:04X}" for value in sorted(missing_across_fonts)]
    (output_dir / "font-report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(
        f"扫描 {len(scanned_files)} 个文本文件，收集 {len(codepoints)} 个字符；"
        f"生成 {len(FONT_FILES)} 个字体子集。"
    )
    if missing_across_fonts:
        print(f"提示：源字体本身不包含 {len(missing_across_fonts)} 个扫描字符，详见 font-report.json。")


if __name__ == "__main__":
    main()
