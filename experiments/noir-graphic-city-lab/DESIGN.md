# Noir Graphic City Lab — Reconstruction v1

## Why the previous experiment was discarded

The earlier lab optimized proxy statistics (black share, midtone retention, accent share) and repeatedly tuned a weak visual premise. Those numbers were useful for diagnosing rendering regressions, but they were poor aesthetic objectives: a different valid Genesis Noir shot can have radically different luminance distributions.

The reset starts from art direction, not image histograms.

## What Genesis Noir is actually doing

Production notes from Feral Cat Den describe a motion-graphics-first workflow: scenes were designed and prevised in Cinema 4D, 2D and 3D elements had to align precisely to a camera, and a large portion of animation remained 2D/vector work inside the game pipeline.

The important consequence for this lab:

> The target is not a universal shader. It is a compositional system that lets clean vector-like drawing, dark 3D masses, silhouettes, light and motion-graphic staging coexist.

## Visual pillars

1. **Clean vector architecture**
   - Thin off-white lines.
   - Lines correspond to windows, rails, roof breaks, facade seams, props and silhouettes.
   - Avoid indiscriminate wireframe and random wobble.

2. **Rich blue-black surfaces**
   - Dark surfaces are not empty black.
   - Keep restrained value separation between facade planes.
   - Fine grain belongs mostly to surfaces and atmosphere.

3. **Silhouette people**
   - Solid black bodies.
   - Outer contour only, using an inverted hull.
   - Minimal internal marks.
   - Strong hat/coat/pose variation.

4. **Bright graphic light**
   - A few white windows, signs, pools and beams can carry the focal structure.
   - Bloom is narrow and restrained.

5. **Camera-authored composition**
   - The primary benchmark is a composed city tableau, not free-camera coverage.
   - A second offset camera is kept only as a robustness check.

## Technical references worth borrowing

- Depth/normal outline projects are useful for environment contours and selective edge extraction.
- Inverted-hull outline is appropriate for character silhouettes because it yields an external contour instead of geometry wireframe.
- NPR hatch/sketch repositories are useful for surface treatments, but hatching should not be applied globally.
- Screen-space/raymarched god-ray projects are useful if the simple beam geometry becomes inadequate.
- pmndrs/postprocessing is a viable future compositor, but a larger effects stack is not a visual goal.

## Evaluation

No single scalar aesthetic score.

The acceptance decision is visual and comparative. Every candidate is reviewed using:
- beauty frame at full size;
- thumbnail readability;
- structure-only frame;
- clean frame before grain;
- an offset camera to expose shot-specific cheats.

The reviewer asks:
- Is it beautiful and deliberate at first glance?
- Is the focal hierarchy clear?
- Do lines feel designed rather than generated?
- Are surfaces rich but not dirty?
- Does the character read as a graphic silhouette rather than low-poly geometry?
- Does the scene retain quality when the camera moves modestly?

Numeric data is allowed only for engineering diagnostics: capture failures, clipping bugs, performance, missing resources, or temporal instability. It does not decide whether the art is good.

## First reconstruction scope

One small city plaza:
- one hero detective;
- six background figures;
- one diner/club;
- one illuminated office tower;
- one right-side theater block;
- street lamps, rail/seam detail, wet pavement and restrained grass;
- one spotlight composition;
- one primary camera + one offset robustness camera.

Do not expand content until this single tableau reaches the target visual family.
