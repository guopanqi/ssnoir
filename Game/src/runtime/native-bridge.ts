import { GameRuntimeState } from "./game-state.ts";

export type NativeCallable = (...args: unknown[]) => unknown;
export type NativeBridge = Record<string, NativeCallable>;

function arity(name: string, args: unknown[], expected: number): void {
  if (args.length !== expected) throw new Error(name + " expects " + expected + " argument(s), got " + args.length);
}
function id(value: unknown): string {
  const result = String(value);
  if (!result.trim() || result === "undefined" || result === "null") throw new Error("expected nonempty Scheme id");
  return result;
}
function integer(value: unknown): number {
  const result = Number(String(value));
  if (!Number.isSafeInteger(result)) throw new Error("expected an integer: " + String(value));
  return result;
}
function string(value: unknown): string {
  if (typeof value === "string") return value;
  // LIPS may expose R7RS strings as boxed Scheme values to JS-native calls.
  if (value !== null && typeof value === "object" &&
      /^(LString|String)$/.test(value.constructor?.name ?? "")) return String(value);
  throw new Error("expected Scheme string, got " + (value === null ? "null" : typeof value) +
    "/" + ((value as { constructor?: { name?: string } })?.constructor?.name ?? "unknown"));
}
function portableScalar(value: unknown): string | number | boolean {
  if (typeof value === "string" || typeof value === "boolean") return value;
  if (typeof value === "number" && Number.isFinite(value)) return value;
  const ctor = (value as { constructor?: { name?: string } } | null)?.constructor?.name ?? "";
  if (ctor === "LString" || ctor === "String") return String(value);
  if (/^(LNumber|LFloat|LInteger|LBigInt|Integer|Float|BigInt)$/.test(ctor)) {
    const n = Number(String(value));
    if (Number.isFinite(n)) return n;
  }
  throw new Error("not a portable Scheme scalar: " + ctor);
}

/** Names and argument expectations ported from NativeFunctions.Register, not a new DSL. */
export function createNativeBridge(state: GameRuntimeState): NativeBridge {
  return {
    "tr": (...args) => {
      arity("tr", args, 2);
      // C# GameLanguage.Tr chooses the currently active language; Chinese is
      // the baseline during the migration, not a missing native stub.
      return string(args[0]);
    },
    "__i18n-missing-speaker!": (...args) => {
      arity("__i18n-missing-speaker!", args, 1);
      if (!string(args[0]).trim()) throw new Error("speaker must be nonempty");
      return undefined; // Warning-only C# diagnostic, no gameplay effect.
    },
    "get-global": (...args) => {
      arity("get-global", args, 1);
      return state.getGlobal(id(args[0]));
    },
    "set-global!": (...args) => {
      arity("set-global!", args, 2);
      // Normalize LIPS's boxed Scheme strings and numbers at the native boundary.
      state.setGlobal(id(args[0]), portableScalar(args[1]));
      return undefined;
    },
    "__item-count": (...args) => {
      arity("__item-count", args, 1);
      return state.getItemCount(id(args[0]));
    },
    "__set-item-count!": (...args) => {
      arity("__set-item-count!", args, 2);
      state.setItemCount(id(args[0]), integer(args[1]));
      return undefined;
    },
    "__growth-level": (...args) => {
      arity("__growth-level", args, 0);
      return state.getGrowthLevel();
    },
    "__set-growth-level!": (...args) => {
      arity("__set-growth-level!", args, 1);
      state.setGrowthLevel(integer(args[0]));
      return undefined;
    },
    "__register-rest-block!": (...args) => {
      arity("__register-rest-block!", args, 4);
      state.registerRestBlocker(string(args[0]), string(args[1]), string(args[2]), string(args[3]));
      return undefined;
    },
    "__release-rest-block!": (...args) => {
      arity("__release-rest-block!", args, 1);
      state.releaseRestBlocker(string(args[0]));
      return undefined;
    },
    "__clear-rest-blockers!": (...args) => {
      arity("__clear-rest-blockers!", args, 0);
      state.clearRestBlockers();
      return undefined;
    },
    "__notify!": (...args) => {
      arity("__notify!", args, 1);
      state.notify(String(args[0]));
      return undefined;
    },
    "__fail-game!": (...args) => {
      arity("__fail-game!", args, 2);
      state.failGame(string(args[0]), string(args[1]));
      return undefined;
    }
  };
}
