declare module "lips" {
  interface LipsInterpreter {
    exec(source: string | unknown[], options?: { env?: unknown }): Promise<unknown[]>;
    get(name: string): unknown;
    set(name: string, value: unknown): void;
  }
  export function Interpreter(name: string, environment?: Record<string, unknown>): LipsInterpreter;
  export const env: { __parent__: unknown };
  export function unserialize_bin(bytes: Uint8Array): unknown[];
  export function unbox(value: unknown): unknown;
  export function type(value: unknown): string;
  export const nil: unknown;
  export class Pair {
    car: unknown;
    cdr: unknown;
  }
}
