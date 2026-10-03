import * as THREE from 'three';

function limb(material, radius, length, angleZ = 0, angleX = 0) {
  const mesh = new THREE.Mesh(
    new THREE.CylinderGeometry(radius, radius * 0.86, length, 5, 1, false),
    material,
  );
  mesh.rotation.z = angleZ;
  mesh.rotation.x = angleX;
  return mesh;
}

function addCharacterEdges(source, targetGroup, material) {
  source.traverse((child) => {
    if (!child.isMesh || !child.userData.graphicEdge) return;
    const edge = new THREE.LineSegments(
      new THREE.EdgesGeometry(child.geometry, 32),
      material,
    );
    edge.position.copy(child.getWorldPosition(new THREE.Vector3()));
    edge.quaternion.copy(child.getWorldQuaternion(new THREE.Quaternion()));
    edge.scale.copy(child.getWorldScale(new THREE.Vector3()));
    targetGroup.add(edge);
  });
}

export function createCharacter({
  name,
  position,
  yaw = 0,
  scale = 1,
  pose = 'neutral',
  role = 'crowd',
  mats,
  groups,
}) {
  const g = new THREE.Group();
  g.name = name;
  g.position.fromArray(position);
  g.rotation.y = yaw;
  g.scale.setScalar(scale);

  const bodyMat = role === 'hero' ? mats.inkLift : mats.ink;
  const torso = new THREE.Mesh(
    new THREE.CylinderGeometry(0.58, 0.82, 2.35, 5),
    bodyMat,
  );
  torso.position.y = 2.55;
  g.add(torso);

  const coat = new THREE.Mesh(
    new THREE.CylinderGeometry(0.72, role === 'singer' ? 1.18 : 1.02, 1.85, 5),
    bodyMat,
  );
  coat.position.y = 1.55;
  coat.userData.graphicEdge = true;
  g.add(coat);

  const head = new THREE.Mesh(new THREE.SphereGeometry(0.48, 7, 5), mats.ink);
  head.scale.set(0.90, 1.08, 0.88);
  head.position.y = 4.12;
  g.add(head);

  if (role !== 'singer') {
    const brim = new THREE.Mesh(new THREE.CylinderGeometry(0.78, 0.78, 0.08, 10), mats.ink);
    brim.position.y = 4.52;
    brim.userData.graphicEdge = true;
    g.add(brim);
    const crown = new THREE.Mesh(new THREE.CylinderGeometry(0.43, 0.48, 0.52, 8), mats.ink);
    crown.position.y = 4.76;
    crown.userData.graphicEdge = true;
    g.add(crown);
  } else {
    const hair = new THREE.Mesh(new THREE.SphereGeometry(0.54, 7, 5, 0, Math.PI * 2, 0, Math.PI * 0.62), mats.ink);
    hair.position.set(0, 4.28, -0.03);
    hair.rotation.x = Math.PI;
    g.add(hair);
  }

  const faceMat = role === 'crowd' ? mats.paperDim : mats.paper;
  const face = new THREE.Mesh(new THREE.PlaneGeometry(role === 'crowd' ? 0.18 : 0.28, role === 'crowd' ? 0.09 : 0.14), faceMat);
  face.position.set(0, 4.13, 0.45);
  face.userData.noEdge = true;
  g.add(face);

  const legA = limb(mats.ink, 0.18, 1.48, pose === 'walking' ? 0.16 : 0.04);
  legA.position.set(-0.34, 0.52, 0);
  g.add(legA);
  const legB = limb(mats.ink, 0.18, 1.48, pose === 'walking' ? -0.18 : -0.04);
  legB.position.set(0.34, 0.52, 0);
  g.add(legB);

  const leftArmAngle = pose === 'gesture' ? 1.02 : pose === 'walking' ? 0.34 : 0.10;
  const rightArmAngle = pose === 'walking' ? -0.38 : -0.10;
  const armA = limb(bodyMat, 0.16, 1.75, leftArmAngle);
  armA.position.set(-0.78, 2.75, 0);
  g.add(armA);
  const armB = limb(bodyMat, 0.16, 1.75, rightArmAngle);
  armB.position.set(0.78, 2.75, 0);
  g.add(armB);

  if (role !== 'crowd') {
    const lapel = new THREE.Mesh(new THREE.PlaneGeometry(role === 'singer' ? 0.24 : 0.20, 1.34), role === 'singer' ? mats.gold : mats.paperDim);
    lapel.position.set(role === 'singer' ? 0.22 : -0.24, 2.78, 0.64);
    lapel.rotation.z = role === 'singer' ? -0.12 : 0.15;
    if (role === 'singer') lapel.userData.graphicAccent = true;
    g.add(lapel);
  }

  groups.characters.add(g);

  if (role === 'hero' || role === 'singer') {
    g.updateWorldMatrix(true, true);
    addCharacterEdges(g, groups.lines, mats.line);
  }

  return g;
}
