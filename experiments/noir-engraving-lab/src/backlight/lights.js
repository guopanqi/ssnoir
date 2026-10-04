// 逆光 BACKLIGHT · 灯光系统
//
// 四类光，各有明确理由：
//   key     低角度冷键（西偏北 15°）：屋顶亮边、地面长影、"城在逆光里"的证据
//   hemi    天光：只够让背光面不至于变成纯剪纸
//   uplight 主角塔的两盏仰射灯：全城唯一从下面打亮的立面
//   lamp    钠灯珠 / 塔顶信标 / 三处叙事色标：不占真灯，只给一点可见光
// 外加两样"看得见的光"：仰射光锥 + 一道慢扫探照灯。
import * as THREE from 'three';
import { KEY, AMBIENT, UPLIGHT, HERO, BEAM, SEARCHLIGHT, LAMPS, ACCENTS } from './config.js';
import { beamMaterial, glowPointsMaterial, makePoints } from './materials.js';

const V = (a) => new THREE.Vector3(a[0], a[1], a[2]);
const CITY_CENTER = new THREE.Vector3(-150, 0, -70);

function makeBeam(origin, dir, { length, r0, r1, material }) {
  const g = new THREE.CylinderGeometry(r1, r0, length, 22, 1, true);
  g.translate(0, length * 0.5, 0);   // 底部在原点，沿 +Y 展开
  const mesh = new THREE.Mesh(g, material);
  mesh.position.copy(origin);
  mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
  mesh.frustumCulled = false;
  return mesh;
}

function hash1(x) {
  const s = Math.sin(x * 127.1) * 43758.5453123;
  return s - Math.floor(s);
}

export function buildLights(city) {
  const group = new THREE.Group();
  group.name = '逆光_灯光';

  // ---- 键光 + 天光 -------------------------------------------------------
  const key = new THREE.DirectionalLight(new THREE.Color().setRGB(...KEY.color), KEY.intensity);
  const kd = V(KEY.dir).normalize();
  key.position.copy(CITY_CENTER).addScaledVector(kd, 1500);
  key.target.position.copy(CITY_CENTER);
  key.castShadow = true;
  key.shadow.mapSize.set(KEY.shadowMap, KEY.shadowMap);
  key.shadow.camera.left = -KEY.shadowSize;
  key.shadow.camera.right = KEY.shadowSize;
  key.shadow.camera.top = KEY.shadowSize;
  key.shadow.camera.bottom = -KEY.shadowSize;
  key.shadow.camera.near = 200;
  key.shadow.camera.far = 3400;
  key.shadow.bias = -0.0006;
  key.shadow.normalBias = 3.0;
  key.shadow.radius = 1.0;
  group.add(key, key.target);

  const hemi = new THREE.HemisphereLight(
    new THREE.Color().setRGB(...AMBIENT.skyColor),
    new THREE.Color().setRGB(...AMBIENT.groundColor),
    AMBIENT.intensity,
  );
  group.add(hemi);

  // ---- 主角塔仰射灯（静态阴影：烘一次就不更新） ---------------------------
  const site = V(HERO.site);
  const glow = [];   // 阶段开关要一起扳的发光材质
  const uplights = UPLIGHT.map((u, i) => {
    const light = new THREE.SpotLight(
      new THREE.Color().setRGB(...u.color), u.intensity, u.distance, u.angle, u.penumbra, 2,
    );
    light.position.copy(site).add(V(u.off));
    light.target.position.copy(site).add(V(u.aim));
    light.userData.baseIntensity = u.intensity;
    light.castShadow = true;
    light.shadow.mapSize.set(1024, 1024);
    light.shadow.bias = -0.0012;
    light.shadow.normalBias = 0.08;
    light.shadow.camera.near = 2;
    light.shadow.camera.far = u.distance;
    group.add(light, light.target);

    // 可见光锥：从灯具打出去的那口光
    const origin = light.position.clone();
    const dir = light.target.position.clone().sub(origin).normalize();
    const len = origin.distanceTo(light.target.position) * 1.06;
    const beam = makeBeam(origin, dir, {
      length: len,
      r0: 1.5,
      r1: Math.tan(u.angle) * len * 0.62,
      material: beamMaterial({ color: BEAM.color, intensity: BEAM.intensity, fade: BEAM.fade }),
    });
    beam.name = '仰射光锥' + i;
    glow.push(beam.material);
    group.add(beam);
    return light;
  });

  // ---- 探照灯：不做真光，只做一道扫过黑天的可见光锥 -----------------------
  const searchMat = beamMaterial({
    color: SEARCHLIGHT.color, intensity: SEARCHLIGHT.intensity, fade: 1.25,
  });
  const sOrigin = V(SEARCHLIGHT.p);
  const sDir = V(SEARCHLIGHT.aim).sub(sOrigin).normalize();
  const search = makeBeam(sOrigin, sDir, {
    length: SEARCHLIGHT.length,
    r0: 2.2,
    r1: SEARCHLIGHT.radius,
    material: searchMat,
  });
  search.name = '探照灯';
  glow.push(searchMat);
  group.add(search);
  const sweepAxis = new THREE.Vector3(0.35, 0, 1).normalize();
  const baseQuat = search.quaternion.clone();

  // ---- 钠灯珠：沿干道撒 ---------------------------------------------------
  const lampPositions = [];
  const lampRand = [];
  const road = city.ground.find((g) => g.name === '干道');
  if (road) {
    const arr = road.geometry.attributes.position.array;
    const quads = arr.length / 18;   // 6 顶点 × 3 分量
    for (let q = 0; q < quads; q++) {
      if (q % LAMPS.step !== 0) continue;
      const b = q * 18;
      const cx = (arr[b] + arr[b + 3] + arr[b + 6] + arr[b + 9] + arr[b + 12] + arr[b + 15]) / 6;
      const cz = (arr[b + 2] + arr[b + 5] + arr[b + 8] + arr[b + 11] + arr[b + 14] + arr[b + 17]) / 6;
      if (hash1(q * 1.7) > 0.86) continue;   // 少数灯是灭的
      lampPositions.push(cx, LAMPS.y, cz);
      lampRand.push(hash1(q * 3.1 + 0.5));
    }
  }
  const lampMat = glowPointsMaterial({
    color: LAMPS.color, intensity: LAMPS.intensity, worldSize: LAMPS.size, fogFade: 0.9,
  });
  const lamps = makePoints(lampPositions, lampPositions.map(() => 1), lampRand, lampMat);
  lamps.name = '钠灯珠';
  glow.push(lampMat);
  group.add(lamps);

  // ---- 三处叙事色标（挂在具体建筑上） -------------------------------------
  const accents = ACCENTS.map((a) => {
    const mat = glowPointsMaterial({ color: a.c, intensity: a.i, worldSize: a.size, fogFade: 0.85 });
    const p = makePoints(a.p, [1], [0.5], mat);
    p.name = '色标_' + a.name;
    group.add(p);
    glow.push(mat);
    return { mat, name: a.name };
  });

  return {
    group, key, hemi, uplights, search, accents, glow,
    lampMat,
    update(t) {
      const ang = Math.sin(t * SEARCHLIGHT.sweep * Math.PI * 2) * 0.42;
      search.quaternion.copy(baseQuat).premultiply(
        new THREE.Quaternion().setFromAxisAngle(sweepAxis, ang),
      );
    },
  };
}
