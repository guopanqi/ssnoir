import * as THREE from 'three';
import { createCharacter } from './characters.js';

function material(color, extra = {}) {
  return new THREE.MeshBasicMaterial({ color, ...extra });
}

function boxFaceMaterials(mats, facadeIndex, hero = false) {
  const result = [mats.ink, mats.ink, mats.inkLift, mats.ink, mats.inkLift, mats.ink];
  result[facadeIndex] = hero ? mats.mid : mats.inkLift;
  return result;
}

function addEdges(mesh, group, mat, threshold = 24) {
  const lines = new THREE.LineSegments(new THREE.EdgesGeometry(mesh.geometry, threshold), mat);
  lines.position.copy(mesh.position);
  lines.quaternion.copy(mesh.quaternion);
  lines.scale.copy(mesh.scale);
  group.add(lines);
  return lines;
}

function addWindowGrid({ parent, facade, countX, countY, width, height, mats, groups, sparse = false }) {
  for (let y = 0; y < countY; y++) {
    for (let x = 0; x < countX; x++) {
      if (sparse && ((x + y * 3) % 4 !== 0)) continue;
      const win = new THREE.Mesh(new THREE.PlaneGeometry(width, height), mats.paperDim);
      const px = (x - (countX - 1) / 2) * (width * 1.8);
      const py = 2.1 + y * (height * 1.85);
      if (facade === '+z') win.position.set(px, py, parent.geometry.parameters.depth / 2 + 0.011);
      if (facade === '+x') {
        win.position.set(parent.geometry.parameters.width / 2 + 0.011, py, px);
        win.rotation.y = Math.PI / 2;
      }
      if (facade === '-x') {
        win.position.set(-parent.geometry.parameters.width / 2 - 0.011, py, px);
        win.rotation.y = -Math.PI / 2;
      }
      if (!sparse && (x + y) % 3 === 0) win.userData.graphicAccent = true;
      parent.add(win);
    }
  }
}

function addBuilding({ groups, mats, x, z, w, h, d, facadeIndex, hero = false, line = false }) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), boxFaceMaterials(mats, facadeIndex, hero));
  mesh.position.set(x, h / 2, z);
  groups.city.add(mesh);
  if (line) addEdges(mesh, groups.lines, mats.line, 26);
  return mesh;
}

function addTheater({ groups, mats }) {
  const theater = addBuilding({
    groups, mats, x: 0, z: -34, w: 22, h: 13, d: 5.5, facadeIndex: 4, hero: true, line: true,
  });
  addWindowGrid({ parent: theater, facade: '+z', countX: 5, countY: 2, width: 1.5, height: 1.05, mats, groups });

  const portal = new THREE.Mesh(new THREE.BoxGeometry(7.2, 6.2, 0.34), mats.ink);
  portal.position.set(0, 3.1, -30.98);
  groups.city.add(portal);
  addEdges(portal, groups.lines, mats.line);

  const door = new THREE.Mesh(new THREE.PlaneGeometry(4.1, 4.7), mats.paper);
  door.position.set(0, 2.45, -30.79);
  groups.city.add(door);

  const marquee = new THREE.Mesh(new THREE.BoxGeometry(9.7, 0.68, 2.2), mats.paper);
  marquee.position.set(0, 7.0, -29.85);
  groups.city.add(marquee);
  addEdges(marquee, groups.lines, mats.line);

  const sign = new THREE.Mesh(new THREE.BoxGeometry(1.1, 5.5, 0.28), mats.gold);
  sign.position.set(7.25, 8.9, -30.86);
  groups.accents.add(sign);

  for (let i = 0; i < 5; i++) {
    const bulb = new THREE.Mesh(new THREE.CircleGeometry(0.12, 10), mats.goldHot);
    bulb.position.set(-3.6 + i * 1.8, 7.0, -28.72);
    groups.accents.add(bulb);
  }
}

function addTenements({ groups, mats }) {
  const left = [
    [-13.8, 26, 9.0, 10.5, 12],
    [-14.8, 10, 11.0, 15.0, 13],
    [-14.2, -7, 9.5, 12.0, 12],
  ];
  const right = [
    [14.4, 31, 10.5, 13.0, 13],
    [14.0, 13, 9.5, 10.0, 12],
    [14.5, -5, 10.5, 16.5, 14],
  ];
  left.forEach(([x,z,w,h,d], i) => {
    const b = addBuilding({ groups,mats,x,z,w,h,d,facadeIndex:0,line:i===1 });
    addWindowGrid({ parent:b, facade:'+x', countX:3, countY:3, width:0.8, height:1.15, mats, groups, sparse:true });
  });
  right.forEach(([x,z,w,h,d], i) => {
    const b = addBuilding({ groups,mats,x,z,w,h,d,facadeIndex:1,line:i===2 });
    addWindowGrid({ parent:b, facade:'-x', countX:3, countY:4, width:0.8, height:1.1, mats, groups, sparse:true });
  });

  const bridge = new THREE.Mesh(new THREE.BoxGeometry(8.2, 0.45, 2.5), mats.inkLift);
  bridge.position.set(0, 9.5, 6);
  groups.city.add(bridge);
  addEdges(bridge, groups.lines, mats.line);
}

function addStreet({ groups, mats }) {
  const road = new THREE.Mesh(new THREE.PlaneGeometry(18, 90), mats.ink);
  road.rotation.x = -Math.PI / 2;
  road.position.set(0, 0, 8);
  groups.city.add(road);

  [-9.5, 9.5].forEach((x) => {
    const walk = new THREE.Mesh(new THREE.BoxGeometry(3, 0.18, 90), mats.inkLift);
    walk.position.set(x, 0.09, 8);
    groups.city.add(walk);
  });

  for (let z = -23; z < 48; z += 11) {
    const dash = new THREE.Mesh(new THREE.PlaneGeometry(0.13, 3.2), mats.paperDim);
    dash.rotation.x = -Math.PI / 2;
    dash.position.set(0, 0.012, z);
    groups.city.add(dash);
  }

  [-7.8, 7.8].forEach((x) => {
    for (const z of [-16, 9, 34]) {
      const post = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.11, 4.7, 6), mats.ink);
      post.position.set(x, 2.35, z);
      groups.city.add(post);
      const arm = new THREE.Mesh(new THREE.BoxGeometry(0.95, 0.08, 0.08), mats.paperDim);
      arm.position.set(x + (x < 0 ? 0.42 : -0.42), 4.45, z);
      groups.lines.add(arm);
      const lamp = new THREE.Mesh(new THREE.SphereGeometry(0.18, 8, 5), mats.gold);
      lamp.position.set(x + (x < 0 ? 0.84 : -0.84), 4.38, z);
      groups.accents.add(lamp);
    }
  });
}

function addGraphicBackdrop({ groups, mats }) {
  const skyline = [
    [-31, -48, 14, 32, 12],
    [-16, -52, 10, 24, 10],
    [18, -51, 12, 28, 11],
    [32, -47, 15, 38, 12],
  ];
  skyline.forEach(([x,z,w,h,d], i) => {
    const mesh = addBuilding({ groups,mats,x,z,w,h,d,facadeIndex:4,hero:false,line:i===3 });
    mesh.material[4] = i === 3 ? mats.mid : mats.inkLift;
  });

  const moon = new THREE.Mesh(new THREE.CircleGeometry(5.4, 64), mats.paper);
  moon.position.set(-18, 26, -64);
  groups.backdrop.add(moon);
}

function addCast({ groups, mats }) {
  createCharacter({
    name:'Neil', position:[-1.2,0,11.8], yaw:Math.PI, scale:1.12, pose:'neutral', role:'hero', mats, groups,
  });
  createCharacter({
    name:'Nightingale', position:[1.0,0,9.7], yaw:Math.PI + 0.16, scale:1.08, pose:'gesture', role:'singer', mats, groups,
  });
  createCharacter({
    name:'Dock bruiser', position:[-5.8,0,25], yaw:Math.PI*0.94, scale:1.16, pose:'walking', role:'crowd', mats, groups,
  });

  const crowd = [
    [-4.4, 31, .90, 3.05], [2.8, 34, .82, 3.2], [5.3, 28, 1.02, 2.95],
    [-1.5, 38, .94, 3.12], [6.4, 42, .86, 3.0], [-6.3, 43, .80, 3.18],
    [3.4, 20, .78, 3.3], [-6.7, 18, .88, 3.02],
  ];
  crowd.forEach(([x,z,s,yaw], i) => createCharacter({
    name:`Crowd ${i+1}`, position:[x,0,z], yaw, scale:s,
    pose:i%2 ? 'walking':'neutral', role:'crowd', mats, groups,
  }));

  createCharacter({
    name:'Alley witness', position:[-7.4,0,-3.5], yaw:1.42, scale:.95, pose:'gesture', role:'crowd', mats, groups,
  });
}

export function buildWorld(scene, profile) {
  const groups = {
    city: new THREE.Group(),
    characters: new THREE.Group(),
    lines: new THREE.Group(),
    accents: new THREE.Group(),
    backdrop: new THREE.Group(),
  };
  Object.values(groups).forEach((g) => scene.add(g));

  const mats = {
    ink: material(profile.palette.ink),
    inkLift: material(profile.palette.inkLift),
    mid: material(profile.palette.mid),
    paperDim: material(profile.palette.paperDim),
    paper: material(profile.palette.paper),
    gold: material(profile.palette.gold),
    goldHot: material(profile.palette.goldHot),
    line: new THREE.LineBasicMaterial({
      color: profile.palette.paper,
      transparent: true,
      opacity: profile.graphic.lineOpacity,
      depthTest: true,
    }),
  };

  addStreet({ groups, mats });
  addTenements({ groups, mats });
  addTheater({ groups, mats });
  addGraphicBackdrop({ groups, mats });
  addCast({ groups, mats });

  const foreground = new THREE.Mesh(new THREE.BoxGeometry(8, 18, 5), mats.ink);
  foreground.position.set(-13.5, 9, 48);
  groups.city.add(foreground);

  return { groups, mats };
}
