/**
 * A+ percentage resolution; mirrors Engine/Runtime/Core/FateStrip.cs.
 * The action die is fixed at the start of the turn. Execution draws one hidden
 * percentile to select the published fail/neutral/success outcome.
 */
export type RollOutcome = "fail" | "neutral" | "success";
export interface RollResult {
  prepared: number;
  percentageRoll: number;
  outcome: RollOutcome;
  strip: RollOutcome[];
}
export function probabilities(prepared: number): [number, number, number] {
  if (!Number.isSafeInteger(prepared)) throw new Error("invalid prepared value");
  // 1–2 dangerous, 3–4 risky, 5–6 reliable. Keep 7+ high-B scaling.
  if (prepared <= 1) return [80, 20, 0];
  if (prepared === 2) return [60, 40, 0];
  if (prepared === 3) return [40, 50, 10];
  if (prepared === 4) return [30, 50, 20];
  if (prepared === 5) return [0, 70, 30];
  if (prepared === 6) return [0, 50, 50];
  if (prepared === 7) return [0, 10, 90];
  return [0, 0, 100];
}
export function fateStrip(prepared: number): RollOutcome[] {
  const [fail, neutral] = probabilities(prepared);
  return Array.from({length: 100}, (_x, i) =>
    i < fail ? "fail" : i < fail + neutral ? "neutral" : "success");
}
export function resolveFate(die: number, skill: number, mod: number, percentageRoll: number): RollResult {
  if (!Number.isInteger(die) || die < 1 || die > 6) throw new Error("action die must be d6");
  if (!Number.isInteger(percentageRoll) || percentageRoll < 1 || percentageRoll > 100)
    throw new Error("percentage roll must be 1..100");
  if (!Number.isInteger(skill) || skill < -1 || skill > 4 || !Number.isInteger(mod))
    throw new Error("invalid skill/mod");
  const prepared = die + skill + mod;
  const strip = fateStrip(prepared);
  return { prepared, percentageRoll, strip, outcome: strip[percentageRoll - 1] };
}
