import * as THREE from 'three';
import { createMaterials } from './materials.js';
import { addEdges, addWindowStrip, box } from './primitives.js';

function createTheater(outlines, materials) {
  const group = new THREE.Group();
  group.name = 'Theater';

  group.add(addEdges(box(18, 9, 13, materials.landmark, 0, 0, 0), outlines, materials, 'hero'));
  group.add(addEdges(box(8.2, 13, 7, materials.landmark, 0, 9, -1.4), outlines, materials, 'hero'));
  group.add(addEdges(box(12, 1.05, 3.2, materials.building, 0, 4.5, 8), outlines, materials, 'hero'));

  const sign = box(1.4, 7.5, 0.5, materials.red, -5.8, 7.8, 6.65);
  sign.castShadow = false;
  group.add(sign);

  addWindowStrip(group, materials, {
    x: 0, y: 4.9, z: 6.58,
    count: 9, spacing: 1.05, warm: true,
  });

  return group;
}

function createWarehouse(outlines, materials) {
  const group = new THREE.Group();
  group.name = 'Warehouse';

  group.add(addEdges(
    box(20, 6.2, 15, materials.buildingDim, 28, 0, -7),
    outlines, materials, 'hero',
  ));

  const roof = new THREE.Mesh(
    new THREE.CylinderGeometry(8.8, 8.8, 20, 3, 1, false, 0, Math.PI),
    materials.buildingDim,
  );
  roof.rotation.z = Math.PI * 0.5;
  roof.rotation.y = Math.PI * 0.5;
  roof.position.set(28, 6.2, -7);
  roof.scale.y = 0.42;
  roof.castShadow = true;
  group.add(addEdges(roof, outlines, materials, 'hero', 22));

  addWindowStrip(group, materials, {
    x: 28, y: 3.7, z: 0.55,
    count: 8, spacing: 1.8,
  });

  return group;
}

function createStreetRow(outlines, materials, seed, side) {
  const group = new THREE.Group();
  group.name = side > 0 ? 'NorthRow' : 'SouthRow';

  for (let i = 0; i < 7; i++) {
    const width = 7 + ((seed + i * 7) % 5);
    const depth = 9 + ((seed + i * 11) % 6);
    const height = 8 + ((seed + i * 13) % 13);
    const x = -38 + i * 12;
    const z = side * (17 + ((seed + i) % 3));
    const material = i % 3 === 0 ? materials.building : materials.buildingDim;
    const importance = i === 2 || i === 5 ? 'hero' : 'context';

    group.add(addEdges(
      box(width, height, depth, material, x, 0, z),
      outlines, materials, importance,
    ));

    if (i % 2 === 0) {
      addWindowStrip(group, materials, {
        x,
        y: 3.4,
        z: z - side * (depth / 2 + 0.05),
        count: Math.max(2, Math.floor(width / 1.8)),
        spacing: 1.45,
        warm: i % 4 === 0,
        phase: seed + i,
      });
    }

    if (i === 4) {
      group.add(addEdges(
        box(1.1, 4.8, 1.1, materials.buildingDim, x + width * 0.28, height, z),
        outlines, materials, 'context',
      ));
    }
  }

  return group;
}

function createFireEscape(outlines, materials, x, z) {
  const group = new THREE.Group();
  group.name = 'FireEscape';

  for (let i = 0; i < 4; i++) {
    const y = 3.4 + i * 2.6;
    group.add(addEdges(
      box(4.5, 0.16, 1.4, materials.buildingDim, x, y, z),
      outlines, materials, 'context', 5,
    ));
    group.add(
      box(4.5, 0.08, 0.08, materials.buildingDim, x, y + 0.8, z - 0.6),
      box(4.5, 0.08, 0.08, materials.buildingDim, x, y + 0.8, z + 0.6),
    );
  }

  return group;
}

function createLampGeometry(materials, x, z) {
  const group = new THREE.Group();
  group.name = 'StreetLamp';

  const pole = box(0.22, 4.4, 0.22, materials.buildingDim, x, 0, z);
  const head = box(0.75, 0.28, 0.55, materials.warm, x, 4.35, z);
  pole.castShadow = false;
  head.castShadow = false;
  group.add(pole, head);

  return group;
}

export function buildWorld(scene, profile) {
  const materials = createMaterials(profile);
  const world = new THREE.Group();
  const outlines = new THREE.Group();

  world.name = 'WorldGeometry';
  outlines.name = 'LineLayer';
  scene.add(world, outlines);

  const ground = new THREE.Mesh(new THREE.PlaneGeometry(150, 110), materials.ground);
  ground.rotation.x = -Math.PI / 2;
  ground.receiveShadow = true;
  world.add(ground);

  for (const z of [-7.8, 7.8]) {
    const sidewalk = new THREE.Mesh(
      new THREE.BoxGeometry(150, 0.22, 5.5),
      materials.sidewalk,
    );
    sidewalk.position.set(0, 0.1, z);
    sidewalk.receiveShadow = true;
    world.add(sidewalk);
  }

  const wetRoad = new THREE.Mesh(new THREE.PlaneGeometry(150, 12), materials.wetRoad);
  wetRoad.rotation.x = -Math.PI / 2;
  wetRoad.position.y = 0.015;
  wetRoad.receiveShadow = true;
  world.add(wetRoad);

  world.add(createTheater(outlines, materials));
  world.add(createWarehouse(outlines, materials));
  world.add(createStreetRow(outlines, materials, 3, 1));
  world.add(createStreetRow(outlines, materials, 9, -1));
  world.add(createFireEscape(outlines, materials, -21, 11.6));

  world.add(addEdges(
    box(7, 4.5, 1.1, materials.buildingDim, -28, 0, -22),
    outlines, materials, 'hero',
  ));

  const lampPositions = [];
  for (let x = -42; x <= 42; x += 14) {
    const position = [x, 4.2, -5.8];
    lampPositions.push(position);
    world.add(createLampGeometry(materials, x, -5.8));
  }

  return {
    materials,
    lampPositions,
    groups: { world, outlines },
  };
}
