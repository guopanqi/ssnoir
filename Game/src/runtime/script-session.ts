import { LipsSession } from "../scheme/lips-session.ts";
import { GameRuntimeState } from "./game-state.ts";
import { createNativeBridge } from "./native-bridge.ts";

/** Content is injected from ?raw in browser, fs in Node; no fetch/FS in Mini Games. */
export interface ScriptSources { [canonicalPath: string]: string }

/**
 * Equivalent to SchemeInterpreter's startup order:
 * stdlib.scm -> engine.scm -> its load-file scripts/theatre.scm.
 * This deliberately does NOT start the entire world/encounter SceneManager yet.
 */
export class GameScriptSession {
  readonly name: string;
  readonly state: GameRuntimeState;
  private readonly vm: LipsSession;
  private readonly sources: ScriptSources;

  private constructor(name: string, state: GameRuntimeState, vm: LipsSession, sources: ScriptSources) {
    this.name = name;
    this.state = state;
    this.vm = vm;
    this.sources = sources;
  }

  static async create(
    name: string, state: GameRuntimeState, sources: ScriptSources, lipsStandardLibrary: Uint8Array
  ): Promise<GameScriptSession> {
    let session: GameScriptSession;
    const globals = {
      ...createNativeBridge(state),
      ":at": ":at",
      "load-file": async (...args: unknown[]) => {
        if (args.length !== 1 || args[0] === null || args[0] === undefined) {
          throw new Error("load-file expects a single path string");
        }
        // LIPS supplies a boxed Scheme string at the JavaScript native boundary.
        const path = String(args[0]);
        if (!path || path === "[object Object]") throw new Error("load-file expects a string path");
        return session.loadFile(path);
      }
    };
    const vm = new LipsSession(name, globals);
    session = new GameScriptSession(name, state, vm, sources);
    if (!lipsStandardLibrary.length) throw new Error("LIPS compiled standard library is required");
    // Evaluate the pinned precompiled std.xcb in the shared parent environment.
    // The compiled AST preserves reader syntax unavailable to cold std.scm.
    // No runtime network or filesystem access on any host.
    await vm.bootstrapStandardLibrary(lipsStandardLibrary);
    await session.loadFile("scripts/stdlib.scm");
    await session.loadFile("scripts/engine.scm");
    return session;
  }

  private static canonicalPath(path: string): string {
    const normalized = path.startsWith("scripts/") || path.startsWith("scenes/") ? path : "scenes/" + path;
    if (!/^(scripts|scenes)\/[\w\p{L}\p{N}\/.-]+\.scm$/u.test(normalized) ||
        normalized.split("/").some(part => part === ".." || part === ".")) {
      throw new Error("Invalid content script path: " + path);
    }
    return normalized;
  }

  async loadFile(path: string): Promise<unknown> {
    const name = GameScriptSession.canonicalPath(path);
    if (!Object.hasOwn(this.sources, name)) throw new Error("Content script not bundled: " + name);
    try {
      return await this.vm.evaluate(this.sources[name]);
    } catch (error) {
      throw new Error("Scheme load failed (" + name + "): " + String(error), { cause: error });
    }
  }

  async evaluate(code: string): Promise<unknown> {
    // Native state rollback on failed Scheme expressions. Scheme closures themselves
    // are not rolled back: transaction boundaries are therefore only native state.
    const snapshot = this.state.snapshot();
    try {
      return await this.vm.evaluate(code);
    } catch (error) {
      this.state.restore(snapshot);
      throw error;
    }
  }

  async evaluateNumber(code: string): Promise<number> {
    const result = Number(String(await this.evaluate(code)));
    if (!Number.isFinite(result)) throw new Error("Scheme expression must return a number");
    return result;
  }
}
