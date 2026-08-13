#!/usr/bin/env python3

import argparse
import hashlib
import json
import time
from datetime import datetime, timezone
from pathlib import Path


PLAYER_BUILD_PREFIX = "##utp:"


def seconds_between(start_ns: int, end_ns: int) -> float:
    if end_ns < start_ns:
        raise SystemExit(f"计时结束值早于开始值: {start_ns} -> {end_ns}")
    return round((end_ns - start_ns) / 1_000_000_000, 3)


def read_player_build_info(build_log: Path) -> dict:
    if not build_log.is_file():
        raise SystemExit(f"Unity 构建日志不存在: {build_log}")

    result = None
    for line in build_log.read_text(encoding="utf-8", errors="replace").splitlines():
        if not line.startswith(PLAYER_BUILD_PREFIX) or '"type":"PlayerBuildInfo"' not in line:
            continue
        payload = json.loads(line[len(PLAYER_BUILD_PREFIX) :])
        if payload.get("type") == "PlayerBuildInfo":
            result = payload

    if result is None:
        raise SystemExit(f"Unity 构建日志缺少 PlayerBuildInfo: {build_log}")
    if not isinstance(result.get("duration"), int) or not isinstance(result.get("steps"), list):
        raise SystemExit(f"PlayerBuildInfo 格式无效: {build_log}")
    return result


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--font-report", type=Path, required=True)
    parser.add_argument("--release-plan", type=Path, required=True)
    parser.add_argument("--build-log", type=Path, required=True)
    parser.add_argument("--build-start-ns", type=int, required=True)
    parser.add_argument("--staging-start-ns", type=int, required=True)
    parser.add_argument("--staging-end-ns", type=int, required=True)
    parser.add_argument("--font-start-ns", type=int, required=True)
    parser.add_argument("--font-end-ns", type=int, required=True)
    parser.add_argument("--unity-start-ns", type=int, required=True)
    parser.add_argument("--unity-end-ns", type=int, required=True)
    parser.add_argument("--cleanup-start-ns", type=int, required=True)
    parser.add_argument("--cleanup-end-ns", type=int, required=True)
    args = parser.parse_args()

    output_dir = args.output_dir.resolve()
    player_build = read_player_build_info(args.build_log.resolve())
    unity_report_path = output_dir / "unity-build-report.json"
    if not unity_report_path.is_file():
        raise SystemExit(f"Unity 构建报告不存在: {unity_report_path}")
    unity_report = json.loads(unity_report_path.read_text(encoding="utf-8"))
    unity_timing_keys = (
        "releaseAssetPreparationSeconds",
        "tapTapSdkBuildSeconds",
        "archiveValidationSeconds",
    )
    for key in unity_timing_keys:
        if not isinstance(unity_report.get(key), (int, float)) or unity_report[key] < 0:
            raise SystemExit(f"Unity 构建报告缺少有效计时字段: {key}")

    hashing_start_ns = time.time_ns()
    artifacts = []
    for name in ("game.zip", "game_wasm_split.zip"):
        path = output_dir / name
        if not path.is_file() or path.stat().st_size == 0:
            raise SystemExit(f"构建产物不存在或为空: {path}")
        artifacts.append({"name": name, "bytes": path.stat().st_size, "sha256": sha256(path)})
    hashing_end_ns = time.time_ns()

    player_steps = {}
    for step in player_build["steps"]:
        description = step.get("description")
        duration = step.get("duration")
        if not isinstance(description, str) or not isinstance(duration, int):
            raise SystemExit("PlayerBuildInfo 包含无效步骤。")
        if description in player_steps:
            raise SystemExit(f"PlayerBuildInfo 包含重复步骤: {description}")
        player_steps[description] = round(duration / 1000, 3)

    generated_at = datetime.now(timezone.utc)
    timings = {
        "totalSeconds": seconds_between(args.build_start_ns, time.time_ns()),
        "stagingPreparationSeconds": seconds_between(args.staging_start_ns, args.staging_end_ns),
        "fontSubsettingSeconds": seconds_between(args.font_start_ns, args.font_end_ns),
        "unityBatchSeconds": seconds_between(args.unity_start_ns, args.unity_end_ns),
        "unityPlayerBuildSeconds": round(player_build["duration"] / 1000, 3),
        "unityPlayerStepsSeconds": player_steps,
        "releaseAssetPreparationSeconds": round(unity_report["releaseAssetPreparationSeconds"], 3),
        "tapTapSdkBuildSeconds": round(unity_report["tapTapSdkBuildSeconds"], 3),
        "tapTapConversionEstimateSeconds": round(
            max(0, unity_report["tapTapSdkBuildSeconds"] - player_build["duration"] / 1000),
            3,
        ),
        "archiveValidationSeconds": round(unity_report["archiveValidationSeconds"], 3),
        "outputCleanupSeconds": seconds_between(args.cleanup_start_ns, args.cleanup_end_ns),
        "artifactHashingSeconds": seconds_between(hashing_start_ns, hashing_end_ns),
    }

    report = {
        "generatedAtUtc": generated_at.isoformat(),
        "timings": timings,
        "artifacts": artifacts,
        "fonts": json.loads(args.font_report.read_text(encoding="utf-8")),
        "releasePlan": json.loads(args.release_plan.read_text(encoding="utf-8")),
    }
    report_path = output_dir / "build-report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    for artifact in artifacts:
        size_mb = artifact["bytes"] / 1024 / 1024
        print(f"{artifact['name']}: {size_mb:.2f} MiB")
    print(
        "构建耗时: "
        f"总计 {timings['totalSeconds']:.2f}s，"
        f"Unity batch {timings['unityBatchSeconds']:.2f}s，"
        f"Player {timings['unityPlayerBuildSeconds']:.2f}s"
    )


if __name__ == "__main__":
    main()
