declare module "lips" {
  interface LipsInterpreter {
    exec(source: string | unknown[], options?: { env?: unknown }): Promise<unknown[]>;
  }
  export function Interpreter(name: string, environment?: Record<string, unknown>): LipsInterpreter;
  export const env: { __parent__: unknown };
  export function unserialize_bin(bytes: Uint8Array): unknown[];
}
