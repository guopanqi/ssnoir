import { Interpreter, env as lipsEnv, unserialize_bin } from "lips";
import { accelerateAssociationLookup, accelerateMembershipLookup } from "./association-lookup.ts";

/**
 * Stage-one Scheme candidate: a persistent per-scene LIPS VM.
 * Keep world and scenario VMs separate; do not use the BiwaScheme
 * global environment (verified to leak top-level bindings).
 */
export class LipsSession {
  private readonly interpreter: ReturnType<typeof Interpreter>;

  constructor(name: string, globals: Record<string, unknown> = {}) {
    this.interpreter = Interpreter(name, globals);
  }

  async bootstrapStandardLibrary(binary: Uint8Array): Promise<void> {
    // LIPS std.xcb is compiled with its parser directives in the correct
    // order. Parsing all of std.scm as a single (begin ...) does NOT work on
    // a cold interpreter: regex literals are registered during bootstrap.
    const ast = unserialize_bin(binary);
    await this.interpreter.exec(ast, { env: lipsEnv.__parent__ });
  }

  async evaluate(source: string): Promise<unknown> {
    const results = await this.interpreter.exec(source);
    return results.at(-1);
  }

  accelerateContentLookups(): void {
    const original = this.interpreter.get("assoc-get");
    if (typeof original !== "function") throw new Error("Content assoc-get helper is not initialized");
    this.interpreter.set("assoc-get", accelerateAssociationLookup(original as (...args: unknown[]) => unknown));
    const member = this.interpreter.get("member?");
    if (typeof member !== "function") throw new Error("Content member? helper is not initialized");
    this.interpreter.set("member?", accelerateMembershipLookup(member as (...args: unknown[]) => unknown));
  }

  async evaluateNumber(source: string): Promise<number> {
    const raw = await this.evaluate(source);
    const result = Number(String(raw));
    if (!Number.isFinite(result)) {
      throw new Error("Expected a Scheme number, got " + String(raw));
    }
    return result;
  }
}
