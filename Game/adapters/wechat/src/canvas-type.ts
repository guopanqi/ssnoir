/** Host Canvas handles may have Object as their constructor. Track actual allocations. */
export function createCanvasTypeRegistry() {
  const canvases = new WeakSet<object>();
  class MiniGameCanvas {
    static [Symbol.hasInstance](value: unknown): boolean {
      return typeof value === "object" && value !== null && canvases.has(value);
    }
  }
  return {
    register<T extends object>(canvas: T): T { canvases.add(canvas); return canvas; },
    install(global: any): void {
      if (typeof global.HTMLCanvasElement !== "function" || global.HTMLCanvasElement === Object) {
        global.HTMLCanvasElement = MiniGameCanvas;
      }
    }
  };
}
