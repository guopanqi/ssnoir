# Research Log

## 2026-10-04 — Full reconstruction

User review rejected the previous experiment. The failure was not a missing parameter; the experiment had optimized the wrong abstractions.

### Findings from renewed Genesis Noir research

Feral Cat Den explicitly describes Genesis Noir as an attempt to bring motion-graphics sensibility into a game. Their workflow previsualized scenes in Cinema 4D, required precise camera alignment between 2D and 3D elements, and retained large amounts of 2D/vector animation in-engine.

This changes the lab model:
- style is not a post-process;
- camera composition is part of asset authoring;
- vector-like linework is authored structure, not merely detected edges;
- 2D/3D hybridization is a first-class technique;
- clean graphic hierarchy matters more than matching a histogram.

### Open-source technique survey

Useful:
- post-process depth/normal edge extraction for selected environment contours;
- inverted hull for character outer silhouettes;
- NPR hatching/engraving experiments for localized surface treatment;
- god-ray / volumetric passes for real light shafts;
- modern postprocessing frameworks for a controlled compositor.

Not useful as the foundation:
- full-scene wireframe;
- full-screen sketch wobble;
- global crosshatching;
- aggressive posterization;
- a stack of effects with no compositional intent.

### Evaluation reset

Removed black-share, midtone-retention and color-budget metrics from aesthetic acceptance.

New evidence set:
- Structure: geometry + intentional lines, no emissive/atmosphere/post.
- Clean: complete scene before grain.
- Final: finished frame.
- Offset shot: robustness check.

The final decision remains human visual review.

### Reconstruction v1

The old scene implementation is replaced by a single composed city plaza. The first question is deliberately simple:

> Does one still frame finally look like it belongs to the same visual family as the supplied Genesis Noir references?

Nothing else should be optimized until the answer is yes.
