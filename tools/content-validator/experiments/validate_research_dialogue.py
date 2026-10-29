#!/usr/bin/env python3
"""Validate asynchronous designer answers independently from Rxx playtest feedback."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
BASE = ROOT / "experiments/抽象决策关系研究"
REQUESTS = BASE / "研究沟通.json"
INBOX = BASE / "反馈/questions"
FILENAME = re.compile(r"SSNoir-dialogue-(RQ-[A-Z0-9-]+)-[0-9]{8}-[0-9]{6}-[a-f0-9]{12}\.json$")


def main():
    requests = json.loads(REQUESTS.read_text(encoding="utf-8"))
    assert requests["schema"] == "ssnoir.research-requests/v1"
    questions = {q["id"]: q for q in requests["questions"]}
    assert len(questions) == len(requests["questions"])
    checked = 0
    for path in sorted(INBOX.glob("*.json")):
        assert path.stat().st_size <= 200 * 1024, str(path)
        match = FILENAME.fullmatch(path.name)
        assert match, f"Invalid filename: {path.name}"
        data = json.loads(path.read_text(encoding="utf-8"))
        # Past replies remain valid when the question is later revised or retired.
        # The client snapshots the question text and version on submission.
        assert data.get("schema") == "ssnoir.research-dialogue/v1"
        assert data.get("questionId") == match.group(1)
        assert isinstance(data.get("questionTitle"), str) and data["questionTitle"].strip()
        assert isinstance(data.get("questionVersion"), str) and data["questionVersion"]
        assert isinstance(data.get("related"), list) and all(isinstance(x, str) for x in data["related"])
        answer = data.get("answer")
        assert isinstance(answer, str) and 0 < len(answer.strip()) <= 12000
        assert isinstance(data.get("context"), str) and data["context"].strip()
        assert isinstance(data.get("createdAt"), str) and "T" in data["createdAt"]
        # Answer is human-authored public material, never a model quality verdict.
        # Do not rewrite, rate, or modify the submitted content.
        checked += 1
    print(f"PASS: {len(questions)} questions defined, {checked} independent designer replies valid")


if __name__ == "__main__":
    main()
