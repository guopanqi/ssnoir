/**
 * Force the WeChat adapter initialization BEFORE the Pixi and LIPS
 * modules are evaluated. A static top-level import from probe.ts
 * evaluates this module first, per ESM dependency ordering.
 */
import "@minisheep/mini-program-polyfill-core/wechat-polyfill";
import "@minisheep/three-platform-adapter/wechat-game";
import { game } from "@minisheep/three-platform-adapter";

export const communityRuntime = game.useCanvas();
// The Three plugin rewrites dependency globals to THREEGlobals.document,
// which may be a different object from globalThis.document.
const globals = globalThis as any;
const documents = [globals.document, globals.THREEGlobals?.document]
  .filter((value, index, items) => value && items.indexOf(value) === index);
if (!documents.length) throw new Error("Community WeChat adapter did not provide document");
for (const doc of documents) {
  if (typeof doc.querySelectorAll !== "function") {
    // LIPS looks for HTML <script> tags, which do not exist in Mini Games.
    doc.querySelectorAll = (selector: string) => {
      if (selector === "script") return [];
      throw new Error("Unsupported Mini Game HTML selector: " + selector);
    };
  }
}
