import * as THREE from 'three';

function limb(material, radius, length, angleZ = 0) {
  const mesh = new THREE.Mesh(
    new THREE.CylinderGeometry(radius, radius * 0.84, length, 8, 1, false),
    material,
  );
  mesh.rotation.z = angleZ;
  return mesh;
}

function addOutlineShell(mesh, group, material, amount = 1.035) {
  const shell = new THREE.Mesh(mesh.geometry, material);
  shell.position.copy(mesh.position);
  shell.quaternion.copy(mesh.quaternion);
  shell.scale.copy(mesh.scale).multiplyScalar(amount);
  shell.renderOrder = -1;
  group.add(shell);
}

export function createCharacter({
  name,
  position,
  yaw = 0,
  scale = 1,
  pose = 'neutral',
  role = 'crowd',
  variant = 0,
  mats,
  groups,
}) {
  const g = new THREE.Group();
  g.name = name;
  g.position.fromArray(position);
  g.rotation.y = yaw;
  g.scale.setScalar(scale);

  const silhouette = role === 'hero' ? mats.characterLift : mats.character;
  const outlineMat = role === 'crowd' ? mats.outlineDim : mats.outline;
  const shellAmount = role === 'hero' ? 1.045 : 1.035;
  const outlined = [];

  const coatBottom = role === 'hero' ? 1.20 : (variant % 3 === 2 ? 1.04 : 0.90);
  const coatLength = role === 'hero' ? 2.82 : 2.18;
  const torso = new THREE.Mesh(
    new THREE.CylinderGeometry(0.58, coatBottom, coatLength, 8),
    silhouette,
  );
  torso.position.y = role === 'hero' ? 2.02 : 2.18;
  g.add(torso); outlined.push(torso);

  const shoulder = new THREE.Mesh(
    new THREE.BoxGeometry(role === 'hero' ? 1.52 : 1.24, 0.32, 0.54),
    silhouette,
  );
  shoulder.position.y = 3.23;
  g.add(shoulder); outlined.push(shoulder);

  const head = new THREE.Mesh(new THREE.SphereGeometry(0.46, 12, 8), mats.character);
  head.scale.set(0.91, 1.04, 0.87);
  head.position.y = 4.03;
  g.add(head); outlined.push(head);

  if (role !== 'singer' && !(role === 'crowd' && variant % 4 === 3)) {
    const brimRadius = role === 'hero' ? 0.77 : (variant % 3 === 1 ? 0.57 : 0.67);
    const brim = new THREE.Mesh(new THREE.CylinderGeometry(brimRadius, brimRadius, 0.065, 18), mats.character);
    brim.position.y = 4.43;
    g.add(brim); outlined.push(brim);

    const crown = new THREE.Mesh(
      new THREE.CylinderGeometry(0.39, 0.45, role === 'hero' ? 0.50 : 0.37, 14),
      mats.character,
    );
    crown.position.y = 4.66;
    g.add(crown); outlined.push(crown);
  } else if (role === 'singer') {
    const hair = new THREE.Mesh(
      new THREE.SphereGeometry(0.53, 12, 8, 0, Math.PI * 2, 0, Math.PI * 0.66),
      mats.character,
    );
    hair.position.set(0, 4.19, -0.04);
    hair.rotation.x = Math.PI;
    g.add(hair); outlined.push(hair);
  }

  const step = pose === 'walking' ? 0.18 : 0.035;
  const legA = limb(mats.character, 0.16, 1.52, step);
  legA.position.set(-0.30, 0.57, 0);
  g.add(legA); outlined.push(legA);
  const legB = limb(mats.character, 0.16, 1.52, -step);
  legB.position.set(0.30, 0.57, 0);
  g.add(legB); outlined.push(legB);

  const gesture = pose === 'gesture' ? 0.82 : pose === 'walking' ? 0.30 : 0.08;
  const armA = limb(silhouette, 0.145, 1.66, gesture);
  armA.position.set(-0.68, 2.70, 0);
  g.add(armA); outlined.push(armA);
  const armB = limb(silhouette, 0.145, 1.66, pose === 'walking' ? -0.32 : -0.08);
  armB.position.set(0.68, 2.70, 0);
  g.add(armB); outlined.push(armB);

  for (const mesh of outlined) addOutlineShell(mesh, g, outlineMat, shellAmount);

  if (role !== 'crowd') {
    const lapelA = new THREE.Mesh(new THREE.PlaneGeometry(0.10, 1.12), mats.paperDim);
    lapelA.position.set(role === 'singer' ? 0.18 : -0.18, 2.72, 0.60);
    lapelA.rotation.z = role === 'singer' ? -0.10 : 0.14;
    g.add(lapelA);

    const lapelB = new THREE.Mesh(new THREE.PlaneGeometry(0.055, 0.74), mats.paperDim);
    lapelB.position.set(role === 'singer' ? -0.12 : 0.14, 2.86, 0.605);
    lapelB.rotation.z = role === 'singer' ? 0.18 : -0.16;
    g.add(lapelB);
  }

  groups.characters.add(g);
  return g;
}
