# SSNoir Game — phase-one acceptance and handoff

**Goal:** validate a single TypeScript / Scheme / Three.js / PixiJS game foundation across web, desktop, and the WeChat Mini Game host *before* porting SSNoir's C# game rules.

## Current automated gates

| Gate | Evidence | Scope |
| --- | --- | --- |
| TypeScript + Scheme tests | `npm run verify` | LIPS VM isolation, closures, macros, original unmodified `stdlib.scm`, typecheck and builds |
| Shared WebGL2 scene / Pixi UI | `npm run capture` | Chromium 1024x576 mouse + 812x375 simulated mobile touch, actual Scheme counter increment, PNG artifacts |
| WeChat bundle | `npm run build:wechat` | Single IIFE, syntax check, 4 MiB entry limit, original Scheme source bundled at build time |
| Mocked WeChat host | `npm run smoke:wechat-harness` | Actual `dist/wechat/game.js` under mocked `wx`, WebGL2 and touch route exercised in Chromium |
| Electron | `xvfb-run -a npm run smoke:desktop` | Same Web build renders under Linux Electron; smoke process alone disables sandbox |

All are run in [ssnoir-game-foundation.yml](../../.github/workflows/ssnoir-game-foundation.yml) and surfaced as commit status `ssnoir/game-foundation`. A **green CI means the automated scope only**.

## Evidence of real platform compatibility still required

- [x] Import `Game/dist/wechat` as a **Mini Game** in WeChat Developer Tools, using a test AppID; console/version/renderer and input observed on macOS Stable 2.02.2608080 / base library 3.17.3. See the device record.
- [ ] Android WeChat device: render, Pixi Chinese text, tap, repeat after background/foreground, verify memory and shader errors.
- [ ] iOS WeChat device: same acceptance; include tested phone, OS, WeChat, and base library versions.
- [ ] Native Windows and macOS desktop Electron smoke (outside Linux Xvfb); Steamworks integration is a later release-platform task.

Browser emulation **cannot** establish that the actual WeChat runtime exposes the same browser APIs. Do not approve migration of all production UI on the basis of mock tests alone.

## Boundaries

2026-10-11 local automated evidence: `npm run verify` passed all 15 tests, typecheck and both builds. `npm run capture` passed desktop, 812×375 and 640×480 touch plus resize; reviewed the generated Chinese text/card screenshots. All three WeChat mock Canvas modes and both TapTap mock Canvas modes passed. Official TapTap conversion produced a validated 425,804-byte ZIP with four files. `npm run smoke:desktop` passed on macOS with Electron 39.0.0; this command uses SwiftShader and `--no-sandbox`, and proves initialization/count 1 only. It does not close native desktop graphics/interaction or Windows acceptance.

The local Playwright download previously stalled with Node 26.10.0; the exact expected headless browser build 1187 installed successfully using bundled Node 24.19.0. Electron's cached official archive matched its pinned SHA-256 and was extracted with macOS `ditto` to repair an incomplete binary installation. These are local dependency repairs, not application changes.

- The prototype uses **one** `src/foundation/render.ts` for Web, WeChat, and desktop, with per-host canvas/input adapters.
- `LipsSession` is a phase-one compatibility wrapper, *not* a full SceneManager or native-game-function bridge.
- Only `stdlib.scm` is evaluated, not SSNoir's entire `engine.scm` / `world.scm`; those belong to phase two.
- Unity/C# stays in version control for parity tests until the migration can replace it; no second long-term client is planned.
- LIPS IIFE documentation-metadata build guard is pinned and must be checked whenever dependencies change; see [SCHEME-CANDIDATES.md](SCHEME-CANDIDATES.md).

## How to continue

1. Inspect the latest run under [Game Foundation Actions](https://github.com/guopanqi/ssnoir/actions/workflows/ssnoir-game-foundation.yml) and the `ssnoir-game-foundation` artifact before making new changes.
2. Fix failures against actual logs, save screenshots, and keep the **entire run red** until all required automated checks pass.
3. If simulated wx tests pass, proceed to real WeChat developer tool / device acceptance without claiming mobile compatibility early.
4. After platform compatibility approval, start phase two (Scheme native API and C# logic parity tests) in this same `Game/` project.

## Added target: TapTap Mini Game

WeChat build -> official TapTap WeixinGameConverter 2.0.5 -> `game.zip`; see [TAPTAP-ACCEPTANCE.md](TAPTAP-ACCEPTANCE.md). CI conversion/package/mock tests are distinct from still-pending TapTap native runtime tests.

## WeChat Developer Tools real-world regression (October 9, 2026)

Reported on macOS Developer Tools mg 2.02.2608080, base library 3.17.4: `wx.createOffscreenCanvas` was undefined during Pixi text adapter initialization. The HarmonyOS 3.7.0 message preceding it is unrelated informational text.

`src/wechat/bootstrap.ts` now prefers `wx.createOffscreenCanvas({type:'2d'})` and, when unavailable or unable to create a 2D context, uses **a subsequent `wx.createCanvas()`**, which the Mini Game Canvas contract defines as off-screen after the first on-screen allocation. It validates the 2D context and never aliases the screen canvas. CI tests both the missing API and a throwing API, including a converted TapTap bundle. This is a mitigation candidate; the original reporter must retry Developer Tools to establish actual target compatibility. The next possible runtime gate is whether `getContext('webgl2')` is supported in their environment.

## Open-source adapter baseline

WeChat now uses a selective vendored MIT [finscn/weapp-adapter](../vendor/weapp-adapter/README-SSNOIR.md) Canvas/HTMLElement/EventTarget base, with the SSNoir-specific Pixi 8, Three r186 and Scheme integration confined to `src/wechat/bootstrap.ts`. The upstream repository and license are pinned and unmodified. Platform compatibility remains subject to CI and actual WeChat/TapTap device testing; the upstream's 2019 vintage is a known limitation.

**Device test handoff:** [WECHAT-DEVICE-ACCEPTANCE.md](WECHAT-DEVICE-ACCEPTANCE.md) defines the macOS DevTools, iOS, Android and TapTap acceptance steps and the `getDiagnostics()` runtime probe. Do not mark native compatibility complete without device evidence.

## 2026-10-11 前后台帧循环整合

共享渲染层现在显式提供 pause/resume，微信在异步初始化前注册 onHide/onShow 并保留当前可见状态；浏览器使用 visibilitychange。重复 show 不增加帧循环，取消后的旧回调即使迟到也不会重新调度；销毁后不能恢复。街道材质随场景销毁。

本次 `npm run verify` 的 17 项测试、三个浏览器尺寸/触摸/resize capture、微信三种 Canvas 模式、TapTap 两种模式全部通过。模拟宿主实际检查 hide 后待执行帧为 0、连续 show 后为 1、恢复点击计数为 3。TapTap 官方转换后 ZIP 为 425,977 字节、四个文件。这些证明共享循环和适配绑定的自动检查，不代替实际后台事件验收。

微信开发者工具已执行新包并显示 visibility=true 与 shared foundation mounted。工具同时报告 `worker path empty`，堆栈位于 app.asar；目前来源和复现条件未确定，不通过添加虚假 worker 配置掩盖。实际 hide/show 回调及返回后输入仍待观察。自动窗口操作多次返回 noWindowsAvailable 或用户切换窗口，因此已请求用户在工具中完成该步骤。
