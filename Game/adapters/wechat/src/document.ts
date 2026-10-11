/** Define only declared missing capabilities; never assign through host setters. */
function defineCapability(target: any, name: string, value: unknown): void {
  const descriptor = Object.getOwnPropertyDescriptor(target, name);
  if (descriptor && !descriptor.configurable) {
    throw new Error("Mini Game host forbids missing capability: " + name);
  }
  Object.defineProperty(target, name, { value, writable: true, configurable: true, enumerable: true });
}

/** Install the explicitly supported DOM subset, including partial host objects. */
export function installMiniGameDocument(global: any, facade: Record<string, any>, screenCanvas?: unknown): void {
  const document = global.document ?? (global.document = facade);
  for (const [name, value] of Object.entries(facade)) {
    const current = document[name];
    // Existing metadata may be a read-only getter returning null/undefined.
    // Only method capabilities are judged by their value; metadata by presence.
    if (typeof value === "function" ? typeof current !== "function" : !(name in document)) {
      defineCapability(document, name, value);
    }
  }
  for (const name of ["addEventListener", "removeEventListener", "dispatchEvent"]) {
    if (typeof document[name] !== "function" && typeof facade[name] === "function")
      defineCapability(document, name, facade[name].bind(facade));
  }
  if (screenCanvas !== undefined) installScreenCanvasMembership(document, screenCanvas);
  for (const name of ["createElement", "createElementNS", "querySelectorAll", "getElementsByTagName"]) {
    if (typeof document[name] !== "function") throw new Error("Mini Game document contract missing: " + name);
  }
}

/** Mini Game has one visible canvas and no HTML element tree. */
export function installScreenCanvasMembership(document: any, screenCanvas: unknown): void {
  if (!document.body) throw new Error("Mini Game document.body is required");
  if (typeof document.body.contains !== "function") {
    const body = document.body;
    defineCapability(body, "contains", (node: unknown): boolean => node === body || node === screenCanvas);
  }
}
