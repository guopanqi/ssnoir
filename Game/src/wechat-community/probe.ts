/**
 * Independent evaluation of community Mini Game runtime, NOT production.
 * No handwritten document/window emulator is allowed in this entry point.
 * Reuses the exact same SSNoir Three + Pixi + LIPS foundation as production.
 * Reference: github.com/minisheeep/threejs-miniprogram-template (wechat-game-ts).
 */
import { communityRuntime as runtime } from "./prelude";
import "pixi.js/unsafe-eval";
import { DOMAdapter, DOMPipe, extensions } from "pixi.js";
import { mountFoundation } from "../foundation/render";

declare const wx: any;
const g = globalThis as any;
// Mini Games have no HTML elements, and SSNoir does not use DOMContainer.
// Disable Pixi's optional DOM render pipe instead of fabricating div/appendChild.
extensions.remove(DOMPipe);
try {
  const canvas = runtime.canvas as HTMLCanvasElement;
  const system = wx.getSystemInfoSync();
  const width = Math.max(1, system.windowWidth);
  const height = Math.max(1, system.windowHeight);
  canvas.width = width;
  canvas.height = height;
  const gl = canvas.getContext("webgl2", { antialias: true, stencil: true });
  if (!gl) throw new Error("community adapter canvas lacks WebGL2");
  g.WebGL2RenderingContext ||= gl.constructor;

  const create2D = (w = 2, h = 2): HTMLCanvasElement => {
    // The polyfill owns document.createElement, not SSNoir.
    const c = g.document.createElement("canvas") as HTMLCanvasElement;
    if (c === canvas) throw new Error("polyfill returned on-screen Canvas for Pixi glyphs");
    c.width = w;
    c.height = h;
    if (!c.getContext("2d")) throw new Error("polyfill has no 2D Canvas for Pixi text");
    return c;
  };
  const sample2D = create2D();
  DOMAdapter.set({
    createCanvas: create2D,
    createImage: () => new g.Image(),
    getCanvasRenderingContext2D: () => sample2D.getContext("2d")!.constructor,
    getWebGLRenderingContext: () => g.WebGLRenderingContext || class WebGL1NotSupported {},
    getNavigator: () => g.navigator,
    getBaseUrl: () => g.location?.href || "https://ssnoir.invalid/game",
    getFontFaceSet: () => null,
    fetch: async () => { throw Error("remote assets are not tested by this probe"); },
    parseXML: () => { throw Error("XML not tested by this probe"); }
  } as Parameters<typeof DOMAdapter.set>[0]);

  mountFoundation({
    canvas, context: gl, width, height,
    animationFrame: cb => (canvas as any).requestAnimationFrame(cb),
    cancelAnimationFrame: id => (canvas as any).cancelAnimationFrame(id),
    onError: error => console.error("[SSNoir community adapter]", error)
  }).then(session => {
    const notifyTouch = (event: any) => {
      runtime.eventHandler(event);
      const touch = event.changedTouches?.[0];
      if (touch) session.activateAt(touch.clientX, touch.clientY);
    };
    wx.onTouchStart(runtime.eventHandler);
    wx.onTouchMove(runtime.eventHandler);
    wx.onTouchCancel(runtime.eventHandler);
    wx.onTouchEnd(notifyTouch);
    g.__SSNOIR_WECHAT_FOUNDATION__ = { getSchemeValue: session.getCount };
    console.log("[SSNoir] community adapter mounted; count " + session.getCount());
  }).catch(error => console.error("[SSNoir community adapter] render failed", error));
} catch (error) {
  console.error("[SSNoir community adapter] bootstrap failed", error);
  throw error;
}
