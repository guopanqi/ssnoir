# WeChat Mini Game — developer tools and device acceptance

This is the manual release gate for the **single main-branch** SSNoir game. CI can validate mocked wx API contracts but cannot certify the real WeChat graphics runtime.

## Build/import

1. Use a checkout of `main`, run `cd Game && npm install && npm run build:wechat`.
2. Import **`Game/dist/wechat`**, not `Game/dist/web`, as a **小游戏** project in WeChat Developer Tools. Use your own AppID for device previews; the committed tourist AppID is for local experiments only.
3. Set landscape mode according to the committed `game.json`. Record Developer Tools version, base library version, device/OS and WeChat version.

## What to look for

1. The console should show `[SSNoir] Mini Game adapter=finscn/weapp-adapter+SSNoir`, followed by `Pixi 2D canvas backend` or `Pixi 2D canvas fallback`.
2. It must render the eight simple 3D city buildings and the Pixi Chinese text/card, **not just a blank canvas**.
3. It must show `Scheme 计算结果：1`. Tap **执行 Scheme 计数**; it should become `2`. Repeat once, check `3`.
4. On console supporting expressions, inspect `globalThis.__SSNOIR_WECHAT_FOUNDATION__?.getDiagnostics()` for `canvas2D`, `webglVersion` and API presence. The diagnostic does not contain identifiers or credentials.
5. Switch to background and return, verify the scene continues and touch still works; repeat after a cold restart.
6. Repeat once on Android and once on iOS, with native WeChat rather than just the macOS simulator.
7. For TapTap, use the **same** build via `npm run build:taptap` and the official converter output `dist/taptap/game.zip`; test the generated package, not an independent copy of the game.

## Reporting a failure

Send: device/tool version, base library, OS, whether the scene or Chinese fonts are visible, the last `[SSNoir]` console message, and the **first** red exception with the stack. Screenshots are sufficient for on-device errors. A HarmonyOS compatibility announcement is informational, not an exception.

Important interpretation:
- `wx.createOffscreenCanvas is required`: outdated build; ensure your `dist/wechat/game.js` comes from recent `main`.
- `canvas2D: secondary-canvas`: expected fallback when the offscreen API is absent; *not* itself an error.
- `WebGL2 is required` or stencil/shader failure: capture device graphics context details. Do not silently downgrade to WebGL1 because PixiJS 8 currently shares WebGL2 with Three.
- Failure after initial counter: investigate input/coordinate mapping or LIPS runtime, not just the canvas API.

Automatic green CI and successful TapTap conversion **do not** close these native device gates.
