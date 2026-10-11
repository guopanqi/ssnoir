import "pixi.js/unsafe-eval";
import { DOMAdapter } from "pixi.js";
import type { wechatHost } from "./host";

export function installPixiMiniGameAdapter(host: typeof wechatHost): void {
  DOMAdapter.set({
    createCanvas: (width = 2, height = 2) => host.createOffscreenCanvas(width, height),
    createImage: () => host.createImage(),
    getCanvasRenderingContext2D: () => host.getCanvas2DConstructor(),
    getWebGLRenderingContext: () => host.getGLConstructor(),
    getNavigator: () => host.getNavigator(),
    getBaseUrl: () => host.getBaseUrl(),
    getFontFaceSet: () => null,
    fetch: async () => { throw new Error("Mini Game adapter has no remote asset transport configured"); },
    parseXML: () => { throw new Error("Mini Game adapter does not support XML assets"); }
  } as Parameters<typeof DOMAdapter.set>[0]);
}
