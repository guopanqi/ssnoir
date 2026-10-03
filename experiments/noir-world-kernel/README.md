# Noir World Kernel

Independent Three.js visual experiment for SSNoir.

Phase A/B asks one question: **can simple procedural geometry already render as a convincing noir world?**

## Scope

- Procedural primitives only; no imported project assets and no AI-generated meshes yet.
- Strong value grouping instead of material realism.
- Selective structural lines rather than full-object outlines.
- Black / paper-white / restricted gold palette.
- Fixed cinematographic shots.
- Deterministic GitHub Actions screenshots for visual review.

## Local

```bash
npm install
npm run dev
npm run build
npm run capture
```

Capture output is written to `captures/latest/`.

## Visual review contract

The capture harness exposes five images:

- `final-wide.png`: primary art-direction judgment.
- `final-detail.png`: close-read quality.
- `final-alley.png`: second-view generalization.
- `shape-wide.png`: massing and negative space.
- `line-wide.png`: line hierarchy.

The histogram values in `manifest.json` are diagnostic only. They never decide whether the image is good.

## Architecture

```
World geometry
  -> toon value grouping
  -> selective geometry lines
  -> hard directional / practical lights
  -> restrained monochrome + gold composite
  -> fixed shot
```

The next phase should improve the renderer and composition based on actual capture evidence before expanding the city generator or adding AI assets.
