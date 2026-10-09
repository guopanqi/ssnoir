import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

// 地标：剪影的锚点。每个地标负责一种"一眼认出"的轮廓：
// 主角塔（退台+尖顶+探照灯）、钟塔（亮面圆钟）、气柜（带光环的圆柱）、教堂（尖塔）、烟囱（烟柱）。

function paint(g, tintHex, jitter = 0.1) {
  const c = new THREE.Color(tintHex);
  const count = g.attributes.position.count;
  const col = new Float32Array(count * 3);
  for (let i = 0; i < count; i++) {
    col[i * 3] = c.r;
    col[i * 3 + 1] = c.g;
    col[i * 3 + 2] = c.b;
  }
  g.setAttribute('color', new THREE.BufferAttribute(col, 3));
  return g;
}

export function createLandmarks(profile) {
  const group = new THREE.Group();
  const inkTargets = []; // 需要墨线壳的地标
  const anchors = {};
  const heroWindowSpecs = []; // 主角塔自己的窗（跟普通楼同一套语法，密度更低）

  const mat = () => new THREE.MeshLambertMaterial({ vertexColors: true });

  // ── 主角塔：Meridian Tower ─────────────────────────────
  const heroTint = 0x131720;
  const heroGeoms = [];
  const HX = -30, HZ = -60;
  const tiers = [
    [30, 0, 50],
    [23, 50, 78],
    [16, 78, 104],
    [10, 104, 118],
  ];
  for (const [w, y0, y1] of tiers) {
    heroGeoms.push(paint(new THREE.BoxGeometry(w, y1 - y0, w).translate(HX, (y0 + y1) / 2, HZ), heroTint));
  }
  // 冠部四道竖棱：退台塔顶的锯齿
  for (let i = 0; i < 4; i++) {
    const a = (i / 4) * Math.PI * 2 + Math.PI / 4;
    const fin = paint(new THREE.BoxGeometry(1.2, 10, 1.2).translate(
      HX + Math.cos(a) * 4.2, 123, HZ + Math.sin(a) * 4.2,
    ), heroTint);
    heroGeoms.push(fin);
  }
  const spire = paint(new THREE.CylinderGeometry(0.18, 0.9, 26, 6).translate(HX, 118 + 13, HZ), 0x0b0e12);
  heroGeoms.push(spire);
  const heroMesh = new THREE.Mesh(mergeGeometries(heroGeoms, false), mat());
  heroMesh.name = 'heroTower';
  group.add(heroMesh);
  inkTargets.push({ mesh: heroMesh, thickness: profile.ink.heroThickness });

  // 主角塔的窗：稀疏、成组熄灭，像加班到深夜的办公楼
  {
    let seed = 7;
    const rand = () => {
      seed = (seed * 16807) % 2147483647;
      return seed / 2147483647;
    };
    const tiers = [[30, 0, 50], [23, 50, 78], [16, 78, 104]];
    for (const [w, y0, y1] of tiers) {
      for (const [nx, nz] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const cols = Math.floor((w - 4) / 3.4);
        const rows = Math.floor((y1 - y0 - 4) / 3.8);
        for (let i = 0; i < cols; i++) {
          for (let j = 0; j < rows; j++) {
            if (rand() > 0.22) continue;
            const u = (i + 0.5) / cols - 0.5;
            const px = HX + nx * (w / 2 + 0.15) + nz * u * w;
            const pz = HZ + nz * (w / 2 + 0.15) + nx * u * w;
            const warm = rand();
            const tint = warm < 0.75 ? [1.0, 0.63, 0.29] : [0.6, 0.77, 0.88];
            heroWindowSpecs.push({
              x: px, y: y0 + 3 + (j + 0.5) * 3.8, z: pz, nx, nz,
              r: tint[0], g: tint[1], b: tint[2],
              bright: 0.5 + rand() * 0.5, w: 1.5, h: 2.0,
            });
          }
        }
      }
    }
  }

  // 塔基红霓虹：全城唯一允许的红
  const marquee = new THREE.Mesh(
    new THREE.BoxGeometry(16, 3.2, 0.7),
    new THREE.MeshBasicMaterial({ color: 0xd9452e }),
  );
  marquee.position.set(HX, 15, HZ + tiers[0][0] / 2 + 0.5);
  marquee.name = 'marquee';
  group.add(marquee);
  const marqueeGlow = new THREE.Sprite(new THREE.SpriteMaterial({
    map: null, color: 0xff5a3a, transparent: true, blending: THREE.AdditiveBlending,
    depthWrite: false, opacity: 0.55,
  }));
  marqueeGlow.scale.set(30, 9, 1);
  marqueeGlow.position.copy(marquee.position);
  group.add(marqueeGlow);
  anchors.marqueeGlow = marqueeGlow;
  anchors.marqueePosition = marquee.position.clone();

  // 探照灯锚点
  anchors.beacon = new THREE.Object3D();
  anchors.beacon.position.set(HX, 144, HZ);
  group.add(anchors.beacon);

  // ── 钟塔 ───────────────────────────────────────────────
  const CX = 120, CZ = -10;
  const clockGeoms = [
    paint(new THREE.BoxGeometry(15, 46, 15).translate(CX, 23, CZ), 0x141920),
    paint(new THREE.BoxGeometry(18, 3, 18).translate(CX, 48, CZ), 0x12161c), // 檐口
    paint(new THREE.BoxGeometry(10, 9, 10).translate(CX, 54, CZ), 0x12161c), // 钟楼体
    paint(new THREE.ConeGeometry(7.4, 12, 4).rotateY(Math.PI / 4).translate(CX, 64.5, CZ), 0x0f1218),
  ];
  const clockMesh = new THREE.Mesh(mergeGeometries(clockGeoms, false), mat());
  clockMesh.name = 'clockTower';
  group.add(clockMesh);
  inkTargets.push({ mesh: clockMesh, thickness: profile.ink.clockThickness });

  // 亮着的钟面：朝向城市西南
  const faceDir = new THREE.Vector3(-1, 0, 0.42).normalize();
  const clockFace = new THREE.Mesh(
    new THREE.CircleGeometry(3.6, 24),
    new THREE.MeshBasicMaterial({ color: 0xffe6b8 }),
  );
  clockFace.position.set(CX + faceDir.x * 5.3, 54, CZ + faceDir.z * 5.3);
  clockFace.lookAt(clockFace.position.clone().add(faceDir));
  clockFace.name = 'clockFace';
  group.add(clockFace);
  const handMat = new THREE.MeshBasicMaterial({ color: 0x14100a });
  const handV = new THREE.Mesh(new THREE.PlaneGeometry(0.5, 2.4), handMat);
  handV.position.set(0, 0.8, 0.15);
  const handH = new THREE.Mesh(new THREE.PlaneGeometry(1.7, 0.5), handMat);
  handH.position.set(0.55, -0.2, 0.15);
  clockFace.add(handV, handH);

  // ── 气柜（煤气储气罐）──────────────────────────────────
  const GX = -210, GZ = 90;
  const gasGeoms = [
    paint(new THREE.CylinderGeometry(26, 26, 30, 28, 1, true).translate(GX, 15, GZ), 0x14171d, 0.06),
    paint(new THREE.CylinderGeometry(26.6, 26.6, 1.4, 28, 1, true).translate(GX, 6, GZ), 0x171b22, 0.04),
    paint(new THREE.CylinderGeometry(26.6, 26.6, 1.4, 28, 1, true).translate(GX, 22, GZ), 0x171b22, 0.04),
    paint(new THREE.CylinderGeometry(21, 21, 14, 20).translate(GX, 7, GZ), 0x0d1015), // 内罐
  ];
  const gasMesh = new THREE.Mesh(mergeGeometries(gasGeoms, false), mat());
  gasMesh.name = 'gasometer';
  group.add(gasMesh);
  inkTargets.push({ mesh: gasMesh, thickness: profile.ink.gasometerThickness });
  // 气柜顶缘的微光环：工业区若隐若现的第二道光
  const gasRing = new THREE.Mesh(
    new THREE.TorusGeometry(26.2, 0.35, 6, 40),
    new THREE.MeshBasicMaterial({ color: 0x2a343c }),
  );
  gasRing.rotation.x = Math.PI / 2;
  gasRing.position.set(GX, 30.2, GZ);
  gasRing.name = 'gasRing';
  group.add(gasRing);

  // ── 教堂 ───────────────────────────────────────────────
  const KX = 62, KZ = 124;
  const churchGeoms = [
    paint(new THREE.BoxGeometry(13, 12, 26).translate(KX, 6, KZ), 0x14120f),
    paint(new THREE.BoxGeometry(7, 24, 7).translate(KX + 2, 12, KZ + 9), 0x14120f),
  ];
  const spireTop = paint(new THREE.ConeGeometry(4.6, 15, 4).rotateY(Math.PI / 4).translate(KX + 2, 31.5, KZ + 9), 0x100e0c);
  churchGeoms.push(spireTop);
  const churchMesh = new THREE.Mesh(mergeGeometries(churchGeoms, false), mat());
  churchMesh.name = 'church';
  group.add(churchMesh);
  inkTargets.push({ mesh: churchMesh, thickness: profile.ink.churchThickness });
  const cross = new THREE.Group();
  const crossMat = new THREE.MeshBasicMaterial({ color: 0x03050a });
  cross.add(new THREE.Mesh(new THREE.BoxGeometry(0.22, 3.4, 0.22), crossMat));
  const arm = new THREE.Mesh(new THREE.BoxGeometry(1.6, 0.22, 0.22), crossMat);
  arm.position.y = 0.9;
  cross.add(arm);
  cross.position.set(KX + 2, 40.5, KZ + 9);
  group.add(cross);

  // ── 烟囱两根 + 烟柱锚点 ────────────────────────────────
  anchors.stacks = [];
  const stacks = [
    { x: -190, z: 44, r: 3.0, h: 48 },
    { x: -158, z: 22, r: 2.5, h: 56 },
  ];
  const stackGeoms = [];
  for (const s of stacks) {
    stackGeoms.push(paint(new THREE.CylinderGeometry(s.r * 0.82, s.r, s.h, 12).translate(s.x, s.h / 2, s.z), 0x101318));
    const band = paint(new THREE.CylinderGeometry(s.r * 0.86, s.r * 0.86, 1.1, 12).translate(s.x, s.h - 4, s.z), 0x1c2026);
    stackGeoms.push(band);
    anchors.stacks.push(new THREE.Vector3(s.x, s.h + 2, s.z));
  }
  const stackMesh = new THREE.Mesh(mergeGeometries(stackGeoms, false), mat());
  stackMesh.name = 'stacks';
  group.add(stackMesh);

  return { group, inkTargets, anchors, heroWindowSpecs };
}
