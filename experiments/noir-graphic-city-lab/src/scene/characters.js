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

  // Asymmetric three-quarter detective silhouette. The unequal shoulders,
  // hem and arm spacing keep the figure from reading as a front-facing icon.
  shapes.push(polygon([
    [-0.25, 0.82], [-0.46, 0.94], [-0.43, 2.22],
    [-0.50, 2.63], [-0.39, 2.93], [-0.19, 3.08],
    [ 0.31, 3.04], [ 0.48, 2.86], [ 0.56, 2.58],
    [ 0.51, 2.20], [ 0.62, 0.91], [ 0.35, 0.78],
    [ 0.23, 1.04], [-0.18, 1.05],
  ]));

  const leftHandX = pose === 'gesture' ? -0.74 : -0.53;
  shapes.push(polygon([
    [-0.39, 2.77], [-0.50, 2.67], [leftHandX - 0.055, 1.30],
    [leftHandX + 0.075, 1.25], [-0.34, 2.39],
  ]));
  shapes.push(polygon([
    [0.40, 2.76], [0.52, 2.62], [0.75, 1.39],
    [0.62, 1.31], [0.37, 2.33],
  ]));

  shapes.push(polygon([
    [-0.18, 0.98], [-0.05, 0.98], [-0.10, 0.13],
    [-0.25, 0.11], [-0.34, 0.04], [-0.08, 0.02],
    [-0.02, 0.09], [0.00, 0.98],
  ]));
  shapes.push(polygon([
    [0.08, 0.98], [0.21, 0.97], [0.29, 0.16],
    [0.42, 0.12], [0.46, 0.06], [0.22, 0.03],
    [0.13, 0.10],
  ]));

  shapes.push(rectangle(0.055, 3.17, 0.18, 0.34));
  shapes.push(ellipse(0.07, 3.47, 0.26, 0.32));
  shapes.push(rectangle(0.08, 3.76, 0.94, 0.065));
  shapes.push(polygon([
    [-0.22, 3.77], [-0.16, 4.12], [0.31, 4.12], [0.39, 3.77],
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
