/**
 * Force the WeChat adapter initialization BEFORE the Pixi and LIPS
 * modules are evaluated. A static top-level import from probe.ts
 * evaluates this module first, per ESM dependency ordering.
 */
import "@minisheep/mini-program-polyfill-core/wechat-polyfill";
import "@minisheep/three-platform-adapter/wechat-game";
import { game } from "@minisheep/three-platform-adapter";

export const communityRuntime = game.useCanvas();
const doc = (globalThis as any).document;
if (!doc) throw new Error("Community WeChat adapter did not provide document");
if (typeof doc.querySelectorAll !== "function") {
  // LIPS looks for HTML <script> tags, which do not exist in Mini Games.
  doc.querySelectorAll = (selector: string) => {
    if (selector === "script") return [];
    throw new Error("Unsupported Mini Game HTML selector: " + selector);
  };
}
