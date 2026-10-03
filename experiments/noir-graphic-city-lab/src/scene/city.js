import * as THREE from 'three';
import { createCharacter } from './characters.js';

function material(color, extra = {}) {
  return new THREE.MeshBasicMaterial({ color, ...extra });
}

function hash01(n) {
  const x = Math.sin(n * 12.9898 + 78.233) * 43758.5453;
  return x - Math.floor(x);
}

function addEdgeCopies(mesh, group, material, threshold = 26, jitter = true) {
  const base = new THREE.EdgesGeometry(mesh.geometry, threshold);
  const copies = jitter ? 2 : 1;
  for (let i = 0; i < copies; i++) {
    const line = new THREE.LineSegments(base, material);
    line.position.copy(mesh.position);
    line.quaternion.copy(mesh.quaternion);
    line.scale.copy(mesh.scale);
    if (i === 1) line.position.add(new THREE.Vector3(0.012, -0.006, 0.008));
    group.add(line);
  }
}

function addWindowGrid({ groups, mats, x, z, w, h, d, cols, rows, face = 'front', seed = 1 }) {
  const group = new THREE.Group();
  for (let iy = 0; iy < rows; iy++) {
    for (let ix = 0; ix < cols; ix++) {
      const r = hash01(seed + ix * 17 + iy * 101);
      if (r < 0.12) continue;
      const ww = Math.max(0.32, w / (cols * 2.2));
      const wh = Math.max(0.26, h / (rows * 2.7));
      const win = new THREE.Mesh(new THREE.PlaneGeometry(ww, wh), r > 0.78 ? mats.hot : mats.window);
      const px = (ix - (cols - 1) / 2) * (w * 0.78 / Math.max(1, cols - 1));
      const py = 1.1 + iy * ((h - 2.0) / Math.max(1, rows - 1));

      if (face === 'front') win.position.set(x + px, py, z + d / 2 + 0.03);
      else if (face === 'left') {
        win.position.set(x - w / 2 - 0.03, py, z + px * (d / w));
        win.rotation.y = -Math.PI / 2;
      } else {
        win.position.set(x + w / 2 + 0.03, py, z + px * (d / w));
        win.rotation.y = Math.PI / 2;
      }
      group.add(win);
    }
  }
  groups.windows.add(group);
}

function addBuilding({ groups, mats, x, z, w, h, d, tone = 'inkLift', windows = null, seed = 1 }) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mats[tone]);
  mesh.position.set(x, h / 2, z);
  groups.city.add(mesh);
  addEdgeCopies(mesh, groups.lines, mats.lineDim, 28, true);

  if (windows) {
    addWindowGrid({
      groups, mats, x, z, w, h, d,
      cols: windows.cols, rows: windows.rows,
      face: windows.face ?? 'front', seed,
    });
  }
  return mesh;
}

function addLineBox({ groups, mats, x, y, z, w, h, d, bright = false }) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mats.transparent);
  mesh.position.set(x, y, z);
  groups.city.add(mesh);
  addEdgeCopies(mesh, groups.lines, bright ? mats.line : mats.lineDim, 18, true);
  return mesh;
}

function addArchitecture({ groups, mats }) {
  addBuilding({
    groups, mats, x: -21, z: -46, w: 22, h: 42, d: 12,
    tone: 'inkLift', windows: { cols: 12, rows: 17, face: 'front' }, seed: 21,
  });
  addBuilding({
    groups, mats, x: 15, z: -48, w: 12, h: 28, d: 10,
    tone: 'ink', windows: { cols: 5, rows: 10, face: 'front' }, seed: 67,
  });

  const club = addBuilding({ groups, mats, x: -2, z: -24, w: 27, h: 8.5, d: 8, tone: 'mid', seed: 9 });
  addEdgeCopies(club, groups.lines, mats.line, 28, true);

  const awning = new THREE.Mesh(new THREE.BoxGeometry(12, 0.55, 2.8), mats.inkLift);
  awning.position.set(-2, 5.1, -18.7);
  groups.city.add(awning);
  addEdgeCopies(awning, groups.lines, mats.line, 18, true);

  for (let i = 0; i < 5; i++) {
    const door = new THREE.Mesh(new THREE.PlaneGeometry(1.65, 3.2), mats.ink);
    door.position.set(-6 + i * 2.0, 1.7, -19.94);
    groups.city.add(door);
    addEdgeCopies(door, groups.lines, mats.lineDim, 18, false);
  }

  const sign = new THREE.Mesh(new THREE.PlaneGeometry(8.6, 1.1), mats.paperDim);
  sign.position.set(-2, 6.15, -19.92);
  groups.windows.add(sign);

  const signBars = [];
  for (let i = 0; i < 7; i++) {
    const bar = new THREE.Mesh(new THREE.PlaneGeometry(0.055, 0.62), mats.ink);
    bar.position.set(-5.1 + i * 1.03, 6.14, -19.88);
    groups.city.add(bar);
    signBars.push(bar);
  }

  addBuilding({ groups, mats, x: 17, z: -19, w: 12, h: 13, d: 10, tone: 'inkLift', seed: 14 });
  addBuilding({ groups, mats, x: -18, z: -8, w: 10, h: 17, d: 13, tone: 'ink', seed: 15 });
  addBuilding({ groups, mats, x: 18, z: 5, w: 12, h: 18, d: 16, tone: 'ink', seed: 16 });

  // Fire escape / rails / street geometry: cheap line density, not mesh detail.
  for (const z of [-10, 8, 25]) {
    const rail = addLineBox({ groups, mats, x: 8.8, y: 1.8, z, w: 0.08, h: 3.3, d: 8, bright: false });
    rail.rotation.y = 0;
  }
  for (let i = 0; i < 9; i++) {
    const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, 5.0, 5), mats.ink);
    pole.position.set(-8.5 + (i % 2) * 17, 2.5, -15 + i * 8.5);
    groups.city.add(pole);
    addEdgeCopies(pole, groups.lines, mats.lineDim, 14, false);
  }
}

function addGroundDrawing({ groups, mats }) {
  const road = new THREE.Mesh(new THREE.PlaneGeometry(38, 100), mats.road);
  road.rotation.x = -Math.PI / 2;
  road.position.set(0, 0, 8);
  groups.city.add(road);

  // Hand-drawn pavement seams.
  const verts = [];
  for (let z = -30; z < 50; z += 4.5) {
    const jitter = (hash01(z * 3.2) - 0.5) * 0.65;
    verts.push(-11, 0.025, z + jitter, 11, 0.025, z - jitter * 0.4);
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(verts, 3));
  const seams = new THREE.LineSegments(geo, mats.lineDim);
  groups.lines.add(seams);

  // Grass/scratch tufts create the dense drawn foreground seen in the reference.
  const grassVerts = [];
  for (let i = 0; i < 220; i++) {
    const side = hash01(i * 1.9) > 0.5 ? 1 : -1;
    const x = side * (8.5 + hash01(i * 4.1) * 8.0);
    const z = -20 + hash01(i * 6.7) * 66;
    const hh = 0.25 + hash01(i * 9.3) * 1.2;
    const lean = (hash01(i * 11.7) - 0.5) * 0.5;
    grassVerts.push(x, 0.03, z, x + lean, hh, z + (hash01(i * 13.1)-0.5)*0.3);
  }
  const grassGeo = new THREE.BufferGeometry();
  grassGeo.setAttribute('position', new THREE.Float32BufferAttribute(grassVerts, 3));
  groups.lines.add(new THREE.LineSegments(grassGeo, mats.lineDim));

  // Reflective pavement value patches.
  for (let i = 0; i < 18; i++) {
    const patch = new THREE.Mesh(
      new THREE.PlaneGeometry(1.2 + hash01(i*2.2)*4.8, 0.15 + hash01(i*5.1)*0.65),
      mats.pavementGlow,
    );
    patch.rotation.x = -Math.PI / 2;
    patch.rotation.z = (hash01(i*7.1)-0.5)*0.18;
    patch.position.set((hash01(i*3.4)-0.5)*18, 0.018, -12 + hash01(i*8.2)*48);
    groups.haze.add(patch);
  }
}

function addLightStage({ groups, mats }) {
  const source = new THREE.Vector3(-14, 28, -6);
  const target = new THREE.Vector3(-4, 0.2, -11);
  const dir = target.clone().sub(source);
  const length = dir.length();
  const beam = new THREE.Mesh(
    new THREE.ConeGeometry(5.6, length, 32, 1, true),
    mats.beam,
  );
  beam.position.copy(source).add(target).multiplyScalar(0.5);
  beam.quaternion.setFromUnitVectors(new THREE.Vector3(0, -1, 0), dir.clone().normalize());
  groups.accents.add(beam);

  const pool = new THREE.Mesh(new THREE.CircleGeometry(5.4, 48), mats.pool);
  pool.rotation.x = -Math.PI / 2;
  pool.scale.set(1.55, 0.72, 1);
  pool.position.set(target.x, 0.04, target.z);
  groups.accents.add(pool);

  // Dust in the beam.
  const pts = [];
  for (let i = 0; i < 240; i++) {
    const t = hash01(i*2.6);
    const center = source.clone().lerp(target, t);
    const spread = t * 3.4;
    pts.push(
      center.x + (hash01(i*4.1)-0.5)*spread,
      center.y + (hash01(i*5.9)-0.5)*0.8,
      center.z + (hash01(i*7.7)-0.5)*spread,
    );
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
  groups.haze.add(new THREE.Points(geo, mats.dust));
}

function addCast({ groups, mats }) {
  createCharacter({
    name:'Neil', position:[3.5,0,20.0], yaw:3.35, scale:1.15,
    pose:'neutral', role:'hero', mats, groups,
  });

  const crowd = [
    [-6.0,-7.2,.88,3.02],[-2.8,-8.0,.92,3.14],[0.0,-7.0,.84,2.98],
    [-7.4,-2.7,.82,3.08],[-3.9,-2.0,.96,3.18],[1.6,-2.8,.80,2.94],
    [-5.4,3.1,.86,3.10],[-1.8,2.2,.80,3.22],[2.0,2.8,.90,3.02],
  ];
  crowd.forEach(([x,z,s,yaw],i)=>createCharacter({
    name:`Crowd ${i+1}`, position:[x,0,z], yaw, scale:s,
    pose:i%3===0?'gesture':i%2?'walking':'neutral',
    role:'crowd', variant:i, mats, groups,
  }));

  createCharacter({
    name:'Nightingale', position:[-4.2,0,-11.0], yaw:3.0, scale:1.05,
    pose:'gesture', role:'singer', mats, groups,
  });
}

export function buildWorld(scene, profile) {
  const groups = {
    city: new THREE.Group(),
    characters: new THREE.Group(),
    lines: new THREE.Group(),
    windows: new THREE.Group(),
    haze: new THREE.Group(),
    accents: new THREE.Group(),
    backdrop: new THREE.Group(),
  };
  Object.values(groups).forEach(g => scene.add(g));

  const mats = {
    ink: material(profile.palette.ink),
    inkLift: material(profile.palette.inkLift),
    mid: material(profile.palette.mid),
    road: material(profile.palette.inkLift),
    character: material(profile.palette.ink),
    characterLift: material(0x0b101d),
    paperDim: material(profile.palette.paperDim),
    paper: material(profile.palette.paper),
    hot: material(profile.palette.hot),
    transparent: material(profile.palette.ink, { transparent:true, opacity:0 }),
    window: material(profile.palette.paper, { transparent:true, opacity:profile.graphic.windowOpacity }),
    line: new THREE.LineBasicMaterial({
      color: profile.palette.paper, transparent:true,
      opacity: profile.graphic.lineOpacity, depthTest:true,
    }),
    lineDim: new THREE.LineBasicMaterial({
      color: profile.palette.paperDim, transparent:true,
      opacity: profile.graphic.dimLineOpacity, depthTest:true,
    }),
    beam: material(profile.palette.paper, {
      transparent:true, opacity:profile.graphic.beamOpacity,
      side:THREE.DoubleSide, depthWrite:false, blending:THREE.AdditiveBlending,
    }),
    pool: material(profile.palette.paper, {
      transparent:true, opacity:0.18, side:THREE.DoubleSide,
      depthWrite:false, blending:THREE.AdditiveBlending,
    }),
    pavementGlow: material(profile.palette.paperDim, {
      transparent:true, opacity:0.16, depthWrite:false,
    }),
    dust: new THREE.PointsMaterial({
      color:profile.palette.paper, size:0.075, transparent:true,
      opacity:profile.graphic.hazeOpacity, depthWrite:false,
      blending:THREE.AdditiveBlending,
    }),
  };

  addGroundDrawing({ groups, mats });
  addArchitecture({ groups, mats });
  addLightStage({ groups, mats });
  addCast({ groups, mats });

  return { groups, mats };
}
