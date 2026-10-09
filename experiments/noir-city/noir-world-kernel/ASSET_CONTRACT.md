# Vector / AI Asset Contract

This experiment treats AI generation as an **asset-authoring input**, not as the renderer.

The visual system owns the world language. Generated output must conform before it can enter the catalog.

## Preferred AI entry point

For characters, cars, signs, furniture, plants and graphic hero props:

```
prompt / reference images
    ↓
AI or human-authored SVG
    ↓
sanitize + validate
    ↓
normalize pivot / scale / palette / stroke
    ↓
SVGProp or PuppetCharacter
    ↓
Three.js scene
```

A generated 3D mesh is not the default path for this experiment. It may be tested later for spatial hero objects, but it must still be normalized before use.

## SVG source rules

Every checked-in SVG must:

- contain a `viewBox`;
- contain no external images or external references;
- contain no script, `foreignObject`, JavaScript URLs or CSS `url(...)` dependencies;
- use the restricted source palette:
  - `#010205` — ink / black fill;
  - `#f2efe6` — paper-white stroke or light;
  - `#e2b63d` — sparse gold accent;
- keep source `stroke-width` between 0.4 and 3.0 units;
- remain legible as a silhouette before interior detail is considered.

The runtime may override scale, pivot, mirroring and stroke scale.

## Character convention

Current character SVGs use a nominal `0 0 120 320` viewBox and a ground pivot near `[60, 310]`.

This is a convention, not a hard requirement. A future asset manifest should store per-asset:

```json
{
  "id": "character.detective.no-man-study",
  "type": "puppet",
  "file": "detective-study.svg",
  "pivot": [60, 310],
  "defaultScale": 0.0104,
  "tags": ["male", "coat", "fedora", "noir"]
}
```

## Quality gate

`npm run validate:assets` is a technical gate only. Passing validation does **not** mean the asset is visually good.

Visual acceptance still requires fixed-shot captures and human/art-direction review.

The pipeline therefore separates:

1. **valid** — safe, normalized and renderable;
2. **coherent** — belongs to the current world language;
3. **good** — actually improves the composition and survives close inspection.

Only the first category is automated.
