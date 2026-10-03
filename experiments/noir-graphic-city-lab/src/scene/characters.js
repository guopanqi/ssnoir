import * as THREE from 'three';

function limb(material, radius, length, angleZ = 0) {
  const mesh = new THREE.Mesh(
    new THREE.CylinderGeometry(radius, radius * 0.84, length, 6, 1, false),
    material,
  );
  mesh.rotation.z = angleZ;
  return mesh;
}

function addEdges(mesh, group, material, threshold = 20) {
  const edge = new THREE.LineSegments(new THREE.EdgesGeometry(mesh.geometry, threshold), material);
  edge.position.copy(mesh.getWorldPosition(new THREE.Vector3()));
  edge.quaternion.copy(mesh.getWorldQuaternion(new THREE.Quaternion()));
  edge.scale.copy(mesh.getWorldScale(new THREE.Vector3()));
  group.add(edge);
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
  const lineMat = role === 'crowd' ? mats.lineDim : mats.line;

  const coatBottom = role === 'hero' ? 1.24 : (variant % 3 === 2 ? 1.08 : 0.92);
  const coatLength = role === 'hero' ? 2.75 : 2.15;
  const torso = new THREE.Mesh(
    new THREE.CylinderGeometry(0.60, coatBottom, coatLength, 6),
    silhouette,
  );
  torso.position.y = role === 'hero' ? 2.03 : 2.18;
  g.add(torso);

  const shoulder = new THREE.Mesh(new THREE.BoxGeometry(role === 'hero' ? 1.58 : 1.28, 0.34, 0.56), silhouette);
  shoulder.position.y = 3.25;
  g.add(shoulder);

  const head = new THREE.Mesh(new THREE.SphereGeometry(0.47, 8, 6), mats.character);
  head.scale.set(0.92, 1.04, 0.88);
  head.position.y = 4.04;
  g.add(head);

  if (role !== 'singer' && !(role === 'crowd' && variant % 4 === 3)) {
    const brimRadius = role === 'hero' ? 0.78 : (variant % 3 === 1 ? 0.58 : 0.68);
    const brim = new THREE.Mesh(new THREE.CylinderGeometry(brimRadius, brimRadius, 0.075, 12), mats.character);
    brim.position.y = 4.43;
    g.add(brim);

    const crown = new THREE.Mesh(new THREE.CylinderGeometry(0.40, 0.46, role === 'hero' ? 0.52 : 0.38, 10), mats.character);
    crown.position.y = 4.67;
    g.add(crown);
  } else if (role === 'singer') {
    const hair = new THREE.Mesh(new THREE.SphereGeometry(0.54, 8, 6, 0, Math.PI * 2, 0, Math.PI * 0.66), mats.character);
    hair.position.set(0, 4.20, -0.04);
    hair.rotation.x = Math.PI;
    g.add(hair);
  }

  const step = pose === 'walking' ? 0.18 : 0.04;
  const legA = limb(mats.character, 0.17, 1.55, step);
  legA.position.set(-0.30, 0.58, 0);
  g.add(legA);
  const legB = limb(mats.character, 0.17, 1.55, -step);
  legB.position.set(0.30, 0.58, 0);
  g.add(legB);

  const gesture = pose === 'gesture' ? 0.82 : pose === 'walking' ? 0.30 : 0.08;
  const armA = limb(silhouette, 0.15, 1.70, gesture);
  armA.position.set(-0.69, 2.72, 0);
  g.add(armA);
  const armB = limb(silhouette, 0.15, 1.70, pose === 'walking' ? -0.32 : -0.08);
  armB.position.set(0.69, 2.72, 0);
  g.add(armB);

  groups.characters.add(g);
  g.updateWorldMatrix(true, true);

  for (const child of g.children) {
    if (child.isMesh) addEdges(child, groups.lines, lineMat, 22);
  }

  if (role !== 'crowd') {
    const lapel = new THREE.Mesh(new THREE.PlaneGeometry(0.13, 1.20), mats.paperDim);
    lapel.position.set(role === 'singer' ? 0.18 : -0.20, 2.74, 0.60);
    lapel.rotation.z = role === 'singer' ? -0.10 : 0.14;
    g.add(lapel);
  }

  return g;
}
