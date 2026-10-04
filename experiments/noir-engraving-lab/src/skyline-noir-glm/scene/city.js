import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

// 程序化城市：剪影优先。
// 每栋楼都必须能被轮廓认出来：要么有退台、要么有屋顶件（水塔/烟囱/广告牌/山墙），
// 要么被归入几乎无细节的远景带。几何全部合并成少数几个 mesh，窗是独立合并几何。

const DISTRICT_HEIGHT = {
  downtown: [52, 150],
  mid: [22, 62],
  outer: [9, 26],
  oldtown: [10, 24],
  industry: [6, 14],
};

function tintedBox(geomList, rng, w, h, d, x, yBase, z, tintHex, jitter = 0.18) {
  const g = new THREE.BoxGeometry(w, h, d);
  g.translate(x, yBase + h / 2, z);
  paintGeometry(g, rng, tintHex, jitter);
  geomList.push(g);
  return { x, z, w, d, h, top: yBase + h };
}

function paintGeometry(g, rng, tintHex, jitter) {
  const c = new THREE.Color(tintHex);
  const f = 1 + (rng.next() - 0.5) * 2 * jitter;
  const count = g.attributes.position.count;
  const col = new Float32Array(count * 3);
  for (let i = 0; i < count; i++) {
    col[i * 3] = c.r * f;
    col[i * 3 + 1] = c.g * f;
    col[i * 3 + 2] = c.b * f;
  }
  g.setAttribute('color', new THREE.BufferAttribute(col, 3));
}

function addWaterTower(propList, rng, x, topY, z, s = 1) {
  const tank = new THREE.CylinderGeometry(2.1 * s, 2.1 * s, 3.4 * s, 10);
  tank.translate(x, topY + 5.4 * s, z);
  paintGeometry(tank, rng, 0x0c0f13);
  const cap = new THREE.ConeGeometry(2.35 * s, 1.7 * s, 10);
  cap.translate(x, topY + 7.9 * s, z);
  paintGeometry(cap, rng, 0x0c0f13);
  propList.push(tank, cap);
  for (let i = 0; i < 4; i++) {
    const leg = new THREE.BoxGeometry(0.28, 3.8 * s, 0.28);
    const a = (i / 4) * Math.PI * 2 + 0.6;
    leg.translate(x + Math.cos(a) * 1.5 * s, topY + 1.9 * s, z + Math.sin(a) * 1.5 * s);
    paintGeometry(leg, rng, 0x0c0f13);
    propList.push(leg);
  }
}

function addRoofClutter(propList, rng, b, district) {
  const x = b.x + (rng.next() - 0.5) * (b.w - 6);
  const z = b.z + (rng.next() - 0.5) * (b.d - 6);
  const roll = rng.next();
  if (roll < 0.34 && b.h > 14) {
    addWaterTower(propList, rng, x, b.top, z, district === 'oldtown' ? 0.8 : 1);
  } else if (roll < 0.6) {
    const ch = new THREE.BoxGeometry(1.6, 3 + rng.next() * 3, 1.6);
    ch.translate(x, b.top + 1.5 + rng.next() * 1.5, z);
    paintGeometry(ch, rng, 0x0b0e12);
    propList.push(ch);
  } else if (roll < 0.78 && b.h > 20) {
    const pent = new THREE.BoxGeometry(b.w * 0.4, 2.6, b.d * 0.4);
    pent.translate(b.x, b.top + 1.3, b.z);
    paintGeometry(pent, rng, 0x0b0e12);
    propList.push(pent);
  } else if (roll < 0.9 && b.h > 26) {
    const mast = new THREE.CylinderGeometry(0.12, 0.2, 7, 5);
    mast.translate(x, b.top + 3.5, z);
    paintGeometry(mast, rng, 0x0b0e12);
    propList.push(mast);
  }
}

function districtOf(cx, cz, p) {
  const [dx, dz] = p.downtown;
  const r = Math.hypot(cx - dx, cz - dz);
  if (r < p.downtownRadius) return 'downtown';
  if (r < p.midRadius) return 'mid';
  if (cx > 60 && cz > 100) return 'oldtown';
  if (cx < -150 && cz > 20) return 'industry';
  return 'outer';
}

function buildDowntownBuilding(list, windowSpecs, rng, b, tint, profile) {
  // 退台三段 + 顶冠 + 天线：装饰艺术塔楼的剪影语法
  const h1 = b.h * 0.52;
  const h2 = b.h * 0.3;
  const h3 = b.h * 0.18;
  const base = tintedBox(list, rng, b.w, h1, b.d, b.x, 0, b.z, tint);
  const mid = tintedBox(list, rng, b.w * 0.74, h2, b.d * 0.74, b.x, h1, b.z, tint);
  const top = tintedBox(list, rng, b.w * 0.5, h3, b.d * 0.5, b.x, h1 + h2, b.z, tint);
  if (rng.chance(0.7)) {
    const crown = new THREE.BoxGeometry(b.w * 0.24, 5 + rng.next() * 6, b.d * 0.24);
    crown.translate(b.x, base.top + h2 + h3 + 2.5, b.z);
    paintGeometry(crown, rng, tint);
    list.push(crown);
  }
  if (rng.chance(0.35)) {
    const ant = new THREE.CylinderGeometry(0.14, 0.3, 10 + rng.next() * 8, 5);
    ant.translate(b.x, base.top + h2 + h3 + 12, b.z);
    paintGeometry(ant, rng, 0x0b0e12);
    list.push(ant);
  }
  addWindows(windowSpecs, rng, [base, mid, top], 'downtown', profile);
}

function addWindows(windowSpecs, rng, boxes, district, profile) {
  const [sw, sh] = profile.city.windowSpacing;
  const [ww, wh] = profile.city.windowSize;
  const litRatio = profile.city.litRatio[district];
  for (const b of boxes) {
    const faces = [
      { nx: 1, nz: 0, span: b.d, off: b.w / 2 },
      { nx: -1, nz: 0, span: b.d, off: b.w / 2 },
      { nx: 0, nz: 1, span: b.w, off: b.d / 2 },
      { nx: 0, nz: -1, span: b.w, off: b.d / 2 },
    ];
    for (const f of faces) {
      const cols = Math.floor((f.span - 3) / sw);
      const rows = Math.floor((b.h - 4) / sh);
      if (cols < 1 || rows < 1) continue;
      for (let i = 0; i < cols; i++) {
        for (let j = 0; j < rows; j++) {
          const u = (i + 0.5) / cols - 0.5;
          const yy = 3 + (j + 0.5) * sh;
          const px = b.x + f.nx * (f.off + 0.15) + f.nz * u * f.span;
          const pz = b.z + f.nz * (f.off + 0.15) + f.nx * u * f.span;
          if (!rng.chance(litRatio)) continue;
          let acc = rng.next();
          let tier = profile.city.windowTiers[0];
          for (const t of profile.city.windowTiers) {
            if (acc < t.weight) { tier = t; break; }
            acc -= t.weight;
          }
          windowSpecs.push({
            x: px, y: yy, z: pz, nx: f.nx, nz: f.nz,
            r: tier.color[0], g: tier.color[1], b: tier.color[2],
            bright: (0.55 + rng.next() * 0.55) * (profile.city.windowBrightness?.[district] ?? 1),
            w: ww, h: wh,
          });
        }
      }
    }
  }
}

function buildBlock(list, props, windowSpecs, rng, cx, cz, district, profile, tint) {
  const lots = district === 'oldtown' ? 3 : district === 'industry' ? 1 : rng.int(1, 2);
  const [hMin, hMax] = DISTRICT_HEIGHT[district];
  for (let i = 0; i < lots; i++) {
    const lw = (profile.city.blockHalf * 2 - 6) / lots;
    const w = lw - rng.range(2, 5);
    const d = profile.city.blockHalf * 2 - rng.range(6, 14);
    const ox = lots === 1 ? 0 : (i - (lots - 1) / 2) * lw;
    const x = cx + ox + (rng.next() - 0.5) * 3;
    const z = cz + (rng.next() - 0.5) * 4;
    const h = rng.range(hMin, hMax);
    if (district === 'industry') {
      const b = tintedBox(list, rng, w * 1.4, h, d * 0.7, x, 0, z, tint);
      // 工业棚：锯齿屋顶的一根天窗脊
      const ridge = new THREE.BoxGeometry(w * 1.1, 1.2, 1.4);
      ridge.translate(x, b.top + 0.6, z);
      paintGeometry(ridge, rng, tint);
      list.push(ridge);
      continue;
    }
    if (district === 'downtown') {
      buildDowntownBuilding(list, windowSpecs, rng, { x, z, w, d, h }, tint, profile);
      continue;
    }
    const b = tintedBox(list, rng, w, h, d, x, 0, z, tint);
    if (district === 'oldtown' && rng.chance(0.3)) {
      // 山墙山形屋顶：老城的屋脊线
      const roof = new THREE.CylinderGeometry(0.001, w * 0.62, 3.4, 4, 1);
      roof.rotateY(Math.PI / 4);
      roof.scale(1, 1, d / w);
      roof.translate(x, b.top + 1.7, z);
      paintGeometry(roof, rng, tint);
      list.push(roof);
    }
    if (district !== 'far' && rng.chance(0.4)) addRoofClutter(props, rng, b, district);
    addWindows(windowSpecs, rng, [b], district, profile);
  }
}

function buildFarBand(list, rng, profile) {
  const [rMin, rMax] = profile.city.farBandRadius;
  for (let i = 0; i < profile.city.farBandCount; i++) {
    const ang = (i / profile.city.farBandCount) * Math.PI * 2 + rng.range(-0.06, 0.06);
    const r = rng.range(rMin, rMax);
    const x = Math.cos(ang) * r + profile.city.downtown[0] * 0.3;
    const z = Math.sin(ang) * r + profile.city.downtown[1] * 0.3;
    const w = rng.range(40, 96);
    const d = rng.range(28, 60);
    const h = rng.range(30, 110);
    tintedBox(list, rng, w, h, d, x, 0, z, profile.city.districtTint.far, 0.1);
  }
}

function buildViaduct(list, rng, profile, lampPoints) {
  const v = profile.city.viaduct;
  const deck = new THREE.BoxGeometry(v.halfLength * 2, 1.8, 7);
  deck.translate(0, v.deckY, v.at);
  paintGeometry(deck, rng, 0x0d1015);
  list.push(deck);
  for (const side of [-1, 1]) {
    const rail = new THREE.BoxGeometry(v.halfLength * 2, 1.1, 0.4);
    rail.translate(0, v.deckY + 1.4, v.at + side * 3.3);
    paintGeometry(rail, rng, 0x0c0f13);
    list.push(rail);
  }
  for (let x = -v.halfLength; x <= v.halfLength; x += 26) {
    const pier = new THREE.BoxGeometry(2.4, v.deckY - 0.9, 2.4);
    pier.translate(x + 13 + rng.range(-2, 2), (v.deckY - 0.9) / 2, v.at);
    paintGeometry(pier, rng, 0x0c0f13);
    list.push(pier);
  }
  // 高架上的作业灯：一排小暖点，是城市的第二地平线
  for (let x = -v.halfLength + 12; x <= v.halfLength - 12; x += 24) {
    lampPoints.push({ x, y: v.deckY + 2.6, z: v.at, viaduct: true });
  }
}

function buildLamps(lampPoints, profile, rng) {
  const g = profile.city.grid;
  // 干道 A：南北大道（x = 0，整列街块已让位）
  for (let z = -g * 6; z <= g * 6; z += g) {
    if (!rng.chance(0.88)) continue;
    lampPoints.push({ x: 0, y: 5.8, z: z + rng.range(-3, 3), artery: true });
  }
  // 干道 B：东西向
  for (let x = -g * 6; x <= g * 6; x += g) {
    if (Math.abs(x) < 10 || !rng.chance(0.82)) continue;
    lampPoints.push({ x: x + rng.range(-3, 3), y: 5.8, z: 24, artery: true });
  }
  // 市中心广场
  const [dx, dz] = profile.city.downtown;
  for (let i = 0; i < 7; i++) {
    const a = (i / 7) * Math.PI * 2;
    lampPoints.push({ x: dx + Math.cos(a) * 70, y: 5.8, z: dz + Math.sin(a) * 70, artery: true });
  }
  // 老城与工业的零星灯
  for (let i = 0; i < 9; i++) {
    lampPoints.push({ x: rng.range(70, 260), y: 5.8, z: rng.range(110, 260), artery: false });
  }
  for (let i = 0; i < 4; i++) {
    lampPoints.push({ x: rng.range(-280, -160), y: 5.8, z: rng.range(20, 140), artery: false });
  }
}

export function createCity(profile, rng) {
  const p = profile.city;
  const group = new THREE.Group();
  const windowSpecs = [];
  const lampPoints = [];
  const buildingGeoms = [];
  const propGeoms = [];

  // 地面
  const ground = new THREE.Mesh(
    new THREE.PlaneGeometry(1900, 1900),
    new THREE.MeshLambertMaterial({ color: 0x05070a }),
  );
  ground.rotation.x = -Math.PI / 2;
  ground.name = 'ground';
  group.add(ground);

  // 街块网格。x=0 的整列让位给一条南北大道：城市需要一条真正的大街，
  // 长街机位站在它里面，探照灯与主角塔在它尽头。
  const n = 6;
  for (let gx = -n; gx <= n; gx++) {
    for (let gz = -n; gz <= n; gz++) {
      const cx = gx * p.grid;
      const cz = gz * p.grid;
      // 高架铁路需要一条专用走廊：整行街块让位，铁路从自己的街道上空穿过
      if (Math.abs(cz - p.viaduct.at) < 18) continue;
      if (cx === 0) continue; // 大道
      const district = districtOf(cx, cz, p);
      if (district !== 'downtown' && rng.chance(0.06)) continue; // 留出空地/停车场
      buildBlock(buildingGeoms, propGeoms, windowSpecs, rng, cx, cz, district, profile, p.districtTint[district]);
    }
  }

  buildFarBand(buildingGeoms, rng, profile);
  buildViaduct(buildingGeoms, rng, profile, lampPoints);
  buildLamps(lampPoints, profile, rng);

  const buildingMesh = new THREE.Mesh(
    mergeGeometries(buildingGeoms, false),
    new THREE.MeshLambertMaterial({ vertexColors: true }),
  );
  buildingMesh.name = 'buildings';
  group.add(buildingMesh);

  const propMesh = new THREE.Mesh(
    mergeGeometries(propGeoms, false),
    new THREE.MeshLambertMaterial({ vertexColors: true }),
  );
  propMesh.name = 'roofProps';
  group.add(propMesh);

  return { group, windowSpecs, lampPoints, buildingMesh };
}
