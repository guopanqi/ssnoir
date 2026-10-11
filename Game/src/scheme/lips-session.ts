import { Interpreter } from "lips";

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
