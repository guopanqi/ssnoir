/**
 * LIPS scans <script type="..."> tags during browser auto-start.
 * Mini Games have no HTML document or script elements. The community
 * polyfill supplies document but omits querySelectorAll; this capability
 * is irrelevant to our explicitly instantiated Lisp interpreter.
 *
 * Keep this narrow and load before the shared foundation/LIPS imports.
 */
const doc = (globalThis as any).document;
if (doc && typeof doc.querySelectorAll !== "function") {
  doc.querySelectorAll = (selector: string) => {
    if (selector === "script") return [];
    throw new Error("Unsupported Mini Game HTML selector: " + selector);
  };
}
