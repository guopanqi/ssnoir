declare module "lips" {
  interface LipsInterpreter {
    exec(source: string): Promise<unknown[]>;
  }
  export function Interpreter(name: string, environment?: Record<string, unknown>): LipsInterpreter;
}
