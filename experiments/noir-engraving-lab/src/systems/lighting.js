import * as THREE from 'three';
import { RectAreaLightUniformsLib } from 'three/addons/lights/RectAreaLightUniformsLib.js';
import { RENDER_LAYERS } from '../config/layers.js';

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

  const coneMaterial = new THREE.MeshBasicMaterial({
    color: p.warm,
    transparent: true,
    opacity: 0.010,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
  });

  // Narrative lights use dedicated layers so they sculpt their subject without
  // accidentally turning nearby roads and buildings into new focal points.
  const warehouseWork = new THREE.RectAreaLight(0x91b7e5, 6.2, 8.0, 7.0);
  warehouseWork.position.set(17.0, 7.8, 7.5);
  warehouseWork.lookAt(30.0, 3.2, 19.0);
  warehouseWork.layers.set(RENDER_LAYERS.WAREHOUSE);
  group.add(warehouseWork);

  const alleyCut = new THREE.RectAreaLight(0x718caf, 13.0, 1.35, 7.5);
  alleyCut.position.set(-18.7, 6.2, 21.0);
  alleyCut.lookAt(-23.8, 3.5, 26.5);
  alleyCut.layers.set(RENDER_LAYERS.ALLEY);
  group.add(alleyCut);

  const alleyDoor = new THREE.PointLight(0xe6c27a, 2.2, 5.5, 2.0);
  alleyDoor.position.set(-20.45, 1.8, 37.6);
  alleyDoor.castShadow = false;
  alleyDoor.layers.set(RENDER_LAYERS.ALLEY);
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
