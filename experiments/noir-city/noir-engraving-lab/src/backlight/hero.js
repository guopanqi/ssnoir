// 逆光 BACKLIGHT · 主角塔（自建 Art Deco）
//
// 城市数据里市中心场址上那栋 98 m 填充方块已被烘焙剔除，位子留给这座塔：
// 四段收分 + 竖向壁柱 + 少数层亮着的竖窗带 + 发光冠 + 信标 + 细尖。
// 它是全城唯一被两盏仰射灯从下面打亮的立面，也是唯一的叙事焦点。
import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import { HERO } from './config.js';
import { makePoints, glowPointsMaterial } from './materials.js';

const box = (w, h, d, x, y, z) => {
  const g = new THREE.BoxGeometry(w, h, d);
  g.translate(x, y, z);
  return g;
};

// 可复现的槽位随机（静态场景不用 Math.random）
function slotHash(a, b) {
  const s = Math.sin(a * 12.9898 + b * 78.233) * 43758.5453123;
  return s - Math.floor(s);
}

export function buildHero() {
  const group = new THREE.Group();
  group.name = '主角塔';
  group.position.set(HERO.site[0], HERO.site[1], HERO.site[2]);

  const body = [];
  const strips = [];
  const crown = [];
  const tiers = HERO.tiers;

  tiers.forEach((t, ti) => {
    const h = t.y1 - t.y0;
    body.push(box(t.w, h, t.d, 0, (t.y0 + t.y1) * 0.5, 0));

    // 竖向壁柱：每面按间距均分，从地面拉到该段顶
    const P = HERO.pilaster;
    const addPiers = (faceW, plane, along) => {
      const n = Math.max(2, Math.round(faceW / P.gap));
      for (let i = 0; i < n; i++) {
        const u = -faceW * 0.5 + (i + 0.5) * (faceW / n);
        if (along === 'z') body.push(box(P.w, h * 0.99, P.depth, u, (t.y0 + t.y1) * 0.5, plane));
        else body.push(box(P.depth, h * 0.99, P.w, plane, (t.y0 + t.y1) * 0.5, u));
      }
    };
    addPiers(t.w, t.d * 0.5 + P.depth * 0.5 - 0.12, 'z');
    addPiers(t.w, -(t.d * 0.5 + P.depth * 0.5 - 0.12), 'z');
    addPiers(t.d, t.w * 0.5 + P.depth * 0.5 - 0.12, 'x');
    addPiers(t.d, -(t.w * 0.5 + P.depth * 0.5 - 0.12), 'x');

    // 竖窗带：只在主身三段，一部分槽位亮着（其余是黑的）
    if (ti >= 1) {
      const S = HERO.strip;
      const sh = h * S.hFrac;
      const sy = (t.y0 + t.y1) * 0.5;
      const addStrip = (faceW, plane, along) => {
        const n = Math.max(2, Math.round(faceW / S.gap));
        for (let i = 0; i < n; i++) {
          if (slotHash(ti * 7 + 1, i * 3 + (along === 'z' ? 0 : 11)) > S.lit) continue;
          const u = -faceW * 0.5 + (i + 0.5) * (faceW / n);
          if (along === 'z') strips.push(box(S.w, sh, 0.24, u, sy, plane));
          else strips.push(box(0.24, sh, S.w, plane, sy, u));
        }
      };
      addStrip(t.w, t.d * 0.5 + 0.02, 'z');
      addStrip(t.w, -(t.d * 0.5 + 0.02), 'z');
      addStrip(t.d, t.w * 0.5 + 0.02, 'x');
      addStrip(t.d, -(t.w * 0.5 + 0.02), 'x');
    }
  });

  // 冠部：鼓座之上两级收进 + 腰线发光带 + 灯笼，再接细尖
  const top = tiers[tiers.length - 1].y1;
  const s1 = { w: 11.5, d: 9.5, y0: top, y1: top + 3.0 };
  const s2 = { w: 9.5, d: 8.0, y0: s1.y1, y1: HERO.crown - 2.0 };
  const lantern = { w: 7.0, d: 6.0, y0: s2.y1, y1: HERO.crown };
  body.push(box(s1.w, s1.y1 - s1.y0, s1.d, 0, (s1.y0 + s1.y1) * 0.5, 0));
  body.push(box(s2.w, s2.y1 - s2.y0, s2.d, 0, (s2.y0 + s2.y1) * 0.5, 0));

  // 腰线发光带：贴着第一级收分的肩
  crown.push(box(s1.w + 0.5, 1.7, s1.d + 0.5, 0, s1.y0 + 1.0, 0));
  // 灯笼：塔冠房间，整层透光
  crown.push(box(lantern.w, lantern.y1 - lantern.y0, lantern.d, 0, (lantern.y0 + lantern.y1) * 0.5, 0));

  // 细尖：只做剪影，不打光
  body.push(box(1.8, HERO.spire - HERO.crown, 1.6, 0, (HERO.crown + HERO.spire) * 0.5, 0));
  body.push(box(0.5, 6.0, 0.5, 0, HERO.spire + 3.0, 0));

  const bodyMesh = new THREE.Mesh(mergeGeometries(body, false), null);
  bodyMesh.name = '塔身';
  bodyMesh.castShadow = true;
  bodyMesh.receiveShadow = true;
  const stripMesh = new THREE.Mesh(mergeGeometries(strips, false), null);
  stripMesh.name = '塔窗带';
  const crownMesh = new THREE.Mesh(mergeGeometries(crown, false), null);
  crownMesh.name = '塔冠';
  group.add(bodyMesh, stripMesh, crownMesh);

  // 塔顶信标：一个辉光点（时间脉动由 main 驱动）
  const beaconMat = glowPointsMaterial({
    color: HERO.beacon.color,
    intensity: HERO.beacon.intensity,
    worldSize: HERO.beacon.size,
    fogFade: 0.55,
  });
  const beacon = makePoints([0, HERO.crown + 2.5, 0], [1.0], [0.5], beaconMat);
  beacon.name = '信标';
  group.add(beacon);

  return { group, bodyMesh, stripMesh, crownMesh, beacon, beaconMat };
}
