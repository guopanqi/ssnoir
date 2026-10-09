/**
 * WeChat Mini Game does not provide standard browser globals. Supply only the
 * small canvas/event subset required by the shared Three + Pixi probe.
 * Missing capabilities must fail visibly; this is NOT a generic DOM emulator.
 */
declare const wx: any;
const g = globalThis as any;
const canvas = wx.createCanvas();
const device = wx.getSystemInfoSync();
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
      if (!canvasBackend) {
        canvasBackend = "offscreen-2d";
        console.log("[SSNoir MiniGame] Pixi 2D canvas backend: wx.createOffscreenCanvas");
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
    const result = prepare(wx.createCanvas(), "secondary wx.createCanvas");
    if (!canvasBackend) {
      canvasBackend = "secondary-canvas";
      console.log("[SSNoir MiniGame] Pixi 2D canvas backend: secondary wx.createCanvas");
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
g.navigator ||= { userAgent: "SSNoir WeChat Mini Game", platform: device.platform || "wechat" };
g.devicePixelRatio ||= device.pixelRatio || 1;
g.innerWidth ||= width;
g.innerHeight ||= height;
g.location ||= { href: "https://ssnoir.invalid/game", origin: "https://ssnoir.invalid" };
g.addEventListener ||= () => {};
g.removeEventListener ||= () => {};
g.document ||= {
  baseURI: "https://ssnoir.invalid/game",
  createElement(name: string) {
    if (name === "canvas") return createOffscreenCanvas(2, 2);
    if (name === "img") return wx.createImage();
    throw new Error("Unsupported document.createElement: " + name);
  },
  createElementNS(_ns: string, name: string) {
    if (name === "canvas") return createOffscreenCanvas(2, 2);
    throw new Error("Unsupported document.createElementNS: " + name);
  }
};
g.Image ||= wx.createImage().constructor;
g.HTMLCanvasElement ||= canvas.constructor;
g.CanvasRenderingContext2D ||= ctx.constructor;
// Do NOT alias WebGL1 and WebGL2 constructors. Pixi checks the context
// type to choose native vertex-array support vs. the WebGL1 VAO extension.
g.WebGLRenderingContext ||= class UnavailableWebGL1Context {};
g.WebGL2RenderingContext ||= gl.constructor;
g.requestAnimationFrame ||= (callback: FrameRequestCallback) => canvas.requestAnimationFrame(callback);
g.cancelAnimationFrame ||= (handle: number) => canvas.cancelAnimationFrame(handle);

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
  animationFrame: (callback: FrameRequestCallback) => canvas.requestAnimationFrame(callback) as number,
  cancelAnimationFrame: (handle: number) => canvas.cancelAnimationFrame(handle),
  onTouchEnd: (callback: (x: number, y: number) => void) => {
    wx.onTouchEnd((event: { changedTouches?: Array<{ clientX: number; clientY: number }> }) => {
      const touch = event.changedTouches?.[0];
      if (touch) callback(touch.clientX, touch.clientY);
    });
  },
  showFailure: (error: unknown) => {
    const message = String(error instanceof Error ? error.stack || error.message : error);
    console.error("[SSNoir MiniGame foundation]", message);
    if (typeof wx.showModal === "function") wx.showModal({ title: "SSNoir 技术验证失败", content: message.slice(0, 650), showCancel: false });
  }
};
