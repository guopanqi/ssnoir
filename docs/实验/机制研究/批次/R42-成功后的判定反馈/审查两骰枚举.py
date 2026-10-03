"""R42 独立两骰纸面核查；不导入协调者模型，不调用正式引擎。"""

from fractions import Fraction
from hashlib import sha256
import json
from pathlib import Path


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
SOURCE = ROOT / "Engine/Runtime/Core/FateStrip.cs"
# 人工核对 FateStrip.DistributionFor 后冻结；索引为放入骰 + 技能。
COUNTS = {
    1: (3, 3, 0), 2: (2, 3, 1), 3: (1, 3, 2),
    4: (1, 2, 3), 5: (0, 2, 4), 6: (0, 1, 5), 7: (0, 0, 6),
}


def outcomes(die, skill):
    bad, middle, good = COUNTS[die + skill]
    assert bad + middle + good == 6
    return (0,) * bad + (1,) * middle + (2,) * good


def branches(first, second, remaining, alarm):
    result = []
    for f1, x in enumerate(outcomes(first, 0 if alarm else 1), 1):
        next_alarm = bool(alarm or x == 2)
        for f2, y in enumerate(outcomes(second, 0 if next_alarm else 1), 1):
            # 到目标立即成功：后一个结果仅作未消费随机源的耦合枚举。
            result.append({
                "fates": [f1, f2], "gains": [x, y],
                "success": x >= remaining or x + y >= remaining,
                "finish_on_first": x >= remaining,
                "alarm_after_first": next_alarm,
            })
    assert len(result) == 36
    return result


def value(first, second, remaining, alarm):
    return Fraction(sum(b["success"] for b in branches(first, second, remaining, alarm)), 36)


def main():
    comparisons = []
    counts = {}
    for alarm in (0, 1):
        for remaining in range(1, 5):
            tally = {"low_first": 0, "high_first": 0, "tie": 0}
            for low in range(1, 7):
                for high in range(low, 7):
                    a = value(low, high, remaining, alarm)
                    b = value(high, low, remaining, alarm)
                    kind = "low_first" if a > b else "high_first" if a < b else "tie"
                    tally[kind] += 1
                    if alarm or remaining in (1, 2):
                        assert a == b
                    if not alarm and remaining == 4 and low < high:
                        assert a > b
                    comparisons.append({
                        "alarm": alarm, "remaining": remaining, "dice": [low, high],
                        "low_first": str(a), "high_first": str(b),
                        "low_minus_high": str(a - b), "preferred": kind,
                    })
            counts[f"alarm={alarm},remaining={remaining}"] = tally
    assert value(3, 4, 3, 0) == Fraction(23, 36)
    assert value(4, 3, 3, 0) == Fraction(13, 18)
    assert value(3, 4, 4, 0) == Fraction(1, 4)
    assert value(4, 3, 4, 0) == Fraction(2, 9)
    witnesses = [{
        "remaining": remaining, "order": [first, second],
        "value": str(value(first, second, remaining, 0)),
        "branches": branches(first, second, remaining, 0),
    } for remaining in (3, 4) for first, second in ((3, 4), (4, 3))]
    output = {
        "identity": "独立两骰数学枚举；原生0、真人0；非全局四骰重算",
        "fate_source": str(SOURCE.relative_to(ROOT)),
        "fate_source_sha256": sha256(SOURCE.read_bytes()).hexdigest(),
        "script_sha256": sha256(Path(__file__).read_bytes()).hexdigest(),
        "parameters": {"target": 4, "skill_before_alarm": 1, "skill_after_alarm": 0,
                       "fast_gains": [0, 1, 2], "alarm_trigger": "first good, once"},
        "comparison_count": len(comparisons),
        "enumerated_fate_pairs": len(comparisons) * 2 * 36,
        "counts_including_equal_dice": counts,
        "comparisons": comparisons,
        "witnesses": witnesses,
    }
    (HERE / "审查两骰结果.json").write_text(
        json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: output[k] for k in
                      ("identity", "comparison_count", "enumerated_fate_pairs", "counts_including_equal_dice")},
                     ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
