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
      state.applyInjury(integer(args[0]));return undefined;
    },
    "__heal-injury!": (...args) => {
      arity("__heal-injury!",args,1);state.injury.heal(integer(args[0]));return undefined;
    },
    "__hospitalization-pending?": (...args) => {
      arity("__hospitalization-pending?",args,0);return state.hasPendingHospitalization();
    },
    "__actor-composure": (...args) => {
      arity("__actor-composure",args,1);return state.team.composure(id(args[0]));
    },
    "__set-actor-composure!": (...args) => {
      arity("__set-actor-composure!",args,2);
      const overflow=state.team.setComposure(id(args[0]),integer(args[1]));
      if(overflow>0)state.applyInjury(overflow);
      return undefined;
    },
    "__spend-actor-composure!": (...args) => {
      arity("__spend-actor-composure!",args,2);
      const overflow=state.team.spend(id(args[0]),integer(args[1]));
      if(overflow>0)state.applyInjury(overflow);
      return undefined;
    },
    "__actor-status": (...args) => {
      arity("__actor-status",args,1);return state.team.status(id(args[0]));
    },
    "__set-actor-status!": (...args) => {
      arity("__set-actor-status!",args,2);
      const status=id(args[1]);
      if(status!=="active"&&status!=="away")throw new Error("invalid actor status");
      state.team.setStatus(id(args[0]),status);return undefined;
    },
    "__actor-stat": (...args) => {
      arity("__actor-stat",args,2);return state.team.stat(id(args[0]),id(args[1]));
    },
    "__has-companion?": (...args) => {
      arity("__has-companion?",args,1);
      return state.team.find(id(args[0]))?.role==="companion";
    },
    "__dismiss-companion!": (...args) => {
      arity("__dismiss-companion!",args,1);
      const actor=state.team.require(id(args[0]));
      state.team.dismiss(actor.id);state.notify(actor.name+"离开队伍");return undefined;
    },
    "__recruit-companion!": (...args) => {
      arity("__recruit-companion!",args,3);
      const values=schemeList(args[2],"companion stats");
      const stats:Record<string,number>={};
      for(const entry of values) {
        const pair=schemeList(entry,"companion stat entry");
        if(pair.length!==2)throw new Error("companion stats need (skill value) pairs");
        stats[id(pair[0])]=integer(pair[1]);
      }
      state.team.recruit(id(args[0]),string(args[1]),stats as import("./team-state.ts").Stats);
      state.notify(string(args[1])+"加入队伍");return undefined;
    },
    "__grant-support!": (...args) => {
      arity("__grant-support!",args,1);state.team.grantSupport(string(args[0]));return undefined;
    },
    "__has-support?": (...args) => {
      arity("__has-support?",args,1);return state.team.hasSupport(string(args[0]));
    },
    "__set-carried-support!": (...args) => {
      arity("__set-carried-support!",args,1);state.team.setCarriedSupport(string(args[0]));return undefined;
    },
    "__carried-support": (...args) => {
      arity("__carried-support",args,0);return state.team.getCarriedSupport();
    },
    "__set-actor-die-profile!": (...args) => {
      arity("__set-actor-die-profile!",args,4);
      const fixed=integer(args[2]);
      state.team.dieProfile(id(args[0]),integer(args[1]),fixed===0?null:fixed,string(args[3]));
      return undefined;
    },
    "__set-actor-permanent-die-penalty!": (...args) => {
      arity("__set-actor-permanent-die-penalty!",args,3);
      state.team.permanentDiePenalty(id(args[0]),string(args[1]),integer(args[2]));
      return undefined;
    },
    "__apply-hangover!": (...args) => {
      arity("__apply-hangover!",args,0);state.team.applyHangover();return undefined;
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
