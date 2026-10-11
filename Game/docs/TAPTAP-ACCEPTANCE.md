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
the user's current VS Code debug target (observed 2026-10-11). The previous
Unity project's AppID is a different target and is not used by this package.
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

### Startup blocked on iOS: isolation probe

On 2026-10-11 the user reported TapTap iOS App 5.34.2 still showing only
“当前小程序不可用” after the AppID fix and startup-error entry. There is no
evidence yet that `game.js` executed; absence of a modal alone does not prove
a platform rejection. The local macOS Console showed no connected iPhone.

Run `node scripts/build-taptap.mjs --host-probe` to generate the separate
`dist/taptap-host-probe/game.zip` (1,873 bytes at this checkpoint). It retains
the same app/manifest/converter but contains only ES5 host API calls: display
a startup modal and draw text on a 2D canvas. It loads no renderer, Scheme,
adapter or asset. Converted script execution with mocked `wx` and `tap` both
produced the modal and canvas text; device execution remains pending.

For a failure before the game menu is accessible, the official iOS diagnostic
path is USB connection to macOS Console, select the iPhone, start streaming,
and filter subsystem `com.taptap.instantGame` while reproducing the scan:
https://developer.taptap.cn/minigameapidoc/dev/dev-support/debugging/

The user subsequently clarified that every scan used the VS Code debugger's
QR code, not a developer-backend uploaded version. The visible debugger served
`dist/taptap/game`, so the separate probe was never exercised. Its plugin log
contained repeated repacks but **no download request** at inspection time.
The next gate is phone-to-Mac access to the displayed `/download` address.
The plugin reads `game.json.appId` (camel-case); builds now write it from the
same TapTap configuration and validate both IDs match, preventing rebuilds
from erasing the debugger target and requiring another manual ID entry.

The direct host-probe QR was subsequently downloaded by the iPhone; the user
reported the expected “入口已执行，未加载技术底座” modal. This establishes
execution of the minimal converted entry on the actual TapTap iOS host.
It does not establish WebGL2 or library initialization.

`node scripts/build-taptap.mjs --startup-probe` creates a separate
`dist/taptap-startup-probe/game.zip` with the complete foundation and a startup
modal that delays `require('./foundation.js')` until confirmation. The converted
entry was checked with a simulated require failure: it performed no import
before confirmation and displayed the caught error afterward. Device result
for this second probe remains pending. Normal release entry is unchanged.

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
