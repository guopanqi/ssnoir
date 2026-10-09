# SSNoir Mini Game open-source adapter foundation

Upstream: https://github.com/finscn/weapp-adapter
Source Git tree: `324a459b9b1e347f7a24199d097950247569823b` (master)
License: MIT, copyright (c) 2018 finscn. See [LICENSE](LICENSE).

Original unmodified source files vendored here are: `src/Canvas.js`, `src/util/mixin.js`, `src/WindowProperties.js`, `src/performance.js`, `src/HTMLElement.js`, `src/Element.js`, `src/Node.js`, `src/EventTarget.js`, `src/util/index.js`. These form the actual Canvas/DOM-element/event substrate used in `Game/src/wechat/bootstrap.ts`, not an inactive reference copy.

SSNoir's delta is kept *outside* upstream files:
- Browser-free `document` factory composed from upstream `HTMLElement` and `EventTarget` (supports LIPS script scan);
- Pixi 8's `DOMAdapter`, offscreen Canvas 2D priority/fallback, and shared Three/Pixi WebGL2 context;
- wx touch, error reporting, native RAF and runtime diagnostics.

Why not import upstream `index.js` wholesale? It mutates `GameGlobal/window`, assumes 2018-era wx APIs/graphics, and indiscriminately polyfills unrelated capabilities. Our single-purpose game host uses its reusable primitives instead, retaining upstream source/license for review. This is a **selective derivative**, not a promise that upstream supports modern Pixi 8 or Three r186 without integration work.

Never silently edit the original vendored source. Place changes in `src/wechat/` or explicitly document any upstream patches and re-run web/WeChat/TapTap CI and native device acceptance. The original repository was last pushed in 2019, so upstream updates cannot be assumed.

All original copies are pinned to their upstream Git blob SHA-1s by `Game/tests/weapp-adapter.test.mjs`. These checks fail CI if any vendored byte changes unintentionally.
