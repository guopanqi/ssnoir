# Phase 2 — single-runtime acceptance contract

**Status: IN PROGRESS. Real world/engine smoke and all four platform mock regressions passed in GitHub Actions run [38116949362](https://github.com/guopanqi/ssnoir/actions/runs/38116949362), commit `e59c0771`. This is a partial gate, not Phase 2 acceptance.**
The user approved Phase 1 locally. Phase 2 is to replace the *game runtime* while preserving real authored SSNoir content and first-stage Web/WeChat/TapTap/Electron infrastructure.

## End-to-end completion gates

Each gate needs source code, runnable regression tests and an explicit reproducible CI run. For important semantics, compare against `Engine/Runtime` C# reference or the existing content-validator; do not silently swap to a new game rule or shorten authored story.

1. **Original content loader:** Load `UnityClient/Assets/Resources/Content/scenes/world/world.scm` and its complete literal dependencies in a fresh LIPS VM without network/disk access *at runtime*. Do not modify source content. Materialize `get-render-data` into a valid nested scene/node tree, with no skipped/duplicated initialization side effects.
2. **World traversal:** Enter actual authored places and display their real nodes (including disabled, clocks, dossier, arrival hooks). Implement `on-action`, `on-turn-end`, `on-turn-start`, first-day initiation and independent per-module persistent state.
3. **Native parity:** Port all native functions exercised by world + encounter content, including player/companion/composure/injury/scars, relationship support, clock effects, failure, narratives and script loading. Missing capabilities must fail loudly, not be no-op shims except where C# has the same optional semantics.
4. **Action resolution:** Resolve original authored instant and roll/recovery-roll outcomes via LIPS closures with stable dice slot identity, four actor skills, C# FateStrip, modifiers, consumes, failure rollback policy and action report. Handle multi-die requirements and non-player actors.
5. **Encounter lifecycle:** World and encounter interpreter isolation, shared typed native state, world dice stash/restore on encounter return, turn-cost pressure, opponent rules, forced actions, collapse hospitalization and `end-encounter` callbacks.
6. **Save/resume:** Round-trip typed team, dice slot identities, inventory, globals, per-module Scheme `world-save/world-load!`, day count, rest blockers and settings. Replay must not repeat already-paid story outcomes. Version/corruption validations required.
7. **Narrative presentation:** Actual node/actions/choices, dialogues and required theatre/stage/video/motion blocking story steps are driven by runtime state on the single shared Web + Mini Game renderer, not by a separate hardcoded test scene.
8. **Platform and regression:** `Game/npm run verify` and Web screenshot, Electron Xvfb smoke, WeChat normal/missing/throwing offscreen variants, TapTap official conversion/smoke all green. Demonstrate a deterministic actual authored scene trace against C# reference. Real WeChat/TapTap device acceptance still requires native testing once playable content exists.

## Verified increments / sources of truth

- Phase 1 host: `Game/adapters/wechat`, `Game/adapters/taptap`, `Game/src/foundation`; passed local developer-tool validation according to user.
- `Game/src/runtime/game-state.ts`: inventory/globals/rest blockers/partial save. **Partial**.
- `Game/src/runtime/script-session.ts`: LIPS native bridge, original engine + theatre scripts, ordered top-level loader.
- `Game/src/runtime/node-converter.ts`: strict original DSL subset. Arrivals, clock parsing and complex support metadata pending.
- `Game/src/runtime/fate-strip.ts`, `turn-state.ts`, `action-runner.ts`: exact fate distribution, fixed four protagonist dice, callable source effects in synthetic scenes. Actor system, roll modifier application, world action contexts and persistence pending.
- `Game/tests/game-world.ts-test.mjs`: **real-world gate**. This must become green before moving to full playable chapter content; its first failures expose missing native functions.

Current stage-two work must not revert to the simplistic platform probe and claim that as completion.

## Implementation process

Only `main` branch; coordinate with concurrently running research agents via expected HEAD on writes. Favor a small semantic surface and faithful C# behavior over rapidly adding unrelated scaffolding. On failure, retain an explicit failing acceptance test, port the real required capability, then rerun complete CI. Do not leave main red after calling a sub-milestone accepted. Commit reference docs and update this file as each gate is actually satisfied.

## Verified checkpoint — 2026-10-11

- Existing foundation: Web desktop/mobile/compact, Electron headless, WeChat normal/missing/throwing offscreen 2D, TapTap official converter + mock wx all GREEN in [38116949362](https://github.com/guopanqi/ssnoir/actions/runs/38116949362).
- Node tests 30/30 + new Phase 2 tests 17/17; `tests/game-world.ts-test.mjs` loaded the real unchanged `scenes/world/world.scm`, dependencies, and converted `get-render-data` into a nonempty validated root node. **This is a real authored content smoke, not a full gameplay parity trace.**
- Migrated foundations: original engine+theatre Scheme bootstrap, global/inventory/rest blockers, exact original FateStrip, fixed dice identities, callable Scheme outcome closures, action requirement gate, injury bands and scar snapshot.
- **Next priority**: implement real navigation and action resolution in authored world, actor/team/composure/injury hospitalization and report pipelines, world/encounter transitions, per-module save/resume and narrative stage presentation. Full Phase 2 gates 2–8 remain OPEN; stage-one device acceptance is user-reported, not independent CI proof.

## Actor/team native increment

`src/runtime/team-state.ts` owns the one protagonist and companions; records slot identity, four skills, composure, away/active transitions, growth spending, supports, carried support and temporary helpers. `native-bridge.ts` exposes the corresponding Scheme functions. Typed snapshots validate actor cardinality, dice and support cross-references and reject temporary summoned helpers. Hospitalization pending/7-injury resolution is still a separate gate and must not be inferred from a passing team test.

## Save/resume runtime increment — 2026-10-11

- Original, unmodified `world.scm` now has a versioned portable `WorldSaveController` envelope: the native inventory/globals/team/dice/rest state and the full Scheme `world-save` datum are captured together. Restoration creates a fresh interpreter, restores native state first, then invokes authored `world-load!` once. Encounter saves and pending hospitalization are rejected.
- `tests/world-save.ts-test.mjs` runs the actual content, saves and restores a changed world day, cash, relationship support and settings; verifies that a second load does not replay the cash reward; and rejects unsupported versions, encounter saves and temporary helpers. The Node 22 strip-mode constructor fix at [f5ef194](https://github.com/guopanqi/ssnoir/commit/f5ef19457f417531d9382c5c228a064411702deb) made this regression executable.
- The complete Game foundation CI including Node tests, Web, Electron, WeChat variants and TapTap converter passed at [38117830637](https://github.com/guopanqi/ssnoir/actions/runs/38117830637). **Gate 6 remains OPEN** until in-game save/load UI and complete authored-state replay traces (including remaining dice and rest blockers) are verified. No claim of all eight gates is made.

## Shared playable Pages preview checkpoint — 2026-10-11

- Preview URL: **https://guopanqi.github.io/ssnoir/game/**. An existing unified Pages workflow also preserves the research home page and `/open-field/`; it now builds `Game/dist/web` from the unchanged original Content and deploys under `/game/`. Deploy succeeded at [Actions 38119516469](https://github.com/guopanqi/ssnoir/actions/runs/38119516469), commit `833f119b`. This is **Web static deployment**, not evidence that every chapter is completable.
- On the single shared renderer, original root story cards (not just places) are navigable with page controls. The UI now calls `GameActionRunner.performSlots` with real team/slot identities, `GameSceneDirector.endRound` for encounter time/opponent rules and `WorldTurnController.endDay` for world days. The controls are intentionally provisional; the old Phase 1 counter remains available without controlling the real game.
- Portable in-game save/resume was attached to the Web/Mini Game renderer through `WorldSaveController`. Restore is transactional at the host boundary: a new director/interpreter is created and `world-load!` completes before the UI swaps worlds. The new Playwright browser regression in `Game/scripts/capture.mjs` tests saving and resuming an actual authored world; **its full four-platform CI result is not yet accepted**.
- A real encounter-round regression initially failed because the TS implementation rejected CITY rest blockers while ending a combat round ([Actions 38119292732](https://github.com/guopanqi/ssnoir/actions/runs/38119292732)). The C# `SceneManager.BeginRoundTransition` does not impose this restriction; commit [833f119](https://github.com/guopanqi/ssnoir/commit/833f119b8d2e43a0d1e637581df587a41f6fd13f) removes the incorrect check. Passing follow-up full CI is still required.
- **All eight full gates remain subject to final authored gameplay trace evidence**, complete dossier/clock/UI/node parity, real scene stage/theatre/motion/video rendering, verified hospitalization/collapse semantics and native device tests. Do not mark Stage 2 complete merely because the preview URL loads or Pages green.

## C# presentation metadata parity increment

The original C# `NodeConverter` semantic clocks (`gauge`, `countdown`, `readout`, title, note, display label), note-node and clock-node semantics, and carry-item/support IDs are now converted into explicit typed runtime fields rather than discarded. The original `get-dossier` is converted into complete typed `DossierEntry` records (story metadata, clocks, completion steps, journal) through `world-host.getDossier()`, with validation of duplicate dossier IDs and malformed content. The world pane displays root countdown and note readout metadata; a fully designed dossier screen with browsing, arrival and stage presentation is still needed. This does not close the visual parity gate.
