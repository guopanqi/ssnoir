import { unbox } from "lips";
import { GameRuntimeState } from "./game-state.ts";
import { schemeList } from "./node-converter.ts";

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
  // The npm ES module, Vite IIFE and TapTap converter may minify class names.
  // Use LIPS's public unbox API, not constructor.name / instanceof names.
  const raw = unbox(value);
  if (typeof raw === "string") return raw;
  throw new Error("expected Scheme string");
}
function portableScalar(value: unknown): string | number | boolean {
  const raw = unbox(value);
  if (typeof raw === "string" || typeof raw === "boolean") return raw;
  if (typeof raw === "number" && Number.isFinite(raw)) return raw;
  if (typeof raw === "bigint") {
    const num = Number(raw);
    if (Number.isSafeInteger(num)) return num;
  }
  throw new Error("not a portable Scheme scalar: " + typeof raw);
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
    "random-choice": (...args) => {
      arity("random-choice",args,1);
      const options=schemeList(args[0],"random-choice");
      if (!options.length) return null; // Matches C# empty-list behavior.
      const index=Math.floor(Math.random()*options.length);
      return options[index]; // Keep Scheme value boxed in its originating interpreter.
    },
    "__injury-severity": (...args) => {
      arity("__injury-severity",args,0);return state.injury.severity;
    },
    "__injury-band-index": (...args) => {
      arity("__injury-band-index",args,0);return state.injury.band;
    },
    "__scar-count": (...args) => {
      if(args.length>1)throw new Error("__scar-count accepts at most one part");
      return state.injury.scarCount(args.length?string(args[0]):undefined);
    },
    "__injure!": (...args) => {
      arity("__injure!",args,1);
      if(state.injury.injure(integer(args[0]))) {
        // C# immediately resolves collapse, healing fee, scars and dice invalidation.
        // Stop until the Phase 2 hospitalization controller is attached.
        throw new Error("hospitalization controller not yet installed for 7/7 injury");
      }
      return undefined;
    },
    "__heal-injury!": (...args) => {
      arity("__heal-injury!",args,1);state.injury.heal(integer(args[0]));return undefined;
    },
    "__hospitalization-pending?": (...args) => {
      arity("__hospitalization-pending?",args,0);return false;
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
    "__record-clock-effect!": (...args) => {
      arity("__record-clock-effect!",args,3);
      state.recordClockEffect(string(args[0]),string(args[1]),integer(args[2]));
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
