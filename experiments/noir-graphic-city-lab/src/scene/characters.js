import * as THREE from 'three';

function makeShell(mesh, material, scale = 1.035) {
  const shell = new THREE.Mesh(mesh.geometry, material);
  shell.position.copy(mesh.position);
  shell.quaternion.copy(mesh.quaternion);
  shell.scale.copy(mesh.scale).multiplyScalar(scale);
  return shell;
}

function cylinder(material, r1, r2, length, sides = 10) {
  return new THREE.Mesh(new THREE.CylinderGeometry(r1, r2, length, sides), material);
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
  const outline = kind === 'hero' ? materials.heroOutline : materials.crowdOutline;
  const parts = [];

  const coatBottom = kind === 'hero' ? 1.18 : [0.86, 0.98, 1.08][variant % 3];
  const coat = cylinder(body, 0.58, coatBottom, kind === 'hero' ? 2.9 : 2.15, 12);
  coat.position.y = kind === 'hero' ? 2.05 : 2.18;
  g.add(coat); parts.push(coat);

  const shoulders = new THREE.Mesh(
    new THREE.BoxGeometry(kind === 'hero' ? 1.48 : 1.20, 0.30, 0.54),
    body,
  );
  shoulders.position.y = 3.25;
  g.add(shoulders); parts.push(shoulders);

  const head = new THREE.Mesh(new THREE.SphereGeometry(0.45, 16, 10), body);
  head.scale.set(0.91, 1.04, 0.88);
  head.position.y = 4.04;
  g.add(head); parts.push(head);

  if (!(kind === 'crowd' && variant % 4 === 3)) {
    const brimR = kind === 'hero' ? 0.78 : (variant % 3 === 1 ? 0.56 : 0.66);
    const brim = cylinder(body, brimR, brimR, 0.065, 20);
    brim.position.y = 4.43;
    g.add(brim); parts.push(brim);

    const crown = cylinder(body, 0.39, 0.45, kind === 'hero' ? 0.50 : 0.37, 16);
    crown.position.y = 4.66;
    g.add(crown); parts.push(crown);
  }

  const step = pose === 'walk' ? 0.18 : 0.035;
  const legL = cylinder(body, 0.15, 0.17, 1.48, 10);
  legL.position.set(-0.29, 0.56, 0); legL.rotation.z = step;
  g.add(legL); parts.push(legL);
  const legR = cylinder(body, 0.15, 0.17, 1.48, 10);
  legR.position.set(0.29, 0.56, 0); legR.rotation.z = -step;
  g.add(legR); parts.push(legR);

  const armAngle = pose === 'gesture' ? 0.72 : pose === 'walk' ? 0.26 : 0.07;
  const armL = cylinder(body, 0.135, 0.15, 1.55, 10);
  armL.position.set(-0.66, 2.70, 0); armL.rotation.z = armAngle;
  g.add(armL); parts.push(armL);
  const armR = cylinder(body, 0.135, 0.15, 1.55, 10);
  armR.position.set(0.66, 2.70, 0); armR.rotation.z = pose === 'walk' ? -0.29 : -0.07;
  g.add(armR); parts.push(armR);

  for (const part of parts) g.add(makeShell(part, outline, kind === 'hero' ? 1.020 : 1.012));

  if (kind === 'hero') {
    const lapel = new THREE.Mesh(new THREE.PlaneGeometry(0.10, 1.10), materials.detail);
    lapel.position.set(-0.18, 2.74, 0.59);
    lapel.rotation.z = 0.14;
    g.add(lapel);
  }

  parent.add(g);
  return g;
}
