/**
 * Actual shared Three.js + PixiJS + LIPS render path in WeChat,
 * not the earlier independent Canvas2D placeholder.
 * All adaptations live in bootstrap / Pixi DOMAdapter.
 */
import { wechatHost } from "../../adapters/wechat/src/host";
import { installPixiMiniGameAdapter } from "../../adapters/wechat/src/pixi";
import { mountFoundation } from "../foundation/render";

installPixiMiniGameAdapter(wechatHost);

mountFoundation({
  canvas: wechatHost.canvas,
  context: wechatHost.context,
  width: wechatHost.width,
  height: wechatHost.height,
  animationFrame: wechatHost.animationFrame,
  cancelAnimationFrame: wechatHost.cancelAnimationFrame,
  onError: wechatHost.showFailure
}).then(session => {
  wechatHost.onVisibilityChange(visible => {
    if (visible) session.resume(); else session.pause();
    console.log("[SSNoir] Mini Game visibility=" + String(visible));
  });
  wechatHost.onTouchEnd((x, y) => {
    try { session.activateAt(x, y); }
    catch (error) { wechatHost.showFailure(error); }
  });
  Object.assign(globalThis, {
    __SSNOIR_WECHAT_FOUNDATION__: {
      getSchemeValue: session.getCount,
      getDiagnostics: wechatHost.getDiagnostics,
      getRenderDiagnostics: session.getRenderDiagnostics
    }
  });
  console.log("[SSNoir] Canvas identity " + JSON.stringify(session.getRenderDiagnostics()));
  console.log("[SSNoir] Mini Game runtime " + JSON.stringify(wechatHost.getDiagnostics()));
  console.log("[SSNoir] Three/Pixi/Scheme shared foundation mounted; initial count " + session.getCount());
}).catch(wechatHost.showFailure);
