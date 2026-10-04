// 灯岸 LANTERN — by Spark · 真实城市几何装配
//
// 只读冻结在 public/heishui/city.{json,bin} 的快照（与黑水共用同一份烘焙），
// 运行时不接触 CityBox 流水线、不碰 Unity / Engine / Scheme。
// 坐标：Blender (x,y,z) -> three (x, z, -y)，与烘焙一致。
import * as THREE from 'three';

function readPrim(bin, prim) {
  const n = prim.count;
  return {
    pos: new Float32Array(bin, prim.position, n * 3),
    role: new Uint8Array(bin, prim.roleOffset, n),
    idx: new Uint32Array(bin, prim.index, prim.indexCount),
    tier: prim.tierOffset !== undefined ? new Uint8Array(bin, prim.tierOffset, n) : null,
    place: prim.placeOffset !== undefined ? new Uint16Array(bin, prim.placeOffset, n) : null,
    n,
  };
}

function mergedGeometry(p, extra) {
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(p.pos, 3));
  g.setIndex(new THREE.BufferAttribute(p.idx, 1));
  if (extra) for (const [k, v] of Object.entries(extra)) g.setAttribute(k, v);
  return g;
}

// 填充建筑原型：与 CityBox pipeline/geometry.py::proto_meshes 同构
// （底 z=0、高 1、xy 落在 ±0.5；场景侧按实例表做“平移+绕Y+缩放到 w/h/d”）。
function buildProtos() {
  const h = 0.5;
  const cubeV = [[-h, -h, 0], [h, -h, 0], [h, h, 0], [-h, h, 0], [-h, -h, 1], [h, -h, 1], [h, h, 1], [-h, h, 1]];
  const cubeF = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]];
  const fan = (verts, faces) => {
    const pos = [];
    for (const f of faces) for (let k = 1; k < f.length - 1; k++) for (const i of [f[0], f[k], f[k + 1]]) {
      const v = verts[i];
      pos.push(v[0], v[2], -v[1]); // Blender -> three
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    return g;
  };
  const out = { box: fan(cubeV, cubeF) };
  const s = 0.20, o = 0.16, hh = 0.10;
  const rb = cubeV.concat([[o - s, o - s, 1], [o + s, o - s, 1], [o + s, o + s, 1], [o - s, o + s, 1],
    [o - s, o - s, 1 + hh], [o + s, o - s, 1 + hh], [o + s, o + s, 1 + hh], [o - s, o + s, 1 + hh]]);
  const rbF = cubeF.concat([[8, 11, 10, 9], [12, 13, 14, 15], [8, 9, 13, 12], [9, 10, 14, 13], [10, 11, 15, 14], [11, 8, 12, 15]]);
  out.roofbox = fan(rb, rbF);
  const r = 0.74;
  const pi = [[-h, -h, 0], [h, -h, 0], [h, h, 0], [-h, h, 0], [-h, -h, r], [h, -h, r], [h, h, r], [-h, h, r], [0, -h, 1], [0, h, 1]];
  const piF = [[0, 3, 2, 1], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7], [4, 8, 9, 7], [5, 6, 9, 8], [4, 5, 8], [7, 9, 6]];
  out.pitch = fan(pi, piF);
  const a = 0.33, b = 0.68;
  const sb = cubeV.concat([[-a, -a, b], [a, -a, b], [a, a, b], [-a, a, b], [-a, -a, 1], [a, -a, 1], [a, a, 1], [-a, a, 1]]);
  const sbF = cubeF.concat([[8, 9, 13, 12], [9, 10, 14, 13], [10, 11, 15, 14], [11, 8, 12, 15], [12, 13, 14, 15]]);
  out.setback = fan(sb, sbF);
  return out;
}

function buildRoofProtos() {
  const fan = (verts, faces) => {
    const pos = [];
    for (const f of faces) for (let k = 1; k < f.length - 1; k++) for (const i of [f[0], f[k], f[k + 1]]) {
      const v = verts[i];
      pos.push(v[0], v[2], -v[1]);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    return g;
  };
  const boxF = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]];
  const bx = [[-0.5, -0.5, 0], [0.5, -0.5, 0], [0.5, 0.5, 0], [-0.5, 0.5, 0],
    [-0.5, -0.5, 1], [0.5, -0.5, 1], [0.5, 0.5, 1], [-0.5, 0.5, 1]];
  const prism = (n) => {
    const v = [], f = [];
    for (let i = 0; i < n; i++) v.push([0.5 * Math.cos((i / n) * Math.PI * 2), 0.5 * Math.sin((i / n) * Math.PI * 2), 0]);
    for (let i = 0; i < n; i++) v.push([0.5 * Math.cos((i / n) * Math.PI * 2), 0.5 * Math.sin((i / n) * Math.PI * 2), 1]);
    f.push([...Array(n).keys()].reverse());
    for (let i = 0; i < n; i++) { const j = (i + 1) % n; f.push([i, j, n + j, n + i]); }
    f.push([...Array(n).keys()].map((i) => n + i));
    return fan(v, f);
  };
  const ridge = [[-0.5, -0.5, 0], [0.5, -0.5, 0], [0.5, 0.5, 0], [-0.5, 0.5, 0], [0, -0.5, 1], [0, 0.5, 1]];
  const ridgeF = [[0, 3, 2, 1], [0, 1, 4], [2, 3, 5], [1, 2, 5, 4], [3, 0, 4, 5]];
  return { box: fan(bx, boxF), stack: prism(6), ridge: fan(ridge, ridgeF) };
}

function hash1(x) {
  const s = Math.sin(x * 127.1) * 43758.5453123;
  return s - Math.floor(s);
}

// 窗光面片：每 4 顶点一组，共用一个随机数
function perQuadRandom(p) {
  const r = new Float32Array(p.n);
  for (let q = 0; q < p.n / 4; q++) {
    const i = q * 12;
    const cx = (p.pos[i] + p.pos[i + 3] + p.pos[i + 6] + p.pos[i + 9]) * 0.25;
    const cy = (p.pos[i + 1] + p.pos[i + 4] + p.pos[i + 7] + p.pos[i + 10]) * 0.25;
    const cz = (p.pos[i + 2] + p.pos[i + 5] + p.pos[i + 8] + p.pos[i + 11]) * 0.25;
    const v = hash1(cx * 0.37 + cy * 0.71 + cz * 0.53);
    for (let k = 0; k < 4; k++) r[q * 4 + k] = v;
  }
  return new THREE.BufferAttribute(r, 1);
}

export async function loadCity(base) {
  const json = await (await fetch(base + 'city.json')).json();
  const bin = await (await fetch(base + 'city.bin')).arrayBuffer();
  const prims = Object.fromEntries(json.primitives.map((p) => [p.name, p]));

  const take = (name) => {
    const desc = prims[name];
    if (!desc) return null;
    return mergedGeometry(readPrim(bin, desc));
  };
  // 地点外壳附带 place 索引（焦点判定用）
  let shellPlace = null;
  if (prims['地点外壳']) {
    const p = readPrim(bin, prims['地点外壳']);
    shellPlace = p.place;
  }

  const ground = [];
  for (const name of ['郊野', '城市地面', '街区pad', '干道']) {
    const g = take(name);
    if (g) ground.push({ name, geometry: g });
  }

  // 填充建筑：按原型分 InstancedMesh
  const protos = buildProtos();
  const fillMeshes = [];
  const districtOf = (z) => Math.max(0, json.districtOrder.indexOf(z));
  json.buildingProtos.forEach((name, pi) => {
    const list = json.buildings.filter((b) => b.p === pi);
    if (!list.length || !protos[name]) return;
    const g = protos[name].clone();
    const inst = new THREE.InstancedMesh(g, null, list.length);
    inst.name = '填充建筑_' + name;
    const district = new Float32Array(list.length);
    const rand = new Float32Array(list.length);
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), pos = new THREE.Vector3(), scl = new THREE.Vector3();
    const up = new THREE.Vector3(0, 1, 0);
    list.forEach((b, i) => {
      pos.set(b.x, 1.05, -b.y);
      q.setFromAxisAngle(up, b.a);
      scl.set(b.w, b.h, b.d);
      m.compose(pos, q, scl);
      inst.setMatrixAt(i, m);
      district[i] = districtOf(b.z);
      rand[i] = hash1(i * 0.6180339887 + b.x * 0.013 + b.y * 0.0071);
    });
    inst.instanceMatrix.needsUpdate = true;
    g.setAttribute('aDistrict', new THREE.InstancedBufferAttribute(district, 1));
    g.setAttribute('aRand', new THREE.InstancedBufferAttribute(rand, 1));
    inst.frustumCulled = false;
    fillMeshes.push(inst);
  });

  // 屋顶件 / 树：同一套实例表，片区取 downtown（主角级以下半档）
  const roofProtos = buildRoofProtos();
  const dtDowntown = Math.max(0, json.districtOrder.indexOf('downtown'));
  const dress = (list, proto, name, sy = 1) => {
    if (!list.length || !proto) return null;
    const inst = new THREE.InstancedMesh(proto, null, list.length);
    inst.name = name;
    const district = new Float32Array(list.length);
    const rand = new Float32Array(list.length);
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), pos = new THREE.Vector3(), scl = new THREE.Vector3();
    const up = new THREE.Vector3(0, 1, 0);
    list.forEach((r, i) => {
      pos.set(r.x, 1.05, -r.y);
      q.setFromAxisAngle(up, r.a);
      scl.set(r.w, r.h * sy, r.d);
      m.compose(pos, q, scl);
      inst.setMatrixAt(i, m);
      district[i] = dtDowntown;
      rand[i] = hash1(i * 0.377 + r.x * 0.021);
    });
    inst.instanceMatrix.needsUpdate = true;
    inst.geometry = proto.clone();
    inst.geometry.setAttribute('aDistrict', new THREE.InstancedBufferAttribute(district, 1));
    inst.geometry.setAttribute('aRand', new THREE.InstancedBufferAttribute(rand, 1));
    inst.frustumCulled = false;
    return inst;
  };
  json.roofProtos.forEach((name, pi) => {
    const list = json.roofs.filter((r) => r.p === pi);
    const inst = dress(list, roofProtos[name], '屋顶_' + name);
    if (inst) fillMeshes.push(inst);
  });
  if (json.trees.length) {
    const crown = new THREE.IcosahedronGeometry(0.5, 0);
    crown.scale(1, 0.85, 1);
    crown.translate(0, 0.42, 0);
    const inst = dress(json.trees.map((t) => ({ ...t, w: t.w * 1.35, h: t.h * 1.15, d: t.d * 1.35 })), crown, '绿化');
    if (inst) fillMeshes.push(inst);
  }

  // 窗光
  let winG = null;
  if (prims['窗光']) {
    const p = readPrim(bin, prims['窗光']);
    winG = mergedGeometry(p, {
      aTier: new THREE.BufferAttribute(p.tier, 1),
      aRand: perQuadRandom(p),
    });
  }

  return {
    json,
    districts: json.districtOrder,
    ground,
    structG: take('基底结构'),
    shellG: take('地点外壳'),
    shellPlace,
    placeNames: json.placeNames,
    waterG: take('河面'),
    winG,
    fillMeshes,
    stats: json.stats,
  };
}
