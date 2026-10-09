import BiwaScheme from "biwascheme";

/** Persistent Scheme interpreter for semantics / lifecycle validation.
 * This is a probe: encounter/world isolation and native functions come in stage 2.
 */
export class SchemeSession {
  private readonly interpreter = new BiwaScheme.Interpreter((error: unknown) => {
    throw error instanceof Error ? error : new Error(String(error));
  });

  evaluate(source: string): unknown {
    let completed = false;
    let value: unknown;
    this.interpreter.evaluate(source, result => {
      completed = true;
      value = result;
    });
    if (!completed) {
      throw new Error("Unexpected asynchronous Scheme evaluation: " + source.slice(0, 120));
    }
    return value;
  }
}

export function evaluateScheme(source: string): unknown {
  return new SchemeSession().evaluate(source);
}
