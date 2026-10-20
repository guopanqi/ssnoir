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

## Original `play-stage!` runtime (in progress)

The original C# `StoryStagePlayer` beat semantics have been ported into `src/runtime/stage-playback.ts`: simultaneous commands, input-only dialogue, no skipping of moving actors, linear movement, relative/multi-point Bézier paths, prop anchors, poses, light and effect events. The **same** Pixi canvas now hosts `StageVectorView` in addition to `TheatreVectorView`. The source of `play-stage!` remains the existing Unity Scheme scripts and the host's typed story queue.

`tests/authored-stage.ts-test.mjs` extracts and executes the actual unmodified Frank ship-settlement stage expression from `scenes/world/人物/弗兰克.scm` and advances it to the final beat; this is not just a synthetic animation example. **Stage art is not yet at asset parity**: when a portrait/prop is not provided by the host resolver, an explicit missing-resource graphic is displayed. This does not close narrative presentation Gate 7. Actual voice/audio, motion/video backends and Pixi visuals still require verification.

### Stage asset integration (bounded release baseline)

Four original SSNoir portrait silhouette PNGs (Neil, Nightingale, Frank and Baines) are referenced at build time via `?inline` instead of creating a separate HTTP/DOM image loader or copying an unbounded Unity resource folder into the Mini Game package. `StageImageRegistry` decodes them through the existing host `Image` interface; the shared StageVectorView asynchronously replaces missing-asset markers only after a real Pixi `Texture` exists. Other portraits, special poses, props, voice and sound are still explicitly unresolved; do **not** claim final graphic/audio parity on this basis.

This is an authored runtime reliability check, not a full asset migration. Mini Game device decoding and package size still require the actual CI and native test gates.

## World navigation optimization (2026-10-12)

The original clickable preview evaluated the entire original `get-render-data` tree **three times** on a normal world place entry (twice in `GameActionRunner.enterPlace`, once in `WorldHost.enterPlace`), plus an avoidable extra evaluation at initial boot. This was a source of multi-second UI blocking on real authored nodes. The runtime now uses the already prepared Scheme-backed tree: **one** post-`on-enter-place` evaluation if no arrivals, **two** if arrivals must execute, and no extra world re-evaluation from the host. Encounter transitions continue to switch to their own freshly prepared tree. A real world navigation regression asserts one evaluation for an authored place with no arrivals; runtime logs include entry elapsed milliseconds and the re-materialization count. Native device performance is still an independent acceptance gate.

## Main game presentation / QA separation (2026-10-12)

The public client previously showed a large technical foundation counter unrelated to the story. The normal Pixi overlay now foregrounds the original city, Neil, authored destinations, dossier, real day, cash and available action dice; action/roll/location rows display their authored titles and semantic categories. The independent Scheme smoke button is available only behind `?foundation-debug=1` (Web) or `__SSNOIR_FOUNDATION_DEBUG__` (mock Mini Game host). Platform regression screenshots still exercise the button in explicit QA mode. The scene is still prototype art, not final noir visual acceptance.

## Content change reconciliation (2026-10-12)

The original three-letters prologue was extended: an optional `醉酒的尼尔` opener at the old-street tavern precedes the obligatory `有人敲门` on day 3, and the chapter-one ransom dossier is hidden until the commission while independent city dossier entries may still appear. Old tests assuming door/day-one rest blocking or an empty entire dossier were stale. World save tests that mutate the day directly must explicitly run the authored rest-blocker derivation before taking a snapshot, to compare consistent states. Runtime content is authoritative; these tests were adjusted to match the new authored sequence rather than changing the story to fit old tests.

## Player-facing browser performance gate (2026-10-12)

The Playwright capture now has a separate **normal mode** path, not the old foundation-debug test. It captures city and entered-place screenshots, performs an actual pointer click on the first authored location, waits for the game view to update, and rejects navigation requiring more than two complete authored tree materializations. It emits measured navigation milliseconds in CI for future regression trends. This tests the interactive path and the story UI, not only a state snapshot; mobile and native-device timings remain separate gates.

## Avoid duplicate runtime on normal launch (2026-10-12)

The player-facing UI no longer starts the two independent Phase-1 LIPS namespace probes or the duplicate platform cash session. These cost interpreter and package startup time while not powering the game. They now initialize only in explicit foundation QA mode; Web and wx smoke harnesses opt in. Ordinary gameplay uses the sole authentic GameSceneDirector and its typed native state. This removes redundant initialization without modifying the original Scheme scripts or losing the existing platform acceptance probes.

## Current prologue native function parity (2026-10-12)

The actual revised tavern prologue invokes C# natives `__apply-drunk!`, `__play-camera!`, and `__play-intertitle!`. These are now in the shared TS bridge with C#-equivalent validation and typed events. Protagonist `isDrunk` is persisted in TeamSave, remains during encounters, and clears on the next consuming city roll (not on arbitrary day change). Intertitles advance one authentic narration page per acknowledgment; camera transition payloads preserve anchor/duration/destination and are explicitly displayed in the current prototype client. Full 3D camera-anchor motion and voice playback are *not* yet at art parity and still need their own stage gate. Opening C#/TS end-to-end regression must be green before claiming accepted.

## Native registration coverage gate

`Game/tests/native-contract.test.mjs` scans `Engine/Runtime/Scripting/NativeFunctions.cs` and `Game/src/runtime/native-bridge.ts`. Every C# game native beginning with `__` must have a named TS bridge; the check prevents a newly-authored Scheme scene from invoking a missing runtime primitive without CI catching the omission. Name coverage is NOT semantic parity: script-driven story and C# trace comparisons remain mandatory.

## Graphic-noir city preview

The phase-one eight bare WebGL cuboids are now architecturally legible through hard roof cornices, restrained instanced lit windows, a street lamp, and localized lighting. This is an inexpensive shared WebGL2 backdrop with no extra texture/network dependencies. It is **not** a substitute for original CityBox art assets, whose migration still has a separate acceptance gate. While an authored place is loading, every gameplay control is locked against racing the script's world entry effects.

## Encounter carry cards and relationship supports — 2026-10-20

The C# `SceneManager.CurrentCarryNodes` path was absent from the TypeScript encounter host. The same authored `(encounter-action-nodes)` now produces a separate typed list of carry/support cards, outside `get-render-data`. Cards retain Scheme effect closures in the encounter VM; UI shows them beside encounter cards rather than adding them to the authored scenario tree. The engine enforces exactly one carried relationship support per encounter and disables it on use. On exit/re-entry the limit resets. The original C# item slot consumer clips remaining stock at zero. The new regression runs the original Frank support card and cigarette action in `勒索信`; platform builds must pass before acceptance.

This closes only **the encounter carry-card subgate**, not the full multi-actor or visual parity gates. The game still needs a full C# vs TS trace through several authored encounters.
