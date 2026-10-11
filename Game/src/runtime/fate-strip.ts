/**
 * Direct port of Engine/Runtime/Core/FateStrip.cs.
 * Outcome odds depend on prepared value, NOT the sum of two dice.
 */
export type RollOutcome = "fail" | "neutral" | "success";
export interface RollResult { prepared: number; fateDie: number; outcome: RollOutcome; strip: RollOutcome[] }
export function fateStrip(prepared: number): RollOutcome[] {
  if (!Number.isSafeInteger(prepared)) throw new Error("invalid prepared value");
  const [fail, neutral] = prepared <= 1 ? [3,3]
    : prepared === 2 ? [2,3]
    : prepared === 3 ? [1,3]
    : prepared === 4 ? [1,2]
    : prepared === 5 ? [0,2]
    : prepared === 6 ? [0,1] : [0,0];
  return Array.from({length: 6}, (_x, i) =>
    i < fail ? "fail" : i < fail + neutral ? "neutral" : "success");
}
export function resolveFate(die: number, skill: number, mod: number, fateDie: number): RollResult {
  if (![die,fateDie].every(n => Number.isInteger(n) && n >= 1 && n <= 6)) throw new Error("dice must be d6");
  if (!Number.isInteger(skill) || skill < -1 || skill > 4 || !Number.isInteger(mod)) throw new Error("invalid skill/mod");
  const prepared = die + skill + mod;
  const strip = fateStrip(prepared);
  return { prepared, fateDie, strip, outcome: strip[fateDie-1] };
}
