/**
 * 城市几何装配。
 *
 * 数据来自 `tools/heishui-export.py` 烘焙的 public/heishui/city.json + city.bin：
 *   - 连续形体（郊野 / 城市地面 / 街区 / 干道 / 河面 / 基底结构 / 地点外壳）是合并好的三角网格；
 *   - 重复形体（填充建筑 / 屋顶件 / 树）是实例表，这里用 InstancedMesh 画；
 *   - 窗光是合并的四边面片，顶点属性带 tier。
 *
 * 描线一根都不在这里。正式城市把描线烘成 Mesh（全城 100 万顶点）；黑水的线是
 * 屏幕空间的、按光调制的，见 post.js。
 */
import * as THREE from 'three';
import { DISTRICT_TINT, CITY } from './config.js';

export const ROLE = { GROUND: 0, HERO: 1, FILL: 2, WINDOW: 3, WATER: 4 };

/* -------------------------------------------------------------- 单位原型 */

/** Blender 顶点 (bx,by,bz) -> three (bx, bz, -by)。底 y=0，高 1，xy 落在 ±0.5。 */
const b2t = (v) => [v[0], v[2], -v[1]];

function protoMesh(verts, faces) {
  const pos = [];
  for (const f of faces) {
    // 扇形三角化
    for (let k = 1; k < f.length - 1; k++) {
      for (const i of [f[0], f[k], f[k + 1]]) pos.push(...b2t(verts[i]));
    }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.computeVertexNormals();
  return g;
}

/** 与 pipeline/geometry.py::proto_meshes 一致。 */
export function buildProtos() {
  const h = 0.5;
  const cube = [[-h, -h, 0], [h, -h, 0], [h, h, 0], [-h, h, 0], [-h, -h, 1], [h, -h, 1], [h, h, 1], [-h, h, 1]];
  const cubeF = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]];
  const out = {};
  out.box = protoMesh(cube, cubeF);

  const s = 0.20, o = 0.16, hh = 0.10;
  const rb = cube.concat([[o - s, o - s, 1], [o + s, o - s, 1], [o + s, o + s, 1], [o - s, o + s, 1],
    [o - s, o - s, 1 + hh], [o + s, o - s, 1 + hh], [o + s, o + s, 1 + hh], [o - s, o + s, 1 + hh]]);
  const rbF = cubeF.concat([[8, 11, 10, 9], [12, 13, 14, 15], [8, 9, 13, 12], [9, 10, 14, 13], [10, 11, 15, 14], [11, 8, 12, 15]]);
  out.roofbox = protoMesh(rb, rbF);

  const r = 0.74;
  const pi = [[-h, -h, 0], [h, -h, 0], [h, h, 0], [-h, h, 0], [-h, -h, r], [h, -h, r], [h, h, r], [-h, h, r], [0, -h, 1], [0, h, 1]];
  const piF = [[0, 3, 2, 1], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7], [4, 8, 9, 7], [5, 6, 9, 8], [4, 5, 8], [7, 9, 6]];
  out.pitch = protoMesh(pi, piF);

  const a = 0.33, b = 0.68;
  const sb = cube.concat([[-a, -a, b], [a, -a, b], [a, a, b], [-a, a, b], [-a, -a, 1], [a, -a, 1], [a, a, 1], [-a, a, 1]]);
  const sbF = cubeF.concat([[8, 9, 13, 12], [9, 10, 14, 13], [10, 11, 15, 14], [11, 8, 12, 15], [12, 13, 14, 15]]);
  out.setback = protoMesh(sb, sbF);
  return out;
}

/** 屋顶件原型（geometry.py::roof_protos）。 */
export function buildRoofProtos() {
  const out = {};
  const boxF = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]];
  const bx = [[-0.5, -0.5, 0], [0.5, -0.5, 0], [0.5, 0.5, 0], [-0.5, 0.5, 0],
    [-0.5, -0.5, 1], [0.5, -0.5, 1], [0.5, 0.5, 1], [-0.5, 0.5, 1]];
  out.box = protoMesh(bx, boxF);

  const prism = (n) => {
    const v = [], f = [];
    const ring = [];
    for (let i = 0; i < n; i++) ring.push([0.5 * Math.cos(i / n * Math.PI * 2), 0.5 * Math.sin(i / n * Math.PI * 2)]);
    for (const [x, y] of ring) v.push([x, y, 0]);
    for (const [x, y] of ring) v.push([x, y, 1]);
    f.push([...Array(n).keys()].reverse());
    for (let i = 0; i < n; i++) { const j = (i + 1) % n; f.push([i, j, n + j, n + i]); }
    f.push([...Array(n).keys()].map((i) => n + i));
    return protoMesh(v, f);
  };
  out.stack = prism(6);

  // 天窗脊：三角棱柱，沿 y 长（geometry.py 的 ridge）
  const ridge = [[-0.5, -0.5, 0], [0.5, -0.5, 0], [0.5, 0.5, 0], [-0.5, 0.5, 0], [0, -0.5, 1], [0, 0.5, 1]];
  const ridgeF = [[0, 3, 2, 1], [0, 1, 4], [2, 3, 5], [1, 2, 5, 4], [3, 0, 4, 5]];
  out.ridge = protoMesh(ridge, ridgeF);
  return out;
}

/** 树：正式城市借用了 pitch 原型，在城市视角下"树"和"小屋"分不开。这里换成一个压扁的冠。 */
function treeProto() {
  const g = new THREE.IcosahedronGeometry(0.5, 0);
  g.scale(1, 0.85, 1);
  g.translate(0, 0.42, 0);
  g.computeVertexNormals();
  return g;
}

/* -------------------------------------------------------------- 装配 */

function readPrimitive(json, bin, prim) {
  const n = prim.count;
  const pos = new Float32Array(bin, prim.position, n * 3);
  const role = new Uint8Array(bin, prim.roleOffset, n);
  const idx = new Uint32Array(bin, prim.index, prim.indexCount);
  let tier = null, place = null;
  if (prim.tierOffset !== undefined) tier = new Uint8Array(bin, prim.tierOffset, n);
  if (prim.placeOffset !== undefined) place = new Uint16Array(bin, prim.placeOffset, n);
  return { pos, role, idx, tier, place, n };
}

function toGeometry(p, extra) {
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(p.pos, 3));
  g.setIndex(new THREE.BufferAttribute(p.idx, 1));
  if (extra) for (const [k, v] of Object.entries(extra(p))) g.setAttribute(k, v);
  return g;
}

/**
 * 从 data/ 载入并返回一个 scene-graph：
 *   { group, meshes, groundMeshes, waterMesh, shells, windows, bounds }
 * 材质在 noir.js 里装配，这里只保证几何和属性齐备。
 */
export async function loadCity(base = './data/') {
  const json = await (await fetch(base + 'city.json')).json();
  const bin = await (await fetch(base + 'city.bin')).arrayBuffer();
  const prims = Object.fromEntries(json.primitives.map((p) => [p.name, p]));
  const group = new THREE.Group();
  group.name = '黑水_城市';

  const protos = buildProtos();
  const roofProtos = buildRoofProtos();
  const districts = json.districtOrder;

  /* --- 连续形体 --- */
  const groundParts = [];
  /** 取一个已合并的 prim。isGround=true 的进"贴地组"，会参与湿反射排除。 */
  const take = (name, roleOverride, extra, isGround = false) => {
    const desc = prims[name];
    if (!desc) return null;
    const p = readPrimitive(json, bin, desc);
    const g = toGeometry(p, extra);
    g.userData.role = roleOverride;
    if (isGround) groundParts.push({ name, geometry: g, role: roleOverride });
    return g;
  };

  const landG = take('郊野', ROLE.GROUND, (p) => ({ aRole: new THREE.BufferAttribute(p.role, 1) }), true);
  const urbanG = take('城市地面', ROLE.GROUND, (p) => ({ aRole: new THREE.BufferAttribute(p.role, 1) }), true);
  const padG = take('街区pad', ROLE.GROUND, (p) => ({ aRole: new THREE.BufferAttribute(p.role, 1) }), true);
  const roadG = take('干道', ROLE.GROUND, (p) => ({ aRole: new THREE.BufferAttribute(p.role, 1) }), true);
  const structG = take('基底结构', ROLE.HERO, (p) => ({ aRole: new THREE.BufferAttribute(p.role, 1) }));
  const shellG = take('地点外壳', ROLE.HERO, (p) => ({
    aRole: new THREE.BufferAttribute(p.role, 1),
    aPlace: new THREE.BufferAttribute(p.place, 1),
  }));
  const winG = take('窗光', ROLE.WINDOW, (p) => ({
    aTier: new THREE.BufferAttribute(p.tier, 1),
    aRand: perQuadRandom(p),
  }));
  const waterG = take('河面', ROLE.WATER, (p) => ({ aRole: new THREE.BufferAttribute(p.role, 1) }));

  /* --- 填充建筑：每个原型一个 InstancedMesh --- */
  const byProto = protos && json.buildingProtos.map(() => []);
  for (const b of json.buildings) byProto[b.p].push(b);
  const fillMeshes = [];
  json.buildingProtos.forEach((name, pi) => {
    const list = byProto[pi];
    if (!list.length) return;
    const g = protos[name].clone();
    const inst = new THREE.InstancedMesh(g, null, list.length);
    inst.name = '填充建筑_' + name;
    const district = new Float32Array(list.length);
    const rand = new Float32Array(list.length);
    const size = new Float32Array(list.length * 3);
    const m = new THREE.Matrix4();
    const q = new THREE.Quaternion();
    const pos = new THREE.Vector3();
    const scl = new THREE.Vector3();
    list.forEach((b, i) => {
      pos.set(b.x, 1.05, -b.y);
      q.setFromAxisAngle(new THREE.Vector3(0, 1, 0), b.a);
      scl.set(b.w, b.h, b.d);
      m.compose(pos, q, scl);
      inst.setMatrixAt(i, m);
      district[i] = Math.max(0, districts.indexOf(b.z));
      rand[i] = hash1(i * 0.6180339887 + b.x * 0.013 + b.y * 0.0071);
      size[i * 3] = b.w; size[i * 3 + 1] = b.h; size[i * 3 + 2] = b.d;
    });
    inst.instanceMatrix.needsUpdate = true;
    inst.geometry.setAttribute('aDistrict', new THREE.InstancedBufferAttribute(district, 1));
    inst.geometry.setAttribute('aRand', new THREE.InstancedBufferAttribute(rand, 1));
    inst.geometry.setAttribute('aSize', new THREE.InstancedBufferAttribute(size, 3));
    inst.frustumCulled = false;
    fillMeshes.push(inst);
    group.add(inst);
  });

  /* --- 屋顶件 --- */
  json.roofProtos.forEach((name, pi) => {
    const list = json.roofs.filter((r) => r.p === pi);
    if (!list.length || !roofProtos[name]) return;
    const inst = new THREE.InstancedMesh(roofProtos[name], null, list.length);
    inst.name = '屋顶_' + name;
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), pos = new THREE.Vector3(), scl = new THREE.Vector3();
    list.forEach((r, i) => {
      pos.set(r.x, 1.05, -r.y);
      q.setFromAxisAngle(new THREE.Vector3(0, 1, 0), r.a);
      scl.set(r.w, r.h, r.d);
      inst.setMatrixAt(i, m.compose(pos, q, scl));
    });
    inst.instanceMatrix.needsUpdate = true;
    inst.frustumCulled = false;
    fillMeshes.push(inst);
    group.add(inst);
  });

  /* --- 树 --- */
  if (json.trees.length) {
    const inst = new THREE.InstancedMesh(treeProto(), null, json.trees.length);
    inst.name = '绿化';
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), pos = new THREE.Vector3(), scl = new THREE.Vector3();
    json.trees.forEach((t, i) => {
      pos.set(t.x, 1.05, -t.y);
      q.setFromAxisAngle(new THREE.Vector3(0, 1, 0), t.a);
      scl.set(t.w * 1.35, t.h * 1.15, t.d * 1.35);
      inst.setMatrixAt(i, m.compose(pos, q, scl));
    });
    inst.instanceMatrix.needsUpdate = true;
    inst.frustumCulled = false;
    fillMeshes.push(inst);
    group.add(inst);
  }

  return {
    group,
    json,
    districts,
    protos,
    ground: groundParts,
    landG, urbanG, padG, roadG, structG, shellG, winG, waterG,
    fillMeshes,
    bounds: json.bounds,
    mirrorY: CITY.mirrorY,
  };
}

/** 每个四边面片一个随机数（窗光面片是 4 顶点一组）。 */
function perQuadRandom(p) {
  const n = p.n / 4;
  const r = new Float32Array(p.n);
  for (let q = 0; q < n; q++) {
    const i = q * 12;
    const cx = (p.pos[i] + p.pos[i + 3] + p.pos[i + 6] + p.pos[i + 9]) * 0.25;
    const cy = (p.pos[i + 1] + p.pos[i + 4] + p.pos[i + 7] + p.pos[i + 10]) * 0.25;
    const cz = (p.pos[i + 2] + p.pos[i + 5] + p.pos[i + 8] + p.pos[i + 11]) * 0.25;
    const v = hash1(cx * 0.37 + cy * 0.71 + cz * 0.53);
    for (let k = 0; k < 4; k++) r[q * 4 + k] = v;
  }
  return new THREE.BufferAttribute(r, 1);
}

export function hash1(x) {
  const s = Math.sin(x * 127.1) * 43758.5453123;
  return s - Math.floor(s);
}

export { DISTRICT_TINT };
