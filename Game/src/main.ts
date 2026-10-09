import { mountFoundation } from "./foundation/render";

function requireElement<T extends Element>(selector: string): T {
  const value = document.querySelector<T>(selector);
  if (!value) throw new Error("Required Web element missing: " + selector);
  return value;
}
const canvas = requireElement<HTMLCanvasElement>("#game");
const errorLabel = requireElement<HTMLElement>("#error");

async function start(): Promise<void> {
  const session = await mountFoundation({
    canvas,
    width: window.innerWidth,
    height: window.innerHeight,
    animationFrame: callback => requestAnimationFrame(callback),
    cancelAnimationFrame: handle => cancelAnimationFrame(handle)
  });
  // Browser and WX both normalize their native input to the same virtual-canvas hit test.
  // Do not assume Pixi's own DOM event system owns the Three.js WebGL canvas.
  canvas.addEventListener("pointerup", event => {
    const rect = canvas.getBoundingClientRect();
    const x = (event.clientX - rect.left) * canvas.width / rect.width;
    const y = (event.clientY - rect.top) * canvas.height / rect.height;
    session.activateAt(x, y);
  });
  Object.assign(window, {
    __SSNOIR_FOUNDATION__: { getSchemeValue: session.getCount }
  });
  window.addEventListener("resize", () => session.resize(window.innerWidth, window.innerHeight));
  window.addEventListener("beforeunload", () => session.dispose(), { once: true });
}
start().catch(error => {
  errorLabel.textContent = "Foundation failed: " + String(error?.stack ?? error);
});
