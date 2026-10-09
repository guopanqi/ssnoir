# Evaluating community Three.js adapters, not changing production yet

**Branch**: `experiment/wechat-community-adapter`.

**Candidate**: [minisheeep/threejs-miniprogram-template, `wechat-game-ts`](https://github.com/minisheeep/threejs-miniprogram-template/tree/wechat-game-ts), commit `df8fb2af099d9ef7ada868043602dbd5470a57f1`. Candidate depends on `@minisheep/mini-program-polyfill-core@1.1.4`, `@minisheep/three-platform-adapter@2.0.4`, the independently hosted `https://npm.minisheep.cn` registry, and the build-time plugin.

The `src/wechat-community/probe.ts` entry uses **the real shared** `src/foundation/render.ts` (Three r186 + Pixi 8 + LIPS and original stdlib). No copy of the game logic. It delegates browser-global shims and WebGL canvas to the community adapter; Pixi uses the polyfilled Canvas via its official DOMAdapter.

Acceptance steps:
- [ ] Candidate dependency installation in GitHub Actions with a clean environment
- [ ] Vite plugin builds WebGL2 / Pixi 8 + LIPS (under 4MiB)
- [ ] Mock wx runtime can execute the community-adapted IIFE and increment Scheme via touch
- [ ] Same test with wx.createOffscreenCanvas unavailable, matching the actual WeChat DevTools failure
- [ ] TapTap official converter accepts the candidate build and smoke interaction still works
- [ ] Manual WeChat DevTools, Android, iOS tests

**Do not merge this adapter into the normal mini-game build until these gates pass**. In particular the private registry is a long-term maintenance/dependency risk; evaluate before committing to it. Older alternates: deepkolos/platformize targets Three r133 and Pixi 6; wechat-miniprogram/threejs-miniprogram uses Three r108 and is a Mini Program, not a Mini Game, solution.

Run from `Game/`:

```sh
npm install --no-audit --no-fund
npm run build:wechat-community
npm run smoke:wechat-community
SSNOIR_WX_CANVAS_MODE=missing npm run smoke:wechat-community
```

Automatic tests do not establish actual WeChat or TapTap device support.

## Actual evaluation result (2026-10-09)

**Status: Candidate builds, but fails runtime smoke; DO NOT merge to main yet.**

- Registry `https://npm.minisheep.cn` was reachable from a clean GitHub-hosted Linux runner. The community adapter plugin lacked a resolvable `@rollup/pluginutils` dependency; pinned `@rollup/pluginutils@5.2.0` was explicitly added for this experiment.
- Real SSNoir shared scene bundles through the community Vite plugin at about **1.42 MB uncompressed** (under 4 MiB), preserving the original shared `mountFoundation` and stdlib.
- The existing production WeChat + TapTap foundation still passes in the branch once TypeScript issues were corrected (e.g. [foundation run 37887556046](https://github.com/guopanqi/ssnoir/actions/runs/37887556046)).
- The first community run failed because the original Chromium mock lacked modern `wx.getWindowInfo` / `wx.getPerformance`. These calls are legitimate host requirements rather than proof of target incompatibility.
- The package's browser detection cannot run unmodified in a conventional Chrome `window` containing `HTMLElement`: it patches `THREEGlobals` twice. The test mock now reflects the Mini Game's DOM-less environment.
- The community polyfill does not implement `THREEGlobals.document.querySelectorAll('script')`, which the **LIPS Scheme interpreter** calls while scanning for browser script tags. A limited empty-script-list shim in `src/wechat-community/prelude.ts` lets the experiment advance without implementing generic HTML DOM.
- The current gate fails in **PixiJS 8.20.1** initialization: Pixi's optional `DOMPipe` tries to access `THREEGlobals.document.createElement('div').style.position`, but the community adapter's pseudo-element has no `style`. This extension is unnecessary for SSNoir's pure Graphics/Text UI. Calling `extensions.remove(DOMPipe)` in the game entry did not prevent the extension from loading, likely due renderer auto imports. See [community run 37888762711](https://github.com/guopanqi/ssnoir/actions/runs/37888762711). **Full scene/touch smoke has not passed.**

### Decision and next steps

**Do not replace the main branch's working platform bootstrap yet.** This is an actual compatibility failure in the browser-based mocked host, not evidence of failure on a real WeChat phone; however it is enough to withhold automatic adoption.

The next useful investigation is PixiJS 8's [documented custom extension import mode](https://pixijs.download/v8.19.0/docs/migrations.html): explicitly register the WebGL/Graphics/Text pipelines while keeping the optional HTML `DOMPipe` disabled. Alternatively test renderer initialization on the actual WeChat DevTools, where the host's native global availability can be observed. Both paths must retain the same shared SSNoir scene and test against the missing `wx.createOffscreenCanvas` behavior.

The remaining acceptance steps (Pixi Text + Three + Scheme + touch; missing offscreen Canvas; then official TapTap conversion and a converted-bundle smoke) are **still red/pending**. No production game code or TapTap converter was changed by this experiment. Last complete green production smoke from before the experiment: [37886760118](https://github.com/guopanqi/ssnoir/actions/runs/37886760118).
