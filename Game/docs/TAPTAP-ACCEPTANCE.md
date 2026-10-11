# TapTap Mini Game — phase-one converter acceptance

## Architecture

`Game/src/foundation/render.ts` remains the **single** render+Scheme implementation.

```text
Game/source ──> Vite WeChat IIFE (dist/wechat)
                         │
                         ▼
             Official converter v2.0.5
                         │
                         ▼
                dist/taptap/game.zip
```

The official user-supplied `wx_converter.py` was tested directly against SSNoir's prior successful WeChat artifact; it completed all ten conversion stages, and generated a ZIP containing `game.js`, `game.json`, `project.config.json`, and `check-version.js`. No Unity plugin was required for this zero-plugin input. The first locally generated archive was **425,818 bytes**, from a 1.29MB WeChat script. This is **conversion success only**, not runtime/device certification.

### Build and tests

Run from `Game/`:

```sh
npm install
npm run build:taptap
npm run smoke:taptap-harness
```

The converter input uses `platforms/taptap/project.config.json`, whose AppID is
the existing SSNoir TapTap ID from `UnityClient/Assets/TapTapMiniGame/Editor/MiniGameConfig.asset`.
It never uses the WeChat tourist or local test-account AppID. This matches the
working Laya project at `/Users/usr/Documents/laya_projects/flood`: its release
script switches to the TapTap AppID before calling the same converter. The
converter preserves that configuration; it does not replace AppIDs itself.
The earlier SSNoir ZIP incorrectly contained `touristappid`. This packaging
error is fixed; whether it explains the client's unavailable message still
requires testing the corrected ZIP on the device.

The TapTap entry now loads `foundation.js` through a small independent
`game.js`, matching the working Laya package's module-loading structure.
It logs `entry reached` before loading renderer dependencies, registers
`wx.onError`, and catches synchronous initialization and module parsing errors
with a modal. A platform rejection before `game.js` executes cannot display
this modal. The ZIP now contains five files; the shared renderer bundle is
still generated from the same WeChat source and converted by the vendor tool.

`build:taptap` runs the submitted converter source (SHA-256 verified) with the actual WeChat bundle, then validates syntax, ZIP structure, version, orientation, presence of original runtime injection, and a 20MB ZIP budget. `smoke:taptap-harness` executes the **converted game.js** under Chromium with a mocked `wx` / `GameGlobal` host and checks the shared LIPS/Three/Pixi touch action. Both are included in GitHub Actions, with `game.zip` uploaded as an artifact.

### True target-runtime gates (still pending)

- [ ] TapTap developer demo/runtime actually loads this ZIP, with its required `weixinminigame` compatibility mode
- [ ] TapTap Android device: WebGL2 render, Chinese fonts, touch, background/foreground
- [ ] TapTap iOS device: same, plus memory and base library/API differences if available
- [ ] SDK integration decisions: whether the compatibility host provides the `wx` namespace, and when TapTap-native `tap` APIs need adapters
- [ ] Production metadata (game ID, company/product names, release version) before store upload

**Important:** This vendor converter injects `GameGlobal.fetch = undefined` and copies a `check-version.js` originally intended for the MiniHost/Unity ecosystem. Our app imports no Unity plugins, but WebGL2 / DOM emulation on an actual TapTap Mini Game remains unproven. A passing converter and Chromium mock test **cannot prove** publication/runtime compatibility.

TapTap official docs describe platform-specific adapters for JavaScript engines and define ZIP upload and size restrictions:

- https://developer.taptap.cn/minigameapidoc/dev/engine/Cocos-Laya-Egret/
- https://developer.taptap.cn/minigameapidoc/tap-operation/operation-standards/review-standards/

The WeChat Canvas compatibility adapter now supports environments without `wx.createOffscreenCanvas`. CI checks this fallback against the converted TapTap output too, but native TapTap Canvas availability still requires device confirmation.
