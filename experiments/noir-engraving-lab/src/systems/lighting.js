import * as THREE from 'three';

export function createLighting(scene, profile, lampPositions) {
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
  key.shadow.camera.left = -60;
  key.shadow.camera.right = 60;
  key.shadow.camera.top = 46;
  key.shadow.camera.bottom = -46;
  key.shadow.camera.near = 1;
  key.shadow.camera.far = 105;
  group.add(key);

  const fill = new THREE.DirectionalLight(cfg.fillColor, cfg.fillIntensity);
  fill.position.fromArray(cfg.fillPosition);
  group.add(fill);

  group.add(new THREE.HemisphereLight(
    cfg.ambientSky,
    cfg.ambientGround,
    cfg.ambientIntensity,
  ));

  // 第一阶段只允许光束作为很轻的空气提示，不能成为可见几何体。
  const coneMaterial = new THREE.MeshBasicMaterial({
    color: p.warm,
    transparent: true,
    opacity: 0.010,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
  });

  const warehouseSearch = new THREE.SpotLight(
    0x91b7e5,
    260,
    58,
    Math.PI * 0.15,
    0.38,
    1.35,
  );
  warehouseSearch.position.set(11, 13, -4);
  warehouseSearch.target.position.set(30, 3.0, 18.5);
  warehouseSearch.castShadow = false;
  group.add(warehouseSearch, warehouseSearch.target);

  const alleyRim = new THREE.SpotLight(
    0x6f8fb8,
    3.2,
    16,
    Math.PI * 0.26,
    0.18,
    0.0,
  );
  alleyRim.position.set(-20.3, 6.4, 17.2);
  alleyRim.target.position.set(-24.2, 4.0, 27.0);
  alleyRim.castShadow = false;
  group.add(alleyRim, alleyRim.target);

  const alleyDoor = new THREE.SpotLight(
    0xe6c27a,
    3.4,
    12,
    Math.PI * 0.18,
    0.12,
    0.0,
  );
  alleyDoor.position.set(-20.45, 2.4, 36.65);
  alleyDoor.target.position.set(-21.25, 0.0, 29.0);
  alleyDoor.castShadow = false;
  group.add(alleyDoor, alleyDoor.target);

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
