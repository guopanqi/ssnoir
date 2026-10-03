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

  const ambient = new THREE.HemisphereLight(
    cfg.ambientSky,
    cfg.ambientGround,
    cfg.ambientIntensity,
  );
  group.add(ambient);

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
  group.add(warehouseWork);

  // Three.js Object3D layers control camera visibility, not per-object light
  // linking. The alley therefore uses a physically bounded spotlight whose cone
  // is wider than the alley itself; the walls / bridge create the visible cuts.
  const alleyCut = new THREE.SpotLight(
    0x718caf,
    1100,
    32,
    0.48,
    0.02,
    2.0,
  );
  alleyCut.position.set(-14.0, 10.5, 17.0);
  alleyCut.target.position.set(-23.5, 3.2, 29.5);
  alleyCut.castShadow = true;
  alleyCut.shadow.mapSize.set(1024, 1024);
  alleyCut.shadow.camera.near = 1;
  alleyCut.shadow.camera.far = 32;
  group.add(alleyCut, alleyCut.target);

  // A tiny local lift replaces the former global hemisphere "alley ambient".
  // Its short distance prevents the alley setup from raising the whole city.
  const alleyAmbient = new THREE.PointLight(0x17243a, 7.5, 12.0, 2.0);
  alleyAmbient.position.set(-21.25, 4.2, 27.0);
  group.add(alleyAmbient);

  const alleyDoor = new THREE.PointLight(0xe6c27a, 1.0, 4.5, 2.0);
  alleyDoor.position.set(-20.45, 1.8, 37.6);
  alleyDoor.castShadow = false;
  group.add(alleyDoor);

  // Street lamps are visual punctuation by default, not automatic light emitters.
  // This avoids soft circular pools competing with narrative lighting. The optional
  // cone geometry remains available for explicit future experiments.
  for (const position of lampPositions) {
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
