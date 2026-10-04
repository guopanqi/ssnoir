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
