import { Interpreter, env as lipsEnv, unserialize_bin } from "lips";

/**
 * Stage-one Scheme candidate: a persistent per-scene LIPS VM.
 * Keep world and encounter VMs separate; do not use the BiwaScheme
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

  async evaluateNumber(source: string): Promise<number> {
    const raw = await this.evaluate(source);
    const result = Number(String(raw));
    if (!Number.isFinite(result)) {
      throw new Error("Expected a Scheme number, got " + String(raw));
    }
    return result;
  }
}
