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

- [ ] Import `Game/dist/wechat` as a **Mini Game** in WeChat Developer Tools, using your own AppID; capture console/version/renderer output.
- [ ] Android WeChat device: render, Pixi Chinese text, tap, repeat after background/foreground, verify memory and shader errors.
- [ ] iOS WeChat device: same acceptance; include tested phone, OS, WeChat, and base library versions.
- [ ] Native Windows and macOS desktop Electron smoke (outside Linux Xvfb); Steamworks integration is a later release-platform task.

Browser emulation **cannot** establish that the actual WeChat runtime exposes the same browser APIs. Do not approve migration of all production UI on the basis of mock tests alone.

## Boundaries

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
