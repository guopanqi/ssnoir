// 逆光 BACKLIGHT · 真实城市几何装配
//
// 只读冻结在 public/backlight/city.{json,bin} 的快照（tools/export-backlight.py 自己烘的那份，
// 拆顶点 + 面平面法线，供硬终止线的卡通分带使用）。运行时不碰 CityBox / Unity / Scheme。
// 坐标：Blender (x,y,z) -> three (x,z,-y)，与烘焙一致。
//
// 实例件的 base 高度从数据里来（填充建筑/树在街区台面上 = 1.05，屋顶件在各自楼顶），
// 不写死常数。
import * as THREE from 'three';
import { DISTRICT_TINT, TREE_TINT } from './config.js';

function readPrim(bin, prim) {
  const n = prim.count;
  return {
    n,
    pos: new Float32Array(bin, prim.position, n * 3),
    nrm: new Int8Array(bin, prim.normal, n * 4),
    role: new Uint8Array(bin, prim.roleOffset, n),
    tier: prim.tierOffset !== undefined ? new Uint8Array(bin, prim.tierOffset, n) : null,
    place: prim.placeOffset !== undefined ? new Uint16Array(bin, prim.placeOffset, n) : null,
  };
}

function flatGeometry(p, extra) {
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(p.pos, 3));
  g.setAttribute('normal', new THREE.BufferAttribute(p.nrm, 4, true));
  if (extra) for (const [k, v] of Object.entries(extra)) g.setAttribute(k, v);
  g.computeBoundingSphere();
  return g;
}

// 填充建筑原型：与 CityBox pipeline/geometry.py::proto_meshes 同构
// （底 y=0、高 1、xz 落在 ±0.5；实例表做平移+绕Y+缩放到 w/h/d）。
// 非索引扇形三角形 -> computeVertexNormals 得到面平面法线（与烘焙的面法线同口径）。
function buildProtos() {
  const h = 0.5;
  const cubeV = [[-h, -h, 0], [h, -h, 0], [h, h, 0], [-h, h, 0], [-h, -h, 1], [h, -h, 1], [h, h, 1], [-h, h, 1]];
  const cubeF = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]];
  const fan = (verts, faces) => {
    const pos = [];
    for (const f of faces) for (let k = 1; k < f.length - 1; k++) for (const i of [f[0], f[k], f[k + 1]]) {
      const v = verts[i];
      pos.push(v[0], v[2], -v[1]);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.computeVertexNormals();
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
    g.computeVertexNormals();
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

/** 窗光面片：每 6 顶点（2 三角）一组，共用一个随机数 + 一个街区簇随机数。 */
function perQuadRandom(p) {
  const per = 6;
  const rand = new Float32Array(p.n);
  const block = new Float32Array(p.n);
  for (let q = 0; q < p.n / per; q++) {
    const base = q * per;
    let cx = 0, cy = 0, cz = 0;
    for (let k = 0; k < per; k++) {
      cx += p.pos[(base + k) * 3];
      cy += p.pos[(base + k) * 3 + 1];
      cz += p.pos[(base + k) * 3 + 2];
    }
    cx /= per; cy /= per; cz /= per;
    const r = hash1(cx * 0.37 + cy * 0.71 + cz * 0.53);
    // 街区簇：以 46 m 为一格，格内共用一个开关
    const gx = Math.floor(cx / 46), gy = Math.floor(cy / 46), gz = Math.floor(cz / 46);
    const b = hash1(gx * 7.13 + gy * 3.71 + gz * 11.7);
    for (let k = 0; k < per; k++) { rand[base + k] = r; block[base + k] = b; }
  }
  return { rand: new THREE.BufferAttribute(rand, 1), block: new THREE.BufferAttribute(block, 1) };
}

/**
 * 实例装配：平移 + 绕 Y + 缩放。
 * 反照率走 instanceColor（片区色 × 每栋抖动），材质本体保持白色；
 * 这样"哪一片更黑"是数据，而不是 shader 里的分支。
 */
function dress(list, proto, name, districts, districtOf, tintOverride) {
  const inst = new THREE.InstancedMesh(proto, null, list.length);
  inst.name = name;
  const m = new THREE.Matrix4(), q = new THREE.Quaternion(), pos = new THREE.Vector3(), scl = new THREE.Vector3();
  const up = new THREE.Vector3(0, 1, 0);
  const col = new THREE.Color();
  list.forEach((b, i) => {
    pos.set(b.x, b.base, -b.y);
    q.setFromAxisAngle(up, b.a);
    scl.set(b.w, b.h, b.d);
    m.compose(pos, q, scl);
    inst.setMatrixAt(i, m);
    const di = districtOf(b);
    const tint = tintOverride ?? DISTRICT_TINT[districts[di]] ?? DISTRICT_TINT.oldtown;
    const j = 0.86 + 0.28 * hash1(i * 0.6180339887 + b.x * 0.013 + b.y * 0.0071);
    col.setRGB(tint[0] * j, tint[1] * j, tint[2] * j);
    inst.setColorAt(i, col);
  });
  inst.instanceMatrix.needsUpdate = true;
  if (inst.instanceColor) inst.instanceColor.needsUpdate = true;
  inst.frustumCulled = false;
  return inst;
}

export async function loadCity(base) {
  const json = await (await fetch(base + 'city.json')).json();
  const bin = await (await fetch(base + 'city.bin')).arrayBuffer();
  const prims = Object.fromEntries(json.primitives.map((p) => [p.name, p]));

  const take = (name, extra) => {
    const desc = prims[name];
    if (!desc) return null;
    const p = readPrim(bin, desc);
    return { geometry: flatGeometry(p, typeof extra === 'function' ? extra(p) : extra), prim: p };
  };

  const ground = [];
  for (const name of ['郊野', '城市地面', '街区pad', '干道']) {
    const g = take(name, (p) => ({ aRole: new THREE.BufferAttribute(p.role, 1) }));
    if (g) ground.push({ name, geometry: g.geometry });
  }

  const fillMeshes = [];
  const protos = buildProtos();
  const districts = json.districtOrder;
  const districtOf = (b) => Math.max(0, districts.indexOf(b.z));
  json.buildingProtos.forEach((name, pi) => {
    const list = json.buildings.filter((b) => b.p === pi);
    if (!list.length || !protos[name]) return;
    fillMeshes.push(dress(list, protos[name].clone(), '填充建筑_' + name, districts, districtOf));
  });

  const roofProtos = buildRoofProtos();
  json.roofProtos.forEach((name, pi) => {
    const list = json.roofs.filter((r) => r.p === pi);
    if (!list.length || !roofProtos[name]) return;
    const inst = dress(list, roofProtos[name].clone(), '屋顶_' + name, districts,
      () => Math.max(0, districts.indexOf('downtown')));
    fillMeshes.push(inst);
  });

  if (json.trees.length) {
    const crown = new THREE.IcosahedronGeometry(0.5, 0);
    crown.scale(1, 0.85, 1);
    crown.translate(0, 0.42, 0);
    crown.computeVertexNormals();
    const list = json.trees.map((t) => ({ ...t, w: t.w * 1.35, h: t.h * 1.15, d: t.d * 1.35 }));
    fillMeshes.push(dress(list, crown, '绿化', districts, () => 0, TREE_TINT));
  }

  // 窗光：面片 + 每窗随机 + 街区簇
  let winG = null, winQuad = null;
  if (prims['窗光']) {
    const p = readPrim(bin, prims['窗光']);
    winQuad = perQuadRandom(p);
    winG = flatGeometry(p, {
      aTier: new THREE.BufferAttribute(p.tier, 1),
      aRand: winQuad.rand,
      aBlock: winQuad.block,
    });
  }

  let shellPlace = null;
  if (prims['地点外壳']) shellPlace = readPrim(bin, prims['地点外壳']).place;

  return {
    json,
    districts,
    ground,
    structG: take('基底结构', (p) => ({ aRole: new THREE.BufferAttribute(p.role, 1) }))?.geometry || null,
    shellG: take('地点外壳', (p) => ({
      aRole: new THREE.BufferAttribute(p.role, 1),
      aPlace: new THREE.BufferAttribute(p.place, 1),
    }))?.geometry || null,
    shellPlace,
    waterG: take('河面')?.geometry || null,
    winG,
    fillMeshes,
    placeNames: json.placeNames,
    placeBounds: json.placeBounds,
    stats: json.stats,
  };
}
