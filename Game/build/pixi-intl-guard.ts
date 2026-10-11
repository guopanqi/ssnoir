import type { Plugin } from "vite";

/** Pixi 8.20.1's Segmenter fallback must also handle an absent Intl global. */
export function pixiIntlGuard(): Plugin {
  return {
    name: "pixi-absent-intl-guard",
    transform(code, id) {
      if (!id.endsWith("/pixi.js/lib/scene/text/canvas/CanvasTextMetrics.mjs")) return;
      const needle = 'typeof Intl?.Segmenter === "function"';
      if (code.split(needle).length !== 2) {
        throw new Error("Pixi Intl guard site changed; review the dependency before building");
      }
      return {
        code: code.replace(needle, 'typeof Intl !== "undefined" && typeof Intl.Segmenter === "function"'),
        map: null
      };
    }
  };
}
