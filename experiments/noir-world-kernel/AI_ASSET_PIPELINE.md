# AI Vector Asset Pipeline

The kernel now supports a real external vector-generation ingress.

## Provider choice

The first implemented provider is **Recraft** because its current API exposes a vector-only generation endpoint and native SVG models.

Current integration target:

- Base URL: `https://external.api.recraft.ai/v1`
- Endpoint: `POST /images/generations/vector`
- Default model: `recraftv4_1_vector`
- Response: `b64_json`, decoded locally into SVG

The token is read only from `RECRAFT_API_TOKEN`. It is never stored in source or generated provenance.

Official docs used when this adapter was implemented:
- https://www.recraft.ai/docs/api-reference/getting-started
- https://www.recraft.ai/docs/api-reference/endpoints
- https://www.recraft.ai/docs/api-reference/image-inputs-and-results

## Generate one asset

```bash
export RECRAFT_API_TOKEN=...
npm run generate:vector -- ai/requests/detective-walk.example.json
npm run validate:assets
npm run build
npm run capture
```

The generation tool writes:

```
src/assets/generated/<catalog-id>.svg
src/assets/generated/manifest.generated.json
ai/runs/<catalog-id>.json
```

The run JSON records prompt/model/seed/normalization metadata but never the API token.

## Why generated SVG is not accepted directly

Provider output passes through `normalizeGeneratedSvg` before it reaches the catalog.

The normalizer:

1. rejects script, external images, external references, CSS URL dependencies and other unsafe SVG features;
2. rejects excessively fragmented vectors;
3. remaps generated colors into the kernel palette;
4. clamps source stroke width;
5. derives a ground pivot and world scale from the SVG viewBox;
6. writes normalized provenance into the generated manifest.

After that, the existing `validate:assets` gate still runs.

This means provider success is only the start of the pipeline:

```
generated
  -> safe
  -> normalized
  -> technically valid
  -> fixed-shot visual review
  -> accepted / rejected
```

Only the first four stages are automated.

## Future style consistency

Do not immediately create a style reference from Genesis Noir screenshots.

First build and approve a small internal vector kit. Once 5–10 assets actually fit SSNoir, those owned/curated assets can become the reference material for a reusable vector style. The Recraft adapter already accepts optional `styleId` and `styleMatch` fields for that later step.


## Automatic audition and recursive validation

Generated assets are now reviewed as first-class evidence rather than merely appearing in the runtime catalog.

`npm run capture` always produces a neutral-stage audition of one known-good authored asset:

```
audition-detective-walk.png
```

For every entry in `src/assets/generated/manifest.generated.json`, capture also emits:

```
generated-<catalog-id>.png
```

The audition scene uses the asset's catalog pivot / scale and shows it against a restrained neutral stage. This is deliberately separate from city composition: an asset must first survive close inspection on its own before it earns a place in a street scene.

The validator now walks `src/assets/**` recursively, so generated SVGs cannot bypass the technical gate by living under `src/assets/generated/`. It also checks that generated manifest IDs/files are unique and that every generated SVG is represented exactly once by the manifest.

### Candidate status

Live generation writes new assets with:

```json
{
  "status": "candidate"
}
```

This is intentional. Generation + normalization means **technically admissible**, not art-approved.

The intended acceptance loop is:

```
request
  -> provider
  -> normalize
  -> recursive technical validation
  -> neutral audition screenshot
  -> art-direction review
  -> only then place in a real scene
```

### Provider-contract check

The Recraft adapter was checked against the current official API documentation:

- `/images/generations/vector` is the vector-only generation endpoint;
- vector models use names ending in `_vector`, including `recraftv4_1_vector`;
- `response_format: b64_json` returns Base64 image bytes;
- vector output is always SVG;
- V4 / V4.1 style matching uses `flexible` or `precise`; `regular` is for V2 / V3.

The request normalizer enforces the model-sensitive `styleMatch` rule before a paid API request is sent.
