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

const createOffscreenCanvas = (w: number, h: number) => {
  if (typeof wx.createOffscreenCanvas !== "function") {
    throw new Error("wx.createOffscreenCanvas is required by Pixi text rendering");
  }
  const result = wx.createOffscreenCanvas({ type: "2d", width: w, height: h });
  result.style ||= {};
  result.addEventListener ||= () => {};
  result.removeEventListener ||= () => {};
  return result;
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
  getGLConstructor: () => gl.constructor as typeof WebGLRenderingContext,
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
