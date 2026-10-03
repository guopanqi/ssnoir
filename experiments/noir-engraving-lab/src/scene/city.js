import * as THREE from 'three';
import { createMaterials } from './materials.js';
import { addEdges, addWindowStrip, box } from './primitives.js';

function createTheater(outlines, materials, x = -2, z = 17) {
  const group = new THREE.Group();
  group.name = 'Theater';

  const width = 16;
  const depth = 13;
  const frontZ = z - depth * 0.5;

  group.add(addEdges(
    box(width, 9, depth, materials.landmark, x, 0, z),
    outlines, materials, 'hero',
  ));

  // A taller rear mass gives the theater a recognizable skyline without
  // spending geometry on facade detail.
  group.add(addEdges(
    box(7.6, 13, 6.4, materials.landmark, x + 0.8, 9, z + 1.6),
    outlines, materials, 'hero',
  ));

  // Stepped stage-house crown: a silhouette feature, not facade detail.
  // From the high-city shot this is what separates the theater from the
  // generic rectangular street blocks.
  group.add(addEdges(
    box(6.2, 1.8, 5.4, materials.landmark, x + 0.8, 22.0, z + 1.6),
    outlines, materials, 'hero',
  ));
  group.add(addEdges(
    box(4.4, 1.35, 4.1, materials.landmark, x + 0.8, 23.8, z + 1.6),
    outlines, materials, 'hero',
  ));
  group.add(addEdges(
    box(0.55, 2.8, 0.55, materials.landmark, x + 0.8, 25.15, z + 1.6),
    outlines, materials, 'hero',
  ));

  // A low entrance mass anchors the marquee so the street-level shot reads
  // "theater entrance" before relying on windows or the red blade sign.
  group.add(addEdges(
    box(9.6, 3.0, 1.8, materials.landmark, x, 0, frontZ - 0.55),
    outlines, materials, 'hero',
  ));

  // The marquee projects toward the street. It should read in silhouette
  // before windows or printed linework do.
  group.add(addEdges(
    box(11.5, 1.05, 2.8, materials.building, x, 4.5, frontZ - 1.25),
    outlines, materials, 'hero',
  ));

  const sign = box(1.25, 7.5, 0.5, materials.red, x - 5.6, 7.8, frontZ - 0.28);
  sign.castShadow = false;
  group.add(sign);

  addWindowStrip(group, materials, {
    x,
    y: 4.9,
    z: frontZ - 0.05,
    count: 8,
    spacing: 1.05,
    warm: true,
  });

  return group;
}

function createWarehouse(outlines, materials, x = 28, z = -18) {
  const group = new THREE.Group();
  group.name = 'Warehouse';

  const depth = 15;
  const frontZ = z + depth * 0.5;

  group.add(addEdges(
    box(20, 6.2, depth, materials.buildingDim, x, 0, z),
    outlines, materials, 'hero',
  ));

  const roof = new THREE.Mesh(
    new THREE.CylinderGeometry(8.8, 8.8, 20, 3, 1, false, 0, Math.PI),
    materials.buildingDim,
  );
  roof.rotation.z = Math.PI * 0.5;
  roof.rotation.y = Math.PI * 0.5;
  roof.position.set(x, 6.2, z);
  roof.scale.y = 0.42;
  roof.castShadow = true;
  group.add(addEdges(roof, outlines, materials, 'hero', 22));

  addWindowStrip(group, materials, {
    x,
    y: 3.7,
    z: frontZ + 0.05,
    count: 8,
    spacing: 1.8,
  });

  return group;
}

function createStreetRow(outlines, materials, {
  seed,
  side,
  skip = [],
}) {
  const group = new THREE.Group();
  group.name = side > 0 ? 'NorthRow' : 'SouthRow';
  const skipped = new Set(skip);

  for (let i = 0; i < 7; i++) {
    if (skipped.has(i)) continue;

    const width = 7 + ((seed + i * 7) % 5);
    const depth = 9 + ((seed + i * 11) % 6);
    const height = 8 + ((seed + i * 13) % 13);
    const x = -38 + i * 12;
    const z = side * (19 + ((seed + i) % 3));
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

function createAlley(outlines, materials) {
  const group = new THREE.Group();
  group.name = 'Alley';

  // The alley is a real break in the south street wall (row index 1 is
  // omitted). A dark terminal gate gives the view a destination.
  group.add(addEdges(
    box(8.5, 4.8, 1.1, materials.buildingDim, -26, 0, -31),
    outlines, materials, 'hero',
  ));

  // Two shallow service wings narrow the mouth without sealing it.
  group.add(addEdges(
    box(2.4, 6.5, 11, materials.buildingDim, -33.0, 0, -24.0),
    outlines, materials, 'context',
  ));
  group.add(addEdges(
    box(2.2, 8.0, 10, materials.buildingDim, -19.3, 0, -24.5),
    outlines, materials, 'context',
  ));

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

  for (const z of [-8.0, 8.0]) {
    const sidewalk = new THREE.Mesh(
      new THREE.BoxGeometry(150, 0.22, 5.4),
      materials.sidewalk,
    );
    sidewalk.position.set(0, 0.1, z);
    sidewalk.receiveShadow = true;
    world.add(sidewalk);
  }

  const wetRoad = new THREE.Mesh(new THREE.PlaneGeometry(150, 11.8), materials.wetRoad);
  wetRoad.rotation.x = -Math.PI / 2;
  wetRoad.position.y = 0.015;
  wetRoad.receiveShadow = true;
  world.add(wetRoad);

  // North wall: reserve one full parcel for the theater.
  world.add(createStreetRow(outlines, materials, {
    seed: 3,
    side: 1,
    skip: [3],
  }));
  world.add(createTheater(outlines, materials));

  // South wall: reserve a western parcel for the alley and the eastern
  // parcels for the warehouse. Landmarks replace generic fill rather than
  // stacking on top of it.
  world.add(createStreetRow(outlines, materials, {
    seed: 9,
    side: -1,
    skip: [1, 5, 6],
  }));
  world.add(createAlley(outlines, materials));
  world.add(createWarehouse(outlines, materials));

  world.add(createFireEscape(outlines, materials, -12.5, 12.4));

  // Sparse lamps create punctuation instead of an evenly sampled grid.
  const lampPositions = [
    [-40, 4.2, -5.8],
    [-16, 4.2, -5.8],
    [16, 4.2, -5.8],
    [40, 4.2, -5.8],
  ];

  for (const position of lampPositions) {
    world.add(createLampGeometry(materials, position[0], position[2]));
  }

  return {
    materials,
    lampPositions,
    groups: { world, outlines },
  };
}
