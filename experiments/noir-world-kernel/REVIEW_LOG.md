# Noir World Kernel — Visual Review Log

This file records capture-grounded art-direction findings. Histogram/brightness data may diagnose broken output, but it is never an aesthetic score.

## Round 01 — readable world before asset expansion

Evidence:
- commit: `152ca09d9569fe4685a34f8dbf9012c8c7b6aa5f`
- GitHub Actions run: `37143001756`
- artifact: `noir-world-kernel-37143001756-1`
- browser/runtime errors: none

### What changed

The round deliberately stayed within procedural primitives and the existing renderer:
- widened the usable mid-value range and reduced black crush;
- reduced fog and vignette pressure;
- reframed wide/detail/alley shots;
- reduced the visual weight of practical lamp shades;
- made the hero larger in the wide shot;
- flattened the face material and gave figures controlled yaw;
- preserved the parallel theatre/water-tower/moon-ring work already present on main.

### What the captures actually show

The scene is now readable enough to judge. The theatre facade, street planes, hero, taxi block and office mass separate from one another instead of collapsing into a nearly black frame.

That exposes the real problems:

1. **The character is still an engineering construction, not an illustration.**
   In the detail shot the extruded head reads as a faceted grey solid, detached in visual language from the coat cutout. The body silhouette is more promising than the head. Character work should focus on silhouette/profile grammar rather than adding facial detail.

2. **The line system is too uniform and technical.**
   Many architectural edges read like thin CAD/wireframe marks. Silhouette, structural edge and incidental box edge are not separated strongly enough. The next renderer experiment should treat line class as authored semantic data, not merely `EdgesGeometry` everywhere.

3. **Lighting geometry is visible as geometry.**
   The translucent street/stage wedges and bright triangular lamp shades explain where light comes from, but they read as constructed polygons rather than cinematic light. Light pools need softer/noisier masks and should reveal forms without becoming independent graphic objects.

4. **Architecture reads as grey blocks before it reads as a city.**
   The theatre has begun to acquire a facade rhythm, but surrounding masses remain generic. The next procedural step should not make a larger city; it should introduce a very small facade grammar: bay, window group, cornice, storefront, fire escape, roof silhouette.

5. **The composition has competing symbols.**
   The gold celestial ring is cleaner than the former filled disc, but it is still a major focal object. In the alley view, the cropped NOCTURNE lettering and ring compete with the character. Graphic motifs should support the shot rather than become equal subjects.

### Round-01 decision

Do **not** add AI-generated meshes yet.

The renderer/world kernel has crossed the threshold where visual defects can be judged, but imported assets would currently hide rather than solve the style-system problems.

The next round should concentrate on:
1. character silhouette/head grammar;
2. semantic line hierarchy;
3. non-geometric practical/stage light masks;
4. one reusable theatre/street facade grammar.

Only after those four systems survive wide + detail + alley captures should the experiment introduce one AI-generated hero prop through a normalization pipeline.
