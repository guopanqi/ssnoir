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


## Genesis Noir pivot — rounds 02–09

The first kernel proved that generic low-poly noir + toon shading + blanket edges was the wrong abstraction. The experiment was therefore reset around a more specific question:

> Can a navigable local 3D space be rendered as an authored graphic illustration, with 3D providing depth/occlusion while vectors, silhouettes, luminous planes and reflections provide most of the visible image?

### Reference observations

Visual references from Genesis Noir repeatedly show:
- black spatial masses with only selected white structural strokes;
- very sparse gold used as a symbolic accent rather than a normal material;
- planar/silhouette characters whose readability comes from contour and pose;
- large luminous windows and practical-light halos;
- wet streets described by broken reflected light rather than physically accurate PBR;
- architecture that is simplified according to the shot rather than exhaustively outlined.

This is materially different from a toon renderer.

### What was implemented

The kernel now contains:

1. **Authored wide-line layer**
   - `Line2 / LineMaterial` for stable screen-space structural strokes.
   - Lines are authored by semantic role; automatic `EdgesGeometry` is no longer the primary visual system.

2. **Black 3D spatial skeleton**
   - Buildings and street volumes still provide perspective, occlusion and a walkable coordinate system.
   - Their geometry is deliberately under-described visually.

3. **Planar/vector characters**
   - The procedural puppet study established the silhouette approach.
   - The hero now uses a real SVG asset loaded through `SVGLoader`, converting SVG fill and stroke into Three.js geometry.
   - This is the preferred future ingestion point for hand-authored or AI-generated character drawings.

4. **Graphic light and atmosphere**
   - Street-lamp halos are sprites rather than visible light cones.
   - Bright windows are planar luminous shapes.
   - Rain is a sparse vector layer.

5. **Wet-street reflection field**
   - Reflection is generated as a deterministic painterly texture and projected onto the street.
   - It is intentionally broken and incomplete instead of physically mirrored.

6. **Object-space surface wash**
   - Broad directional brush marks live on building faces.
   - Screen-space block noise was tested, visibly failed, and removed.

### Current evidence

Latest verified SVG-character pass:
- commit: `7e4bcc443ef5d661e8a8f29974cb77c0f84d4cbd`
- Actions run: `37145253678`
- artifact: `noir-world-kernel-37145253678-1`

The current result is substantially closer to the intended Genesis Noir spatial language than the original A/B kernel, but it is still a study rather than a finished art target.

### Remaining visual gap

The largest remaining gap is no longer renderer architecture. It is art-direction quality inside the reusable primitives:

- hero SVG silhouette needs better anatomy, gesture and costume design;
- facade grammar needs curved/period architecture and better shopfront composition;
- line width should eventually support more hand-authored variation than one constant width per path;
- reflected-light masks need per-light/per-window placement instead of a generic field;
- fixed shots need stronger foreground framing and intentional negative space;
- the city needs a small library of SVG/vector props (cars, signs, fire escapes, furniture, plants) so black 3D masses are punctuated by designed 2D information.

### Engineering decision

Do not return to “make every 3D mesh pretty.”

The next production architecture should be:

```
SceneSpec / spatial graph
    ↓
3D procedural skeleton
    ↓
FacadeGrammar + VectorStroke
    ↓
SVG Puppet / SVG Prop layer
    ↓
LightGraphic + ReflectionField
    ↓
shot-specific visibility / composition rules
```

AI-generated assets should first enter through **SVG/vector props and character components**, where style consistency can be constrained. AI-generated 3D hero meshes should remain a later experiment.


## Componentized local-street milestone — v0.5.x

Latest visually reviewed baseline before the asset-contract commit:
- commit: `560bd97f1e19233d80827870dd5856deb89309f2`
- Actions run: `37170038123`
- artifact: `noir-world-kernel-37170038123-1`

### What is now working

- The scene reads as layered illustration rather than a lit greybox.
- The main spatial composition now uses a foreground detective, midground taxi / vegetation / cafe, and dense background window fields.
- The cafe contains interior line detail, an arched doorway and dark occupants rather than two empty luminous rectangles.
- A procedural fire escape adds period-city line density without blanket mesh outlines.
- Wet-road graphics are source-driven and considerably less slab-like than the earlier generic reflection field.
- SVG characters and SVG props run through reusable ingestion code rather than bespoke geometry.

### What is still visibly weak

- The detective SVG is still a study: pose, anatomy, coat construction and facial drawing are substantially less refined than the target reference.
- The cafe and surrounding façades remain too geometrically clean; the next gains should come from better vector asset design, not more post-processing.
- Close shots expose uniform line quality. Future SVGs should carry more authored line rhythm and shape asymmetry.
- The world currently has only a tiny prop/character library, so repetition becomes obvious quickly.

### Next visual experiment

Do not enlarge the city yet.

Build a small **vector asset pack** first:
- 3 detective poses / angles;
- 4–6 pedestrian silhouettes;
- 2 cars;
- fire escape, phone booth, awning, sign, bench, trash can;
- 2–3 plant/tree silhouettes.

Then test whether the same FacadeGrammar + ReflectionField can compose three distinct street corners without changing the renderer.


## Vector-kit generalization milestone — v0.6.x

Evidence:
- commit: `9c336f53ad6e44854412f6a71d02ddfcb2292ac0`
- Actions run: `37170912090`
- artifact: `noir-world-kernel-37170912090-1`

### What the second corner proves

The shared renderer and world primitives now support two visibly distinct local spaces without changing the post-processing stack.

The cafe corner uses:
- rounded low storefront;
- taxi, grass and tree foreground;
- dense office windows;
- cafe interior detail and fire escape.

The terminal corner uses:
- three large arched bays;
- phone booth, bench, trash can and narrow awning;
- a different sedan;
- worker / dress / short-coat pedestrians;
- walking and turning detective poses.

The terminal does not read as a mere reskin of the cafe. That is the important engineering result.

### What still does not meet the target

The limiting factor is now vector-asset authorship rather than scene architecture.

The refined walking detective has a better head/body ratio than the first version, but close shots still reveal:
- mechanically even contour weight;
- simplified shoulder / elbow construction;
- limited gesture;
- little variation in coat shape and hat profile.

This is exactly the class of work that should move out of hand-written SVG strings and into an external vector-authoring loop.

### Next step

The experiment now adds a live AI vector ingress with a strict boundary:

```
AssetRequest
  -> Recraft vector-only endpoint
  -> Base64 SVG
  -> normalizeGeneratedSvg
  -> generated manifest
  -> VectorAssetCatalog
  -> existing validation
  -> fixed-shot review
```

Provider success is never equivalent to visual acceptance.


## AI-ingress hardening — v0.7.1

The Recraft ingress introduced in v0.7.0 is structurally sound, but review found an important gap: the old validator only read SVG files directly under `src/assets`, while generated assets live under `src/assets/generated`. A generated asset could therefore be normalized and catalogued without passing the final repository-wide SVG validator.

v0.7.1 fixes this by:

- recursively validating every SVG under `src/assets/**`;
- validating generated-manifest IDs, file uniqueness, type, pivot and scale;
- rejecting orphan generated SVGs and manifest entries whose file is missing;
- applying the restricted palette to both explicit attributes and inline style attributes;
- validating normalized provider output before it is written to the generated catalog;
- marking live generations as `candidate`;
- adding a neutral `audition` scene;
- automatically capturing every generated candidate as `generated-<catalog-id>.png`.

The experiment therefore has two distinct visual gates now:

1. **asset gate** — neutral close inspection;
2. **world gate** — fixed shots inside cafe / terminal scenes.

No live paid Recraft request is required by CI. CI exercises normalization with mock provider output and validates all checked-in generated assets. Live generation remains opt-in through `RECRAFT_API_TOKEN`.
