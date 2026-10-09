#!/usr/bin/env python3
"""Validate version-pinned researcher replies without touching player feedback."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[3]
BASE=ROOT/"docs/实验/机制研究/反馈"
items=sorted((BASE/"reviews").glob("*.json"))
for path in items:
    assert path.stat().st_size < 40000, path
    data=json.loads(path.read_text(encoding="utf8"))
    assert data["schema"]=="ssnoir.mechanism-review/v1", path
    assert data["sourcePath"]=="docs/实验/机制研究/反馈/inbox/"+path.name, path
    assert len(data["feedbackSha"])==40, path
    assert isinstance(data["observations"],list) and data["observations"], path
    for key in ("headline","acknowledgment","interpretation","question","next"):
        assert isinstance(data.get(key),str) and data[key].strip(), (path,key)
    assert (BASE/"inbox"/path.name).is_file(), path
print(f"PASS: {len(items)} review replies linked to human feedback files")
