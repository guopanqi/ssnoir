# Noir World Kernel

Independent Three.js visual experiment for SSNoir.

The experiment is now explicitly a **Genesis Noir spatial-language study**, not a generic noir/toon-rendering study.

## Working hypothesis

Genesis Noir-like local spaces are better approximated by separating spatial structure from visible illustration:

```
3D depth / occlusion skeleton
  -> mostly black masses
  -> authored vector strokes that describe only useful structure
  -> camera-facing / planar character silhouettes
  -> white luminous planes and sparse gold symbols
  -> wet-street / rain / mist graphic layers
  -> restrained monochrome composite
```

The goal is not to make every mesh readable. The goal is to make a navigable 3D scene read like a composed graphic illustration from each gameplay camera.

## Current rules

- No blanket `EdgesGeometry` as the primary look.
- No toon shading as the primary look.
- Characters are graphic cutouts with authored contours, not low-poly dolls.
- Buildings use black volume for depth and explicit line grammar for readable architecture.
- White/gold planes are treated as graphic light, not realistic PBR emitters.
- Fog, rain and wet-road reflection are graphic layers rather than visible volumetric cones.
- Gold remains sparse and symbolic.
- AI-generated meshes stay out until the style system can absorb arbitrary geometry without losing the visual language.

## Capture contract

`npm run capture` produces:
- `final-wide.png`
- `final-detail.png`
- `final-alley.png`
- `shape-wide.png`
- `line-wide.png`

These images are the evidence for each iteration. Numeric image metrics diagnose broken output only; they are not aesthetic scores.
