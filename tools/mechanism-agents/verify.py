#!/usr/bin/env python3
"""Verify model-generated research in a separate job with NO API credentials.

Syntactic checks, strict import subset, bounded runtime and JSON evidence
contract. Passing this gate is NOT an independent proof of mathematics or UX.
"""
from __future__ import annotations
import argparse
import ast
import json
import os
from pathlib import Path
import resource
import subprocess
import sys
import tempfile

EXPECTED = ("pressure", "route", "information")
REQUIRED = ("hypothesis", "baseline", "counterfactual", "results", "verdict")
ALLOWED_MODULES = {
    "__future__", "collections", "dataclasses", "decimal", "fractions",
    "functools", "itertools", "json", "math", "operator",
    "statistics", "random", "typing",
}
DISALLOWED_CALLS = {
    "open", "exec", "eval", "compile", "__import__", "input", "breakpoint",
    "globals", "locals", "vars", "getattr", "setattr", "delattr",
}


def inspect_source(source: str) -> None:
    if len(source) > 30000:
        raise ValueError("Oversized proposed experiment")
    tree = ast.parse(source, mode="exec")
    if sum(1 for _ in ast.walk(tree)) > 3500:
        raise ValueError("Excessive AST size")
    for n in ast.walk(tree):
        if isinstance(n, ast.Import):
            if any(alias.name.split(".")[0] not in ALLOWED_MODULES for alias in n.names):
                raise ValueError("Unapproved imported module")
        if isinstance(n, ast.ImportFrom):
            if n.level or (n.module or "").split(".")[0] not in ALLOWED_MODULES:
                raise ValueError("Relative or unapproved import")
        if isinstance(n, ast.Call) and isinstance(n.func, ast.Name) and n.func.id in DISALLOWED_CALLS:
            raise ValueError("Unapproved builtin call: " + n.func.id)


def set_resource_limits() -> None:
    resource.setrlimit(resource.RLIMIT_CPU, (20, 22))
    resource.setrlimit(resource.RLIMIT_AS, (512 * 1024 * 1024, 512 * 1024 * 1024))
    resource.setrlimit(resource.RLIMIT_FSIZE, (1024 * 1024, 1024 * 1024))


def execute(source_file: Path) -> dict:
    inspect_source(source_file.read_text(encoding="utf-8"))
    # The secret and repository checkout credentials are intentionally absent
    # from the verifier job AND from the generated-code child environment.
    env = {
        "PATH": "/usr/bin:/bin", "HOME": "/tmp", "LANG": "C.UTF-8",
        "PYTHONNOUSERSITE": "1", "PYTHONHASHSEED": "0",
    }
    with tempfile.TemporaryDirectory(prefix="ssnoir-proof-") as dirname:
        result = subprocess.run(
            [sys.executable, "-I", str(source_file.resolve())],
            cwd=dirname, env=env, capture_output=True,
            timeout=30, text=True, preexec_fn=set_resource_limits,
            check=False,
        )
    if result.returncode:
        raise ValueError("Experiment failed: " + result.stderr[-3000:])
    if len(result.stdout) > 250_000:
        raise ValueError("Experiment generated excessive output")
    obj = json.loads(result.stdout)
    if not isinstance(obj, dict) or any(k not in obj for k in REQUIRED):
        raise ValueError("Experiment needs JSON object containing: " + ", ".join(REQUIRED))
    if not isinstance(obj["results"], (dict, list)):
        raise ValueError("Results must be a structured object or array")
    return obj


def verify(root: Path, out: Path) -> None:
    out.mkdir(parents=True, exist_ok=True)
    rows = []
    failures = []
    for lane in EXPECTED:
        folder = root / ("ssnoir-gpt6-" + lane)
        try:
            meta = json.loads((folder / "metadata.json").read_text(encoding="utf8"))
            report = (folder / "report.md").read_text(encoding="utf8")
            if meta.get("lane") != lane or meta.get("model") != "gpt-6-astra" or meta.get("produced") is not True:
                raise ValueError("Missing genuine-agent metadata")
            if len(report.strip()) < 120:
                raise ValueError("Report is empty or insufficiently specific")
            evidence = execute(folder / "experiment.py")
            (out / (lane + ".json")).write_text(
                json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
            rows.append(f"| {lane} | 执行成功 | 独立生成数学脚本；仍需复核数学与玩法 |")
        except Exception as exc:
            rows.append(f"| {lane} | 失败 | {type(exc).__name__}: {exc} |")
            failures.append(lane)
    summary = (
        "# SSNoir · Three GPT-6 Agent experiments\n\n"
        "3 个独立模型会话在含 API Secret 的 Job 中生成提案与脚本；"
        "本 Job 无 API Secret，仅执行受限脚本。\n\n"
        "| Researcher | Executed | Evidence class |\n"
        "|---|---|---|\n" + "\n".join(rows) + "\n\n"
        "数学脚本通过不等于设计成立、正式 C#/Scheme 因果成立或玩家喜欢；"
        "全部证据都须由协调者审核后才可写入正式研究记忆。\n"
    )
    (out / "verification.md").write_text(summary, encoding="utf-8")
    print(summary)
    if failures:
        raise SystemExit("Independent verification failed for: " + ", ".join(failures))


def self_test() -> None:
    good = 'import json\nprint(json.dumps({"hypothesis":"x","baseline":0,"counterfactual":1,"results":{"gap":1},"verdict":"paper only"}))\n'
    inspect_source(good)
    with tempfile.TemporaryDirectory() as d:
        path = Path(d) / "sample.py"
        path.write_text(good, encoding="utf-8")
        assert execute(path)["results"]["gap"] == 1
    for src in ("import os\n", "from subprocess import run\n", 'open("/tmp/x")\n'):
        try:
            inspect_source(src)
        except ValueError:
            continue
        raise AssertionError("unsafe import/call not rejected")
    print("secretless verifier self-test passed")


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--root", type=Path)
    p.add_argument("--out", type=Path)
    p.add_argument("--self-test", action="store_true")
    a = p.parse_args()
    if a.self_test:
        self_test()
    else:
        if a.root is None or a.out is None:
            p.error("verification requires --root and --out")
        verify(a.root, a.out)


if __name__ == "__main__":
    main()
