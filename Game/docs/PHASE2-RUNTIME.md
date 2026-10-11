# Phase 2 — runtime migration, first vertical slice

Stage-one platform validation was completed locally by the user. **Keep one main branch and one game implementation.**

## Current scope

The first slice ports C# `GameState`/inventory/global/rest-block/native functions and uses **the actual unchanged** SSNoir `scripts/stdlib.scm`, `scripts/engine.scm` and `scripts/theatre.scm` via separate LIPS world/encounter interpreters. Scripts are injected by their canonical path. Nothing fetches a script at runtime or accesses a host filesystem.

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
