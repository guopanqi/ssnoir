import * as THREE from 'three';

const C = {
  void: 0x080b12,
  asphalt: 0x0b1018,
  building: 0x121b2a,
  buildingDim: 0x0d1420,
  landmark: 0x1a2638,
  edge: 0xd8e1ef,
  edgeDim: 0x526078,
  warm: 0xf3d08a,
  cool: 0xa7d5ff,
  red: 0xb3402a,
};

const flat = (color, extra = {}) => new THREE.MeshStandardMaterial({
  color,
  roughness: 1,
  metalness: 0,
  flatShading: true,
  ...extra,
});

const materials = {
  ground: flat(C.asphalt),
  building: flat(C.building),
  buildingDim: flat(C.buildingDim),
  landmark: flat(C.landmark),
  edge: new THREE.LineBasicMaterial({ color: C.edge, transparent: true, opacity: 0.72 }),
  edgeDim: new THREE.LineBasicMaterial({ color: C.edgeDim, transparent: true, opacity: 0.36 }),
  warm: flat(0x171615, { emissive: C.warm, emissiveIntensity: 5.0 }),
  cool: flat(0x11161d, { emissive: C.cool, emissiveIntensity: 3.6 }),
  red: flat(0x160d0d, { emissive: C.red, emissiveIntensity: 2.3 }),
};

function edged(mesh, outlineGroup, important = false, threshold = 32) {
  const edges = new THREE.EdgesGeometry(mesh.geometry, threshold);
  const line = new THREE.LineSegments(edges, important ? materials.edge : materials.edgeDim);
  line.position.copy(mesh.position);
  line.rotation.copy(mesh.rotation);
  line.scale.copy(mesh.scale);
  line.userData.outline = true;
  outlineGroup.add(line);
  return mesh;
}

function box(w, h, d, material, x, y, z) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
  m.position.set(x, y + h * 0.5, z);
  m.castShadow = true;
  m.receiveShadow = true;
  return m;
}

function windowStrip(parent, x, y, z, count, spacing, vertical = false, warm = false) {
  const mat = warm ? materials.warm : materials.cool;
  for (let i = 0; i < count; i++) {
    if ((i * 17 + count * 3) % 5 === 0) continue;
    const w = vertical ? 0.32 : 0.7;
    const h = vertical ? 0.8 : 0.34;
    const pane = box(w, h, 0.08, mat,
      x + (vertical ? 0 : (i - (count - 1) / 2) * spacing),
      y + (vertical ? i * spacing : 0),
      z);
    pane.castShadow = false;
    parent.add(pane);
  }
}

function createTheater(outlines) {
  const g = new THREE.Group();
  g.name = 'Theater';
  const base = edged(box(18, 9, 13, materials.landmark, 0, 0, 0), outlines, true);
  g.add(base);
  const tower = edged(box(8.2, 13, 7, materials.landmark, 0, 9, -1.4), outlines, true);
  g.add(tower);
  const marquee = edged(box(12, 1.05, 3.2, materials.building, 0, 4.5, 8), outlines, true);
  g.add(marquee);
  const sign = box(1.4, 7.5, 0.5, materials.red, -5.8, 7.8, 6.65);
  sign.castShadow = false;
  g.add(sign);
  windowStrip(g, 0, 4.9, 6.58, 9, 1.05, false, true);
  return g;
}

function createWarehouse(outlines) {
  const g = new THREE.Group();
  g.name = 'Warehouse';
  const body = edged(box(20, 6.2, 15, materials.buildingDim, 28, 0, -7), outlines, true);
  g.add(body);
  const roof = new THREE.Mesh(new THREE.CylinderGeometry(8.8, 8.8, 20, 3, 1, false, 0, Math.PI), materials.buildingDim);
  roof.rotation.z = Math.PI * 0.5;
  roof.rotation.y = Math.PI * 0.5;
  roof.position.set(28, 6.2, -7);
  roof.scale.y = 0.42;
  roof.castShadow = true;
  g.add(edged(roof, outlines, true, 22));
  windowStrip(g, 28, 3.7, 0.55, 8, 1.8, false, false);
  return g;
}

function createRow(outlines, seed, side = 1) {
  const g = new THREE.Group();
  for (let i = 0; i < 7; i++) {
    const w = 7 + ((seed + i * 7) % 5);
    const d = 9 + ((seed + i * 11) % 6);
    const h = 8 + ((seed + i * 13) % 13);
    const x = -38 + i * 12;
    const z = side * (17 + ((seed + i) % 3));
    const mat = i % 3 === 0 ? materials.building : materials.buildingDim;
    const b = edged(box(w, h, d, mat, x, 0, z), outlines, i === 2 || i === 5);
    g.add(b);
    if (i % 2 === 0) windowStrip(g, x, 3.4, z - side * (d / 2 + 0.05), Math.max(2, Math.floor(w / 1.8)), 1.45, false, i % 4 === 0);
    if (i === 4) {
      const stack = edged(box(1.1, 4.8, 1.1, materials.buildingDim, x + w * 0.28, h, z), outlines, false);
      g.add(stack);
    }
  }
  return g;
}

function createFireEscape(outlines, x, z) {
  const g = new THREE.Group();
  for (let i = 0; i < 4; i++) {
    const y = 3.4 + i * 2.6;
    const landing = box(4.5, 0.16, 1.4, materials.buildingDim, x, y, z);
    g.add(edged(landing, outlines, false, 5));
    const railA = box(4.5, 0.08, 0.08, materials.buildingDim, x, y + 0.8, z - 0.6);
    const railB = box(4.5, 0.08, 0.08, materials.buildingDim, x, y + 0.8, z + 0.6);
    g.add(railA, railB);
  }
  return g;
}

function createLamp(x, z, lightGroup, coneGroup) {
  const g = new THREE.Group();
  const pole = box(0.22, 4.4, 0.22, materials.buildingDim, x, 0, z);
  const head = box(0.75, 0.28, 0.55, materials.warm, x, 4.35, z);
  pole.castShadow = false; head.castShadow = false;
  g.add(pole, head);

  const p = new THREE.PointLight(C.warm, 16, 15, 2.1);
  p.position.set(x, 4.2, z);
  p.castShadow = false;
  lightGroup.add(p);

  const coneMat = new THREE.MeshBasicMaterial({
    color: C.warm,
    transparent: true,
    opacity: 0.045,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
  });
  const cone = new THREE.Mesh(new THREE.ConeGeometry(4.8, 9, 24, 1, true), coneMat);
  cone.position.set(x, 0.1, z);
  cone.rotation.x = Math.PI;
  coneGroup.add(cone);
  return g;
}

export function buildWorld(scene) {
  scene.background = new THREE.Color(C.void);
  scene.fog = new THREE.FogExp2(C.void, 0.024);

  const world = new THREE.Group();
  const outlines = new THREE.Group();
  const atmosphere = new THREE.Group();
  const lightCones = new THREE.Group();
  const smallLights = new THREE.Group();
  scene.add(world, outlines, atmosphere, lightCones, smallLights);

  const ground = new THREE.Mesh(new THREE.PlaneGeometry(150, 110), materials.ground);
  ground.rotation.x = -Math.PI / 2;
  ground.receiveShadow = true;
  world.add(ground);

  const sidewalkMat = flat(0x101722);
  for (const z of [-7.8, 7.8]) {
    const walk = new THREE.Mesh(new THREE.BoxGeometry(150, 0.22, 5.5), sidewalkMat);
    walk.position.set(0, 0.1, z);
    walk.receiveShadow = true;
    world.add(walk);
  }

  world.add(createTheater(outlines));
  world.add(createWarehouse(outlines));
  world.add(createRow(outlines, 3, 1));
  world.add(createRow(outlines, 9, -1));
  world.add(createFireEscape(outlines, -21, 11.6));

  for (let x = -42; x <= 42; x += 14) {
    world.add(createLamp(x, -5.8, smallLights, lightCones));
  }

  const alleyGate = edged(box(7, 4.5, 1.1, materials.buildingDim, -28, 0, -22), outlines, true);
  world.add(alleyGate);

  const wetRoad = new THREE.Mesh(
    new THREE.PlaneGeometry(150, 12),
    new THREE.MeshPhysicalMaterial({ color: 0x070b11, roughness: 0.22, metalness: 0.2, clearcoat: 0.55, clearcoatRoughness: 0.18 })
  );
  wetRoad.rotation.x = -Math.PI / 2;
  wetRoad.position.y = 0.015;
  wetRoad.receiveShadow = true;
  world.add(wetRoad);

  const rainCount = 1900;
  const rainGeom = new THREE.BufferGeometry();
  const rainPos = new Float32Array(rainCount * 3);
  const rainSpeed = new Float32Array(rainCount);
  for (let i = 0; i < rainCount; i++) {
    rainPos[i * 3] = (Math.random() - 0.5) * 125;
    rainPos[i * 3 + 1] = 3 + Math.random() * 34;
    rainPos[i * 3 + 2] = (Math.random() - 0.5) * 75;
    rainSpeed[i] = 12 + Math.random() * 16;
  }
  rainGeom.setAttribute('position', new THREE.BufferAttribute(rainPos, 3));
  const rain = new THREE.Points(rainGeom, new THREE.PointsMaterial({
    color: 0xb9cce0,
    size: 0.055,
    transparent: true,
    opacity: 0.48,
    depthWrite: false,
  }));
  atmosphere.add(rain);

  const sun = new THREE.DirectionalLight(0xc8d9ff, 1.5);
  sun.position.set(-22, 34, 18);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.camera.left = -55;
  sun.shadow.camera.right = 55;
  sun.shadow.camera.top = 42;
  sun.shadow.camera.bottom = -42;
  sun.shadow.camera.near = 1;
  sun.shadow.camera.far = 95;
  scene.add(sun);

  const fill = new THREE.DirectionalLight(0x6d8fc7, 0.42);
  fill.position.set(34, 16, -28);
  scene.add(fill);

  const ambient = new THREE.HemisphereLight(0x26354c, 0x05070b, 0.48);
  scene.add(ambient);

  let raining = true;
  function update(dt) {
    if (!raining) return;
    const p = rain.geometry.attributes.position.array;
    for (let i = 0; i < rainCount; i++) {
      p[i * 3 + 1] -= rainSpeed[i] * dt;
      p[i * 3] -= 2.8 * dt;
      if (p[i * 3 + 1] < 0.15) {
        p[i * 3 + 1] = 25 + Math.random() * 15;
        p[i * 3] = (Math.random() - 0.5) * 125;
        p[i * 3 + 2] = (Math.random() - 0.5) * 75;
      }
    }
    rain.geometry.attributes.position.needsUpdate = true;
  }

  return {
    groups: { world, outlines, atmosphere, lightCones, smallLights },
    update,
    setRain(v) { raining = v; atmosphere.visible = v; },
    get raining() { return raining; },
  };
}
