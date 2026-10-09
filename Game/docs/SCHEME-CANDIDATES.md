# Scheme runtime candidates: isolation is a hard requirement

SSNoir's existing C# SceneManager holds a long-lived world interpreter plus a separate encounter interpreter per encounter. Defining the same Scheme name in the two namespaces must not cross-mutate values; a failed isolation test is a blocking incompatibility.

## BiwaScheme 0.8.3 (current visual probe only)

The live GitHub Actions test at commit 0c798dbdc55edcb84141e81a59b5084288a17b75 demonstrated a real failure:
- world VM: (define local-stage 17)
- encounter VM: (define local-stage 2)
- world VM then evaluated local-stage => **2**, not 17.

All of its other six basic semantics tests passed, including original SSNoir stdlib.scm, named let, lexical mutation, and define-macro. These are necessary but not sufficient. Separate Interpreter objects do *not* isolate the process-global top environment in the tested version. Keep Biwa only as a rendering foundation probe until fixed or replaced. Do not build world/encounter gameplay against it without a verified isolation strategy.

## LIPS 1.0.0-beta.23.1 (experimental candidate)

An isolated-instance smoke suite is added in Game/tests/lips-candidate.test.mjs. It intentionally runs only through Node.js and does not change production Web/WeChat bundles. The candidate must pass:

1. isolated globals across two instances;
2. persistence of lexical mutable state;
3. standard Scheme truth semantics, macro compatibility;
4. future full SSNoir stdlib/engine/world load and native JS function registration;
5. measured startup time, byte size, mobile/browser/WeChat compatibility.

If LIPS fails a requirement, record the failure and investigate another mature library or a narrowly scoped isolate/namespace patch. Do not silently modify existing .scm syntax just to make a candidate pass.

**Decision status:** undecided. A green rendering smoke test does not certify game runtime semantics.
