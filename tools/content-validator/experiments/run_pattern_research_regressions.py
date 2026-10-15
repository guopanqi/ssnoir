"""Small standard-library runner for recent SSNoir abstract mechanism regressions.
This tests actual committed scripts, not just prose claims. It does not establish
gameplay quality, Unity parity, or human-experience validity.
"""
from pathlib import Path
import subprocess
import sys
import time

HERE = Path(__file__).resolve().parent
SCRIPTS = (
    "pattern_bridge_two_feedbacks_20261015.py",
    "pattern_bridge_permission_audit_20261015.py",
    "pattern_productive_crisis_relay_20261015.py",
    "pattern_purposeful_failure_20261015.py",
    "pattern_public_commitment_revision_20261015.py",
    "pattern_public_signal_participation_20261015.py",
    "pattern_cascade_payoff_stability_20261015.py",
    "pattern_belief_revision_cascade_20261015.py",
    "pattern_cascade_dependencies_20261015.py",
    "pattern_constraint_deduction_20261015.py",
    "pattern_batch_confirmation_20261015.py",
    "pattern_source_provenance_20261015.py",
    "pattern_endogenous_media_echo_20261015.py",
    "pattern_contextual_causal_probe_20261015.py",
    "pattern_causal_retest_20261015.py",
    "pattern_action_commitment_order_20261015.py",
    "pattern_single_public_writ_20261015.py",
    "pattern_adversarial_tempo_20261015.py",
    "pattern_adversarial_motives_20261015.py",
    "pattern_partner_memory_20261015.py",
    "pattern_partner_intervention_20261015.py",
    "pattern_partner_strategy_20261015.py",
    "pattern_bilateral_commitment_20261015.py",
    "pattern_bilateral_outside_option_20261015.py",
    "pattern_cancellation_transfer_20261015.py",
    "pattern_signalling_equilibrium_selection_20261015.py",
)

def main():
    failed = []
    start = time.monotonic()
    for name in SCRIPTS:
        path = HERE / name
        if not path.is_file():
            failed.append((name, "missing test file"))
            continue
        try:
            result = subprocess.run(
                [sys.executable, str(path)], cwd=str(HERE),
                capture_output=True, text=True, timeout=55, check=False,
            )
        except subprocess.TimeoutExpired:
            failed.append((name, "timed out after 55s"))
            print("FAIL", name, "TIMEOUT", flush=True)
            continue
        if result.returncode == 0:
            print("PASS", name, flush=True)
        else:
            error = (result.stderr or result.stdout)[-4500:]
            failed.append((name, error))
            print("FAIL", name, error, flush=True)
    elapsed = time.monotonic() - start
    print(f"Checked {len(SCRIPTS)} committed experiments in {elapsed:.1f}s;"
          f" {len(failed)} failures")
    return 1 if failed else 0

if __name__ == "__main__":
    raise SystemExit(main())
