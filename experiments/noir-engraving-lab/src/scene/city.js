import * as THREE from 'three';
import { createMaterials } from './materials.js';
import { addEdges, addWindowStrip, box } from './primitives.js';

const FRONT_Z = 14;

function facadeZ(side, offset = 0.05) {
  return side * FRONT_Z - side * offset;
}

function centerZ(side, depth) {
  return side * (FRONT_Z + depth * 0.5);
}

function createTheater(outlines, materials) {
  const group = new THREE.Group();
  group.name = 'Theater';

  const side = -1;
  const depth = 13;
  const z = centerZ(side, depth);

  // 一个能从纯黑剪影中认出的剧院：低翼 + 高飞塔 + 前突雨棚。
  group.add(addEdges(
    box(20, 8.6, depth, materials.landmark, 0, 0, z),
    outlines, materials, 'hero',
  ));
  group.add(addEdges(
    box(8.6, 14.5, 7.4, materials.landmark, 0, 8.6, z - 1.1),
    outlines, materials, 'hero',
  ));
  group.add(addEdges(
    box(13.5, 1.0, 3.5, materials.building, 0, 4.2, -12.25),
    outlines, materials, 'hero',
  ));

  // 竖向灯牌只作为一个小面积视觉标点，不负责照亮整条街。
  const sign = box(1.15, 7.2, 0.42, materials.red, -7.2, 6.1, -13.45);
  sign.castShadow = false;
  group.add(sign);

  const entrance = box(9.6, 3.1, 0.30, materials.ground, 0, 0.15, -13.78);
  entrance.castShadow = false;
  group.add(entrance);

  for (const x of [-2.7, 0, 2.7]) {
    const door = box(0.72, 2.25, 0.12, materials.warm, x, 0.25, -13.58);
    door.castShadow = false;
    group.add(door);
  }

  addWindowStrip(group, materials, {
    x: 0,
    y: 5.8,
    z: facadeZ(side),
    count: 5,
    spacing: 1.55,
    warm: false,
    phase: 2,
  });

  return group;
}

function createWarehouse(outlines, materials) {
  const group = new THREE.Group();
  group.name = 'Warehouse';

  const side = 1;
  const depth = 15;
  const x = 30;
  const z = centerZ(side, depth);

  group.add(addEdges(
    box(21, 6.4, depth, materials.buildingDim, x, 0, z),
    outlines, materials, 'hero',
  ));

  // 低多边形三棱顶，让工业体量靠轮廓而不是纹理被读出来。
  const roof = new THREE.Mesh(
    new THREE.CylinderGeometry(8.8, 8.8, 21, 3, 1, false, 0, Math.PI),
    materials.buildingDim,
  );
  roof.rotation.z = Math.PI * 0.5;
  roof.rotation.y = Math.PI * 0.5;
  roof.position.set(x, 6.4, z);
  roof.scale.y = 0.42;
  roof.castShadow = true;
  group.add(addEdges(roof, outlines, materials, 'hero', 22));

  for (const dx of [-5.4, 0, 5.4]) {
    const bay = box(3.6, 3.7, 0.18, materials.ground, x + dx, 0.2, facadeZ(side, 0.08));
    bay.castShadow = false;
    group.add(bay);
  }

  addWindowStrip(group, materials, {
    x,
    y: 4.8,
    z: facadeZ(side),
    count: 6,
    spacing: 2.15,
    phase: 4,
  });

  return group;
}

function createStreetRow(outlines, materials, {
  seed,
  side,
  xs,
  heights = [],
  windowSkip = [],
}) {
  const group = new THREE.Group();
  group.name = side > 0 ? 'SouthRow' : 'NorthRow';

  xs.forEach((x, i) => {
    const width = 7.5 + ((seed + i * 7) % 4);
    const depth = 9.5 + ((seed + i * 11) % 4);
    const height = heights[i] ?? (8.5 + ((seed + i * 13) % 11));
    const z = centerZ(side, depth);
    const material = i % 3 === 0 ? materials.building : materials.buildingDim;

    // 背景建筑默认只承担 context，不再随机升级成 hero。
    group.add(addEdges(
      box(width, height, depth, material, x, 0, z),
      outlines, materials, 'context',
    ));

    if (i % 2 === 0 && !windowSkip.includes(i)) {
      addWindowStrip(group, materials, {
        x,
        y: 3.2,
        z: facadeZ(side),
        count: Math.max(2, Math.floor(width / 1.8)),
        spacing: 1.42,
        warm: (i + seed) % 4 === 0,
        phase: seed + i,
      });
    }

    if (i === 1 || i === xs.length - 2) {
      group.add(addEdges(
        box(1.0, 3.8, 1.0, materials.buildingDim, x + width * 0.24, height, z),
        outlines, materials, 'context',
      ));
    }
  });

  return group;
}

function createFireEscape(outlines, materials, x, z) {
  const group = new THREE.Group();
  group.name = 'FireEscape';

  for (let i = 0; i < 4; i++) {
    const y = 3.0 + i * 2.4;
    group.add(addEdges(
      box(4.2, 0.14, 1.25, materials.buildingDim, x, y, z),
      outlines, materials, 'context', 5,
    ));
    group.add(
      box(4.2, 0.06, 0.06, materials.buildingDim, x, y + 0.72, z - 0.52),
      box(4.2, 0.06, 0.06, materials.buildingDim, x, y + 0.72, z + 0.52),
    );
  }

  return group;
}

function createLampGeometry(materials, x, z) {
  const group = new THREE.Group();
  group.name = 'StreetLamp';

  const pole = box(0.18, 4.25, 0.18, materials.buildingDim, x, 0, z);
  const head = box(0.62, 0.22, 0.46, materials.warm, x, 4.18, z);
  pole.castShadow = false;
  head.castShadow = false;
  group.add(pole, head);

  return group;
}

function createAlley(outlines, materials) {
  const group = new THREE.Group();
  group.name = 'Alley';

  group.add(addEdges(
    box(6.8, 11.5, 20, materials.buildingDim, -27.2, 0, 28.0),
    outlines, materials, 'context',
  ));
  group.add(addEdges(
    box(6.2, 9.2, 20, materials.building, -15.3, 0, 28.0),
    outlines, materials, 'context',
  ));

  const floor = new THREE.Mesh(
    new THREE.PlaneGeometry(5.0, 24),
    materials.wetRoad,
  );
  floor.rotation.x = -Math.PI / 2;
  floor.position.set(-21.25, 0.025, 26);
  floor.receiveShadow = true;
  group.add(floor);

  for (const [z, y] of [[19.5, 5.1], [26.0, 6.7]]) {
    const landing = box(1.55, 0.12, 2.5, materials.buildingDim, -23.75, y, z);
    group.add(addEdges(landing, outlines, materials, 'context', 8));
  }

  group.add(addEdges(
    box(8.5, 10.5, 3.2, materials.buildingDim, -21.25, 0, 39.2),
    outlines, materials, 'context',
  ));

  const door = box(1.15, 2.15, 0.10, materials.warm, -20.45, 0.08, 37.52);
  door.castShadow = false;
  group.add(door);

  return group;
}

export function buildWorld(scene, profile) {
  const materials = createMaterials(profile);
  const world = new THREE.Group();
  const outlines = new THREE.Group();

  world.name = 'WorldGeometry';
  outlines.name = 'LineLayer';
  scene.add(world, outlines);

  const ground = new THREE.Mesh(new THREE.PlaneGeometry(165, 100), materials.ground);
  ground.rotation.x = -Math.PI / 2;
  ground.receiveShadow = true;
  world.add(ground);

  for (const z of [-10.4, 10.4]) {
    const sidewalk = new THREE.Mesh(
      new THREE.BoxGeometry(165, 0.20, 5.8),
      materials.sidewalk,
    );
    sidewalk.position.set(0, 0.10, z);
    sidewalk.receiveShadow = true;
    world.add(sidewalk);
  }

  const wetRoad = new THREE.Mesh(new THREE.PlaneGeometry(165, 15), materials.wetRoad);
  wetRoad.rotation.x = -Math.PI / 2;
  wetRoad.position.y = 0.012;
  wetRoad.receiveShadow = true;
  world.add(wetRoad);

  world.add(createTheater(outlines, materials));
  world.add(createWarehouse(outlines, materials));

  // 北侧给剧院留出真正的“呼吸空间”；南侧给仓库与后巷留缺口。
  world.add(createStreetRow(outlines, materials, {
    seed: 3,
    side: -1,
    xs: [-56, -44, 30, 43, 56],
    heights: [12, 17, 13, 19, 11],
  }));
  world.add(createStreetRow(outlines, materials, {
    seed: 9,
    side: 1,
    xs: [-55, -42, -29, -14, 0, 14, 52],
    heights: [10, 15, 17, 13, 9, 18, 12],
    windowSkip: [2, 3],
  }));

  world.add(createFireEscape(outlines, materials, -42, -13.35));
  world.add(createAlley(outlines, materials));

  const lampPositions = [
    [-25, 4.05, -9.0],
    [20, 4.05, -9.0],
    [48, 4.05, -9.0],
    [-50, 4.05, 9.0],
    [5, 4.05, 9.0],
    [48, 4.05, 9.0],
  ];

  for (const [x, , z] of lampPositions) {
    world.add(createLampGeometry(materials, x, z));
  }

  return {
    materials,
    lampPositions,
    groups: { world, outlines },
  };
}
