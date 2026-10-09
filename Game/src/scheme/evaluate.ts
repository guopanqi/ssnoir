import BiwaScheme from "biwascheme";

/** Compatibility probe only. Not yet the game's stateful Scheme host. */
export function evaluateScheme(source: string): unknown {
  const interpreter = new BiwaScheme.Interpreter((error: unknown) => {
    throw error instanceof Error ? error : new Error(String(error));
  });
  let completed = false;
  let result: unknown;
  interpreter.evaluate(source, (value: unknown) => {
    completed = true;
    result = value;
  });
  if (!completed) {
    throw new Error("Scheme probe used an asynchronous primitive; synchronous evaluation required.");
  }
  return result;
}
