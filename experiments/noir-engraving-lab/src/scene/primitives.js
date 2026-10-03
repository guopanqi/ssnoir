import * as THREE from 'three';

export function box(w, h, d, material, x, y, z) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
  mesh.position.set(x, y + h * 0.5, z);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  return mesh;
}

export function addEdges(mesh, outlineGroup, materials, importance = 'context', threshold = 32) {
  // Line is an information layer, not a universal geometry filter.
  // "silence" is a first-class tier: the object remains fully renderable but
  // contributes no structural line at all.
  if (importance === 'silence') return mesh;

  const materialByImportance = {
    hero: materials.edge,
    context: materials.edgeDim,
  };
  const material = materialByImportance[importance];
  if (!material) throw new Error(`Unknown line importance: ${importance}`);

  const geometry = new THREE.EdgesGeometry(mesh.geometry, threshold);
  const line = new THREE.LineSegments(geometry, material);
  line.position.copy(mesh.position);
  line.rotation.copy(mesh.rotation);
  line.scale.copy(mesh.scale);
  line.userData.lineImportance = importance;
  outlineGroup.add(line);
  return mesh;
}

export function addWindowStrip(parent, materials, {
  x, y, z, count, spacing,
  vertical = false,
  warm = false,
  phase = 0,
}) {
  const material = warm ? materials.warm : materials.cool;

  for (let i = 0; i < count; i++) {
    // 固定节奏，不使用 Math.random，保证每轮实验截图可比较。
    if ((i * 17 + count * 3 + phase) % 5 === 0) continue;

    const width = vertical ? 0.32 : 0.7;
    const height = vertical ? 0.8 : 0.34;

    const pane = box(
      width,
      height,
      0.08,
      material,
      x + (vertical ? 0 : (i - (count - 1) / 2) * spacing),
      y + (vertical ? i * spacing : 0),
      z,
    );
    pane.castShadow = false;
    parent.add(pane);
  }
}

export function seededRandom(seed) {
  let state = seed >>> 0;
  return () => {
    state = (1664525 * state + 1013904223) >>> 0;
    return state / 0x100000000;
  };
}
