# Noir World Kernel

Independent Three.js visual experiment for SSNoir.

This experiment studies how to construct a **Genesis Noir-like local 3D world**, rather than applying a generic noir/toon shader to ordinary meshes.

## Current architecture

```
Scene / spatial layout
    ↓
3D black depth skeleton
    ↓
FacadeGrammar
    ├─ sparse structural VectorStroke
    └─ luminous window planes
    ↓
PuppetCharacter + SVGProp
    ↓
source-driven ReflectionField
    ↓
rain / halo / sparse gold symbolism
    ↓
restrained monochrome composite
```

### Reusable systems

- `style/VectorStroke.js` — screen-space line hierarchy based on `Line2 / LineMaterial`.
- `world/FacadeGrammar.js` — black building masses, selective structural strokes, window rhythm and reflection-source output.
- `world/SVGProp.js` — generic SVG fill/stroke → Three.js geometry ingestion.
- `world/PuppetCharacter.js` — character presets built on the SVG ingestion layer.
- `world/ReflectionField.js` — wet-street reflection generated from actual windows, signs and lamps rather than an unrelated random texture.

The current taxi and characters intentionally use the same SVG path that future AI-authored vector assets can use.

## Art-direction rules

- 3D establishes space, perspective and occlusion; it does not need to explain every surface.
- Do not return to blanket `EdgesGeometry`.
- Do not make toon shading the primary look.
- Characters and selected props may be planar/vector assets living inside a real 3D scene.
- Bright windows, lamps and signs are graphic shapes first and realistic emitters second.
- Reflections must remember their source lights.
- Gold is rare and symbolic.
- Any generated texture must disappear into the image; if the viewer sees “the noise algorithm,” it has failed.

## Capture contract

`npm run capture` now produces:

- `final-wide.png`
- `final-street.png`
- `final-detail.png`
- `final-alley.png`
- `shape-wide.png`
- `line-wide.png`

The street shot is intentionally gameplay-height. Numeric metrics remain diagnostics only and never decide aesthetic quality.
