# Phase 2 — action execution contract (incremental)

The original C# `FateStrip` probability rows have been translated exactly, including the *prepared value* calculation and a separate d6 used only to select the strip's outcome. `ActionTurnState` preserves stable dice slot IDs when an actor spends a die.

`GameActionRunner` handles a prepared Scheme scene tree, enforces action requirements, selects a branch and triggers the original authored Scheme **closure inside its originating interpreter**. `node`, `roll`, `instant`, `outcome`, `req-die`, and `req-item` continue to come unchanged from original `engine.scm`. It does not convert effects into JavaScript strings or use eval.

**Boundaries that remain to migrate before this can be called full SceneManager parity:** actor selection and additional companions, difficulty modifiers, action report, on-action rules, world/encounter turn transitions, hospitalization, additional resource slots, narrated/blocked effects and persistent scene closure snapshots. Native rollback covers state and dice, not mutated Scheme lexical closures. This smoke proves live rules work; it does not claim the complete game is playable.
