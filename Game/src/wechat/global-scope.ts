// Host globals must exist before browser-style dependency side effects execute.
const hostGlobal = globalThis as unknown as { window?: unknown };
hostGlobal.window ||= hostGlobal;
export const hadNativeTextEncoder = typeof globalThis.TextEncoder === "function";
