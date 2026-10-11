# Phase 2 — runtime migration, first vertical slice

Stage-one platform validation was completed locally by the user. **Keep one main branch and one game implementation.**

## Current scope

The first slice ports C# `GameState`/inventory/global/rest-block/native functions and uses **the actual unchanged** SSNoir `scripts/stdlib.scm`, `scripts/engine.scm` and `scripts/theatre.scm` via separate LIPS world/encounter interpreters. Scripts are injected by their canonical path. The LIPS R7RS bootstrap is supplied as pinned compiled `lips/dist/std.xcb` bytes (read from the installed package at test/build time). This avoids a cold-start parser issue with source `std.scm`, whose reader extensions are defined during evaluation. The runtime only receives `Uint8Array` and never fetches it. Nothing fetches a script at runtime or accesses a host filesystem.

- `src/runtime/game-state.ts` owns portable primitives and a **partial** snapshot; initialization mirrors Unity's starting cash (15), cigarettes (2), medicine (1), chapter (0), etc.
- `src/runtime/native-bridge.ts` implements a named, typed, strict subset of `Engine/Runtime/Scripting/NativeFunctions.cs`: `get-global`, `set-global!`, inventory, growth, rest blockers, notification and failure.
- `src/runtime/script-session.ts` loads the exact content in C# startup order. Names not implemented are intentionally **unbound**, not silent no-ops.
- `tests/game-runtime.ts-test.mjs` runs real original Scheme item and rest-block scripts against TS native state. An error rolls back *native state*, but **not Scheme environment mutations**; do not assume actions are fully transactional until the action runner is implemented.

No duplicate editable copies of game content; Node tests read the current Unity content, and future Web/WeChat hosts will consume `?raw` build-time injected strings.

## Not yet implemented

- World scene content module orchestration, display/node conversion, dynamic scene tree
- Actor teams, dice pool, action resolution, injury/scars, encounter rollback, world day/turn
- Scheme world/encounter closure snapshots (the save model is deliberately partial)
- Theatre scene parsing/player and presentation, interaction with actual chapter content
- Native parity matrix for all 59 native functions, including unsupported visual effects
- Real device validation of stage-two script behavior

Do not call this a full C# SceneManager port. Expand via a playable story slice after the bridge is proven; retain baseline Web, Electron, WeChat and TapTap CI.

## Cross-platform integration (stage 2.1)

`src/runtime/host-session.ts` now builds a real `GameScriptSession` inside the existing shared renderer. The **original** `engine.scm` `add-item!` increments cash on the existing validation button; counter 1 starts with 15 cash, clicking advances to (2,16) and then (3,17). Both browser and WeChat/TapTap mocks assert the pair, including after resizing or hide/show. This verifies the native bridge and content load in the actual IIFE, not only in Node tests.

`vite.lips-stdlib.ts` bundles `node_modules/lips/dist/std.xcb` from the pinned npm dependency as base64; `src/runtime/base64.ts` decodes bytes without relying on `atob`, Node Buffer or a filesystem in Mini Games. The same unmodified SSNoir Scheme source is imported via Vite `?raw`; no independent game logic, extra client or runtime HTTP requests are introduced.
