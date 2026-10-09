import { mountFoundation } from "./foundation/render";

const canvas = document.querySelector<HTMLCanvasElement>("#game");
const errorLabel = document.querySelector<HTMLElement>("#error");
if (!canvas || !errorLabel) throw new Error("Missing Web canvas / diagnostics element.");

async function start(): Promise<void> {
  const session = await mountFoundation({
    canvas,
    width: window.innerWidth,
    height: window.innerHeight,
    animationFrame: callback => requestAnimationFrame(callback),
    cancelAnimationFrame: handle => cancelAnimationFrame(handle)
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
