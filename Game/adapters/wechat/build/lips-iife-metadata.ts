import type { Plugin } from "vite";

/** Enable only for pinned LIPS 1.0.0-beta.23.1 IIFE builds. */
export function lipsIifeMetadata(): Plugin {
  return {
    name: "patch-lips-iife-doc-metadata",
    renderChunk(code) {
      const needle = 'return e.split("\\n").map((e) => {';
      if (!code.includes(needle)) throw new Error("LIPS IIFE doc metadata patch site moved");
      return { code: code.replace(needle, 'if (typeof e !== "string") return e; ' + needle), map: null };
    }
  };
}
