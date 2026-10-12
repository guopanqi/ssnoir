#!/usr/bin/env python3
"""S3-AA source contract: record the existing companion/solo timing mismatch.

This is a static source audit, NOT a Unity session or a fix of the contradiction.
The separate day10 native workflow job executes and strictly replays the real action.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
WORLD = ROOT / "UnityClient/Assets/Resources/Content/scenes/world/三封信.scm"
STAGE = ROOT / "UnityClient/Assets/Resources/Content/scripts/theatre/路灯下.scm"

def between(source, opening, following):
    begin = source.index(opening)
    end = source.index(following, begin + len(opening))
    return source[begin:end]

def check(ok, message):
    if not ok:
        raise AssertionError(message)

def audit():
    world = WORLD.read_text(encoding="utf-8")
    stage = STAGE.read_text(encoding="utf-8")
    solo = between(world, "(define (node-search-for-frank)", "(define (finish-bridge-identity!)")
    reveal = between(world, "(define (finish-bridge-identity!)", "(define (finish-bridge-inquiry!)")
    aftermath = between(world, "(define (close-warren-with-her!)", "(define (settle-beat2!)")
    end_turn = between(world, '(define-turn-rule "离开老街后谈起莱恩"',
                       '(define-turn-rule "她在巷口等你"')
    findings = {
        "solo_card_says_companion_gone":
            ':subtitle "她已经走了，这一趟只有你自己"' in solo,
        "solo_card_requires_generic_die_not_neil":
            ":requires (list (req-die))" in solo and "req-actor" not in solo,
        "solo_unlocks_immediately_after_west_reveal":
            "(set! bridge-identified? #t)" in reveal
            and "(if (and bridge-identified? (not union-checked?))" in world,
        "identity_reveal_does_not_dismiss_companion":
            "dismiss-companion!" not in reveal,
        "dialogue_precedes_dismissal":
            aftermath.index("(bridge-aftermath-dialogue!)") <
            aftermath.index("(dismiss-companion! '夜莺)"),
        "end_turn_rule_invokes_dismissal_path":
            "(close-warren-with-her!)" in end_turn,
        "stage_has_both_characters_and_lyon_confession":
            '(theatre-image "夜莺"' in stage
            and '(theatre-image "尼尔"' in stage
            and "之前的男朋友" in stage,
    }
    for name, passed in findings.items():
        check(passed, "S3-AA source contract changed: " + name)
    return {
        "schema": "ssnoir.s3-aa.source-companion-contract/v1",
        "status": "known_inconsistency_not_fixed",
        "basis": {
            "world": str(WORLD.relative_to(ROOT)),
            "stage": str(STAGE.relative_to(ROOT)),
        },
        "findings": findings,
        "meaning": (
            "The solo card already claims Nightingale has left while a generic die "
            "is accepted; west reveal does not dismiss her, and the end-turn "
            "dialogue path later does. Static evidence does not certify which "
            "actor is available in a particular game session."
        ),
        "native_evidence": {
            "same_day_nightingale_die": "GitHub Actions 38214574855",
            "day9_to_day10_dismissal": "GitHub Actions 38215151932",
            "day10_neil_solo": "S3-AA separate native workflow job",
        },
        "next_required_decision": (
            "Choose one canonical departure event; then align card availability, "
            "actor/dice eligibility and lamplight dialogue in one coherent change."
        ),
    }

def main():
    result = json.dumps(audit(), ensure_ascii=False, indent=2) + "\n"
    if len(sys.argv) > 1:
        Path(sys.argv[1]).write_text(result, encoding="utf-8")
    else:
        print(result, end="")

if __name__ == "__main__":
    main()
