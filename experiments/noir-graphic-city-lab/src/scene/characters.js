import * as THREE from 'three';

function shapeMesh(shape, material, z = 0) {
  const mesh = new THREE.Mesh(new THREE.ShapeGeometry(shape), material);
  mesh.position.z = z;
  return mesh;
}

function rect(w, h, material, x, y, z = 0) {
  const mesh = new THREE.Mesh(new THREE.PlaneGeometry(w, h), material);
  mesh.position.set(x, y, z);
  return mesh;
}

function ellipse(rx, ry, material, x, y, z = 0) {
  const mesh = new THREE.Mesh(new THREE.CircleGeometry(1, 48), material);
  mesh.scale.set(rx, ry, 1);
  mesh.position.set(x, y, z);
  return mesh;
}

function heroCoatShape() {
  const s = new THREE.Shape();
  s.moveTo(-0.30, 0.50);
  s.lineTo(-0.66, 0.62);
  s.lineTo(-0.60, 2.15);
  s.lineTo(-0.70, 2.62);
  s.lineTo(-0.57, 2.94);
  s.lineTo(-0.30, 3.10);
  s.lineTo(0.30, 3.10);
  s.lineTo(0.57, 2.94);
  s.lineTo(0.70, 2.62);
  s.lineTo(0.60, 2.15);
  s.lineTo(0.66, 0.62);
  s.lineTo(0.30, 0.50);
  s.lineTo(0.22, 1.06);
  s.lineTo(-0.22, 1.06);
  s.closePath();
  return s;
}

function crowdBodyShape(variant = 0) {
  const flare = [0.62, 0.72, 0.82][variant % 3];
  const shoulder = [0.58, 0.66, 0.62][variant % 3];
  const s = new THREE.Shape();
  s.moveTo(-0.34, 0.55);
  s.lineTo(-flare, 0.72);
  s.lineTo(-0.58, 2.18);
  s.lineTo(-shoulder, 2.72);
  s.lineTo(-0.30, 3.02);
  s.lineTo(0.30, 3.02);
  s.lineTo(shoulder, 2.72);
  s.lineTo(0.58, 2.18);
  s.lineTo(flare, 0.72);
  s.lineTo(0.34, 0.55);
  s.closePath();
  return s;
}

function hatCrownShape(width = 0.78, height = 0.46) {
  const s = new THREE.Shape();
  s.moveTo(-width * 0.42, 0);
  s.lineTo(-width * 0.34, height);
  s.lineTo(width * 0.30, height);
  s.lineTo(width * 0.42, 0);
  s.closePath();
  return s;
}

function addHeroDetails(g, material) {
  const lapel = rect(0.034, 0.46, material, -0.12, 2.56, 0.018);
  lapel.rotation.z = 0.22;
  g.add(lapel);

  const sleeve = rect(0.034, 0.52, material, 0.58, 2.02, 0.018);
  sleeve.rotation.z = -0.08;
  g.add(sleeve);

  const cuff = rect(0.16, 0.040, material, 0.60, 1.78, 0.019);
  cuff.rotation.z = -0.10;
  g.add(cuff);
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
}) {
  const g = new THREE.Group();
  g.name = name;
  g.position.fromArray(position);
  g.rotation.y = yaw;
  g.scale.setScalar(scale);

  const body = materials.character;

  if (kind === 'hero') {
    g.add(shapeMesh(heroCoatShape(), body, 0.004));

    const legL = rect(0.15, 1.10, body, -0.18, 0.56, 0.002);
    legL.rotation.z = 0.035;
    g.add(legL);
    const legR = rect(0.15, 1.10, body, 0.18, 0.56, 0.002);
    legR.rotation.z = -0.035;
    g.add(legR);

    const armL = rect(0.16, 1.36, body, -0.57, 2.18, 0.006);
    armL.rotation.z = pose === 'gesture' ? 0.48 : 0.12;
    g.add(armL);
    const armR = rect(0.16, 1.36, body, 0.57, 2.18, 0.006);
    armR.rotation.z = -0.10;
    g.add(armR);

    g.add(rect(0.24, 0.34, body, 0, 3.14, 0.008));
    g.add(ellipse(0.29, 0.34, body, 0, 3.46, 0.010));
    g.add(rect(0.96, 0.065, body, 0, 3.77, 0.012));
    const crown = shapeMesh(hatCrownShape(0.68, 0.37), body, 0.014);
    crown.position.set(0, 3.76, 0.014);
    g.add(crown);
    addHeroDetails(g, materials.detail);
  } else {
    g.add(shapeMesh(crowdBodyShape(variant), body, 0.004));

    const step = pose === 'walk' ? 0.13 : 0.025;
    const legL = rect(0.16, 0.92, body, -0.18, 0.46, 0.002);
    legL.rotation.z = step;
    g.add(legL);
    const legR = rect(0.16, 0.92, body, 0.18, 0.46, 0.002);
    legR.rotation.z = -step;
    g.add(legR);

    const armAngle = pose === 'gesture' ? 0.54 : pose === 'walk' ? 0.18 : 0.05;
    const armL = rect(0.18, 1.15, body, -0.56, 2.13, 0.006);
    armL.rotation.z = armAngle;
    g.add(armL);
    const armR = rect(0.18, 1.15, body, 0.56, 2.13, 0.006);
    armR.rotation.z = pose === 'walk' ? -0.20 : -0.05;
    g.add(armR);

    g.add(rect(0.20, 0.26, body, 0, 3.05, 0.008));
    g.add(ellipse(0.31, 0.33, body, 0, 3.32, 0.010));
    if (variant % 4 !== 3) {
      const brimWidth = variant % 3 === 1 ? 0.88 : 1.05;
      g.add(rect(brimWidth, 0.066, body, 0, 3.61, 0.012));
      const crown = shapeMesh(hatCrownShape(variant % 3 === 1 ? 0.64 : 0.74, 0.34), body, 0.014);
      crown.position.set(0, 3.60, 0.014);
      g.add(crown);
    }
  }

  parent.add(g);
  return g;
}
