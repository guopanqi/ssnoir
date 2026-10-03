import * as THREE from 'three';

function polygon(points) {
  const s = new THREE.Shape();
  s.moveTo(points[0][0], points[0][1]);
  for (let i = 1; i < points.length; i++) s.lineTo(points[i][0], points[i][1]);
  s.closePath();
  return s;
}

function rectangle(x, y, w, h) {
  return polygon([
    [x - w / 2, y - h / 2],
    [x + w / 2, y - h / 2],
    [x + w / 2, y + h / 2],
    [x - w / 2, y + h / 2],
  ]);
}

function ellipse(x, y, rx, ry) {
  const s = new THREE.Shape();
  s.absellipse(x, y, rx, ry, 0, Math.PI * 2, false, 0);
  return s;
}

function heroShapes(pose) {
  const shapes = [];

  // One continuous graphic silhouette assembled into one mesh so the outline
  // pass sees a single figure, not a collection of low-poly parts.
  shapes.push(polygon([
    [-0.28, 0.82], [-0.48, 0.94], [-0.48, 2.25],
    [-0.56, 2.66], [-0.45, 2.96], [-0.26, 3.10],
    [ 0.26, 3.10], [ 0.45, 2.96], [ 0.56, 2.66],
    [ 0.48, 2.25], [ 0.48, 0.94], [ 0.28, 0.82],
    [ 0.22, 1.05], [-0.22, 1.05],
  ]));

  // Arms overlap the coat; because all shapes share one mesh the screen-space
  // outline remains the outer silhouette only.
  const leftHandX = pose === 'gesture' ? -0.78 : -0.61;
  shapes.push(polygon([
    [-0.42, 2.78], [-0.55, 2.72], [leftHandX - 0.07, 1.30],
    [leftHandX + 0.07, 1.26], [-0.39, 2.40],
  ]));
  shapes.push(polygon([
    [0.42, 2.78], [0.54, 2.70], [0.64, 1.33],
    [0.50, 1.28], [0.38, 2.40],
  ]));

  // Long, slightly asymmetric legs and shoes.
  shapes.push(polygon([
    [-0.22, 0.98], [-0.07, 0.98], [-0.09, 0.12],
    [-0.25, 0.12], [-0.33, 0.04], [-0.08, 0.02],
    [-0.03, 0.09], [0.00, 0.98],
  ]));
  shapes.push(polygon([
    [0.05, 0.98], [0.20, 0.98], [0.23, 0.14],
    [0.36, 0.11], [0.39, 0.05], [0.15, 0.03],
    [0.08, 0.10],
  ]));

  // Neck/head/hat overlap into one readable graphic unit.
  shapes.push(rectangle(0, 3.18, 0.20, 0.34));
  shapes.push(ellipse(0, 3.47, 0.27, 0.32));
  shapes.push(rectangle(0, 3.76, 0.92, 0.065));
  shapes.push(polygon([
    [-0.31, 3.77], [-0.25, 4.12], [0.23, 4.12], [0.31, 3.77],
  ]));

  return shapes;
}

function crowdShapes(variant, pose) {
  const flare = [0.48, 0.56, 0.64][variant % 3];
  const shoulder = [0.42, 0.48, 0.44][variant % 3];
  const shapes = [
    polygon([
      [-0.25, 0.66], [-flare, 0.76], [-0.45, 2.02],
      [-shoulder, 2.46], [-0.24, 2.68], [0.24, 2.68],
      [shoulder, 2.46], [0.45, 2.02], [flare, 0.76], [0.25, 0.66],
    ]),
  ];

  const step = pose === 'walk' ? 0.08 : 0;
  shapes.push(polygon([
    [-0.18, 0.76], [-0.05, 0.76], [-0.08 - step, 0.09],
    [-0.24 - step, 0.08], [-0.29 - step, 0.03], [-0.05 - step, 0.02],
  ]));
  shapes.push(polygon([
    [0.04, 0.76], [0.17, 0.76], [0.21 + step, 0.10],
    [0.31 + step, 0.08], [0.34 + step, 0.03], [0.11 + step, 0.02],
  ]));

  const gesture = pose === 'gesture' ? 0.22 : 0;
  shapes.push(polygon([
    [-0.37, 2.38], [-0.48, 2.30], [-0.55 - gesture, 1.24],
    [-0.43 - gesture, 1.20], [-0.31, 2.12],
  ]));
  shapes.push(polygon([
    [0.37, 2.38], [0.48, 2.30], [0.55, 1.24],
    [0.43, 1.20], [0.31, 2.12],
  ]));

  shapes.push(rectangle(0, 2.73, 0.16, 0.25));
  shapes.push(ellipse(0, 2.94, 0.26, 0.29));

  if (variant % 4 !== 3) {
    const brim = variant % 3 === 1 ? 0.72 : 0.88;
    shapes.push(rectangle(0, 3.20, brim, 0.055));
    const crownW = variant % 3 === 1 ? 0.50 : 0.60;
    shapes.push(polygon([
      [-crownW * 0.46, 3.21], [-crownW * 0.38, 3.50],
      [ crownW * 0.34, 3.50], [ crownW * 0.46, 3.21],
    ]));
  }

  return shapes;
}

function addHeroDetails(group, material) {
  const lapelGeometry = new THREE.BufferGeometry().setFromPoints([
    new THREE.Vector3(-0.12, 2.82, 0.014),
    new THREE.Vector3(-0.02, 2.42, 0.014),
    new THREE.Vector3( 0.08, 2.73, 0.014),
  ]);
  group.add(new THREE.Line(lapelGeometry, material));

  const sleeveGeometry = new THREE.BufferGeometry().setFromPoints([
    new THREE.Vector3(0.46, 2.22, 0.014),
    new THREE.Vector3(0.56, 1.60, 0.014),
  ]);
  group.add(new THREE.Line(sleeveGeometry, material));
}

export function createCharacter({
  name,
  position,
  yaw = 0,
  scale = 1,
  pose = 'neutral',
  kind = 'crowd',
  variant = 0,
  materials,
  parent,
  outlineTargets,
}) {
  const group = new THREE.Group();
  group.name = name;
  group.position.fromArray(position);
  group.rotation.y = yaw;
  group.scale.setScalar(scale);

  const shapes = kind === 'hero' ? heroShapes(pose) : crowdShapes(variant, pose);
  const silhouette = new THREE.Mesh(
    new THREE.ShapeGeometry(shapes, 18),
    materials.character,
  );
  silhouette.name = `${name} silhouette`;
  group.add(silhouette);
  outlineTargets.push(silhouette);

  if (kind === 'hero') addHeroDetails(group, materials.detailLine);

  parent.add(group);
  return group;
}
