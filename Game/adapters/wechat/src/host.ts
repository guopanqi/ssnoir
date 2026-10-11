/**
 * WeChat host built on open-source finscn/weapp-adapter (MIT).
 * See Game/adapters/wechat/vendor/weapp-adapter/README-SSNOIR.md for frozen upstream provenance.
 * Upstream supplies Canvas, DOM element and EventTarget implementations.
 * Only SSNoir's Three r186 / Pixi 8 and LIPS integration is local.
 */
// Vendored Apache-2.0 UTF-8 polyfill; preserves native implementations.
import "./text-encoding";
import { installMiniGameDocument } from "./document";
// @ts-expect-error -- vendored MIT JavaScript has no TypeScript declarations
import UpstreamPerformance from "../vendor/weapp-adapter/src/performance.js";
// @ts-expect-error -- vendored MIT JavaScript has no TypeScript declarations
import UpstreamCanvas from "../vendor/weapp-adapter/src/Canvas.js";
// @ts-expect-error -- vendored MIT JavaScript has no TypeScript declarations
import UpstreamHTMLElement from "../vendor/weapp-adapter/src/HTMLElement.js";
// @ts-expect-error -- vendored MIT JavaScript has no TypeScript declarations
import UpstreamEventTarget from "../vendor/weapp-adapter/src/EventTarget.js";
declare const wx: any;
const g = globalThis as any;
// Browser dependencies require a millisecond performance clock before initialization.
g.performance ||= UpstreamPerformance;
// FIRST wx.createCanvas() must be reserved for the visible stage.
const canvas = new UpstreamCanvas();
const device = wx.getSystemInfoSync();
console.log("[MiniGame] adapter=finscn/weapp-adapter+host; platform=" +
  String(device.platform) + "; wxOffscreen=" + String(typeof wx.createOffscreenCanvas));
const width = Math.max(1, device.windowWidth);
const height = Math.max(1, device.windowHeight);
canvas.width = width;
canvas.height = height;
canvas.style = canvas.style || {};
canvas.addEventListener ||= () => {};
canvas.removeEventListener ||= () => {};
canvas.getBoundingClientRect ||= () => ({ x: 0, y: 0, left: 0, top: 0, width: canvas.width, height: canvas.height });

// WeChat Mini Game Canvas contract: the FIRST wx.createCanvas() is on-screen;
// every subsequent wx.createCanvas() is off-screen. Some DevTools builds omit
// createOffscreenCanvas despite supporting secondary 2D canvases.
let canvasBackend: "offscreen-2d" | "secondary-canvas" | undefined;
const createOffscreenCanvas = (w: number, h: number): HTMLCanvasElement => {
  const errors: string[] = [];
  const prepare = (candidate: any, method: string): HTMLCanvasElement => {
    if (!candidate || candidate === canvas || typeof candidate.getContext !== "function") {
      throw new Error(method + " returned an invalid or on-screen canvas");
    }
    candidate.width = w;
    candidate.height = h;
    if (!candidate.getContext("2d")) {
      throw new Error(method + " did not provide a 2D rendering context");
    }
    candidate.style ||= {};
    candidate.addEventListener ||= () => {};
    candidate.removeEventListener ||= () => {};
    candidate.getBoundingClientRect ||= () => ({
      x: 0, y: 0, left: 0, top: 0, width: candidate.width, height: candidate.height
    });
    return candidate as HTMLCanvasElement;
  };
  if (typeof wx.createOffscreenCanvas === "function") {
    try {
      const result = prepare(wx.createOffscreenCanvas({ type: "2d", width: w, height: h }), "wx.createOffscreenCanvas");
      if (canvasBackend !== "offscreen-2d") {
        canvasBackend = "offscreen-2d";
        console.log("[MiniGame] Pixi 2D canvas backend: wx.createOffscreenCanvas");
      }
      return result;
    } catch (error) {
      errors.push("createOffscreenCanvas: " + String(error));
    }
  } else {
    errors.push("createOffscreenCanvas: unavailable");
  }
  // Do NOT create the on-screen canvas here: it must have been allocated above.
  try {
    if (typeof wx.createCanvas !== "function") throw new Error("wx.createCanvas unavailable");
    const result = prepare(new UpstreamCanvas(), "secondary wx.createCanvas");
    if (canvasBackend !== "secondary-canvas") {
      canvasBackend = "secondary-canvas";
      console.warn("[MiniGame] Pixi 2D canvas fallback: secondary wx.createCanvas", errors.join("; "));
    }
    return result;
  } catch (error) {
    errors.push("secondary createCanvas: " + String(error));
  }
  throw new Error("WeChat cannot create an off-screen 2D canvas for Pixi text. " + errors.join("; "));
};
const offscreen = createOffscreenCanvas(2, 2);
const ctx = offscreen.getContext("2d");
if (!ctx) throw new Error("WeChat offscreen 2D context unavailable");
const gl = canvas.getContext("webgl2", { stencil: true, antialias: true });
if (!gl) throw new Error("WeChat WebGL2 is required for PixiJS 8");

g.window ||= g;
g.navigator ||= { userAgent: "Mini Game adapter", platform: device.platform || "wechat" };
g.devicePixelRatio ||= device.pixelRatio || 1;
g.innerWidth ||= width;
g.innerHeight ||= height;
g.location ||= { href: "https://minigame.invalid/game", origin: "https://minigame.invalid" };
g.addEventListener ||= () => {};
g.removeEventListener ||= () => {};
// Upstream EventTarget/HTMLElement handle DOM-like event and element semantics.
// Only the Mini Game document factory and LIPS's HTML script scan are specialized.
const miniDocument = Object.assign(new UpstreamEventTarget(), {
  baseURI: "https://minigame.invalid/game",
  readyState: "complete",
  scripts: [] as unknown[],
  head: new UpstreamHTMLElement("head"),
  body: new UpstreamHTMLElement("body"),
  createElement(name: string): any {
    if (name.toLowerCase() === "canvas") return createOffscreenCanvas(2, 2);
    if (name.toLowerCase() === "img") return wx.createImage();
    return new UpstreamHTMLElement(name);
  },
  createElementNS(_ns: string, name: string): any { return this.createElement(name); },
  querySelectorAll(name: string): any[] {
    if (name === "script") return []; // No HTML script tags in a Mini Game
    if (name === "head") return [this.head];
    if (name === "body") return [this.body];
    return [];
  },
  getElementsByTagName(name: string): any[] { return this.querySelectorAll(name); }
});
// TapTap provides a document object without the DOM methods LIPS needs.
// Preserve existing host methods while completing the declared DOM contract.
installMiniGameDocument(g, miniDocument, canvas);
g.HTMLElement ||= UpstreamHTMLElement;
g.Image ||= wx.createImage().constructor;
g.HTMLCanvasElement ||= canvas.constructor;
g.CanvasRenderingContext2D ||= ctx.constructor;
// Do NOT alias WebGL1 and WebGL2 constructors. Pixi checks the context
// type to choose native vertex-array support vs. the WebGL1 VAO extension.
g.WebGLRenderingContext ||= class UnavailableWebGL1Context {};
g.WebGL2RenderingContext ||= gl.constructor;
const raf = typeof canvas.requestAnimationFrame === "function"
  ? canvas.requestAnimationFrame.bind(canvas) : g.requestAnimationFrame?.bind(g);
const caf = typeof canvas.cancelAnimationFrame === "function"
  ? canvas.cancelAnimationFrame.bind(canvas) : g.cancelAnimationFrame?.bind(g);
if (!raf || !caf) throw new Error("Mini Game requestAnimationFrame/cancelAnimationFrame unavailable");
g.requestAnimationFrame ||= raf;
g.cancelAnimationFrame ||= caf;

// Register before asynchronous Scheme/Pixi initialization so an early hide
// cannot be lost. Subscribers receive the current state immediately.
let visible = true;
let visibilityListener: ((visible: boolean) => void) | undefined;
wx.onHide(() => { visible = false; visibilityListener?.(visible); });
wx.onShow(() => { visible = true; visibilityListener?.(visible); });

export const wechatHost = {
  canvas: canvas as HTMLCanvasElement,
  context: gl as WebGL2RenderingContext,
  width,
  height,
  createOffscreenCanvas,
  createImage: () => wx.createImage(),
  getCanvas2DConstructor: () => ctx.constructor as typeof CanvasRenderingContext2D,
  getGLConstructor: () => g.WebGLRenderingContext as typeof WebGLRenderingContext,
  getNavigator: () => g.navigator,
  getBaseUrl: () => g.location.href as string,
  getDiagnostics: () => ({
    adapter: "finscn/weapp-adapter + host",
    platform: String(device.platform ?? "unknown"),
    window: [width, height],
    canvas2D: canvasBackend,
    hasOffscreenAPI: typeof wx.createOffscreenCanvas === "function",
    webgl2: Boolean(gl),
    visible,
    webglVersion: String(gl.getParameter(gl.VERSION) ?? "unknown")
  }),
  animationFrame: (callback: FrameRequestCallback) => raf(callback) as number,
  cancelAnimationFrame: (handle: number) => caf(handle),
  onVisibilityChange: (callback: (visible: boolean) => void) => {
    visibilityListener = callback;
    callback(visible);
  },
  onTouchEnd: (callback: (x: number, y: number) => void) => {
    wx.onTouchEnd((event: { changedTouches?: Array<{ clientX: number; clientY: number }> }) => {
      const touch = event.changedTouches?.[0];
      if (touch) callback(touch.clientX, touch.clientY);
    });
  },
  showFailure: (error: unknown) => {
    const detail = error as { name?: string; message?: string; errMsg?: string; stack?: string } | null;
    const reason = detail?.message || detail?.errMsg || String(error);
    const message = String(detail?.name || "Error") + ": " + reason +
      (detail?.stack ? "\n" + detail.stack : "");
    console.error("[MiniGame runtime]", message);
    if (typeof wx.showModal === "function") wx.showModal({ title: "小游戏运行失败", content: message.slice(0, 650), showCancel: false });
  }
};
