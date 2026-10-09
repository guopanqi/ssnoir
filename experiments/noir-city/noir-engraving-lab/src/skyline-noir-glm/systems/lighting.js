import * as THREE from 'three';
import { getGlowTexture } from '../scene/glowTexture.js';

// 照明组织：一冷（月）一暖（钠灯池），各有动机。
// 楼本身吃不到几盏真点光——暖池主要靠加性光斑与地面光池"画"出来，
// 真 PointLight 只放在长街镜头附近，给近景墙面一点物理正确的水温。

export function createLighting(scene, profile, lampPoints) {
  const L = profile.lighting;
  const group = new THREE.Group();
  group.name = 'lampLayer';

  // 冷键：月亮。不是泛光，是低强度的方向光，只给顶面和受光面一口耳语。
  const moonDir = new THREE.Vector3(...profile.sky.moonDir).normalize();
  const moon = new THREE.DirectionalLight(L.moonLightColor, L.moonLightIntensity);
  moon.position.copy(moonDir).multiplyScalar(500);
  scene.add(moon);

  const hemi = new THREE.HemisphereLight(L.hemiSky, L.hemiGround, L.hemiIntensity);
  scene.add(hemi);

  // 街灯本体：细杆 + 灯头点 + 地面光池
  const poleMat = new THREE.MeshLambertMaterial({ color: 0x0a0d11 });
  const poles = [];
  for (const lp of lampPoints) {
    if (lp.viaduct) continue;
    const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.13, 5.8, 5), poleMat);
    pole.position.set(lp.x, 2.9, lp.z);
    poles.push(pole);
    const arm = new THREE.Mesh(new THREE.BoxGeometry(1.4, 0.12, 0.12), poleMat);
    arm.position.set(lp.x + 0.6, 5.7, lp.z);
    poles.push(arm);
  }
  const poleMesh = new THREE.Group();
  poles.forEach((m) => poleMesh.add(m));
  poleMesh.name = 'lampPoles';
  group.add(poleMesh);

  // 灯头光斑：THREE.Points，一次 draw call，自动朝向相机
  const glowTex = getGlowTexture();
  const street = lampPoints.filter((lp) => !lp.viaduct);
  const viaductLamps = lampPoints.filter((lp) => lp.viaduct);
  const glowGeo = new THREE.BufferGeometry();
  glowGeo.setAttribute(
    'position',
    new THREE.Float32BufferAttribute(street.map((lp) => [lp.x, lp.y + 0.4, lp.z]).flat(), 3),
  );
  const lampGlows = new THREE.Points(glowGeo, new THREE.PointsMaterial({
    map: glowTex,
    color: L.lampColor,
    size: L.lampGlowSize,
    sizeAttenuation: true,
    transparent: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  }));
  lampGlows.name = 'lampGlows';
  group.add(lampGlows);

  const vGlowGeo = new THREE.BufferGeometry();
  vGlowGeo.setAttribute(
    'position',
    new THREE.Float32BufferAttribute(viaductLamps.map((lp) => [lp.x, lp.y, lp.z]).flat(), 3),
  );
  const viaductGlows = new THREE.Points(vGlowGeo, new THREE.PointsMaterial({
    map: glowTex,
    color: L.lampColor,
    size: 4,
    sizeAttenuation: true,
    transparent: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    opacity: 0.8,
  }));
  viaductGlows.name = 'viaductGlows';
  group.add(viaductGlows);

  // 地面光池：合并四边形 + 径向贴图，加性
  const poolGeoms = [];
  for (const lp of street) {
    if (!lp.artery) continue;
    const q = new THREE.PlaneGeometry(L.lampPoolRadius * 2, L.lampPoolRadius * 2);
    q.rotateX(-Math.PI / 2);
    q.translate(lp.x, 0.07, lp.z);
    poolGeoms.push(q);
  }
  const pools = new THREE.Mesh(
    mergeQuads(poolGeoms),
    new THREE.MeshBasicMaterial({
      map: glowTex,
      color: L.lampColor,
      transparent: true,
      opacity: L.lampPoolOpacity,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    }),
  );
  pools.name = 'lampPools';
  group.add(pools);

  // 真 PointLight：只给大道近景
  const pointLights = [];
  const nearPoints = [
    [0, 5.9, 168],
    [0, 5.9, 120],
    [0, 5.9, 72],
    [0, 6.2, -20],
  ];
  for (const [x, y, z] of nearPoints) {
    const pl = new THREE.PointLight(L.lampColor, L.pointLightIntensity, L.pointLightDistance, 1.8);
    pl.position.set(x, y, z);
    scene.add(pl);
    pointLights.push(pl);
  }

  scene.add(group);

  return {
    group,
    pointLights,
    setLampsVisible(v) {
      lampGlows.visible = v;
      viaductGlows.visible = v;
      pools.visible = v;
      poleMesh.visible = v;
      pointLights.forEach((pl) => { pl.intensity = v ? L.pointLightIntensity : 0; });
    },
  };
}

function mergeQuads(geoms) {
  // PlaneGeometry 们都是非索引同构，手写合并避免为一个用途引 BufferGeometryUtils
  const total = geoms.reduce((n, g) => n + g.attributes.position.count, 0);
  const pos = new Float32Array(total * 3);
  const uv = new Float32Array(total * 2);
  let po = 0, uo = 0;
  for (const g of geoms) {
    pos.set(g.attributes.position.array, po);
    uv.set(g.attributes.uv.array, uo);
    po += g.attributes.position.array.length;
    uo += g.attributes.uv.array.length;
  }
  const out = new THREE.BufferGeometry();
  out.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  out.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
  return out;
}
