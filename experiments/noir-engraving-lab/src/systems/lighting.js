import * as THREE from 'three';
import { RectAreaLightUniformsLib } from 'three/addons/lights/RectAreaLightUniformsLib.js';

export function createLighting(scene, profile, lampPositions) {
  RectAreaLightUniformsLib.init();

  const p = profile.palette;
  const cfg = profile.lighting;

  const group = new THREE.Group();
  const lightCones = new THREE.Group();
  group.name = 'Lighting';
  lightCones.name = 'NarrativeLightCones';
  scene.add(group, lightCones);

  const key = new THREE.DirectionalLight(cfg.keyColor, cfg.keyIntensity);
  key.position.fromArray(cfg.keyPosition);
  key.castShadow = true;
  key.shadow.mapSize.set(2048, 2048);
  key.shadow.camera.left = -70;
  key.shadow.camera.right = 70;
  key.shadow.camera.top = 48;
  key.shadow.camera.bottom = -48;
  key.shadow.camera.near = 1;
  key.shadow.camera.far = 120;
  group.add(key);

  const fill = new THREE.DirectionalLight(cfg.fillColor, cfg.fillIntensity);
  fill.position.fromArray(cfg.fillPosition);
  group.add(fill);

  group.add(new THREE.HemisphereLight(
    cfg.ambientSky,
    cfg.ambientGround,
    cfg.ambientIntensity,
  ));

  // 几何光锥只留作交互试验，不进入正式基准。
  const coneMaterial = new THREE.MeshBasicMaterial({
    color: p.warm,
    transparent: true,
    opacity: 0.010,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
  });

  // 仓库采用面积光而不是圆形 Spot：更像装卸门/工作灯留下的矩形明暗关系。
  const warehouseWork = new THREE.RectAreaLight(0x91b7e5, 7.5, 8.5, 9.0);
  warehouseWork.position.set(17.0, 8.5, 7.0);
  warehouseWork.lookAt(30.0, 3.2, 19.5);
  group.add(warehouseWork);

  // 巷道只照出一侧墙和湿地，远端暖门保持唯一高亮点。
  const alleyCut = new THREE.RectAreaLight(0x718caf, 4.2, 1.2, 6.0);
  alleyCut.position.set(-18.6, 6.0, 22.0);
  alleyCut.lookAt(-23.7, 3.8, 26.5);
  group.add(alleyCut);

  const alleyDoor = new THREE.PointLight(0xe6c27a, 1.6, 5.5, 2.0);
  alleyDoor.position.set(-20.45, 2.0, 37.7);
  alleyDoor.castShadow = false;
  group.add(alleyDoor);

  for (const position of lampPositions) {
    const light = new THREE.PointLight(
      cfg.streetColor,
      cfg.streetIntensity,
      cfg.streetDistance,
      2.25,
    );
    light.position.fromArray(position);
    light.castShadow = false;
    group.add(light);

    const cone = new THREE.Mesh(
      new THREE.ConeGeometry(2.8, 4.0, 24, 1, true),
      coneMaterial,
    );
    cone.position.set(position[0], 2.0, position[2]);
    lightCones.add(cone);
  }

  return {
    group,
    lightCones,
    setConesVisible(visible) {
      lightCones.visible = visible;
    },
  };
}
