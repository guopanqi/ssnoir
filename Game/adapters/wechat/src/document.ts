/** Install the explicitly supported DOM subset, including partial host objects. */
export function installMiniGameDocument(global: any, facade: Record<string, any>, screenCanvas?: unknown): void {
  const document = global.document ?? (global.document = facade);
  for (const [name, value] of Object.entries(facade)) {
    const current = document[name];
    if (current == null || (typeof value === "function" && typeof current !== "function")) {
      document[name] = value;
    }
  }
  for (const name of ["addEventListener", "removeEventListener", "dispatchEvent"]) {
    if (typeof document[name] !== "function" && typeof facade[name] === "function")
      document[name] = facade[name].bind(facade);
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
    body.contains = (node: unknown): boolean => node === body || node === screenCanvas;
  }
}
