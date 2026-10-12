#!/usr/bin/env python3
"""Three genuinely independent GPT-6 research agents; one per GitHub matrix job.

No API traffic in --dry-run mode. Generated Python is NEVER executed in the
API-key-bearing agent job; a separate secretless job reviews/executes it.
"""
from __future__ import annotations
import argparse
import asyncio
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
MODEL = "gpt-6-astra"
LANES = {
    "pressure": {
        "name": "SSNoir Agent · Pressure and consequences",
        "question": "R106: 成功推进产生新问题 vs 行动失败产生新问题，在公平、可解释的条件下，是否确实重排修补与推进的优先级？",
        "base": "r103_pressure_trigger_source.py",
        "falsifier": "相同成功概率与暴露负担的强阈值修补法；完全关闭新问题规则",
    },
    "route": {
        "name": "SSNoir Agent · Commitment and escape",
        "question": "R107: 不可逆改道消耗修梯行动后，何时有自愿而严格有利的改道；是否只是一条净容量口诀？",
        "base": "r104_exit_commitment_boundaries.py",
        "falsifier": "永不改道、开局固定改道、最强公开期限阈值",
    },
    "information": {
        "name": "SSNoir Agent · Information and discovery",
        "question": "R108: 普通行动可以自然发现线索时，专门调查与先探索一处再调查，是否还有独立收益？",
        "base": "r105_ambient_vs_paid_information.py",
        "falsifier": "禁用专门探查、先搜一个地点再探查、固定开局探查",
    },
}
APPROVED_SCRIPTS = {
    "r103_pressure_trigger_source.py",
    "r104_exit_commitment_boundaries.py",
    "r105_ambient_vs_paid_information.py",
    "r97b_delayed_seal_simple_rule.py",
    "r99_policy_compression.py",
    "r100_optional_completion.py",
}
ALLOWED_PATHS = ("docs/实验/机制研究/", "skills/research-mechanisms/",
                 "tools/content-validator/experiments/")
SINGLE_FILES = {"AGENTS.md"}


def read_safe(path: str) -> str:
    if not isinstance(path, str) or len(path) > 300:
        raise ValueError("Invalid source path")
    source = (ROOT / path).resolve()
    if not source.is_relative_to(ROOT) or not source.is_file():
        raise ValueError("Source must be a checked-in repository file")
    normalized = source.relative_to(ROOT).as_posix()
    if normalized not in SINGLE_FILES and not normalized.startswith(ALLOWED_PATHS):
        raise ValueError("Source not in permitted research area")
    if source.stat().st_size > 200_000:
        raise ValueError("Source exceeds read limit")
    return source.read_text(encoding="utf-8")[:16_000]


def baseline(name: str) -> str:
    if name not in APPROVED_SCRIPTS:
        raise ValueError("Only separately reviewed prior experiment scripts may execute here")
    script = ROOT / "tools/content-validator/experiments" / name
    env = {"PATH": os.environ.get("PATH", "/usr/bin:/bin"),
           "HOME": "/tmp", "PYTHONNOUSERSITE": "1", "LANG": "C.UTF-8"}
    result = subprocess.run([sys.executable, str(script)], cwd=ROOT,
                            env=env, capture_output=True, text=True, timeout=60,
                            check=False)
    if result.returncode:
        raise RuntimeError(f"Baseline failed: {name}: {result.stderr[-1200:]}")
    return result.stdout[:12_000]


async def live_run(lane: str, output: Path) -> None:
    if not os.getenv("OPENAI_API_KEY"):
        raise SystemExit("OPENAI_API_KEY is missing: set it as a GitHub Actions secret; no GPT-6 agents were started.")
    from agents import Agent, Runner, function_tool

    output.mkdir(parents=True, exist_ok=True)
    config = LANES[lane]
    @function_tool
    def read_research_file(path: str) -> str:
        """Read an allowlisted SSNoir research file. Paths are relative to repository root."""
        return read_safe(path)

    @function_tool
    def run_reviewed_baseline(filename: str) -> str:
        """Run an EXISTING reviewed mathematical experiment; never executes generated code."""
        return baseline(filename)

    @function_tool
    def submit_paper_experiment(python_source: str) -> str:
        """Save a new standalone Python 3 paper falsification, for isolated later execution.

        Script must use only Python stdlib and print a JSON object with
        keys: hypothesis, baseline, counterfactual, results, verdict.
        Do not access files, subprocesses, network, secrets or external packages.
        This merely saves source; the agent cannot claim it executed.
        """
        if not 200 <= len(python_source) <= 30_000:
            raise ValueError("Experiment script must contain 200..30000 characters")
        (output / "experiment.py").write_text(python_source, encoding="utf-8")
        return "experiment.py saved for secretless independent verification; NOT executed."

    context = (
        "SSNoir研究源版本：" + os.getenv("GITHUB_SHA", "local") + "\n"
        "AGENTS.md与skills/research-mechanisms/SKILL.md是工程规则；"
        "研究队列docs/实验/机制研究/研究队列.md、"
        "当前状态.md及并行研究协议.md是续研上下文。\n"
        "你是三个不同模型会话中的一个；其它两条研究线独立运行，"
        "你只负责自己分配的课题。\n"
        "先读研究上下文和原脚本，运行已有基线，提出一个新的、"
        "可能证伪你自己预期的最小数学假设。"
        "请一定调用submit_paper_experiment提交新的精确Python反证脚本，"
        "包含最强可解释简单策略及至少一个直接禁用新增规则的消融。"
        "完成后写一份简洁中文研究备忘录，列出冻结规则、反证路线、"
        "旧证据和新脚本之间的严格界限、潜在失败结论、下一步，"
        "以及脚本还未执行的明确提醒。"
        "你不能改Unity Content、Debug清单、模式库、共享记忆或远程Git。"
        "不要制造或夸大真人反馈，不要伪造未执行的新代码结果。"
    )
    agent = Agent(
        name=config["name"], model=MODEL,
        instructions=context + "\n课题：" + config["question"] +
                     "\n强反证：" + config["falsifier"],
        tools=[read_research_file, run_reviewed_baseline, submit_paper_experiment],
    )
    prompt = ("请自主执行这一轮 SSNoir 玩法机制研究。"
              "第一步读 docs/实验/机制研究/并行研究协议.md；"
              "第二步读取 tools/content-validator/experiments/" + config["base"] +
              " 并运行其基线；第三步自行提出一个新增但可被反证的假设，"
              "写完整可独立运行的 experiment.py，调用工具保存；"
              "最后写研究备忘录，不可把新脚本假装已经跑过。")
    result = await Runner.run(agent, prompt, max_turns=12)
    report = str(result.final_output or "").strip()
    if not report or not (output / "experiment.py").is_file():
        raise RuntimeError("Agent failed to submit both a hypothesis report and runnable script")
    (output / "report.md").write_text(report + "\n", encoding="utf-8")
    (output / "metadata.json").write_text(json.dumps({
        "agent": config["name"], "lane": lane, "model": MODEL, "produced": True,
        "source_sha": os.getenv("GITHUB_SHA", "unknown"),
        "evidence": "Model-authored hypothesis. New generated script NOT executed in agent job.",
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{lane}: independent {MODEL} agent produced report and experiment source")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--lane", choices=sorted(LANES), required=True)
    parser.add_argument("--out", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    if args.dry_run:
        assert len(LANES) == 3 and MODEL == "gpt-6-astra"
        assert all(x["base"] in APPROVED_SCRIPTS for x in LANES.values())
        print(json.dumps({"mode": "dry-run", "agent_model": MODEL,
                          "lane": args.lane, "scope": LANES[args.lane],
                          "api_called": False}, ensure_ascii=False))
        return
    if args.out is None:
        parser.error("--out must be set for a live API run")
    asyncio.run(live_run(args.lane, args.out.resolve()))


if __name__ == "__main__":
    main()
