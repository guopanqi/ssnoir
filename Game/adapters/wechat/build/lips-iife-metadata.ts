import type { Plugin } from "vite";

/** Enable only for pinned LIPS 1.0.0-beta.23.1 IIFE builds. */
export function lipsIifeMetadata(): Plugin {
  return {
    name: "patch-lips-iife-doc-metadata",
    transform(code, id) {
      if (!id.replace(/\\/g, "/").endsWith("/lips/dist/lips.esm.min.js")) return null;
      const needle = 'function is_node(){return typeof global!=="undefined"&&global.global===global}';
      if (!code.includes(needle)) throw new Error("LIPS browser host detection patch site moved");
      // This IIFE is explicitly a browser/Mini Game build. wx also exposes global.global.
      return { code: code.replace(needle, 'function is_node(){return false}'), map: null };
    },
    renderChunk(code) {
      const needle = 'return e.split("\\n").map((e) => {';
      if (!code.includes(needle)) throw new Error("LIPS IIFE doc metadata patch site moved");
      return { code: code.replace(needle, 'if (typeof e !== "string") return e; ' + needle), map: null };
    }
  };
}
