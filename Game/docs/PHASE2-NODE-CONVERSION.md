# Node conversion parity — Phase 2

This stage ports **the authored data contract**, not the Unity UI. Original `engine.scm` builds lists with `node`, `instant`, `roll`, `req-die`, `req-item` and `note`; the TypeScript converter reads the resulting actual LIPS `Pair` objects, checks the same primary C# invariants, and returns a typed `RuntimeNode` tree.

Source of truth: `Engine/Runtime/Scripting/NodeConverter.cs`.

The node parser is strict about cycles, malformed pairs, duplicate names, invalid keywords and mutually exclusive `:children`/`:resolve`. Pure metadata and a callable outcome reference are retained; **the effect is not executed by converting a node**.

Incomplete on purpose: arrivals and actor-dependent action resolution are not activated. Execution will need a context with slots, action reports, time and failure semantics. Full C# implementation also parses clocks, modifiers, dossier entries, arrivals, support metadata and some node-specific restrictions. Do not claim full NodeConverter parity until these are ported and compared.

Regression `tests/game-nodes.ts-test.mjs` exercises unmodified original `engine.scm` constructors and malicious/invalid node structures.
