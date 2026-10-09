/**
 * Actual shared Three.js + PixiJS + LIPS render path in WeChat,
 * not the earlier independent Canvas2D placeholder.
 * All adaptations live in bootstrap / Pixi DOMAdapter.
 */
import { wechatHost } from "./bootstrap";
import "pixi.js/unsafe-eval";
import { DOMAdapter } from "pixi.js";
import { mountFoundation } from "../foundation/render";

DOMAdapter.set({
  createCanvas: (width = 2, height = 2) => wechatHost.createOffscreenCanvas(width, height),
  createImage: () => wechatHost.createImage(),
  getCanvasRenderingContext2D: () => wechatHost.getCanvas2DConstructor(),
  getWebGLRenderingContext: () => wechatHost.getGLConstructor(),
  getNavigator: () => wechatHost.getNavigator(),
  getBaseUrl: () => wechatHost.getBaseUrl(),
  getFontFaceSet: () => null,
  fetch: async () => { throw new Error("Remote assets not supported in foundation probe"); },
  parseXML: () => { throw new Error("XML not supported in foundation probe"); }
} as Parameters<typeof DOMAdapter.set>[0]);

mountFoundation({
  canvas: wechatHost.canvas,
  context: wechatHost.context,
  width: wechatHost.width,
  height: wechatHost.height,
  animationFrame: wechatHost.animationFrame,
  cancelAnimationFrame: wechatHost.cancelAnimationFrame,
  onError: wechatHost.showFailure
}).then(session => {
  wechatHost.onTouchEnd((x, y) => {
    try { session.activateAt(x, y); }
    catch (error) { wechatHost.showFailure(error); }
  });
  Object.assign(globalThis, {
    __SSNOIR_WECHAT_FOUNDATION__: {
      getSchemeValue: session.getCount,
      getDiagnostics: wechatHost.getDiagnostics
    }
  });
  console.log("[SSNoir] Mini Game runtime " + JSON.stringify(wechatHost.getDiagnostics()));
  console.log("[SSNoir] Three/Pixi/Scheme shared foundation mounted; initial count " + session.getCount());
}).catch(wechatHost.showFailure);
