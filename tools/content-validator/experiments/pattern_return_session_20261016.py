"""RETURN-S/M paired-choice session, protocol v2.

Standalone abstraction study; NOT a game feature, participant, or utility solver.
The seven source cards deliberately COVER branches; they are not random draws.
Host must not expose card secrets or this file to an unbriefed participant.
Run: python3 pattern_return_session_20261016.py --selftest
     python3 pattern_return_session_20261016.py --run --condition S --card 1 --output /tmp/return_s.json
"""
import argparse
import json
from fractions import Fraction as F
from pathlib import Path
from itertools import product

# Frozen v1 cards, but the original "start" is now only a historical anchor:
# v2 allows the participant to choose OPEN or GUARD on the SAME sealed card.
# h is the OPEN hazard outcome (GUARD always suppresses it).
# 0 coin always succeeds and 1 coin always fails in LINK for all support rates.
CARDS = (
    ("no-hazard-good-forecast", "OPEN", 0, 0, 1, 0, 0),
    ("no-hazard-weak-support", "OPEN", 0, 1, 0, 1, 1),
    ("no-hazard-strong-support", "OPEN", 0, 1, 1, 1, 0),
    ("hazard-protect-A", "OPEN", 1, 0, 0, 1, 1),
    ("hazard-predict-B", "OPEN", 1, 1, 1, 0, 0),
    ("cautious-opening", "GUARD", 0, 1, 0, 0, 1),
    ("cautious-strong-support", "GUARD", 0, 0, 1, 1, 0),
)
STARTS = ("OPEN", "GUARD")
WORLDS = ("ACCEPT", "DECLINE")
ACCEPT_MOVES = ("LINK", "CUT", "ABANDON")
DECLINE_MOVES = ("MEND", "KEEP")
FINALS = ("A", "B", "ALT", "SIDE")


def new_session(card_index, condition):
    assert isinstance(card_index, int) and 0 <= card_index < len(CARDS)
    assert condition in ("S", "M")
    return {"card": card_index, "condition": condition, "stage": "L1",
            "start": None, "hazard": None, "forecast": None,
            "world_choice": None, "macro": None, "local_action": None,
            "feedback": None, "ready_A": None, "ready_B": None,
            "breach": None, "final": None, "net": None, "trace": []}


def public_rules():
    # Everything required for an informed forward-looking choice is public.
    return {
        "start": {"OPEN_hazard_probability": "1/2", "GUARD_hazard_probability": "0"},
        "information": {"W_prior": "A/B equally likely",
                        "Y_forecast_accuracy": "3/4", "K_strong_probability": "1/2",
                        "K_independent_of_final_demand": True},
        "after_DECLINE": {"MEND": "A ready, B unavailable", "KEEP": "A kept iff no hazard"},
        "after_ACCEPT": {
            "LINK_success": {"OPEN_weak": "1/4", "OPEN_strong": "3/4",
                             "GUARD_weak": "1/8", "GUARD_strong": "1/2"},
            "LINK_success_result": "A and B both ready",
            "LINK_failure_result": "B lost; A kept if no hazard or GUARD; breach 2",
            "CUT": "B ready, A lost, no breach",
            "ABANDON": "A ready, B lost, breach 2"},
        "world_final": {"matching_A_or_B": 14, "nonmatching_A_or_B": 3,
                        "ALT": 5, "SIDE": 1, "only_one_final_choice": True},
    }


def legal_moves(s):
    stage = s["stage"]
    if stage == "L1":
        return STARTS
    if stage == "W1":
        if s["condition"] == "S":
            return WORLDS
        # Same eleven policy possibilities, but presented as a structured
        # world choice and condition fields rather than eleven flat buttons.
        return tuple(("DECLINE", a) for a in DECLINE_MOVES) + tuple(
            ("ACCEPT", low, high) for low, high in product(ACCEPT_MOVES, repeat=2))
    if stage == "L2":
        if s["condition"] == "M":
            return ("APPLY",)
        return ACCEPT_MOVES if s["world_choice"] == "ACCEPT" else DECLINE_MOVES
    if stage == "W2":
        return tuple(a for a in FINALS if a in ("ALT", "SIDE")
                     or (a == "A" and s["ready_A"])
                     or (a == "B" and s["ready_B"]))
    return ()


def visible(s):
    """Participant-safe observation; sealed card identity and hidden data omitted."""
    stage = s["stage"]
    obs = {"stage": stage, "condition": s["condition"],
           "rules": public_rules(), "legal": legal_moves(s)}
    if stage == "W1" and s["condition"] == "M":
        obs["legal"] = {"world": WORLDS, "if_decline": DECLINE_MOVES,
                        "if_accept_weak": ACCEPT_MOVES, "if_accept_strong": ACCEPT_MOVES}
    if stage in ("W1", "L2", "W2", "DONE"):
        obs.update(start=s["start"], hazard=s["hazard"],
                   forecast="B" if s["forecast"] else "A")
    if stage in ("L2", "W2", "DONE"):
        obs["world_choice"] = s["world_choice"]
        if s["world_choice"] == "ACCEPT":
            obs["support"] = "strong" if CARDS[s["card"]][4] else "weak"
        if s["condition"] == "M":
            obs["precommitted_macro"] = s["macro"]
    if stage in ("W2", "DONE"):
        obs.update(local_action=s["local_action"], feedback=s["feedback"],
                   ready_A=s["ready_A"], ready_B=s["ready_B"],
                   breach=s["breach"], true_demand="B" if CARDS[s["card"]][5] else "A")
        # Show actual payoff for every legal W2 choice. Never auto-select.
        w = CARDS[s["card"]][5]
        obs["terminal_net_payoffs"] = {a: ((14 if (a == "A" and not w) or
                                           (a == "B" and w) else 3)
                                       if a in ("A", "B") else (5 if a == "ALT" else 1))
                                       - s["breach"] for a in legal_moves(s)} if stage == "W2" else {}
        if stage == "DONE":
            obs.update(final=s["final"], net=s["net"])
    return obs


def _resolve(s, action):
    card = CARDS[s["card"]]
    _, _, _, _, k, _, coin = card
    start, h, choice = s["start"], s["hazard"], s["world_choice"]
    s["local_action"] = action
    if choice == "DECLINE":
        if action == "MEND":
            a, b, breach, feedback = True, False, 0, "A_MENDED"
        else:
            a, b, breach, feedback = bool(not h or start == "GUARD"), False, 0, "UNCHANGED"
    elif action == "CUT":
        a, b, breach, feedback = False, True, 0, "A_SACRIFICED"
    elif action == "ABANDON":
        a, b, breach, feedback = True, False, 2, "B_ABANDONED"
    else:
        probability = (F(3, 4) if k else F(1, 4)) if start == "OPEN" else (
            F(1, 2) if k else F(1, 8))
        # Shared exogenous deterministic threshold for S/M and OPEN/GUARD.
        # 1/16 < every LINK success p; 15/16 > every LINK success p.
        success = (F(1, 16) if coin == 0 else F(15, 16)) < probability
        if success:
            a, b, breach, feedback = True, True, 0, "LINK_SUCCESS"
        else:
            a, b, breach, feedback = bool(not h or start == "GUARD"), False, 2, "LINK_FAIL"
    s.update(ready_A=a, ready_B=b, breach=breach, feedback=feedback, stage="W2")


def move(s, selection):
    """Pure, deterministic phase transition. Caller supplies the participant choice."""
    allowed = legal_moves(s)
    if selection not in allowed:
        raise ValueError("illegal choice at " + s["stage"] + ": " + repr(selection))
    stage = s["stage"]
    s["trace"].append({"stage": stage, "selection": selection})
    if stage == "L1":
        s["start"] = selection
        s["hazard"] = CARDS[s["card"]][2] if selection == "OPEN" else 0
        s["forecast"] = CARDS[s["card"]][3]
        s["stage"] = "W1"
    elif stage == "W1":
        if s["condition"] == "S":
            s["world_choice"] = selection
        else:
            s["world_choice"] = selection[0]
            s["macro"] = selection
        s["stage"] = "L2"
    elif stage == "L2":
        if s["condition"] == "M":
            choice = s["macro"]
            action = choice[1] if choice[0] == "DECLINE" else choice[
                1 if CARDS[s["card"]][4] == 0 else 2]
        else:
            action = selection
        _resolve(s, action)
    elif stage == "W2":
        w = CARDS[s["card"]][5]
        gain = (14 if (selection == "A" and not w) or
               (selection == "B" and w) else 3) if selection in ("A", "B") else (
                   5 if selection == "ALT" else 1)
        s.update(final=selection, net=gain - s["breach"], stage="DONE")
    return s


def paired_regression():
    from pattern_return_comprehension_protocol_20261015 import outcome
    tests = 0
    for cardid, card in enumerate(CARDS):
        # Anchoring: the original card's frozen OPEN/GUARD case has unchanged
        # terminal readiness, breach and LINK success/fail vs v1.
        for wc, moves in (("DECLINE", DECLINE_MOVES), ("ACCEPT", ACCEPT_MOVES)):
            for action in moves:
                state = new_session(cardid, "S")
                move(state, card[1])
                move(state, wc)
                move(state, action)
                prior = outcome(card, wc, action)
                assert (state["ready_A"], state["ready_B"], state["breach"],
                        state["feedback"]) == (
                    prior["ready_A"], prior["ready_B"], prior["breach"],
                    prior["feedback"])
        for start in STARTS:
            for world, moveset in (("DECLINE", DECLINE_MOVES),
                                   ("ACCEPT", ACCEPT_MOVES)):
                for low, high in ([(m, m) for m in moveset]
                                  if world == "DECLINE" else product(moveset, repeat=2)):
                    action = low if world == "DECLINE" or card[4] == 0 else high
                    macro = (world, low) if world == "DECLINE" else (world, low, high)
                    s = new_session(cardid, "S")
                    m = new_session(cardid, "M")
                    assert "start" not in visible(s) and "support" not in visible(s)
                    move(s, start); move(m, start)
                    assert visible(s)["forecast"] == visible(m)["forecast"]
                    assert "support" not in visible(s)
                    assert "true_demand" not in visible(s)
                    move(s, world); move(m, macro)
                    assert ("support" in visible(s)) == (world == "ACCEPT")
                    assert "true_demand" not in visible(m)
                    move(s, action); move(m, "APPLY")
                    for key in ("ready_A", "ready_B", "breach", "feedback", "local_action"):
                        assert s[key] == m[key], (cardid, start, world, key)
                    assert set(legal_moves(s)) == set(legal_moves(m))
                    for final in legal_moves(s):
                        ss = dict(s); ss["trace"] = list(s["trace"])
                        mm = dict(m); mm["trace"] = list(m["trace"])
                        assert visible(s)["terminal_net_payoffs"][final] == visible(m)["terminal_net_payoffs"][final]
                        move(ss, final); move(mm, final)
                        assert ss["net"] == mm["net"] == visible(s)["terminal_net_payoffs"][final]
                        tests += 1
    assert tests > 100
    print("PASS: paired S/M full action paths and participant-selected final payoffs", tests)
    print("PASS: original seven frozen card outcomes preserved under historical starts")
    print("PASS: initial start freely chosen, no early K/W leak, no automatic W2 best action")
    print("LIMIT: sealed cards are branch coverage only; no participants or independent agents")


def interview(condition, cardid, output):
    s = new_session(cardid, condition)
    transcript = {"protocol": "RETURN-v2", "condition": condition,
                  "trial": "T%02d" % (cardid + 1), "observations": [],
                  "free_text": [], "session": None}
    print("选择四阶段中的动作。场景为封存确定卡，并不代表总体随机频率。")
    print("回答会写入本地文件；不发送到远端。请不要查看代码或规则最优策略。")
    print("预先想法（可留空）：")
    transcript["free_text"].append({"stage": "PRE", "comment": input("> ")})
    while s["stage"] != "DONE":
        obs = visible(s)
        transcript["observations"].append(obs)
        print(json.dumps(obs, ensure_ascii=False, indent=2))
        def pick(label, options):
            for i, option in enumerate(options, 1):
                print(i, option)
            while True:
                raw = input(label + " 编号> ").strip()
                if raw.isdigit() and 1 <= int(raw) <= len(options):
                    return options[int(raw) - 1]
                print("无效编号")
        if s["stage"] == "W1" and condition == "M":
            world = pick("世界是否接约", WORLDS)
            if world == "DECLINE":
                selection = (world, pick("若不接约，返回A怎样做", DECLINE_MOVES))
            else:
                low = pick("若K弱，返回A怎样做", ACCEPT_MOVES)
                high = pick("若K强，返回A怎样做", ACCEPT_MOVES)
                selection = (world, low, high)
        else:
            selection = pick("当前动作", legal_moves(s))
        transcript["free_text"].append({"stage": s["stage"],
                                         "reason": input("理由/顾虑（原话）> ")})
        move(s, selection)
    transcript["session"] = {k: v for k, v in s.items() if k != "trace"}
    transcript["choices"] = s["trace"]
    transcript["observations"].append(visible(s))
    transcript["posttest"] = []
    for prompt in ("接约后回到A还能直接MEND吗？为什么？",
                   "K支援强弱是否等于最后的真实需求W？它改变了什么？",
                   "LINK失败时A是否总能兑现？什么条件会影响？",
                   "你放弃了哪项具体成果？有没有你不愿用高分替代的损失？"):
        print(prompt)
        transcript["posttest"].append({"question": prompt, "raw": input("> ")})
    p = Path(output)
    if p.exists():
        raise FileExistsError(p)
    p.write_text(json.dumps(transcript, ensure_ascii=False, indent=2), encoding="utf-8")
    print("记录已保存于本地", p)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--run", action="store_true")
    ap.add_argument("--condition", choices=("S", "M"))
    ap.add_argument("--card", type=int, choices=range(1, 8))
    ap.add_argument("--output")
    args = ap.parse_args()
    if args.run:
        if args.condition is None or args.card is None or not args.output:
            ap.error("--run requires --condition S/M --card 1..7 --output PATH")
        interview(args.condition, args.card - 1, args.output)
    else:
        paired_regression()
